using lionheart.Functional;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Routines;

/// <summary>One movement in a routine's order, and how many working sets the athlete does of it.</summary>
/// <remarks>
/// No target weight or reps: a routine's targets come from history, the athlete's previous
/// performance on the movement, so a number stored here would only go stale.
/// </remarks>
public sealed record RoutineMovement(Guid MovementDataId, int WorkingSets)
{
    /// <summary>
    /// The movements a set list trained, in first-appearance order, each with its count of sets
    /// that were not warm-ups. A movement trained only in warm-ups is left out.
    /// </summary>
    public static IReadOnlyList<RoutineMovement> TrainedIn(IEnumerable<SetEntry> sets) =>
    [
        .. sets.ByMovement()
            // A drop set extends the set before it rather than being one of the routine's sets.
            .Select(movement => new RoutineMovement(
                movement.Key, movement.Count(set => set.IsWorking && set is not LiftSet { Kind: SetKind.Drop })))
            .Where(movement => movement.WorkingSets > 0)
    ];
}

/// <summary>
/// A workout the athlete repeats: a named order of movements, undated and reusable.
/// </summary>
/// <remarks>
/// Its own aggregate rather than a planned session or a program. A planned session is dated, done
/// once, and its prescriptions are consumed by the lifecycle; a program is a dated block with no
/// content of its own. A routine is neither dated nor consumed — "upper day" is trained many times
/// — and sessions point back at it through <see cref="Sessions.RoutineOrigin"/>.
/// <para>
/// An entity rather than a record, since revising a routine changes it and it stays the same
/// routine. Everything it holds is immutable, and the only way to change it is
/// <see cref="Revise"/> with content that has already passed <see cref="RoutineContent.Of"/>.
/// </para>
/// </remarks>
public sealed class Routine
{
    private Routine() { }

    public Guid RoutineId { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public IReadOnlyList<RoutineMovement> Movements { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }

    public static Routine Define(Guid routineId, Guid userId, RoutineContent content, DateTime createdAt)
    {
        var routine = new Routine { RoutineId = routineId, UserId = userId, CreatedAt = createdAt };
        routine.Revise(content);
        return routine;
    }

    /// <summary>Replaces what the routine says — its name, notes and movements — in place.</summary>
    public void Revise(RoutineContent content)
    {
        Name = content.Name;
        Notes = content.Notes;
        Movements = content.Movements;
    }
}

/// <summary>
/// What a routine says, checked: the only form in which a routine can be defined or revised.
/// </summary>
/// <remarks>
/// Every rule here guards a value a client states, so each one refuses rather than throws — a
/// throw from the entity would land past the point where the endpoint could answer 400.
/// </remarks>
public sealed record RoutineContent
{
    /// <summary>
    /// The most working sets a routine prescribes of one movement. Past this the number is a
    /// typing slip rather than a plan.
    /// </summary>
    public const int MaxWorkingSets = 20;

    private RoutineContent(string name, string notes, IReadOnlyList<RoutineMovement> movements)
    {
        Name = name;
        Notes = notes;
        Movements = movements;
    }

    public string Name { get; }
    public string Notes { get; }
    public IReadOnlyList<RoutineMovement> Movements { get; }

    public static Result<RoutineContent, Problem> Of(
        string name, string notes, IReadOnlyList<RoutineMovement> movements) =>
        Named(name).Bind(trimmed => Ordered(movements)
            .Map(order => new RoutineContent(trimmed, notes, order)));

    private static Result<string, Problem> Named(string name) =>
        name.Trim() is { Length: > 0 } trimmed
            ? Result<string, Problem>.Success(trimmed)
            : Result<string, Problem>.Failure(new Problem.Invalid(nameof(Name), "A routine needs a name."));

    private static Result<IReadOnlyList<RoutineMovement>, Problem> Ordered(
        IReadOnlyList<RoutineMovement> movements) =>
        movements.Count == 0
            ? Result<IReadOnlyList<RoutineMovement>, Problem>.Failure(
                new Problem.Invalid(nameof(Movements), "A routine needs at least one movement."))
            : movements.Select(movement => movement.MovementDataId).Distinct().Count() != movements.Count
                ? Result<IReadOnlyList<RoutineMovement>, Problem>.Failure(
                    new Problem.Invalid(nameof(Movements), "Each movement appears in a routine once."))
                : movements.Index().Traverse(Counted);

    private static Result<RoutineMovement, Problem> Counted((int Index, RoutineMovement Item) entry) =>
        entry.Item.WorkingSets is >= 1 and <= MaxWorkingSets
            ? Result<RoutineMovement, Problem>.Success(entry.Item)
            : Result<RoutineMovement, Problem>.Failure(new Problem.Invalid(
                $"{nameof(Movements)}[{entry.Index}].{nameof(RoutineMovement.WorkingSets)}",
                $"Working sets must be between 1 and {MaxWorkingSets}."));
}
