using lionheart.Data;
using lionheart.Model.Training;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Import;

/// <summary>
/// One athlete's movements, bases, equipment and modifiers, held for a batch that finds each
/// movement it names or stages it, without saving.
/// </summary>
/// <remarks>
/// The movement services each find-or-create one name per call, take a signed-in identity, and
/// save as they go, which suits a form and not an import of a hundred names. This reads the
/// catalogue once and stages everything into the caller's unit of work, so the import's own
/// transaction decides when any of it is written.
/// <para>
/// Names match the way the rest of the application matches them — ignoring case and surrounding
/// spaces — so "Dumbell" in the athlete's catalogue is reused rather than joined by a second
/// spelling of the same thing. A new modifier is stored lowercase, as the modifier service stores
/// them.
/// </para>
/// </remarks>
internal sealed class MovementCatalogue
{
    private readonly ModelContext _context;
    private readonly Guid _userId;
    private readonly DateTime _now;
    private readonly List<MovementBase> _bases;
    private readonly List<Equipment> _equipment;
    private readonly List<MovementModifier> _modifiers;
    private readonly List<MovementData> _movements;
    private readonly List<MovementName> _created = [];
    private readonly HashSet<object> _staged = new(ReferenceEqualityComparer.Instance);

    private MovementCatalogue(
        ModelContext context, Guid userId, DateTime now,
        List<MovementBase> bases, List<Equipment> equipment, List<MovementModifier> modifiers, List<MovementData> movements)
    {
        _context = context;
        _userId = userId;
        _now = now;
        _bases = bases;
        _equipment = equipment;
        _modifiers = modifiers;
        _movements = movements;
    }

    public static async Task<MovementCatalogue> LoadAsync(
        ModelContext context, Guid userId, DateTime now, CancellationToken ct) =>
        new(
            context,
            userId,
            now,
            await context.MovementBases.Where(b => b.UserID == userId).OrderBy(b => b.MovementBaseID).ToListAsync(ct),
            await context.Equipments.Where(e => e.UserID == userId).OrderBy(e => e.EquipmentID).ToListAsync(ct),
            await context.MovementModifiers.Where(m => m.UserID == userId).OrderBy(m => m.MovementModifierID).ToListAsync(ct),
            await context.MovementDatas.Where(m => m.UserID == userId).OrderBy(m => m.MovementDataID).ToListAsync(ct));

    /// <summary>Everything this batch staged because the athlete did not have it.</summary>
    public CatalogueGrowth Growth => new(
        _created,
        [.. _bases.Where(_staged.Contains).Select(b => b.Name)],
        [.. _equipment.Where(_staged.Contains).Select(e => e.Name)],
        [.. _modifiers.Where(_staged.Contains).Select(m => m.Name)]);

    /// <summary>The id of the athlete's movement by these names, staging whatever part of it is new.</summary>
    public Guid IdOf(MovementName name)
    {
        var movementBase = Named(_bases, b => b.Name, name.Base) ?? Staged(_bases, new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = name.Base,
            UserID = _userId,
            Description = string.Empty,
            MuscleGroups = [],
        });

        var equipment = Named(_equipment, e => e.Name, name.Equipment) ?? Staged(_equipment, new Equipment
        {
            EquipmentID = Guid.NewGuid(),
            Name = name.Equipment,
            UserID = _userId,
        });

        var modifier = name.HasModifier
            ? Named(_modifiers, m => m.Name, name.Modifier) ?? Staged(_modifiers, new MovementModifier
            {
                MovementModifierID = Guid.NewGuid(),
                Name = name.Modifier.ToLowerInvariant(),
                UserID = _userId,
            })
            : null;

        return (_movements.FirstOrDefault(movement =>
                    movement.MovementBaseID == movementBase.MovementBaseID
                    && movement.EquipmentID == equipment.EquipmentID
                    && movement.MovementModifierID == modifier?.MovementModifierID)
                ?? NewMovement(name, movementBase, equipment, modifier))
            .MovementDataID;
    }

    /// <summary>
    /// Whether the athlete already has the movement by these names, staging nothing when they do not.
    /// </summary>
    public bool Holds(MovementName name, out Guid movementDataId)
    {
        var movementBase = Named(_bases, b => b.Name, name.Base);
        var equipment = Named(_equipment, e => e.Name, name.Equipment);
        var modifier = name.HasModifier ? Named(_modifiers, m => m.Name, name.Modifier) : null;

        var held = movementBase is null || equipment is null || (name.HasModifier && modifier is null)
            ? null
            : _movements.FirstOrDefault(movement =>
                movement.MovementBaseID == movementBase.MovementBaseID
                && movement.EquipmentID == equipment.EquipmentID
                && movement.MovementModifierID == modifier?.MovementModifierID);

        movementDataId = held?.MovementDataID ?? Guid.Empty;
        return held is not null;
    }

    /// <summary>A movement of this athlete's as the names it is made of, for saying which one is meant.</summary>
    public string NameOf(Guid movementDataId) =>
        _movements.FirstOrDefault(movement => movement.MovementDataID == movementDataId) is { } movement
            ? new MovementName(
                movement.MovementBase.Name, movement.Equipment.Name, movement.MovementModifier?.Name ?? string.Empty).ToString()
            : $"movement {movementDataId}";

    private MovementData NewMovement(
        MovementName name, MovementBase movementBase, Equipment equipment, MovementModifier? modifier)
    {
        _created.Add(name);
        return Staged(_movements, new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = _userId,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase,
            MovementModifierID = modifier?.MovementModifierID,
            MovementModifier = modifier,
            CreatedAt = _now,
        });
    }

    private static T? Named<T>(IEnumerable<T> entities, Func<T, string> nameOf, string name) where T : class =>
        entities.FirstOrDefault(entity => nameOf(entity).Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    private T Staged<T>(List<T> held, T entity) where T : class
    {
        held.Add(entity);
        _staged.Add(entity);
        _context.Add(entity);
        return entity;
    }
}

/// <summary>What an import added to the athlete's catalogue, by name.</summary>
public sealed record CatalogueGrowth(
    IReadOnlyList<MovementName> Movements,
    IReadOnlyList<string> Bases,
    IReadOnlyList<string> Equipment,
    IReadOnlyList<string> Modifiers);
