using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Profile;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

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
        /// tier if it has gone stale or the generator version changed. The only LLM cost is a best-effort
        /// narrative produced during a (re)build; numbers are always deterministic.
        /// </summary>
        Task<Result<AthleteContextCard>> GetOrBuildCardAsync(IdentityUser user);

        /// <summary>Rebuild the slow-changing Stable Profile tier (identity, baselines, long-horizon trends).</summary>
        Task<Result<AthleteContextCard>> RebuildStableProfileAsync(IdentityUser user);

        /// <summary>Recompute the fast, event-driven Recent State tier (today/this-week snapshot).</summary>
        Task<Result<AthleteContextCard>> RefreshRecentStateAsync(IdentityUser user);

        /// <summary>
        /// Assemble the full system prompt: a byte-stable, cacheable prefix (coaching instructions +
        /// Stable Profile + tool-routing policy) followed by the volatile coverage manifest and Recent State.
        /// </summary>
        string RenderSystemMessage(AthleteContextCard card);

        /// <summary>
        /// The static coaching prompt used when no card can be built (brand-new user, transient failure).
        /// Behaviorally equivalent to the card prefix minus the per-user profile.
        /// </summary>
        string RenderFallbackSystemMessage();

        /// <summary>
        /// Event-driven invalidation hook. Marks the user's volatile Recent State tier stale so it is
        /// rebuilt on the next read. Does NOT call <c>SaveChanges</c> — it is batched into the caller's
        /// existing write so there is no extra round trip. Safe to call when no card exists yet (no-op).
        /// </summary>
        Task MarkRecentStateStaleAsync(Guid userId);
    }

    public class AthleteContextCardService(ModelContext context, ChatClient chatClient)
        : IAthleteContextCardService
    {
        /// <summary>Bump when aggregation or rendering changes so existing cards are detected as stale and rebuilt.</summary>
        private const int GENERATOR_VERSION = 2;

        /// <summary>Recent State is considered stale after this long and is lazily recomputed on read.</summary>
        private static readonly TimeSpan RecentStateMaxAge = TimeSpan.FromHours(6);

        /// <summary>Defensive upper bound per rendered tier (prompt-budget guard; content is already bounded by construction).</summary>
        private const int MaxTierChars = 4_000;

        private readonly ModelContext _context = context;
        private readonly ChatClient _chatClient = chatClient;

        public async Task<Result<AthleteContextCard>> GetOrBuildCardAsync(IdentityUser user)
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
                return Result.Success(card);
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

            return Result.Success(card);
        }

        public async Task<Result<AthleteContextCard>> RebuildStableProfileAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId);
            if (card is null)
            {
                return await GetOrBuildCardAsync(user);
            }
            await BuildStableProfileAsync(card, userId);
            await _context.SaveChangesAsync();
            return Result.Success(card);
        }

        public async Task<Result<AthleteContextCard>> RefreshRecentStateAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId);
            if (card is null)
            {
                return await GetOrBuildCardAsync(user);
            }
            await BuildRecentStateAsync(card, userId);
            await _context.SaveChangesAsync();
            return Result.Success(card);
        }

        public async Task MarkRecentStateStaleAsync(Guid userId)
        {
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId);
            if (card is not null)
            {
                // Forcing the as-of into the past makes the next GetOrBuildCardAsync recompute the tier.
                card.RecentStateAsOf = DateTime.MinValue;
            }
        }

        // ---------------------------------------------------------------------
        // Stable Profile tier (slow): identity + baselines + strength profile +
        // training modality + chronic injuries + long-horizon trajectory.
        // ---------------------------------------------------------------------
        private async Task BuildStableProfileAsync(AthleteContextCard card, Guid userId)
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var yearAgo = today.AddDays(-365);
            var d90 = today.AddDays(-90);
            var d180 = today.AddDays(-180);

            var lhUser = await _context.LionheartUsers.FirstOrDefaultAsync(u => u.UserID == userId);

            // One pass over the trailing year of Oura data: baselines + 90d-vs-prior-90d readiness trend.
            var ouraYear = await _context.DailyOuraDatas
                .Where(o => o.UserID == userId && o.Date >= yearAgo)
                .Select(o => new
                {
                    o.Date,
                    o.ReadinessData.RestingHeartRate,
                    o.ReadinessData.HrvBalance,
                    o.ReadinessData.ReadinessScore,
                    o.SleepData.SleepScore
                })
                .ToListAsync();

            var sessionsYear = await _context.TrainingSessions
                .Where(s => s.UserID == userId && s.Date >= yearAgo)
                .Select(s => s.Date)
                .ToListAsync();
            var firstSessionDate = await _context.TrainingSessions
                .Where(s => s.UserID == userId)
                .OrderBy(s => s.Date)
                .Select(s => (DateOnly?)s.Date)
                .FirstOrDefaultAsync();

            var activePrCount = await _context.PersonalRecords
                .CountAsync(p => p.UserID == userId && p.IsActive);

            // Strongest current lifts — gives the model a concrete strength profile to reason from.
            var topPRs = await _context.PersonalRecords
                .Where(p => p.UserID == userId && p.IsActive && p.PRType == PersonalRecordType.Strength)
                .OrderByDescending(p => p.Weight)
                .Select(p => new
                {
                    Movement = p.MovementData.MovementBase.Name,
                    Equipment = p.MovementData.Equipment.Name,
                    p.Weight,
                    p.Reps,
                    p.WeightUnit
                })
                .Take(5)
                .ToListAsync();

            // Training modality signal — most frequently trained movements over the year.
            var yearMovementNames = await _context.Movements
                .Where(m => m.TrainingSession.UserID == userId && m.TrainingSession.Date >= yearAgo)
                .Select(m => m.MovementData.MovementBase.Name)
                .ToListAsync();
            var topMovements = yearMovementNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .GroupBy(n => n)
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => $"{g.Key} (×{g.Count()})")
                .ToList();

            var injuries = await _context.Injuries
                .Where(i => i.UserID == userId)
                .OrderByDescending(i => i.InjuryDate)
                .Select(i => new { i.Name, i.InjuryDate, i.IsActive, EventCount = i.InjuryEvents.Count })
                .Take(8)
                .ToListAsync();

            // ---- Render deterministic facts (numbers never come from the LLM) ----
            var facts = new List<string>();

            var identity = new List<string>();
            if (lhUser is not null)
            {
                if (!string.IsNullOrWhiteSpace(lhUser.Name)) identity.Add(lhUser.Name);
                if (lhUser.Age > 0) identity.Add($"{lhUser.Age}y");
                if (lhUser.Weight > 0) identity.Add($"{lhUser.Weight:0.#} bodyweight");
            }
            facts.Add(identity.Count > 0 ? $"Identity: {string.Join(", ", identity)}." : "Identity: not yet provided.");

            if (ouraYear.Count > 0)
            {
                facts.Add(
                    $"Biometric baselines (trailing 12mo, n={ouraYear.Count}d): resting HR ~{Avg(ouraYear.Select(o => (double)o.RestingHeartRate)):0} bpm, " +
                    $"HRV balance ~{Avg(ouraYear.Select(o => (double)o.HrvBalance)):0}, " +
                    $"readiness ~{Avg(ouraYear.Select(o => (double)o.ReadinessScore)):0}, " +
                    $"sleep score ~{Avg(ouraYear.Select(o => (double)o.SleepScore)):0}.");
            }
            else
            {
                facts.Add("Biometric baselines: no Oura history on record.");
            }

            var historySpan = firstSessionDate is DateOnly f
                ? $"since {f:yyyy-MM}"
                : "no sessions logged yet";
            var perWeek = sessionsYear.Count / 52.0;
            facts.Add(
                $"Training history: {sessionsYear.Count} sessions in the last 12mo (~{perWeek:0.0}/wk), {historySpan}; " +
                $"{activePrCount} active PRs on record.");

            if (topMovements.Count > 0)
            {
                facts.Add($"Most-trained movements (12mo): {string.Join(", ", topMovements)}.");
            }
            if (topPRs.Count > 0)
            {
                var prList = string.Join("; ", topPRs.Select(p =>
                    $"{p.Movement} ({p.Equipment}) {p.Weight:0.#}{UnitAbbrev(p.WeightUnit)}×{p.Reps}"));
                facts.Add($"Top current lifts: {prList}.");
            }

            if (injuries.Count > 0)
            {
                var injuryList = string.Join("; ", injuries.Select(i =>
                    $"{i.Name} ({i.InjuryDate:yyyy-MM}{(i.IsActive ? ", active" : "")}{(i.EventCount > 0 ? $", {i.EventCount} events" : "")})"));
                facts.Add($"Injury history: {injuryList}.");
            }
            else
            {
                facts.Add("Injury history: none recorded.");
            }

            // Long-horizon trajectory: last 90d vs the prior 90d.
            var sess90 = sessionsYear.Count(d => d >= d90);
            var sessPrev90 = sessionsYear.Count(d => d >= d180 && d < d90);
            var read90 = ouraYear.Where(o => o.Date >= d90).Select(o => (double)o.ReadinessScore).ToList();
            var readPrev90 = ouraYear.Where(o => o.Date >= d180 && o.Date < d90).Select(o => (double)o.ReadinessScore).ToList();
            var trendParts = new List<string>
            {
                $"training volume {sess90} vs {sessPrev90} sessions ({Direction(sess90, sessPrev90)})"
            };
            if (read90.Count > 0 && readPrev90.Count > 0)
            {
                trendParts.Add($"avg readiness {read90.Average():0} vs {readPrev90.Average():0} ({Direction(read90.Average(), readPrev90.Average())})");
            }
            facts.Add($"Trajectory (last 90d vs prior 90d): {string.Join("; ", trendParts)}.");

            var deterministic = string.Join("\n", facts);
            var narrative = await TryNarrateAsync(
                "In ONE sentence, characterize this athlete's training identity, dominant modality, and any " +
                "standing constraints, so a coach knows who they are at a glance. Use only the facts; invent nothing.",
                deterministic);

            card.StableProfileText = Cap(string.IsNullOrWhiteSpace(narrative)
                ? deterministic
                : $"{narrative}\n{deterministic}");
            card.StableProfileVersion += 1;
            card.StableProfileAsOf = now;
            card.GeneratorVersion = GENERATOR_VERSION;
            card.LastUpdated = now;
        }

        // ---------------------------------------------------------------------
        // Recent State tier (fast): this-week snapshot, trends vs prior week,
        // acute load, recent PRs, active injuries + the coverage manifest.
        // ---------------------------------------------------------------------
        private async Task BuildRecentStateAsync(AthleteContextCard card, Guid userId)
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var weekAgo = today.AddDays(-7);
            var twoWeeksAgo = today.AddDays(-14);
            var monthAgo = today.AddDays(-28);

            // 14 days of wellness — split into this-week / prior-week for a trend.
            var wellness2w = await _context.WellnessStates
                .Where(w => w.UserID == userId && w.Date >= twoWeeksAgo)
                .OrderByDescending(w => w.Date)
                .ToListAsync();
            var wellnessWeek = wellness2w.Where(w => w.Date >= weekAgo).ToList();
            var wellnessPrevWeek = wellness2w.Where(w => w.Date < weekAgo).ToList();

            // 14 days of Oura — same split for trend; latest night for a concrete snapshot.
            var oura2w = await _context.DailyOuraDatas
                .Where(o => o.UserID == userId && o.Date >= twoWeeksAgo)
                .Select(o => new
                {
                    o.Date,
                    o.ReadinessData.ReadinessScore,
                    o.SleepData.SleepScore,
                    o.ReadinessData.HrvBalance,
                    o.ReadinessData.RestingHeartRate,
                    o.ResilienceData.Stress,
                    o.ResilienceData.ResilienceLevel
                })
                .ToListAsync();
            var ouraWeek = oura2w.Where(o => o.Date >= weekAgo).ToList();
            var ouraPrevWeek = oura2w.Where(o => o.Date < weekAgo).ToList();
            var latestNight = oura2w.OrderByDescending(o => o.Date).FirstOrDefault();

            var activeInjuries = await _context.Injuries
                .Where(i => i.UserID == userId && i.IsActive)
                .Select(i => new
                {
                    i.Name,
                    LastPain = i.InjuryEvents.OrderByDescending(e => e.CreationTime).Select(e => (int?)e.PainLevel).FirstOrDefault(),
                    LastEvent = i.InjuryEvents.OrderByDescending(e => e.CreationTime).Select(e => (DateTime?)e.CreationTime).FirstOrDefault()
                })
                .ToListAsync();

            var acuteSessions = await _context.TrainingSessions.CountAsync(s => s.UserID == userId && s.Date >= weekAgo);
            var chronicSessions = await _context.TrainingSessions.CountAsync(s => s.UserID == userId && s.Date >= monthAgo);
            var activePrCount = await _context.PersonalRecords.CountAsync(p => p.UserID == userId && p.IsActive);

            // Subjective acute load: average session difficulty (RPE proxy) over the last 7 days.
            // Optional owned type — absent ratings materialize as null and are filtered out in memory.
            var weekRatings = await _context.TrainingSessions
                .Where(s => s.UserID == userId && s.Date >= weekAgo)
                .Select(s => s.PerceivedEffortRatings)
                .ToListAsync();
            var ratedDifficulties = weekRatings
                .Where(r => r != null && r.DifficultyRating.HasValue)
                .Select(r => (double)r!.DifficultyRating!.Value)
                .ToList();

            // PRs set in the last 30 days.
            var recentPRs = await _context.PersonalRecords
                .Where(p => p.UserID == userId && p.IsActive && p.CreatedAt >= now.AddDays(-30))
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    Movement = p.MovementData.MovementBase.Name,
                    p.Weight,
                    p.Reps,
                    p.WeightUnit,
                    p.PRType,
                    p.CreatedAt
                })
                .Take(6)
                .ToListAsync();

            // Acute:chronic workload ratio (1-week acute vs 4-week weekly-averaged chronic), session-count proxy.
            double chronicWeekly = chronicSessions / 4.0;
            string acwr = chronicWeekly > 0 ? $"{acuteSessions / chronicWeekly:0.00}" : "n/a";
            string acwrFlag = chronicWeekly <= 0
                ? ""
                : (acuteSessions / chronicWeekly) switch
                {
                    > 1.5 => " (spiking — elevated strain)",
                    < 0.8 => " (detraining/taper)",
                    _ => " (balanced)"
                };

            var lines = new List<string>();

            if (wellnessWeek.Count > 0)
            {
                var latest = wellnessWeek[0];
                var trend = wellnessPrevWeek.Count > 0
                    ? $" ({Direction(wellnessWeek.Average(w => w.OverallScore), wellnessPrevWeek.Average(w => w.OverallScore))} vs prior wk)"
                    : "";
                lines.Add(
                    $"Wellness: latest {latest.Date:yyyy-MM-dd} overall {latest.OverallScore:0.0} " +
                    $"(mood {latest.MoodScore}, energy {latest.EnergyScore}, stress {latest.StressScore}, motivation {latest.MotivationScore}); " +
                    $"7d avg {wellnessWeek.Average(w => w.OverallScore):0.0}{trend}.");
            }
            else
            {
                lines.Add("Wellness: no entries in the last 7 days.");
            }

            if (ouraWeek.Count > 0)
            {
                var trend = ouraPrevWeek.Count > 0
                    ? $" (readiness {Direction(ouraWeek.Average(o => (double)o.ReadinessScore), ouraPrevWeek.Average(o => (double)o.ReadinessScore))} vs prior wk)"
                    : "";
                lines.Add(
                    $"Oura 7d means: readiness {Avg(ouraWeek.Select(o => (double)o.ReadinessScore)):0}, " +
                    $"sleep {Avg(ouraWeek.Select(o => (double)o.SleepScore)):0}, " +
                    $"HRV balance {Avg(ouraWeek.Select(o => (double)o.HrvBalance)):0}, " +
                    $"resting HR {Avg(ouraWeek.Select(o => (double)o.RestingHeartRate)):0}, " +
                    $"stress {Avg(ouraWeek.Select(o => o.Stress)):0.0} (n={ouraWeek.Count}){trend}.");
            }
            else
            {
                lines.Add("Oura: no synced data in the last 7 days.");
            }

            if (latestNight is not null)
            {
                lines.Add(
                    $"Latest night {latestNight.Date:yyyy-MM-dd}: readiness {latestNight.ReadinessScore}, " +
                    $"sleep {latestNight.SleepScore}, HRV balance {latestNight.HrvBalance}, " +
                    $"resting HR {latestNight.RestingHeartRate}" +
                    $"{(string.IsNullOrWhiteSpace(latestNight.ResilienceLevel) || latestNight.ResilienceLevel == "unkown" ? "" : $", resilience {latestNight.ResilienceLevel}")}.");
            }

            var loadLine = $"Training load: {acuteSessions} sessions in 7d, acute:chronic {acwr}{acwrFlag} (vs trailing 28d)";
            if (ratedDifficulties.Count > 0)
            {
                loadLine += $"; avg perceived difficulty {ratedDifficulties.Average():0.0}/10 over {ratedDifficulties.Count} rated session(s)";
            }
            lines.Add(loadLine + ".");

            if (recentPRs.Count > 0)
            {
                var prList = string.Join("; ", recentPRs.Select(p =>
                    $"{p.Movement} {p.Weight:0.#}{UnitAbbrev(p.WeightUnit)}×{p.Reps} ({p.PRType}, {p.CreatedAt:MM-dd})"));
                lines.Add($"Recent PRs (30d): {prList}.");
            }

            if (activeInjuries.Count > 0)
            {
                var inj = string.Join("; ", activeInjuries.Select(i =>
                {
                    var bits = i.Name;
                    if (i.LastPain is int p) bits += $" (last pain {p}/10";
                    if (i.LastEvent is DateTime e) bits += $"{(i.LastPain is int ? ", " : " (")}{(now - e).TotalDays:0}d ago";
                    if (i.LastPain is int || i.LastEvent is DateTime) bits += ")";
                    return bits;
                }));
                lines.Add($"Active injuries: {inj}.");
            }
            else
            {
                lines.Add("Active injuries: none recorded.");
            }

            var deterministic = string.Join("\n", lines);
            var narrative = await TryNarrateAsync(
                "In ONE sentence, summarize this athlete's current readiness-vs-load picture and the single most " +
                "important thing a coach should keep in mind right now. Use only the facts; invent no numbers.",
                deterministic);

            card.RecentStateText = Cap(string.IsNullOrWhiteSpace(narrative)
                ? deterministic
                : $"{narrative}\n{deterministic}");

            // Coverage manifest — steers tool calls: what is loaded + over what window, and the tool to drill in.
            card.CoverageManifest = string.Join("\n", new[]
            {
                $"wellness: 7d summary + latest entry + prior-week trend present (as-of {today:yyyy-MM-dd}); per-day history NOT loaded -> GetWellnessStates(dateRange).",
                $"oura: 7d means + latest night present (n={ouraWeek.Count}); per-night detail/older windows NOT loaded -> GetDailyOuraDataRange(dateRange).",
                $"training: 7d/28d counts + acute:chronic + 7d RPE present; per-session detail NOT loaded -> GetTrainingSessionsByDateRange(dateRange).",
                $"injuries: {activeInjuries.Count} active summarized; full event history/inactive injuries NOT loaded -> GetUserInjuries(filters) or GetAllUserInjuries().",
                $"PRs: {activePrCount} active counted, last-30d listed; full values/progression NOT loaded -> GetPersonalRecords().",
                $"activities (cardio/other): NOT loaded -> GetActivities(dateRange)."
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
                $"{BaseInstructions}\n\n" +
                $"=== ATHLETE PROFILE (stable) ===\n{card.StableProfileText.Trim()}\n\n" +
                $"=== TOOL ROUTING POLICY ===\n{ToolRoutingPolicy}";

            // VOLATILE SUFFIX (manifest + recent state). All freshness/timestamps live here, after the prefix.
            var suffix =
                $"\n\n--- COVERAGE MANIFEST (volatile) ---\n{card.CoverageManifest.Trim()}\n\n" +
                $"--- RECENT STATE (volatile, as-of {card.RecentStateAsOf:yyyy-MM-dd HH:mm} UTC) ---\n{card.RecentStateText.Trim()}";

            return prefix + suffix;
        }

        public string RenderFallbackSystemMessage() => $"{BaseInstructions}\n\n{ToolRoutingPolicy}";

        /// <summary>Best-effort narrative on the flagship model. Any failure (no key, network, etc.) falls back to deterministic text.</summary>
        private async Task<string> TryNarrateAsync(string instruction, string facts)
        {
            if (string.IsNullOrWhiteSpace(facts))
            {
                return string.Empty;
            }

            try
            {
                var messages = new ChatMessage[]
                {
                    new SystemChatMessage(
                        "You write a single, plain, grounded sentence for an athlete-coaching context card. " +
                        "Never restate raw numbers; never invent data; no medical claims."),
                    new UserChatMessage($"{instruction}\n\nFacts:\n{facts}")
                };
                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);
                return string.Join("", completion.Content.Select(c => c.Text)).Trim();
            }
            catch
            {
                // Narrative is non-essential; deterministic facts already carry the signal.
                return string.Empty;
            }
        }

        private static double Avg(IEnumerable<double> values)
        {
            var list = values as ICollection<double> ?? values.ToList();
            return list.Count == 0 ? 0 : list.Average();
        }

        /// <summary>Word describing the direction of <paramref name="recent"/> relative to <paramref name="prior"/> (±5% dead-band).</summary>
        private static string Direction(double recent, double prior)
        {
            if (prior == 0) return recent > 0 ? "up" : "flat";
            var delta = (recent - prior) / Math.Abs(prior);
            return delta > 0.05 ? "up" : delta < -0.05 ? "down" : "flat";
        }

        private static string UnitAbbrev(SetEntry.WeightUnit unit) =>
            unit == SetEntry.WeightUnit.Kilograms ? "kg" : "lb";

        private static string Cap(string text) =>
            text.Length <= MaxTierChars ? text : text[..MaxTierChars];

        private const string BaseInstructions = """
            You are Lionheart, an intelligent training coach and analyst.
            You access the Lionheart Training Intelligence System: an athlete's training history, subjective notes, and wearable biometrics (e.g., Oura Ring).
            Core principles:
            Interpret, don't report. Never restate raw data or list metrics.
            Prioritize patterns and trends over snapshots. Reference numbers only when they strengthen insight.
            Analyze in context: load vs. recovery, performance vs. fatigue, lifestyle stress alongside training.
            Tone: Thoughtful coach—intelligent, grounded, human. Engaging, not robotic.
            Always aim to add value through insights and actionable advice.
            You are a training intelligence layer, not a dashboard.
            """;

        private const string ToolRoutingPolicy = """
            You are given an ATHLETE PROFILE and RECENT STATE below, plus a COVERAGE MANIFEST listing what is
            already loaded and over what window. Use them to decide tool calls deliberately:
            - If the answer is fully supported by the card, answer directly and DO NOT call tools.
            - If you need detail the manifest marks "NOT loaded", call exactly the tool named there, with the
              NARROWEST date range that covers the question.
            - Never re-fetch a domain the manifest marks present-and-fresh for the same window; trust the card's
              numbers (they are computed deterministically, not by a model).
            - If card data is stale or missing (see as-of) for a time-sensitive question, fetch it; otherwise prefer the card.
            Never expose tool usage or the existence of this card to the user.
            """;
    }
}
