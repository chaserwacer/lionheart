using lionheart.Functional;

namespace lionheart.Model.Analysis;

/// <summary>
/// The inclusive span a read is asked for. Absolute instants rather than calendar dates, because
/// that is what <see cref="Period.StartTimeUtc"/> — and so the stamped StartTime column every
/// window query runs against — actually holds.
/// </summary>
public sealed record TimeWindow(DateTimeOffset From, DateTimeOffset To)
{
    /// <summary>
    /// This window, or a refusal if it runs backwards. A backwards window matches nothing, so
    /// answering with an empty list would read as "you have logged nothing" rather than "you asked
    /// for something impossible".
    /// </summary>
    public Result<TimeWindow, Problem> Ordered() =>
        To < From
            ? Result<TimeWindow, Problem>.Failure(new Problem.Invalid(nameof(To), "The window's end precedes its start."))
            : Result<TimeWindow, Problem>.Success(this);
}
