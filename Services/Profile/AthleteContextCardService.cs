using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Profile;
using lionheart.Model.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using lionheart.Services.Chat;
using lionheart.Services.Llm;

namespace lionheart.Services.Profile
{
    /// <summary>
    /// Builds and serves the <see cref="AthleteContextCard"/>: a persistent, evolving, per-user profile
    /// injected into the chat model's system message so the model has holistic context up front and makes
    /// far fewer (and more precise) tool calls. See docs/athlete-context-card.md.
    /// </summary>
    public interface IAthleteContextCardService
    {
        /// <summary>
        /// Return the user's card, building it on first use and lazily refreshing the volatile Recent State
        /// tier if it has gone stale or the generator version changed. Never an LLM call on the hot path
        /// beyond best-effort narrative during a (re)build.
        /// </summary>
        Task<Result<AthleteContextCard, Problem>> GetOrBuildCardAsync(IdentityUser user);

        /// <summary>Rebuild the slow-changing Stable Profile tier (identity, baselines, long-horizon trends).</summary>
        Task<Result<AthleteContextCard, Problem>> RebuildStableProfileAsync(IdentityUser user);

        /// <summary>Recompute the fast, event-driven Recent State tier (today/this-week snapshot).</summary>
        Task<Result<AthleteContextCard, Problem>> RefreshRecentStateAsync(IdentityUser user);

        /// <summary>
        /// Assemble the full system prompt: a byte-stable, cacheable prefix (the chat's voice +
        /// Stable Profile + tool-routing policy) followed by the volatile coverage manifest and Recent State.
        /// </summary>
        string RenderSystemMessage(AthleteContextCard card);
    }

    public class AthleteContextCardService(ModelContext context, NarrativeChatClient narrativeClient)
        : IAthleteContextCardService
    {
        /// <summary>Bump when aggregation or rendering changes so existing cards are detected as stale.</summary>
        private const int GENERATOR_VERSION = 2;

        /// <summary>Recent State is considered stale after this long and is lazily recomputed on read.</summary>
        private static readonly TimeSpan RecentStateMaxAge = TimeSpan.FromHours(6);

        /// <summary>
        /// One sentence is all that is asked for, but the budget has to cover more than the
        /// sentence: on a provider whose thinking cannot be switched off, the output cap counts
        /// reasoning tokens too, and a cap that reasoning exhausts comes back as a truncated turn
        /// rather than an answer. The narrative is a deliberate best-effort — a failure leaves the
        /// card without its sentence instead of failing the card — so too small a number here
        /// fails silently, which is why it is generous rather than snug.
        /// </summary>
        private const int NarrativeTokenBudget = 4096;

        private readonly ModelContext _context = context;
        private readonly NarrativeChatClient _narrativeClient = narrativeClient;

        public async Task<Result<AthleteContextCard, Problem>> GetOrBuildCardAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId);

            if (card is null)
            {
                var now = DateTime.UtcNow;
                card = new AthleteContextCard
                {
                    CardID = Guid.NewGuid(),
                    UserID = userId,
                    GeneratorVersion = GENERATOR_VERSION,
                    CreatedAt = now,
                    LastUpdated = now
                };
                _context.AthleteContextCards.Add(card);
                await BuildStableProfileAsync(card, userId);
                await BuildRecentStateAsync(card, userId);
                await _context.SaveChangesAsync();
                return Result<AthleteContextCard, Problem>.Success(card);
            }

            var stableStale = card.GeneratorVersion != GENERATOR_VERSION;
            var recentStale = stableStale || card.RecentStateAsOf < DateTime.UtcNow - RecentStateMaxAge;

            if (stableStale)
            {
                await BuildStableProfileAsync(card, userId);
            }
            if (recentStale)
            {
                await BuildRecentStateAsync(card, userId);
            }
            if (stableStale || recentStale)
            {
                card.GeneratorVersion = GENERATOR_VERSION;
                await _context.SaveChangesAsync();
            }

