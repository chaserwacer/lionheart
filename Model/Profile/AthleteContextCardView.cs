namespace lionheart.Model.Profile
{
    /// <summary>
    /// The athlete's own view of the context card: the two rendered memory tiers and when each was
    /// last computed.
    /// </summary>
    /// <remarks>
    /// The card exists so the chat model starts a conversation knowing the athlete. Showing it back
    /// to the athlete is what makes the model's answers checkable — an answer that leans on a
    /// stale Recent State reads very differently once you can see how old it is, which is why the
    /// two as-of instants travel with the prose rather than being internal bookkeeping.
    /// <para>
    /// <see cref="AthleteContextCard.CoverageManifest"/> is deliberately absent. It is a routing
    /// instruction addressed to the model ("per-night detail NOT loaded -> GetDailyOuraDataRange"),
    /// not a statement about the athlete, so it belongs to the system prompt and not to a screen.
    /// The version counters are absent for the same reason: they order rebuilds, and the as-of
    /// instants already answer the only question a reader has about freshness.
    /// </para>
    /// </remarks>
    public sealed record AthleteContextCardView(
        string StableProfileText,
        DateTime StableProfileAsOf,
        string RecentStateText,
        DateTime RecentStateAsOf)
    {
        public static AthleteContextCardView Of(AthleteContextCard card) => new(
            StableProfileText: card.StableProfileText,
            StableProfileAsOf: card.StableProfileAsOf,
            RecentStateText: card.RecentStateText,
            RecentStateAsOf: card.RecentStateAsOf);
    }
}
