using System.Text.Json.Serialization;
using lionheart.Functional;
using lionheart.Model.Training.Sessions;

namespace lionheart.Model.Training.Availability;

/// <summary>Whether a non-training day is one date or the same weekday every week.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NonTrainingRepeat>))]
public enum NonTrainingRepeat
{
    [JsonStringEnumMemberName("once")] Once,
    [JsonStringEnumMemberName("weekly")] Weekly,
}

/// <summary>
/// Marks a day non-training, as a client or a chat tool states it: the day, whether its weekday
/// repeats from it, the last day a repeat covers, and a note.
/// </summary>
/// <remarks>
/// Flat rather than the schedule's three cases, for the reason a focus target crosses as an optional
/// object: a flat shape generates a far better client. An absent note is an empty one, restored here
/// so nothing past the request sees a null.
/// </remarks>
public sealed record MarkNonTrainingDayRequest(
    DateOnly? Day,
    NonTrainingRepeat Repeat = NonTrainingRepeat.Once,
    DateOnly? Until = null,
    string? Note = null)
{
    /// <remarks>
    /// The day crosses as an optional value so a request that leaves it out is refused naming it,
    /// rather than bound to the first day of year one, where a weekly repeat would cover that weekday
    /// across all history.
    /// </remarks>
    public Result<NonTrainingSchedule, Problem> ToSchedule() =>
        (Day is { } day
            ? Result<DateOnly, Problem>.Success(day)
            : Result<DateOnly, Problem>.Failure(new Problem.Invalid(nameof(Day), "A non-training day names its day.")))
        .Bind(day => Stated.ClosedEnum(Repeat, nameof(Repeat))
            .Bind(repeat => (repeat, Until) switch
            {
                (NonTrainingRepeat.Once, null) => Result<NonTrainingSchedule, Problem>.Success(new NonTrainingSchedule.OneDay(day)),
                (NonTrainingRepeat.Once, _) => Result<NonTrainingSchedule, Problem>.Failure(
                    new Problem.Invalid(nameof(Until), "Only a weekly repeat has a last day.")),
                (NonTrainingRepeat.Weekly, null) => Result<NonTrainingSchedule, Problem>.Success(new NonTrainingSchedule.EveryWeek(day)),
                (NonTrainingRepeat.Weekly, { } until) => NonTrainingSchedule.EveryWeekUntil.Of(day, until),
                _ => throw new System.Diagnostics.UnreachableException(),
            }));

    public Result<string, Problem> ToNote()
    {
        var note = Note?.Trim() ?? string.Empty;
        return note.Length <= NonTrainingDay.MostNoteLength
            ? Result<string, Problem>.Success(note)
            : Result<string, Problem>.Failure(
                new Problem.Invalid(nameof(Note), $"A note is at most {NonTrainingDay.MostNoteLength} characters."));
    }
}
