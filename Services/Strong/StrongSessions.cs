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
/// Every set is a lift carrying Strong's kind for it: warm-up, drop, failure or working. A warm-up
/// sets no record; drop and failure sets were performed at the weight stated and count as working
/// sets do (see <see cref="SetKind"/>).
/// <para>
/// The session is the athlete's own log (<see cref="Provenance.User"/>), standalone, ad hoc,
/// undecorated like every structured session, and with no perceived effort, which Strong does not
/// record.
/// </para>
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
            Notes = string.Join("\n", NoteLines(workout)),
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            CreatedAt = createdAt,
        };

    private static SetEntry Entry(StrongSet set, Guid movementId) =>
        new LiftSet(movementId, set.Reps, set.Weight, ExportUnit, set.Rpe, Kind(set.Kind))
        {
            Source = new Formed(),
            Facets = [],
        };

    private static SetKind Kind(StrongSetKind kind) => kind switch
    {
        StrongSetKind.Working => SetKind.Working,
        StrongSetKind.WarmUp => SetKind.WarmUp,
        StrongSetKind.Drop => SetKind.Drop,
        StrongSetKind.Failure => SetKind.Failure,
        _ => throw new UnreachableException(),
    };

    // ---- Notes ----

    /// <summary>
    /// Everything Strong recorded about the workout that has no field of its own here, one line each.
    /// </summary>
    internal static IEnumerable<string> NoteLines(StrongWorkout workout) =>
    [
        .. Named(workout.Key.Name),
        .. Present(workout.Notes),
        .. workout.Sets
            .Where(set => set.Note.Length > 0)
            .Select(set => $"{set.Exercise}: {set.Note}")
            .Distinct(),
    ];

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

    /// <summary>A set's load, count and rating as the athlete would say them: "155 lb × 13 @ RPE 8".</summary>
    internal static string Performed(StrongSet set) =>
        $"{set.Weight.ToString(CultureInfo.InvariantCulture)} {UnitLabel} × {set.Reps}"
        + (set.Rpe.IsRated ? $" @ RPE {set.Rpe.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty);

    private static string UnitLabel => ExportUnit switch
    {
        WeightUnit.Pounds => "lb",
        WeightUnit.Kilograms => "kg",
        _ => throw new UnreachableException(),
    };
}
