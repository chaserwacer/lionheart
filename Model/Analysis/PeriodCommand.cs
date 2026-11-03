using lionheart.Functional;

namespace lionheart.Model.Analysis;

/// <summary>
/// When something happened, as a client states it: a start, and an end only if there was one.
/// </summary>
/// <remarks>
/// Flat rather than a polymorphic <see cref="Period"/> body, for the reason docs/api-surface.md
/// gives for the two context creates — a flat shape generates a far better TypeScript client than
/// a two-case union whose only difference is one extra field.
/// <para>
/// It also has to be flat for a second reason: <see cref="Interval"/> throws when its end precedes
/// its start, and a throw raised while the input formatter is deserializing escapes as a 500. A
/// client's reversed dates are a refusal, so the ordering is checked here, where it becomes a 400.
/// </para>
/// </remarks>
public sealed record PeriodCommand(DateTimeOffset Start, DateTimeOffset? End)
{
    public Result<Period, Problem> ToPeriod() => End switch
    {
        null => Result<Period, Problem>.Success(new Instant(Start)),
        { } end when end < Start => Result<Period, Problem>.Failure(
            new Problem.Invalid(nameof(End), "The period's end precedes its start.")),
        { } end => Result<Period, Problem>.Success(new Interval(Start, end))
    };
}
