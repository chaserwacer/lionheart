using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Profile;
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
        /// tier if it has gone stale or the generator version changed. Never an LLM call on the hot path
        /// beyond best-effort narrative during a (re)build.
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
    }

    public class AthleteContextCardService(ModelContext context, NarrativeChatClient narrativeClient)
        : IAthleteContextCardService
    {
        /// <summary>Bump when aggregation or rendering changes so existing cards are detected as stale.</summary>
        private const int GENERATOR_VERSION = 1;

        /// <summary>Recent State is considered stale after this long and is lazily recomputed on read.</summary>
        private static readonly TimeSpan RecentStateMaxAge = TimeSpan.FromHours(6);

        private readonly ModelContext _context = context;
        private readonly NarrativeChatClient _narrativeClient = narrativeClient;

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

        // ---------------------------------------------------------------------
        // Stable Profile tier (slow): identity + baselines + long-horizon trend.
        // ---------------------------------------------------------------------
        private async Task BuildStableProfileAsync(AthleteContextCard card, Guid userId)
        {
            var now = DateTime.UtcNow;
            var lhUser = await _context.LionheartUsers.FirstOrDefaultAsync(u => u.UserID == userId);
            var yearAgo = DateOnly.FromDateTime(now).AddDays(-365);

            // Baseline biometrics over the trailing year (deterministic; numbers never come from the LLM).
            var ouraYear = await _context.DailyOuraDatas
                .Where(o => o.UserID == userId && o.Date >= yearAgo)
                .Select(o => new { o.ReadinessData.RestingHeartRate, o.ReadinessData.HrvBalance, o.ReadinessData.ReadinessScore })
                .ToListAsync();

            var sessionsYear = await _context.TrainingSessions
                .CountAsync(s => s.UserID == userId && s.Date >= yearAgo);

            var activePrCount = await _context.PersonalRecords
                .CountAsync(p => p.UserID == userId && p.IsActive);

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
            if (ouraYear.Count > 0)
            {
                facts.Add(
                    $"Baselines (trailing 12mo): resting HR ~{Avg(ouraYear.Select(o => (double)o.RestingHeartRate)):0}, " +
                    $"HRV balance ~{Avg(ouraYear.Select(o => (double)o.HrvBalance)):0}, " +
                    $"readiness ~{Avg(ouraYear.Select(o => (double)o.ReadinessScore)):0} (n={ouraYear.Count} days).");
            }
            facts.Add($"Training history: {sessionsYear} sessions logged in the last 12 months; {activePrCount} active PRs on record.");
            if (chronicInjuries.Count > 0)
            {
                var injuryList = string.Join("; ", chronicInjuries.Select(i =>
                    $"{i.Name} ({i.InjuryDate:yyyy-MM}{(i.IsActive ? ", active" : "")})"));
                facts.Add($"Injury history: {injuryList}.");
            }

            var deterministic = facts.Count > 0 ? string.Join("\n", facts) : "No profile data on record yet.";
            var narrative = await TryNarrateAsync(
                "Write ONE concise sentence characterizing this athlete's training identity and any standing constraints. " +
                "Use only the facts; invent nothing; no numbers not present.",
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

            var ouraWeek = await _context.DailyOuraDatas
                .Where(o => o.UserID == userId && o.Date >= weekAgo)
                .Select(o => new
                {
                    o.ReadinessData.ReadinessScore,
                    o.SleepData.SleepScore,
                    o.ReadinessData.HrvBalance,
                    o.ResilienceData.Stress
                })
                .ToListAsync();

            var activeInjuries = await _context.Injuries
                .Where(i => i.UserID == userId && i.IsActive)
                .Select(i => new
                {
                    i.Name,
                    LastPain = i.InjuryEvents.OrderByDescending(e => e.CreationTime).Select(e => (int?)e.PainLevel).FirstOrDefault()
                })
                .ToListAsync();

            var acuteSessions = await _context.TrainingSessions.CountAsync(s => s.UserID == userId && s.Date >= weekAgo);
            var chronicSessions = await _context.TrainingSessions.CountAsync(s => s.UserID == userId && s.Date >= monthAgo);
            var activePrCount = await _context.PersonalRecords.CountAsync(p => p.UserID == userId && p.IsActive);

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

            if (ouraWeek.Count > 0)
            {
                lines.Add(
                    $"Oura (7d means): readiness {Avg(ouraWeek.Select(o => (double)o.ReadinessScore)):0}, " +
                    $"sleep {Avg(ouraWeek.Select(o => (double)o.SleepScore)):0}, " +
                    $"HRV balance {Avg(ouraWeek.Select(o => (double)o.HrvBalance)):0}, " +
                    $"stress {Avg(ouraWeek.Select(o => o.Stress)):0.0} (n={ouraWeek.Count}).");
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
                "In ONE sentence, summarize this athlete's current readiness-vs-load picture for a coach. " +
                "Use only the facts; invent no numbers.",
                deterministic);

            card.RecentStateText = string.IsNullOrWhiteSpace(narrative)
                ? deterministic
                : $"{narrative}\n{deterministic}";

            // Coverage manifest — steers tool calls: what is loaded + over what window, and the tool to drill in.
            card.CoverageManifest = string.Join("\n", new[]
            {
                $"wellness: 7d summary + latest entry present (as-of {today:yyyy-MM-dd}); per-day history NOT loaded -> GetWellnessStates(dateRange).",
                $"oura: 7d readiness/sleep/HRV/stress means present (n={ouraWeek.Count}); per-night detail NOT loaded -> GetDailyOuraDataRange(dateRange).",
                $"training: 7d count + acute:chronic present; per-session detail NOT loaded -> GetTrainingSessionsByDateRange(dateRange).",
                $"injuries: {activeInjuries.Count} active summarized; full event history NOT loaded -> GetUserInjuries(filters).",
                $"PRs: {activePrCount} active PRs counted; current values/progression NOT loaded -> GetPersonalRecords()."
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

        /// <summary>Best-effort narrative. Any failure (no key, network, etc.) falls back to deterministic text.</summary>
        private async Task<string> TryNarrateAsync(string instruction, string facts)
        {
            if (string.IsNullOrWhiteSpace(facts) || facts.StartsWith("No profile data"))
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
                ChatCompletion completion = await _narrativeClient.Client.CompleteChatAsync(messages);
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
