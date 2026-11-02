using lionheart.Functional;

namespace lionheart.Model.Analysis;

/// <summary>
/// An inclusive span of calendar days.
/// </summary>
/// <remarks>
/// Distinct from <see cref="TimeWindow"/>, and both are needed: a window is two absolute instants
/// and is what the stamped StartTime column answers, while a day range is what a reader means when
/// they ask about days a wearable filed its documents under. Collapsing the two would force a
/// caller asking for "the 8th" to invent a time zone for its midnight.
/// </remarks>
public sealed record DayRange(DateOnly From, DateOnly To)
{
    /// <summary>
    /// This range, or a refusal if it runs backwards — the same reasoning as
    /// <see cref="TimeWindow.Ordered"/>: an empty answer would read as "nothing recorded" rather
    /// than "that range is impossible".
    /// </summary>
    public Result<DayRange, Problem> Ordered() =>
        To < From
            ? Result<DayRange, Problem>.Failure(
                new Problem.Invalid(nameof(To), "The range's last day precedes its first."))
            : Result<DayRange, Problem>.Success(this);

    public int Days => To.DayNumber - From.DayNumber + 1;
}
