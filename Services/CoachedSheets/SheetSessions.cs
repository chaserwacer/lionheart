using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Programs;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Import;

namespace lionheart.Services.CoachedSheets;

/// <summary>
/// A coached-programme session as the completed training session it imports as, and a programme
/// sheet as the completed programme its sessions belong to.
/// </summary>
/// <remarks>
/// The session is the athlete's own log (<see cref="Provenance.User"/>), undecorated, with no
/// perceived effort, and a member of its programme — which is what keeps this capture method
/// distinguishable from the Strong history beside it. Every set is a pounds lift with the RPE the
/// athlete wrote, if he wrote one.
/// </remarks>
internal static partial class SheetSessions
{
    /// <summary>
    /// The wall-clock time a session is placed at. The sheets give a date and nothing else.
    /// </summary>
    /// <remarks>
    /// Noon rather than midnight, so the session stays on its day at any offset the athlete's clock
    /// could have kept; the notes say the time is not his. An instant rather than an interval,
    /// because a duration would be one more number nobody recorded.
    /// </remarks>
    public static readonly TimeOnly PlacedAt = new(12, 0);

    /// <summary>
    /// The namespace every coached-programme session and programme id is derived under. Fixed
    /// forever: changing it would let the next run import the history twice.
    /// </summary>
    private static readonly Guid Namespace = new("3c9a7e52-41d6-5b0f-8e2a-7d4c19b6f0a3");

    /// <summary>The session id a workout imports as, the same on every run and for no other athlete.</summary>
    /// <remarks>Derived from training-os's session id, which is the date, the sheet and the day.</remarks>
    public static Guid SessionIdFor(SheetWorkout workout, Guid userId) =>
        NameBasedId.Of(Namespace, $"{userId:D}|session|{workout.Session}");

    public static Guid ProgramIdFor(string sheet, Guid userId) =>
        NameBasedId.Of(Namespace, $"{userId:D}|program|{sheet}");

    public static CompletedTrainingSession ToSession(
        SheetWorkout workout,
        SheetProgram program,
        IReadOnlySet<string> templateText,
        Guid userId,
        IReadOnlyDictionary<string, Guid> movementIds,
        TimeZoneInfo zone,
        DateTime createdAt) =>
        new([.. workout.Sets.Select(set => Lift(set, movementIds[set.Exercise]))],
            new Instant(Placed(workout.Date, zone)),
            new EffortNotReported(),
            new NotDecorated())
        {
            TrainingSessionId = SessionIdFor(workout, userId),
            UserId = userId,
            Notes = Notes(workout, program, templateText),
            Provenance = Provenance.User,
            Membership = new InProgram(ProgramIdFor(program.Sheet, userId)),
            CreatedAt = createdAt,
        };

    /// <summary>
    /// The programme a sheet is, spanning the sessions the log holds for it.
    /// </summary>
    /// <remarks>
    /// Completed: these blocks ended years ago. Dated by the log's sessions rather than the sheet's
    /// planned weeks, so the span is what was trained.
    /// </remarks>
    public static CompletedTrainingProgram ToProgram(SheetProgram program, IReadOnlyList<SheetWorkout> workouts, Guid userId) =>
        new(program.Summary)
        {
            TrainingProgramId = ProgramIdFor(program.Sheet, userId),
            UserId = userId,
            Title = program.Title,
            StartDate = workouts.Min(workout => workout.Date),
            EndDate = workouts.Max(workout => workout.Date),
            Tags = program.Tags,
        };

    private static LiftSet Lift(SheetSet set, Guid movementId) =>
        new(movementId, set.Reps, set.Weight, SheetSet.Unit, set.Rpe)
        {
            Source = new Formed(),
            Facets = [],
        };

    /// <remarks>
    /// At the offset the zone kept on that date, the way the Strong import places its times. Noon
    /// is never inside a daylight-saving change, so the local time always exists exactly once.
    /// </remarks>
    private static DateTimeOffset Placed(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(PlacedAt);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    // ---- Notes ----

    /// <summary>
    /// Where the session came from, then the coach's notes for its rows, then the rows that were
    /// never lifted — one line each.
    /// </summary>
    internal static string Notes(SheetWorkout workout, SheetProgram program, IReadOnlySet<string> templateText) =>
        string.Join("\n", (IEnumerable<string>)
        [
            $"{program.Title}, week {workout.Source.Week}, {workout.Day}.",
            "From the programme spreadsheet, which dates a session by its week and day only; the time is set to noon.",
            .. workout.Recorded
                .Select(set => (Set: set, Note: CoachNote(set.Note, templateText)))
                .Where(row => row.Note.Length > 0)
                .Select(row => $"{row.Set.ExerciseAsWritten} {SheetHistory.Pounds(row.Set.Weight)} × {row.Set.Reps}: {row.Note}")
                .Distinct(),
            .. workout.NotCounted.Select(row =>
                $"Not counted as lifted: {row.Exercise} {row.Sets} × {SheetHistory.Pounds(row.Weight)} × {row.Reps}, {Because(row.Kind)}."),
        ]);

    /// <summary>
    /// The coach's words on a row, less the template's boilerplate and any prescribed RPE that is
    /// not a rating at all.
    /// </summary>
    /// <remarks>
    /// training-os joins a row's prescribed RPE and its notes with " | ". Some prescribed-RPE cells
    /// hold a weight, a date serial or a percentage sign the coach typed into the wrong column, and
    /// a line reading "prescribed RPE 45147" would mislead whoever reads the session.
    /// </remarks>
    internal static string CoachNote(string note, IReadOnlySet<string> templateText) =>
        string.Join(" | ", note
            .Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !templateText.Any(text => part.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
            .Where(part => PrescribedRpe().Match(part) is not { Success: true } prescribed || IsRating(prescribed.Groups["value"].Value)));

    private static bool IsRating(string prescribed)
    {
        var numbers = Number().Matches(prescribed)
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToList();

        return numbers.Count > 0 && numbers.All(value => value is >= 1 and <= 10);
    }

    [GeneratedRegex("^prescribed RPE (?<value>.*)$")]
    private static partial Regex PrescribedRpe();

    [GeneratedRegex(@"[0-9]+(?:\.[0-9]+)?")]
    private static partial Regex Number();

    private static string Because(NotPerformedKind kind) => kind switch
    {
        NotPerformedKind.Computed => "the spreadsheet computed the weight and it was never entered",
        NotPerformedKind.Placeholder => "a placeholder entry",
        NotPerformedKind.Contradicted => "the athlete's note says it did not happen as written",
        NotPerformedKind.Implausible => "the weight cannot be right as entered",
        _ => throw new UnreachableException(),
    };
}
