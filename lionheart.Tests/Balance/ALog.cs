using lionheart.Model.BodyMeasurements;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Balance;
using lionheart.Tests.Support;

namespace lionheart.Tests.Balance;

/// <summary>
/// A training log for the pure volume, priority and score tests: movements that train what a test
/// says, sessions of hard sets on days, and bodyweights. Valid and empty by default.
/// </summary>
internal sealed record ALog(
    IReadOnlyDictionary<Guid, LoggedMovement> Movements,
    IReadOnlyList<SessionInLog> Sessions,
    IReadOnlyList<BodyweightOnDay> Bodyweights)
{
    public static ALog Empty => new(new Dictionary<Guid, LoggedMovement>(), [], []);

    public static readonly Guid Bench = Guid.NewGuid();
    public static readonly Guid Dip = Guid.NewGuid();
    public static readonly Guid Row = Guid.NewGuid();
    public static readonly Guid Pulldown = Guid.NewGuid();
    public static readonly Guid Squat = Guid.NewGuid();
    public static readonly Guid Curl = Guid.NewGuid();
    public static readonly Guid Raise = Guid.NewGuid();
    public static readonly Guid Mystery = Guid.NewGuid();

    /// <summary>A catalogue of the usual lifts: a push, a dip that adds to bodyweight, two pulls, a squat, two isolations, and one unclassified.</summary>
    public static ALog Catalogue => Empty
        .WithMovement(Bench, "Bench Press", Trains(MovementPattern.HorizontalPush, [MuscleGroup.Chest], [MuscleGroup.FrontDelts, MuscleGroup.Triceps]))
        .WithMovement(Dip, "Dip", Trains(MovementPattern.HorizontalPush, [MuscleGroup.Chest, MuscleGroup.Triceps], [MuscleGroup.FrontDelts]), addsToBodyweight: true)
        .WithMovement(Row, "Seated Row", Trains(MovementPattern.HorizontalPull, [MuscleGroup.UpperBack, MuscleGroup.Lats], [MuscleGroup.Biceps]))
        .WithMovement(Pulldown, "Lat Pulldown", Trains(MovementPattern.VerticalPull, [MuscleGroup.Lats], [MuscleGroup.Biceps]))
        .WithMovement(Squat, "Squat", Trains(MovementPattern.Squat, [MuscleGroup.Quads, MuscleGroup.Glutes], [MuscleGroup.Adductors]))
        .WithMovement(Curl, "Bicep Curl", Trains(MovementPattern.Isolation, [MuscleGroup.Biceps], []))
        .WithMovement(Raise, "Lateral Raise", Trains(MovementPattern.Isolation, [MuscleGroup.SideDelts], []))
        .WithMovement(Mystery, "Cable Mystery", new ClassificationStanding.Unclassified());

    public static ClassificationStanding Trains(MovementPattern pattern, MuscleGroup[] primary, MuscleGroup[] secondary) =>
        new ClassificationStanding.Classified(
            ResultAssert.Succeeded(MovementClassification.Of(pattern, primary, secondary)),
            new ClassificationSource.Map());

    public ALog WithMovement(Guid id, string name, ClassificationStanding trains, bool addsToBodyweight = false) =>
        this with
        {
            Movements = new Dictionary<Guid, LoggedMovement>(Movements)
            {
                [id] = new LoggedMovement(id, name, trains, addsToBodyweight),
            },
        };

    /// <summary>A session on the day, its hard sets as given; warm-ups are left out the way the reader leaves them out.</summary>
    public ALog WithSession(DateOnly day, params LiftSet[] sets) =>
        this with
        {
            Sessions = [.. Sessions, new SessionInLog(Guid.NewGuid(), day, [.. sets.Where(HardSets.IsHard)])],
        };

    /// <summary>A session of nothing but walking on the day, as a watch records a walk somewhere.</summary>
    public ALog WithWalk(DateOnly day) =>
        this with { Sessions = [.. Sessions, new SessionInLog(Guid.NewGuid(), day, []) { OnlyWalking = true }] };

    public ALog WithBodyweight(DateOnly day, double pounds) =>
        this with { Bodyweights = [.. Bodyweights, new BodyweightOnDay(day, new Bodyweight(pounds, WeightUnit.Pounds))] };

    public TrainingLog Build() => new([.. Sessions.OrderBy(session => session.Day)], Movements, Bodyweights);

    public static LiftSet Set(Guid movement, double pounds = 100, int reps = 8, SetKind kind = SetKind.Working) =>
        new(movement, reps, pounds, WeightUnit.Pounds, Rpe.Unrated, kind) { Source = new Formed(), Facets = [] };

    /// <summary>The same set, <paramref name="count"/> times.</summary>
    public static LiftSet[] Sets(int count, Guid movement, double pounds = 100, int reps = 8) =>
        [.. Enumerable.Range(0, count).Select(_ => Set(movement, pounds, reps))];
}
