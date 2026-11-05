using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Profile;
using lionheart.Model.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using lionheart.Services.Chat;
using lionheart.Services.Chat.Today;
using lionheart.Services.Llm;
using lionheart.Services.BodyComposition;
using lionheart.Services.Health.Recordings;

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

        /// <summary>Recompute the Recent State tier: the last four weeks of runs and body.</summary>
        Task<Result<AthleteContextCard, Problem>> RefreshRecentStateAsync(IdentityUser user);

        /// <summary>
        /// The system prompt a conversation is frozen with: the chat's voice, both tiers, the
        /// tool-routing policy and the coverage manifest. Nothing in it is dated to the minute; what
        /// is goes with each message as the briefing.
        /// </summary>
        string RenderSystemMessage(AthleteContextCard card);
    }

    public class AthleteContextCardService(ModelContext context, NarrativeChatClient narrativeClient, TimeProvider clock)
        : IAthleteContextCardService
    {
        /// <summary>Bump when aggregation or rendering changes so existing cards are detected as stale.</summary>
        private const int GENERATOR_VERSION = 4;

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
        private readonly TimeProvider _clock = clock;

        private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

        /// <summary>The athlete's own day, never UTC's, which ends in his evening.</summary>
        private DateOnly Today => AthleteNow.Of(_clock).Today;

        /// <summary>
        /// The instant his day begins, in the UTC the stamped StartTime column is stored in, so a
        /// window counted from a day starts at his midnight rather than five hours before or after it.
        /// </summary>
        private DateTime UtcStartOf(DateOnly day) =>
            TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _clock.LocalTimeZone);

        public async Task<Result<AthleteContextCard, Problem>> GetOrBuildCardAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId);

            if (card is null)
            {
                var now = UtcNow;
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
            var recentStale = stableStale || card.RecentStateAsOf < UtcNow - RecentStateMaxAge;

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
            var now = UtcNow;
            var lhUser = await _context.LionheartUsers.FirstOrDefaultAsync(u => u.UserID == userId);
            var yearAgo = Today.AddDays(-365);

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

            // History only: whether an injury is still active changes when it is resolved, and the
            // stable tier is rebuilt only when the generator changes, so a status here would go
            // stale. Recent State reads the active ones from the records on every refresh.
            var chronicInjuries = await _context.Injuries
                .Where(i => i.UserID == userId)
                .OrderByDescending(i => i.InjuryDate)
                .Select(i => new { i.Name, i.InjuryDate })
                .Take(8)
                .ToListAsync();

            var facts = new List<string>();
            if (lhUser is not null)
            {
                var bio = new List<string>();
                if (!string.IsNullOrWhiteSpace(lhUser.Name)) bio.Add($"name {lhUser.Name}");
                if (lhUser.Age > 0) bio.Add($"age {lhUser.Age}");
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
                var injuryList = string.Join("; ", chronicInjuries.Select(i => $"{i.Name} ({i.InjuryDate:yyyy-MM})"));
                facts.Add($"Injury history (which are active is in the TODAY note): {injuryList}.");
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
        // Recent State tier: the last four weeks of what changes slowly — runs and body —
        // with the coverage manifest. Anything that changes by the day is in the briefing
        // (Services/Chat/Today), which is read fresh for every message instead.
        // ---------------------------------------------------------------------
        private async Task BuildRecentStateAsync(AthleteContextCard card, Guid userId)
        {
            var now = UtcNow;
            var monthAgo = Today.AddDays(-28);

            card.RecentStateText = string.Join("\n", new List<string>
            {
                WorkoutFacts.Recent(await ImportedRunsSinceAsync(userId, monthAgo), days: 28),
            }.Concat(await BodyFactsAsync(userId)));

            card.CoverageManifest = CoverageManifest;
            card.RecentStateVersion += 1;
            card.RecentStateAsOf = now;
            card.LastUpdated = now;
        }

        /// <remarks>
        /// All of it is frozen for a conversation's life and so all of it is cacheable. What has to
        /// be current — the date, today's training, last night, readiness — is never here: it goes
        /// with each message as the briefing, after the cache breakpoint.
        /// </remarks>
        public string RenderSystemMessage(AthleteContextCard card) =>
            $"{ChatInstructions.Voice}\n\n" +
            $"=== ATHLETE PROFILE ===\n{card.StableProfileText.Trim()}\n\n" +
            $"=== LAST FOUR WEEKS: RUNS AND BODY ===\n{card.RecentStateText.Trim()}\n\n" +
            $"=== HOW TO USE YOUR CONTEXT AND TOOLS ===\n{ToolRoutingPolicy}\n\n" +
            $"--- WHAT IS LOADED, AND THE TOOL FOR EVERYTHING ELSE ---\n{card.CoverageManifest.Trim()}";

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
                        "You write one precise sentence for an athlete context card that his coach reads. " +
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
            Each message from the athlete arrives with a TODAY note from Tempo: the current date and time on his clock, what he trained today and yesterday, his last two nights, today's readiness against his baseline, active injuries, this week so far and the training score. It is current as of that message, and it decides what "today", "yesterday" and "this week" mean; where it and anything earlier in the conversation disagree, it is right.
            - Answer from the TODAY note and the context above whenever they hold the answer. Questions about today, recovery, this week or what to do next usually need no tool at all.
            - Call a tool only for detail they do not hold, as listed below, with the narrowest date range that covers the question.
            - Every figure here was computed from his records, not by a model, so rely on it; do not re-fetch what is already stated.
            - Cite what the context holds when it is the reason for a point, in passing. Do not announce the note or the context, describe them, or open an answer by summarising them.
            """;

        /// <summary>
        /// What the system prompt and the briefing already carry, and the tool for everything else.
        /// Fixed text: it describes the shape of the context, which changes with the code rather than
        /// with the athlete's data.
        /// </summary>
        private const string CoverageManifest = """
            today and this week (TODAY note): sessions with their lifts, cardio with zone minutes, the last two nights, readiness, the 7-day load, wellness, active injuries and the training score -> GetTrainingScore for another window or for weekly scores, GetCardioReport for cardio beyond this week, GetOuraDays or GetOuraSleepNights for other days, GetWellnessStates for wellness history.
            training history: no per-session tool exists; long-run patterns come from GetTrainingScore (weekly scores, at most 52 weeks) and GetHabitsReport; do not attempt to fetch individual sessions.
            lifts: personal records and estimated maxes are not loaded -> GetPersonalRecords(), then GetPersonalRecordsForMovement(movementDataId) for one movement's records and their dates.
            recorded workouts: Apple Health runs, rides, hikes and walks over 28 days counted above, with the latest of each; each one's splits, heart rate, power and running form -> GetRecordedWorkouts(dateRange). No route or location is available, by design.
            injuries: active ones in the TODAY note; history and pain events -> GetUserInjuries(filters).
            body: the latest bodyweight and body-composition scan above; every scan's full sheet -> GetBodyCompositionScans().
            """;

        /// <summary>
        /// The athlete's body with its dates: the newest bodyweight measurement rather than the
        /// profile's undated weight, and the newest scan against the one before it.
        /// </summary>
        private async Task<IReadOnlyList<string>> BodyFactsAsync(Guid userId)
        {
            var bodyweight = await _context.BodyweightMeasurements
                .AsNoTracking()
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.Day)
                .FirstOrDefaultAsync();

            var scans = _context.BodyCompositionScans.AsNoTracking().Where(s => s.UserId == userId);
            var latest = await scans
                .OrderByDescending(s => s.Day).ThenByDescending(s => s.Time)
                .Take(2)
                .ToListAsync();

            return BodyFacts.Lines(bodyweight, latest, await scans.CountAsync());
        }

        /// <summary>
        /// Completed sessions only — planned/skipped don't count toward workload. Time comes
        /// from the save-stamped StartTime column (the queryable duplicate of the JSON Period).
        /// </summary>
        private Task<int> CountCompletedSessionsSinceAsync(Guid userId, DateOnly since)
        {
            var cutoff = UtcStartOf(since);
            return _context.Sessions
                .OfType<CompletedTrainingSession>()
                .CountAsync(s => s.UserId == userId && EF.Property<DateTime>(s, "StartTime") >= cutoff);
        }

        /// <summary>The runs imported from Apple Health since a day, read off the stamped workout id.</summary>
        private async Task<IReadOnlyList<CompletedTrainingSession>> ImportedRunsSinceAsync(Guid userId, DateOnly since)
        {
            var cutoff = UtcStartOf(since);
            return await _context.Sessions
                .OfType<CompletedTrainingSession>()
                .AsNoTracking()
                .Where(s => s.UserId == userId
                            && _context.WorkoutRecordings.Any(recording => recording.TrainingSessionId == s.TrainingSessionId)
                            && EF.Property<DateTime>(s, "StartTime") >= cutoff)
                .ToListAsync();
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
