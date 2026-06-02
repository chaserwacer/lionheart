using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.Profile
{
    /// <summary>
    /// Persistent, evolving "Athlete Context Card" (ACC) for a user.
    /// </summary>
    /// <remarks>
    /// The card is a <b>derived materialized view</b> over the canonical source tables
    /// (<see cref="lionheart.WellBeing.WellnessState"/>, <see cref="lionheart.Model.InjuryManagement.Injury"/>,
    /// <see cref="lionheart.Model.Oura.DailyOuraData"/>, <see cref="lionheart.Model.Training.TrainingSession"/>,
    /// <see cref="lionheart.Model.Training.PersonalRecord"/>, ...). It is never a source of truth and is
    /// regenerable from scratch at any time, which keeps it auditable, idempotent and versioned.
    ///
    /// It carries two memory tiers with different lifecycles (see docs/athlete-context-card.md):
    /// <list type="bullet">
    /// <item><b>Stable Profile</b> — slow-changing semantic memory (identity, baselines, long-horizon trends).
    /// Rendered once as a byte-stable, cacheable system-prompt prefix.</item>
    /// <item><b>Recent State</b> — fast, event-driven working memory (current wellness, active injuries,
    /// latest Oura, acute load). Re-injected after the stable prefix and only when it has changed.</item>
    /// </list>
    /// The <see cref="CoverageManifest"/> is a machine-readable routing layer that tells the chat model
    /// what the card already contains (so it never re-fetches) and which tool to call for anything not loaded.
    /// All numeric content is produced by deterministic aggregation; only narrative prose is model-generated.
    /// </remarks>
    public class AthleteContextCard
    {
        [Key]
        public required Guid CardID { get; init; }

        /// <summary>Owning user. One card per user (enforced by a unique index).</summary>
        public required Guid UserID { get; init; }

        /// <summary>
        /// Version of the generator/schema that produced this card. Bump when the rendering or
        /// aggregation logic changes so existing cards can be detected as stale and rebuilt.
        /// </summary>
        public int GeneratorVersion { get; set; }

        /// <summary>Rendered Stable Profile block — the cacheable prefix body. Numbers are deterministic.</summary>
        public string StableProfileText { get; set; } = string.Empty;

        /// <summary>Monotonic version of the Stable Profile, bumped on each rebuild.</summary>
        public int StableProfileVersion { get; set; }

        /// <summary>When the Stable Profile was last rebuilt (provenance / freshness).</summary>
        public DateTime StableProfileAsOf { get; set; }

        /// <summary>Rendered Recent State block — the volatile section, placed after the stable prefix.</summary>
        public string RecentStateText { get; set; } = string.Empty;

        /// <summary>Monotonic version of the Recent State, bumped on each refresh.</summary>
        public int RecentStateVersion { get; set; }

        /// <summary>When the Recent State was last refreshed (provenance / freshness).</summary>
        public DateTime RecentStateAsOf { get; set; }

        /// <summary>Machine-readable coverage manifest + drill-down pointers used to steer tool calls.</summary>
        public string CoverageManifest { get; set; } = string.Empty;

        public required DateTime CreatedAt { get; init; }
        public DateTime LastUpdated { get; set; }
    }
}
