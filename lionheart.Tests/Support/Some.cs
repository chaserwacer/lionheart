using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Training;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using Programs = lionheart.Model.Training.Programs;

namespace lionheart.Tests.Support;

/// <summary>
/// Canonical fully-populated instances of the remodeled domain. Tests start from these
/// and override what the scenario cares about via <c>with</c>.
/// </summary>
public static class Some
{
    public static readonly DateTime Clock = new(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc);

    public static IReadOnlyList<Facet> Facets =>
        [new(FacetAxis.Modality, "lift"), new(FacetAxis.MovementPattern, "push")];

    public static Decoration Decoration => new(
        Facets,
        FreeTags: ["pr-attempt", "felt-strong"],
        Valence.Positive,
        SummaryText: "Heavy push session, bench PR attempt at high effort.",
        DecoratorVersion: 1);

    public static Texted TextedSource => new("sled pull+push worked up to 235, 2 or 3 sets", 0.6);

    public static EffortState ReportedEffort => new EffortReported(new PerceivedEffortRatings
    {
        RecordedAt = Clock,
        AccumulatedFatigue = 6,
        DifficultyRating = 8,
        EngagementRating = 7,
        ExternalVariablesRating = 3,
    });

    public static LiftSet LiftSet => new(Guid.NewGuid(), Reps: 5, Weight: 225, WeightUnit.Pounds, Rpe.Of(8.5), SetKind.Working)
    {
        Source = new Formed(),
        Facets = Facets,
    };

    public static DistanceTimeSet DistanceTimeSet =>
        new(Guid.NewGuid(), Distance: 1600, DistanceUnit.Meters, TimeSpan.FromMinutes(8), Rpe.Unrated)
        {
            Source = new Formed(),
            Facets = [new(FacetAxis.Modality, "run")],
        };

    public static TrailRunSet TrailRunSet =>
        new(Distance: 6.2, DistanceUnit.Miles, TimeSpan.FromMinutes(64), ElevationGainMeters: 410, Rpe.Of(7))
        {
            Source = new Formed(),
            Facets = [new(FacetAxis.Modality, "run")],
        };

    public static FreeformSet FreeformSet => new()
    {
        Source = TextedSource,
        Facets = [new(FacetAxis.Modality, "sled")],
    };

    public static IReadOnlyList<SetEntry> AllSetShapes =>
        [LiftSet, DistanceTimeSet, TrailRunSet, FreeformSet];

    public static IReadOnlyList<SetPrescription> AllPrescriptionShapes =>
    [
        new LiftPrescription(Guid.NewGuid(), TargetReps: 5, TargetWeight: 225, WeightUnit.Pounds, Rpe.Of(8)),
        new DistanceTimePrescription(Guid.NewGuid(), TargetDistance: 1600, DistanceUnit.Meters, TimeSpan.FromMinutes(8)),
        new FreeformPrescription("hard intervals, coach's choice"),
    ];

    public static PlannedTrainingSession PlannedSession(Guid userId) =>
        new(new DateOnly(2026, 7, 21), AllPrescriptionShapes)
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = userId,
            Notes = "push day",
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.AthleteLog(),
            CreatedAt = Clock,
        };

    public static ActiveTrainingSession ActiveSession(Guid userId) =>
        new(Clock, AllSetShapes)
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = userId,
            Notes = "in progress",
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.AthleteLog(),
            CreatedAt = Clock,
        };

    public static CompletedTrainingSession CompletedSession(Guid userId) =>
        new(AllSetShapes, new Interval(Clock, Clock.AddMinutes(75)), ReportedEffort, new Decorated(Decoration))
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = userId,
            Notes = "done, felt strong",
            Provenance = Provenance.AiAssisted,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.AthleteLog(),
            CreatedAt = Clock,
        };

    public static SkippedTrainingSession SkippedSession(Guid userId) =>
        new(new DateOnly(2026, 7, 21))
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = userId,
            Notes = "travel day",
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.AthleteLog(),
            CreatedAt = Clock,
        };

    public static ExertionContext ExertionContext(Guid userId) =>
        new(ReportedEffort)
        {
            ContextId = Guid.NewGuid(),
            UserId = userId,
            Period = new Interval(Clock, Clock.AddHours(3)),
            Notes = "pressure-washed the driveway",
            Decoration = new Decorated(Decoration),
        };

    public static StateContext StateContext(Guid userId) =>
        new()
        {
            ContextId = Guid.NewGuid(),
            UserId = userId,
            Period = new Instant(Clock),
            Notes = "terrible sleep last night",
            Decoration = new NotDecorated(),
        };

    public static Programs.PlannedTrainingProgram PlannedProgram(Guid userId) =>
        new()
        {
            TrainingProgramId = Guid.NewGuid(),
            UserId = userId,
            Title = "Fall strength block",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 10, 27),
            Tags = ["strength", "hypertrophy"],
        };

    public static Programs.ActiveTrainingProgram ActiveProgram(Guid userId) =>
        new()
        {
            TrainingProgramId = Guid.NewGuid(),
            UserId = userId,
            Title = "Summer conditioning",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 8, 15),
            Tags = ["conditioning"],
        };

    public static Programs.CompletedTrainingProgram CompletedProgram(Guid userId) =>
        new("Hit every main-lift target; conditioning lagged in week 4.")
        {
            TrainingProgramId = Guid.NewGuid(),
            UserId = userId,
            Title = "Spring peaking block",
            StartDate = new DateOnly(2026, 3, 1),
            EndDate = new DateOnly(2026, 4, 26),
            Tags = ["peaking"],
        };

    public static Routine UpperDay(Guid userId, params Guid[] movements) => Routine.Define(
        Guid.NewGuid(),
        userId,
        ResultAssert.Succeeded(RoutineContent.Of(
            "Upper day",
            "pulldown pin 7",
            movements.Length > 0
                ? [.. movements.Select(movement => new RoutineMovement(movement, 2))]
                : [new RoutineMovement(Guid.NewGuid(), 2), new RoutineMovement(Guid.NewGuid(), 3)])),
        Clock);

    public static float[] Embedding => [0.12f, -0.5f, 0.98f, 0f];

    public static AnalyzableEntry AnalyzableEntry(Guid userId) => new(
        Guid.NewGuid(),
        EntryKind.Session,
        userId,
        new Interval(Clock, Clock.AddMinutes(75)),
        Facets,
        Valence.Positive,
        Embedding,
        "Heavy push session.",
        DecoratorVersion: 1);

    public static FacetValue FacetValue => new(
        FacetAxis.Modality, "lift", Aliases: ["lifting", "weights"], Embedding);
}
