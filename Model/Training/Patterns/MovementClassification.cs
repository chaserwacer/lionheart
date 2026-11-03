using System.Text.Json.Serialization;
using lionheart.Functional;

namespace lionheart.Model.Training.Patterns;

/// <summary>
/// What a movement trains: its pattern, the muscle groups it works as the primary mover, and the
/// ones it works secondarily.
/// </summary>
/// <remarks>
/// A set counts as one set for each primary group and half a set for each secondary one, the
/// counting Pelland et al. (Sports Medicine 2026) found fits the dose-response data better than
/// direct work alone. The order of the primary groups is kept, because the first one names the
/// family of an isolation movement.
/// <para>
/// The constructor is private and the factory is the only way in from a client. Storage reads
/// back through the same constructor without re-checking, as every stored JSON shape here does.
/// </para>
/// </remarks>
public sealed record MovementClassification
{
    [JsonConstructor]
    private MovementClassification(
        MovementPattern pattern, IReadOnlyList<MuscleGroup> primary, IReadOnlyList<MuscleGroup> secondary)
    {
        Pattern = pattern;
        Primary = primary;
        Secondary = secondary;
    }

    public MovementPattern Pattern { get; }

    public IReadOnlyList<MuscleGroup> Primary { get; }

    public IReadOnlyList<MuscleGroup> Secondary { get; }

    /// <summary>
    /// Push, pull, legs or core: the compound pattern's own family, or for an isolation or other
    /// movement the family of its first primary muscle.
    /// </summary>
    [JsonIgnore]
    public PatternFamily Family => MovementPatterns.FamilyOf(Pattern) ?? MuscleGroups.FamilyOf(Primary[0]);

    /// <summary>
    /// A classification as stated, or every reason it is not one: at least one primary group, and
    /// no group both primary and secondary. Repeats inside a list are dropped, order kept.
    /// </summary>
    public static Result<MovementClassification, Problem> Of(
        MovementPattern pattern, IEnumerable<MuscleGroup> primary, IEnumerable<MuscleGroup> secondary)
    {
        var primaries = primary.Distinct().ToList();
        var secondaries = secondary.Distinct().ToList();

        var problems = new List<InvalidField>();
        if (!Enum.IsDefined(pattern)) problems.Add(new InvalidField(nameof(Pattern), "Not a movement pattern."));
        if (primaries.Any(muscle => !Enum.IsDefined(muscle)))
            problems.Add(new InvalidField(nameof(Primary), "Not a muscle group."));
        if (secondaries.Any(muscle => !Enum.IsDefined(muscle)))
            problems.Add(new InvalidField(nameof(Secondary), "Not a muscle group."));
        if (primaries.Count is 0)
            problems.Add(new InvalidField(nameof(Primary), "A movement trains at least one primary muscle group."));
        if (primaries.Intersect(secondaries).Any())
            problems.Add(new InvalidField(nameof(Secondary), "A muscle group is primary or secondary, not both."));

        return problems.Count is 0
            ? Result<MovementClassification, Problem>.Success(new MovementClassification(pattern, primaries, secondaries))
            : Result<MovementClassification, Problem>.Failure(new Problem.Invalid(problems));
    }

    /// <summary>Equal by pattern and by both muscle lists in order, as a value is; a record compares lists by reference.</summary>
    public bool Equals(MovementClassification? other) =>
        other is not null && Pattern == other.Pattern
                          && Primary.SequenceEqual(other.Primary)
                          && Secondary.SequenceEqual(other.Secondary);

    public override int GetHashCode() =>
        HashCode.Combine(Pattern, string.Join(',', Primary), string.Join(',', Secondary));

    /// <summary>The share of one set a muscle is paid: 1 as a primary group, ½ as a secondary, else 0.</summary>
    public double ShareOf(MuscleGroup muscle) =>
        Primary.Contains(muscle) ? 1 : Secondary.Contains(muscle) ? 0.5 : 0;
}

/// <summary>
/// Whether the athlete has stated what a movement trains. Only a statement is stored; the map
/// file's entry is read live, so correcting the file reaches every base that has none.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Unstated), "unstated")]
[JsonDerivedType(typeof(Stated), "stated")]
public abstract record StatedClassification
{
    private protected StatedClassification() { }

    public sealed record Unstated : StatedClassification;

    public sealed record Stated(MovementClassification Classification) : StatedClassification;
}

/// <summary>
/// What a movement trains as the app reads it: the athlete's statement, else the map file's
/// entry for its base, else nothing.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Unclassified), "unclassified")]
[JsonDerivedType(typeof(Classified), "classified")]
public abstract record ClassificationStanding
{
    private protected ClassificationStanding() { }

    /// <summary>Neither stated nor in the map. Its sets are listed as unplaced, never guessed at.</summary>
    public sealed record Unclassified : ClassificationStanding;

    public sealed record Classified(MovementClassification Classification, ClassificationSource Source) : ClassificationStanding;
}

/// <summary>Where a classification came from, and so how far to trust it.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Athlete), "athlete")]
[JsonDerivedType(typeof(Map), "map")]
[JsonDerivedType(typeof(MapUnsure), "mapUnsure")]
public abstract record ClassificationSource
{
    private protected ClassificationSource() { }

    /// <summary>Stated or confirmed by the athlete in the app.</summary>
    public sealed record Athlete : ClassificationSource;

    /// <summary>The map file's entry for the base.</summary>
    public sealed record Map : ClassificationSource;

    /// <summary>The map file's entry, marked as a judgement call, with why.</summary>
    public sealed record MapUnsure(string Reason) : ClassificationSource;
}

/// <summary>Replaces the athlete's statement of what a movement base trains, whole.</summary>
public sealed record ClassifyMovementRequest(
    MovementPattern Pattern, IReadOnlyList<MuscleGroup> Primary, IReadOnlyList<MuscleGroup> Secondary)
{
    public Result<MovementClassification, Problem> ToClassification() =>
        MovementClassification.Of(Pattern, Primary ?? [], Secondary ?? []);
}
