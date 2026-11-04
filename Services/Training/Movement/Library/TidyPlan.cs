using lionheart.Model.Training;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Import;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>An athlete's movement library as it stands: what the tidy plans from.</summary>
public sealed record LibraryState(
    IReadOnlyList<BaseRow> Bases,
    IReadOnlyList<PartRow> Equipment,
    IReadOnlyList<PartRow> Modifiers,
    IReadOnlyList<HeldMovement> Movements);

/// <summary>An equipment or a modifier: a name a movement points at.</summary>
public sealed record PartRow(Guid Id, string Name);

/// <summary>A movement base, with what the athlete has said about it, which a fold must not lose.</summary>
public sealed record BaseRow(Guid Id, string Name, StatedClassification Statement, string Description)
{
    public PartRow Part => new(Id, Name);

    public bool SaysAnything => Statement is StatedClassification.Stated || Description.Trim().Length > 0;
}

/// <summary>One of the athlete's movements: its names, the rows they are, its setup and its uses.</summary>
public sealed record HeldMovement(
    Guid Id,
    MovementName Name,
    Guid BaseId,
    Guid EquipmentId,
    Guid? ModifierId,
    MovementSetup Setup,
    DateTime CreatedAt,
    MovementUses Uses);

/// <summary>What the tidy does to one existing movement.</summary>
public sealed record MovementOutcome(Guid MovementDataId, MovementName From, MovementFate Fate);

/// <summary>A movement's fate in a tidy.</summary>
public abstract record MovementFate
{
    private protected MovementFate() { }

    /// <summary>Already named by the convention. Its parts may still move to the row that spells them.</summary>
    public sealed record Kept : MovementFate;

    /// <summary>Kept, under new names.</summary>
    public sealed record Restated(MovementName To, string Unsure) : MovementFate;

    /// <summary>
    /// The same movement as <paramref name="IntoId"/> under the convention, so everything naming it
    /// moves there and it goes.
    /// </summary>
    public sealed record Merged(Guid IntoId, MovementName To, MovementUses Moved, string Unsure) : MovementFate;

    /// <summary>Removed; nothing names it.</summary>
    public sealed record Retired(string Unsure) : MovementFate;
}

/// <summary>What the tidy does to one row of a part — or, for a created one, the name it adds.</summary>
public sealed record PartOutcome(MovementPart Part, string Name, PartFate Fate)
{
    /// <summary>The row that goes, for a folded or removed one.</summary>
    public Guid? GoingRowId => Fate switch
    {
        PartFate.Folded folded => folded.RowId,
        PartFate.Removed removed => removed.RowId,
        PartFate.Renamed or PartFate.Created => null,
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}

/// <summary>A part row's fate in a tidy.</summary>
public abstract record PartFate
{
    private protected PartFate() { }

    /// <summary>The row keeps its id and its movements under a new name.</summary>
    public sealed record Renamed(Guid RowId, string To) : PartFate;

    /// <summary>The row goes; its movements now use the row named <paramref name="Into"/>.</summary>
    public sealed record Folded(Guid RowId, string Into) : PartFate;

    /// <summary>The row goes; no movement uses it any more.</summary>
    public sealed record Removed(Guid RowId) : PartFate;

    /// <summary>A new row, for a name no row has.</summary>
    public sealed record Created : PartFate;
}

/// <summary>What a base that goes hands to the base its movements land on, which had none.</summary>
public sealed record BaseCarry(string From, string Into, StatedClassification Statement, string Description);

/// <summary>A merged movement's setup, handed to the movement it merged into, which had none.</summary>
public sealed record SetupCarry(Guid IntoId, MovementSetup Setup);

/// <summary>Everything one tidy does, planned before anything is written.</summary>
public sealed record TidyPlan(
    IReadOnlyList<MovementOutcome> Movements,
    IReadOnlyList<MovementName> Added,
    IReadOnlyList<PartOutcome> Parts,
    IReadOnlyList<BaseCarry> BaseCarries,
    IReadOnlyList<SetupCarry> SetupCarries)
{
    /// <summary>Nothing to do: the library is already tidy.</summary>
    public bool IsEmpty =>
        Movements.All(movement => movement.Fate is MovementFate.Kept) && Added.Count == 0 && Parts.Count == 0;

    /// <summary>Movements that go into another, keyed by the one that goes.</summary>
    public IReadOnlyDictionary<Guid, Guid> Merges => MergesOf(Movements);

    /// <summary>The name each movement that stays is called afterwards, by id.</summary>
    public IReadOnlyDictionary<Guid, MovementName> Survivors => SurvivorsOf(Movements);

    internal static IReadOnlyDictionary<Guid, Guid> MergesOf(IEnumerable<MovementOutcome> movements) =>
        movements
            .SelectMany(movement => movement.Fate is MovementFate.Merged merged
                ? [(Gone: movement.MovementDataId, merged.IntoId)]
                : Array.Empty<(Guid Gone, Guid IntoId)>())
            .ToDictionary(merge => merge.Gone, merge => merge.IntoId);

    internal static IReadOnlyDictionary<Guid, MovementName> SurvivorsOf(IEnumerable<MovementOutcome> movements) =>
        movements
            .SelectMany(movement => movement.Fate switch
            {
                MovementFate.Kept => [(movement.MovementDataId, Name: movement.From)],
                MovementFate.Restated restated => [(movement.MovementDataId, Name: restated.To)],
                _ => Array.Empty<(Guid MovementDataId, MovementName Name)>(),
            })
            .ToDictionary(survivor => survivor.MovementDataId, survivor => survivor.Name);
}
