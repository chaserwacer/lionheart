using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Strong;

/// <summary>
/// A Strong workout as the completed training session it imports as.
/// </summary>
/// <remarks>
/// Lionheart's set has no place for Strong's three special kinds of set, so each gets the most
/// faithful answer the domain already has, and none of them changes the schema:
/// <list type="bullet">
/// <item><b>Drop and failure sets</b> are lifts like any other. Every rep in them was performed at
/// the weight stated, so they count toward records and volume exactly as a working set would. What
/// kind of set each was is kept in the session's notes, which the athlete and the chat model read.</item>
/// <item><b>A warm-up</b> becomes a freeform set holding its own words, and is not a lift. A warm-up
/// must not set a record, and a freeform set raises none, so the stored session and the records
/// detected from it agree — the session reads exactly as it would had it been logged here that
/// way. The words keep the numbers, so a later set kind can upgrade it without anything being lost.</item>
/// </list>
/// The session is the athlete's own log (<see cref="Provenance.User"/>), standalone, undecorated
/// like every structured session, and with no perceived effort, which Strong does not record.
/// </remarks>
internal static partial class StrongSessions
{
    /// <summary>
    /// The unit every weight in a Strong export is in.
    /// </summary>
    /// <remarks>
    /// The export states no unit; it writes numbers in whatever the app was set to show. This
    /// athlete's is pounds, which the export itself bears out: a 315 × 5 squat, and kilogram plate
    /// loads arriving as 463 and 374. A Strong account set to kilograms would need this changed.
    /// </remarks>
    public const WeightUnit ExportUnit = WeightUnit.Pounds;

    private static string UnitLabel => ExportUnit switch
    {
        WeightUnit.Pounds => "lb",
        WeightUnit.Kilograms => "kg",
        _ => throw new UnreachableException(),
    };

    public static CompletedTrainingSession ToSession(
        StrongWorkout workout,
        Guid userId,
        IReadOnlyDictionary<string, Guid> movementIds,
        DateTime createdAt) =>
        new([.. workout.Sets.Select(set => Entry(set, movementIds[set.Exercise]))],
            workout.Period,
            new EffortNotReported(),
            new NotDecorated())
        {
            TrainingSessionId = workout.Key.SessionIdFor(userId),
            UserId = userId,
            Notes = Notes(workout),
            Provenance = Provenance.User,
            Membership = new Standalone(),
            CreatedAt = createdAt,
        };

    private static SetEntry Entry(StrongSet set, Guid movementId) => set.Kind switch
    {
        StrongSetKind.Working or StrongSetKind.Drop or StrongSetKind.Failure =>
            new LiftSet(movementId, set.Reps, set.Weight, ExportUnit, set.Rpe)
            {
                Source = new Formed(),
                Facets = [],
            },
        // Confidence zero, as a freeform set entered through the form has: no structure was
        // lifted from these words, they are the record.
        StrongSetKind.WarmUp => new FreeformSet
        {
            Source = new Texted($"Warm-up — {set.Exercise}: {Performed(set)}", 0),
            Facets = [],
        },
        _ => throw new UnreachableException(),
    };

    // ---- Notes ----

    /// <summary>
    /// Everything Strong recorded about the workout that has no field of its own here, one line each.
    /// </summary>
    private static string Notes(StrongWorkout workout) =>
        string.Join("\n", (IEnumerable<string>)
        [
            .. Named(workout.Key.Name),
            .. Present(workout.Notes),
            .. workout.Sets
                .Where(set => set.Note.Length > 0)
                .Select(set => $"{set.Exercise}: {set.Note}")
                .Distinct(),
            .. KindLine(workout, StrongSetKind.Drop, "Drop sets"),
            .. KindLine(workout, StrongSetKind.Failure, "Taken to failure"),
        ]);

    /// <summary>
    /// The workout's name, unless it is one Strong chose from the time of day.
    /// </summary>
    /// <remarks>
    /// "Afternoon Workout" on two hundred sessions says nothing the start time does not; a name the
    /// athlete typed — "RIT Legs", "apex gym" — is often the only record of where it happened.
    /// </remarks>
    private static IEnumerable<string> Named(string name) =>
        TimeOfDayName().IsMatch(name.Trim()) ? [] : Present(name);

    [GeneratedRegex("^(Early Morning|Morning|Midday|Afternoon|Evening|Night|Late Night) Workout$")]
    private static partial Regex TimeOfDayName();

    private static IEnumerable<string> Present(string text) =>
        text.Trim() is { Length: > 0 } present ? [present] : [];

    /// <summary>One line naming every set of a kind, grouped by exercise in the order they were done.</summary>
    private static IEnumerable<string> KindLine(StrongWorkout workout, StrongSetKind kind, string label)
    {
        var groups = workout.Sets
            .Where(set => set.Kind == kind)
            .GroupBy(set => set.Exercise)
            .Select(sets => $"{sets.Key} {string.Join(", ", sets.Select(Performed))}")
            .ToList();

        return groups.Count == 0 ? [] : [$"{label}: {string.Join("; ", groups)}"];
    }

    private static string Performed(StrongSet set) =>
        $"{set.Weight.ToString(CultureInfo.InvariantCulture)} {UnitLabel} × {set.Reps}"
        + (set.Rpe.IsRated ? $" @ RPE {set.Rpe.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty);
}