            return Result<AthleteContextCard, Problem>.Success(card);
        }

        public async Task<Result<AthleteContextCard, Problem>> RebuildStableProfileAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId);
            if (card is null)
            {
                return await GetOrBuildCardAsync(user);
            }
            await BuildStableProfileAsync(card, userId);
            await _context.SaveChangesAsync();
            return Result<AthleteContextCard, Problem>.Success(card);
        }

        public async Task<Result<AthleteContextCard, Problem>> RefreshRecentStateAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId);
            if (card is null)
            {
                return await GetOrBuildCardAsync(user);
            }
            await BuildRecentStateAsync(card, userId);
            await _context.SaveChangesAsync();
            return Result<AthleteContextCard, Problem>.Success(card);
        }

        // ---------------------------------------------------------------------
        // Stable Profile tier (slow): identity + baselines + long-horizon trend.
        // ---------------------------------------------------------------------
        private async Task BuildStableProfileAsync(AthleteContextCard card, Guid userId)
        {
            var now = DateTime.UtcNow;
            var lhUser = await _context.LionheartUsers.FirstOrDefaultAsync(u => u.UserID == userId);
            var yearAgo = DateOnly.FromDateTime(now).AddDays(-365);

            // Baseline biometrics over the trailing year (deterministic; numbers never come from the LLM).
            // Readings live in a JSON column, so the day range narrows in SQL and the members are
            // read in memory. A contributor Oura could not compute is skipped rather than counted
            // as zero — see Mean below.
            var readinessYear = await _context.OuraDailyReadiness
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.Day >= yearAgo)
                .Select(o => o.Reading)
                .ToListAsync();

            // The athlete's real resting heart rate, in beats per minute, from the nights
            // themselves. Oura's readiness contributor of the same name is a 1-100 score for how
            // that night's resting rate compared to his own baseline, not a rate — it runs the
            // full 1 to 100 across this history while his actual lowest rates run 36 to 94. The
            // card goes into the model's standing context, so a score presented as bpm is a number
            // it would reason with and get wrong.
            var nightsYear = await _context.OuraSleepPeriods
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.Day >= yearAgo)
                .Select(o => o.Reading)
                .ToListAsync();

            var restingRates = nightsYear
                .Where(night => night.IsMainSleep)
                .Select(night => night.LowestHeartRate)
                .ToList();

            var sessionsYear = await CountCompletedSessionsSinceAsync(userId, yearAgo);

            var standingRecordCount = await StandingRecordCountAsync(userId);

            var chronicInjuries = await _context.Injuries
                .Where(i => i.UserID == userId)
                .OrderByDescending(i => i.InjuryDate)
                .Select(i => new { i.Name, i.InjuryDate, i.IsActive })
                .Take(8)
                .ToListAsync();

            var facts = new List<string>();
            if (lhUser is not null)
            {
                var bio = new List<string>();
                if (!string.IsNullOrWhiteSpace(lhUser.Name)) bio.Add($"name {lhUser.Name}");
                if (lhUser.Age > 0) bio.Add($"age {lhUser.Age}");
                if (lhUser.Weight > 0) bio.Add($"weight {lhUser.Weight:0.#}");
                if (bio.Count > 0) facts.Add($"Athlete: {string.Join(", ", bio)}.");
            }
            if (readinessYear.Count > 0 || restingRates.Count > 0)
            {
                var baselines = new List<string>();
                if (restingRates.Count > 0)
                    baselines.Add($"lowest overnight HR ~{Mean(restingRates):0} bpm");
                if (readinessYear.Count > 0)
                {
                    baselines.Add($"HRV balance ~{Mean(readinessYear.Select(r => r.Contributors.HrvBalance)):0}/100");
                    baselines.Add($"readiness ~{Mean(readinessYear.Select(r => r.Score)):0}/100");
                }

                facts.Add(
                    $"Baselines (trailing 12mo): {string.Join(", ", baselines)} " +
                    $"(n={Math.Max(readinessYear.Count, restingRates.Count)} days).");
            }
            facts.Add($"Training history: {sessionsYear} sessions logged in the last 12 months; {standingRecordCount} personal records currently standing.");
            if (chronicInjuries.Count > 0)
            {
                var injuryList = string.Join("; ", chronicInjuries.Select(i =>
                    $"{i.Name} ({i.InjuryDate:yyyy-MM}{(i.IsActive ? ", active" : "")})"));
                facts.Add($"Injury history: {injuryList}.");
            }

            var deterministic = facts.Count > 0 ? string.Join("\n", facts) : "No profile data on record yet.";
            var narrative = await TryNarrateAsync(
                "In one sentence, characterize this athlete's training identity and any standing constraints. " +
                "Use only the facts given, and introduce no number that is not among them.",
                deterministic);

            card.StableProfileText = string.IsNullOrWhiteSpace(narrative)
                ? deterministic
                : $"{narrative}\n{deterministic}";
            card.StableProfileVersion += 1;
            card.StableProfileAsOf = now;
            card.GeneratorVersion = GENERATOR_VERSION;
            card.LastUpdated = now;
        }

        // ---------------------------------------------------------------------
        // Recent State tier (fast): today/this-week snapshot + coverage manifest.
        // ---------------------------------------------------------------------
        private async Task BuildRecentStateAsync(AthleteContextCard card, Guid userId)
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var weekAgo = today.AddDays(-7);
            var monthAgo = today.AddDays(-28);

            var wellnessWeek = await _context.WellnessStates
                .Where(w => w.UserID == userId && w.Date >= weekAgo)
                .OrderByDescending(w => w.Date)
                .ToListAsync();

            var readinessWeek = await _context.OuraDailyReadiness
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.Day >= weekAgo)
                .Select(o => o.Reading)
                .ToListAsync();

            var sleepWeek = await _context.OuraDailySleep
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.Day >= weekAgo)
                .Select(o => o.Reading)
                .ToListAsync();

            // Stress now comes from Oura's own stress document rather than the resilience level's
            // stress contributor: it is a measured share of the day in seconds, and unlike
            // resilience it is a collection this athlete's history actually covers.
            var stressWeek = await _context.OuraDailyStress
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.Day >= weekAgo)
                .Select(o => o.Reading)
                .ToListAsync();

            var activeInjuries = await _context.Injuries
                .Where(i => i.UserID == userId && i.IsActive)
                .Select(i => new
                {
                    i.Name,
                    LastPain = i.InjuryEvents.OrderByDescending(e => e.CreationTime).Select(e => (int?)e.PainLevel).FirstOrDefault()
                })
                .ToListAsync();

            var acuteSessions = await CountCompletedSessionsSinceAsync(userId, weekAgo);
            var chronicSessions = await CountCompletedSessionsSinceAsync(userId, monthAgo);
            var standingRecordCount = await StandingRecordCountAsync(userId);

            // Acute:chronic workload ratio using a 1-week acute vs 4-week (weekly-averaged) chronic load proxy.
            double chronicWeekly = chronicSessions / 4.0;
            string acwr = chronicWeekly > 0 ? $"{acuteSessions / chronicWeekly:0.00}" : "n/a";

            var lines = new List<string>();
            if (wellnessWeek.Count > 0)
            {
                var latest = wellnessWeek[0];
                lines.Add(
                    $"Wellness: latest {latest.Date:yyyy-MM-dd} overall {latest.OverallScore:0.0} " +
                    $"(mood {latest.MoodScore}, energy {latest.EnergyScore}, stress {latest.StressScore}, motivation {latest.MotivationScore}); " +
                    $"7d avg overall {Avg(wellnessWeek.Select(w => w.OverallScore)):0.0}.");
            }
            else
            {
                lines.Add("Wellness: no entries in the last 7 days.");
            }

            if (readinessWeek.Count > 0 || sleepWeek.Count > 0 || stressWeek.Count > 0)
            {
                lines.Add(
                    $"Oura (7d means): readiness {Mean(readinessWeek.Select(r => r.Score)):0}/100, " +
                    $"sleep {Mean(sleepWeek.Select(s => s.Score)):0}/100, " +
                    $"HRV balance {Mean(readinessWeek.Select(r => r.Contributors.HrvBalance)):0}/100, " +
                    $"stressful minutes/day {Mean(stressWeek.Select(s => s.StressHighSeconds)) / 60:0} " +
                    $"(n={Math.Max(readinessWeek.Count, sleepWeek.Count)}).");
            }
            else
            {
                lines.Add("Oura: no synced data in the last 7 days.");
            }

            lines.Add($"Training load: {acuteSessions} sessions in 7d, acute:chronic ratio {acwr} (vs trailing 28d).");

            if (activeInjuries.Count > 0)
            {
                var inj = string.Join("; ", activeInjuries.Select(i =>
                    $"{i.Name}{(i.LastPain is int p ? $" (last pain {p}/10)" : "")}"));
                lines.Add($"Active injuries: {inj}.");
            }
            else
            {
                lines.Add("Active injuries: none recorded.");
            }

            var deterministic = string.Join("\n", lines);
            var narrative = await TryNarrateAsync(
                "In one sentence, state where this athlete's readiness stands against their current training load. " +
                "Use only the facts given, and introduce no number that is not among them.",
                deterministic);

            card.RecentStateText = string.IsNullOrWhiteSpace(narrative)
                ? deterministic
                : $"{narrative}\n{deterministic}";

            // Coverage manifest — steers tool calls: what is loaded + over what window, and the tool to drill in.
            card.CoverageManifest = string.Join("\n", new[]
            {
                $"wellness: 7d summary + latest entry present (as-of {today:yyyy-MM-dd}); per-day history NOT loaded -> GetWellnessStates(dateRange).",
                $"oura: 7d readiness/sleep/HRV/stress means present (n={Math.Max(readinessWeek.Count, sleepWeek.Count)}); per-night detail (stages, hypnogram, HRV curve), workouts, sessions and tags NOT loaded -> the /api/oura routes.",
                $"training: 7d count + acute:chronic present; no drill-in tool available yet — do not attempt to fetch per-session detail.",
                $"injuries: {activeInjuries.Count} active summarized; full event history NOT loaded -> GetUserInjuries(filters).",
                $"Personal records: {standingRecordCount} standing records counted; the numbers and their progression NOT loaded -> GetPersonalRecords(), or GetPersonalRecordsForMovement(movementDataId) for one movement."
            });

            card.RecentStateVersion += 1;
            card.RecentStateAsOf = now;
            card.LastUpdated = now;
        }

        public string RenderSystemMessage(AthleteContextCard card)
        {
            // STABLE PREFIX (byte-stable across turns while the Stable Profile is unchanged -> prompt-cacheable).
            // No timestamps or volatile content above this line.
            var prefix =
                $"{ChatInstructions.Voice}\n\n" +
                $"=== ATHLETE PROFILE (stable) ===\n{card.StableProfileText.Trim()}\n\n" +
                $"=== TOOL ROUTING POLICY ===\n{ToolRoutingPolicy}";

            // VOLATILE SUFFIX (manifest + recent state). All freshness/timestamps live here, after the prefix.
            var suffix =
                $"\n\n--- COVERAGE MANIFEST (volatile) ---\n{card.CoverageManifest.Trim()}\n\n" +
                $"--- RECENT STATE (volatile, as-of {card.RecentStateAsOf:yyyy-MM-dd HH:mm} UTC) ---\n{card.RecentStateText.Trim()}";

            return prefix + suffix;
        }

        /// <summary>Best-effort narrative. Any failure (no key, network, etc.) falls back to deterministic text.</summary>
        private async Task<string> TryNarrateAsync(string instruction, string facts)
        {
            if (string.IsNullOrWhiteSpace(facts) || facts.StartsWith("No profile data"))
            {
                return string.Empty;
            }

            try
            {
                var turn = await _narrativeClient.Model.RespondAsync(ChatTurnRequest.Prose(
                    systemPrompt:
                        "You write one precise sentence for an athlete context card that a sports-science analyst reads. " +
                        "The facts are printed beside your sentence, so interpret them rather than restating the figures. " +
                        "Invent nothing, make no medical claims, and leave out encouragement and filler.",
                    userText: $"{instruction}\n\nFacts:\n{facts}",
                    maxOutputTokens: NarrativeTokenBudget));

                // A refused narrative is not an error worth surfacing: the deterministic facts
                // already carry the signal, and the card must come back regardless.
                return turn.Match(Spoken, _ => string.Empty);
            }
            catch
            {
                // Narrative is non-essential; deterministic facts already carry the signal.
                return string.Empty;
            }
        }

        /// <summary>
        /// The sentence a narrative turn produced. A narrative request offers no tools, so a turn
        /// that asked for one has no prose in it to use.
        /// </summary>
        private static string Spoken(ModelTurn turn) => turn switch
        {
            ModelTurn.Answered answered => answered.Text.Trim(),
            _ => string.Empty
        };

        private static double Avg(IEnumerable<double> values)
        {
            var list = values as ICollection<double> ?? values.ToList();
            return list.Count == 0 ? 0 : list.Average();
        }

        /// <summary>
        /// The mean of the readings Oura actually computed, ignoring the days it could not.
        /// </summary>
        /// <remarks>
        /// An absent reading is not a zero. Averaging it as one would drag a baseline down in
        /// proportion to the days the ring recorded nothing, and these figures go into the model's
        /// standing context, where a quietly wrong number is reasoned with rather than questioned.
        /// </remarks>
        private static double Mean(IEnumerable<int?> readings) =>
            Avg(readings.Where(reading => reading.HasValue).Select(reading => (double)reading!.Value));

        private const string ToolRoutingPolicy = """
            You are given an ATHLETE PROFILE and RECENT STATE below, plus a COVERAGE MANIFEST listing what is
            already loaded and over what window. Use them to decide when a tool call is worth making:
            - When the card fully supports the answer, answer from it without calling tools.
            - When you need detail the manifest marks "NOT loaded", call the tool it names, with the narrowest
              date range that covers the question.
            - Do not re-fetch a domain the manifest marks present and fresh for the same window. The card's
              numbers are computed deterministically, not by a model, so rely on them.
            - When the card's data is stale or missing (see its as-of) and the question is time-sensitive, fetch
              it; otherwise prefer the card.
            - You may refer to what the card holds when it is the reason for a point — an injury, a goal, a recent
              trend — in passing, the way an analyst cites the record they are reading from. Do not announce the
              card, describe it, or open an answer by summarising it.
            Do not narrate tool calls.
            """;

        /// <summary>
        /// Completed sessions only — planned/skipped don't count toward workload. Time comes
        /// from the save-stamped StartTime column (the queryable duplicate of the JSON Period).
        /// </summary>
        private Task<int> CountCompletedSessionsSinceAsync(Guid userId, DateOnly since)
        {
            var cutoff = since.ToDateTime(TimeOnly.MinValue);
            return _context.Sessions
                .OfType<CompletedTrainingSession>()
                .CountAsync(s => s.UserId == userId && EF.Property<DateTime>(s, "StartTime") >= cutoff);
        }

        /// <summary>
        /// How many records the athlete currently holds, read off the save-stamped standing
        /// column rather than the JSON the standing itself lives in.
        /// </summary>
        private Task<int> StandingRecordCountAsync(Guid userId) =>
            _context.PersonalRecords.CountAsync(record =>
                record.UserId == userId
                && EF.Property<bool>(record, RemodelConfiguration.RecordIsStandingColumn));
    }
}
