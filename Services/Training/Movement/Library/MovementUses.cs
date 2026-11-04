using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>
/// Everything in an athlete's history that names one movement: the count of each kind, which is
/// what a merge moves and what keeps a movement from being retired.
/// </summary>
/// <remarks>
/// Previous performance is not counted: it is read from the sets, so it follows them.
/// </remarks>
public sealed record MovementUses(int Sets, int Prescriptions, int Routines, int Pins, int InjuryNotes, int Records)
{
    public static MovementUses None { get; } = new(0, 0, 0, 0, 0, 0);

    public int Total => Sets + Prescriptions + Routines + Pins + InjuryNotes + Records;

    public bool IsUnused => Total == 0;

    public static MovementUses operator +(MovementUses left, MovementUses right) => new(
        left.Sets + right.Sets, left.Prescriptions + right.Prescriptions, left.Routines + right.Routines,
        left.Pins + right.Pins, left.InjuryNotes + right.InjuryNotes, left.Records + right.Records);

    /// <summary>"12 sets, 3 records": each kind there is any of.</summary>
    public override string ToString() =>
        IsUnused
            ? "nothing"
            : string.Join(", ", new[]
                {
                    (Sets, "set"), (Prescriptions, "planned set"), (Routines, "routine"), (Pins, "pin"),
                    (InjuryNotes, "injury note"), (Records, "record"),
                }
                .Where(kind => kind.Item1 > 0)
                .Select(kind => $"{kind.Item1} {kind.Item2}{(kind.Item1 == 1 ? string.Empty : "s")}"));

    /// <summary>The uses of every movement the history names, by movement; one it never names is absent.</summary>
    public static IReadOnlyDictionary<Guid, MovementUses> In(
        IReadOnlyList<TrainingSession> sessions, IReadOnlyList<Routine> routines, IReadOnlyList<FocusPin> pins,
        IReadOnlyList<InjuryEvent> injuryNotes, IReadOnlyList<PersonalRecord> records) =>
        new[]
            {
                sessions.SelectMany(session => SessionMovements.SetsOf(session).ByMovement())
                    .Select(movement => (movement.Key, Uses: None with { Sets = movement.Count() })),
                sessions.SelectMany(SessionMovements.PrescribedIn)
                    .Select(movement => (Key: movement, Uses: None with { Prescriptions = 1 })),
                routines.SelectMany(routine => routine.Movements.Select(movement => movement.MovementDataId).Distinct())
                    .Select(movement => (Key: movement, Uses: None with { Routines = 1 })),
                pins.Select(pin => (Key: pin.MovementDataId, Uses: None with { Pins = 1 })),
                injuryNotes.SelectMany(note => note.MovementIDs.Distinct())
                    .Select(movement => (Key: movement, Uses: None with { InjuryNotes = 1 })),
                records.Select(record => record.Subject).OfType<RecordSubject.Movement>()
                    .Select(subject => (Key: subject.MovementDataId, Uses: None with { Records = 1 })),
            }
            .SelectMany(kind => kind)
            .GroupBy(use => use.Key, use => use.Uses)
            .ToDictionary(movement => movement.Key, movement => movement.Aggregate(None, (sum, uses) => sum + uses));
}
