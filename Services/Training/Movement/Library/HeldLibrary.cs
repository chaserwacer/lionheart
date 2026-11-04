using lionheart.Model.InjuryManagement;
using lionheart.Model.Training;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Import;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>
/// The athlete's library and everything naming its movements, as tracked rows: what a tidy reads
/// its plan from and stages its changes into.
/// </summary>
/// <remarks>
/// Rows created during staging are held here too, so a name the tidy creates is found by the same
/// lookup as one the athlete already had. A row the plan folds or removes is never found by name,
/// even when it spells one the library keeps: the plan points every movement at the row kept for
/// its name.
/// </remarks>
internal sealed class HeldLibrary
{
    private readonly List<MovementBase> _bases;
    private readonly List<Equipment> _equipment;
    private readonly List<MovementModifier> _modifiers;
    private readonly HashSet<Guid> _going = [];

    public HeldLibrary(
        Guid userId,
        LibraryState state,
        List<MovementBase> bases,
        List<Equipment> equipment,
        List<MovementModifier> modifiers,
        IReadOnlyList<MovementData> movements,
        IReadOnlyList<TrainingSession> sessions,
        IReadOnlyList<Routine> routines,
        IReadOnlyList<FocusPin> pins,
        IReadOnlyList<InjuryEvent> injuryNotes,
        LibraryCounts before)
    {
        UserId = userId;
        State = state;
        _bases = bases;
        _equipment = equipment;
        _modifiers = modifiers;
        Movements = movements;
        Sessions = sessions;
        Routines = routines;
        Pins = pins;
        InjuryNotes = injuryNotes;
        Before = before;
    }

    public Guid UserId { get; }
    public LibraryState State { get; }
    public IReadOnlyList<MovementData> Movements { get; }
    public IReadOnlyList<TrainingSession> Sessions { get; }
    public IReadOnlyList<Routine> Routines { get; }
    public IReadOnlyList<FocusPin> Pins { get; }
    public IReadOnlyList<InjuryEvent> InjuryNotes { get; }
    public LibraryCounts Before { get; }

    /// <summary>Takes the rows the plan folds or removes out of every name lookup from now on.</summary>
    public void SetAside(IEnumerable<Guid> goingRowIds) => _going.UnionWith(goingRowIds);

    public void Rename(MovementPart part, Guid rowId, string name)
    {
        switch (Row(part, rowId))
        {
            case MovementBase movementBase: movementBase.Name = name; break;
            case Equipment equipment: equipment.Name = name; break;
            case MovementModifier modifier: modifier.Name = name; break;
            default: throw new System.Diagnostics.UnreachableException();
        }
    }

    /// <summary>A new row for a name no row has, held so the name is found from now on. Equipment starts enabled.</summary>
    public object Create(MovementPart part, string name) => part switch
    {
        MovementPart.Base => Held(_bases, new MovementBase
        {
            MovementBaseID = Guid.NewGuid(), Name = name, UserID = UserId, Description = string.Empty,
        }),
        MovementPart.Equipment => Held(_equipment, new Equipment { EquipmentID = Guid.NewGuid(), Name = name, UserID = UserId }),
        MovementPart.Modifier => Held(_modifiers, new MovementModifier
        {
            MovementModifierID = Guid.NewGuid(), Name = name.ToLowerInvariant(), UserID = UserId,
        }),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>The row of this part with this id.</summary>
    public object Row(MovementPart part, Guid rowId) => part switch
    {
        MovementPart.Base => _bases.Single(row => row.MovementBaseID == rowId),
        MovementPart.Equipment => _equipment.Single(row => row.EquipmentID == rowId),
        MovementPart.Modifier => _modifiers.Single(row => row.MovementModifierID == rowId),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    public MovementBase BaseNamed(string name) => Named(_bases, row => row.MovementBaseID, row => row.Name, name);

    /// <summary>The rows that spell a movement's name: its base, its equipment, and its modifier if it has one.</summary>
    public (MovementBase Base, Equipment Equipment, MovementModifier? Modifier) PartsFor(MovementName name) =>
        (BaseNamed(name.Base),
            Named(_equipment, row => row.EquipmentID, row => row.Name, name.Equipment),
            name.HasModifier ? Named(_modifiers, row => row.MovementModifierID, row => row.Name, name.Modifier) : null);

    private static T Held<T>(List<T> rows, T row)
    {
        rows.Add(row);
        return row;
    }

    private T Named<T>(IEnumerable<T> rows, Func<T, Guid> idOf, Func<T, string> nameOf, string name) =>
        rows.First(row => !_going.Contains(idOf(row))
                          && nameOf(row).Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
}
