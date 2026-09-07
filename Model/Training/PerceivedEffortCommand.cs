namespace lionheart.Model.Training;

/// <summary>
/// Perceived effort as the athlete may state it. Every rating is optional because rating some of
/// them and skipping the rest is normal; stating none of them — or omitting the whole object — is
/// how a client says effort was not reported.
/// </summary>
/// <remarks>
/// <see cref="PerceivedEffortRatings.RecordedAt"/> is absent on purpose: when the ratings were
/// written down is the server's bookkeeping, not a fact the client is authorized to assert.
/// </remarks>
public sealed record PerceivedEffortCommand(
    int? AccumulatedFatigue,
    int? DifficultyRating,
    int? EngagementRating,
    int? ExternalVariablesRating)
{
    /// <summary>
    /// Total by construction: a client cannot assert a "reported" effort with nothing in it, so
    /// the empty command and the absent one are the same domain fact.
    /// </summary>
    public static EffortState ToEffortState(PerceivedEffortCommand? command, DateTime recordedAt) =>
        command is null || command.IsEmpty
            ? new EffortNotReported()
            : new EffortReported(new PerceivedEffortRatings
            {
                RecordedAt = recordedAt,
                AccumulatedFatigue = command.AccumulatedFatigue,
                DifficultyRating = command.DifficultyRating,
                EngagementRating = command.EngagementRating,
                ExternalVariablesRating = command.ExternalVariablesRating,
            });

    private bool IsEmpty =>
        AccumulatedFatigue is null
        && DifficultyRating is null
        && EngagementRating is null
        && ExternalVariablesRating is null;
}
