using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Interpretation;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Interpretation;

/// <summary>
/// A session logged as text sets the same records one logged through the form does, and one
/// utterance can yield more than one session.
/// </summary>
/// <remarks>
/// The movements here are real rows belonging to the athlete. <see cref="Some.CompletedSession"/>
/// invents its movement ids, so the detector's ownership filter discards every candidate it
/// produces — a test built on it would pass with the detector wired to nothing at all, which is
/// what let a commit of two sessions leave two standing records of one kind.
/// </remarks>
public class InterpretationRecordTests : IDisposable
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 7, 17, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateTimeOffset Wednesday = new(2026, 9, 9, 17, 0, 0, TimeSpan.FromHours(-4));

    private readonly RemodelDatabase _database = new();
    private readonly FakeEmbeddingGenerator _embeddings = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_session_committed_from_text_establishes_its_records()
    {
        var bench = await PlantMovementAsync();

        ResultAssert.Succeeded(await CommitAsync(SessionOf(Monday, Lift(bench, 225, 5))));

        var standing = await StandingAsync();

        // Heaviest set, best set volume, the session total, and one rep max at five reps.
        Assert.Equal(4, standing.Count);
        Assert.Equal(
            225,
            Assert.IsType<Achievement.HeaviestSet>(
                Assert.Single(standing, record => record.Achievement is Achievement.HeaviestSet)
                    .Achievement).Lift.Weight);
    }

    /// <summary>
    /// Two sessions in one commit leave one standing record of a kind, chained in the order they
    /// happened.
    /// </summary>
    /// <remarks>
    /// Nothing races here: one request, one transaction, the sessions handled in sequence. A
    /// detector asked session by session reads what stands before each, and the second read cannot
    /// see the record the first session staged but has not saved — so both end up standing, both
    /// claiming to be the first of their kind. The records screen then shows two, retracting one
    /// leaves three, and only whichever of them ranks highest can ever be succeeded again.
    /// </remarks>
    [Fact]
    public async Task Two_sessions_in_one_commit_leave_one_standing_record_of_a_kind()
    {
        var bench = await PlantMovementAsync();

        ResultAssert.Succeeded(await CommitAsync(
            SessionOf(Monday, Lift(bench, 225, 5)),
            SessionOf(Wednesday, Lift(bench, 245, 5))));

        var heaviest = await ChainAsync(record => record.Achievement is Achievement.HeaviestSet);

        Assert.Equal(2, heaviest.Count);
        var standing = Assert.Single(heaviest, record => record.IsStanding);
        Assert.Equal(245, Assert.IsType<Achievement.HeaviestSet>(standing.Achievement).Lift.Weight);
        Assert.Equal(Wednesday.UtcDateTime, standing.AchievedAt);

        // The later session took it from the earlier one rather than standing beside it.
        var replaced = Assert.IsType<RecordLineage.Replaced>(standing.Lineage);
        var beaten = Assert.Single(heaviest, record => !record.IsStanding);
        Assert.Equal(beaten.PersonalRecordId, replaced.PersonalRecordId);
        Assert.Equal(Monday.UtcDateTime, replaced.AchievedAt);
        Assert.Equal(Wednesday.UtcDateTime, Assert.IsType<RecordStanding.Beaten>(beaten.Standing).At);
    }

    /// <summary>
    /// The same, on the subject that carries no movement id — where the ownership filter cannot
    /// mask the defect by discarding the candidates.
    /// </summary>
    [Fact]
    public async Task Two_trail_sessions_in_one_commit_leave_one_standing_distance_record()
    {
        ResultAssert.Succeeded(await CommitAsync(
            SessionOf(Monday, TrailRun(8)),
            SessionOf(Wednesday, TrailRun(12))));

        var longest = await ChainAsync(record => record.Achievement is Achievement.LongestDistance);

        Assert.Equal(2, longest.Count);
        Assert.Equal(
            12,
            Assert.IsType<Achievement.LongestDistance>(
                Assert.Single(longest, record => record.IsStanding).Achievement).Effort.Distance);
        Assert.Single(longest, record => record.Lineage is RecordLineage.FirstOfItsKind);
    }

    [Fact]
    public async Task A_commit_of_two_sessions_onto_a_record_that_already_stands_chains_both()
    {
        // Planting a standing record first: the earlier session must beat the plant, and the later
        // one must then beat the earlier, so the chain is three links and not two standing rows.
        ResultAssert.Succeeded(await CommitAsync(SessionOf(Monday.AddDays(-7), TrailRun(5))));

        ResultAssert.Succeeded(await CommitAsync(
            SessionOf(Monday, TrailRun(8)),
            SessionOf(Wednesday, TrailRun(12))));

        var longest = await ChainAsync(record => record.Achievement is Achievement.LongestDistance);

        Assert.Equal(3, longest.Count);
        Assert.Single(longest, record => record.IsStanding);
        Assert.Equal(
            [5d, 8d, 12d],
            longest
                .OrderBy(record => record.AchievedAt)
                .Select(record => Assert.IsType<Achievement.LongestDistance>(record.Achievement).Effort.Distance));
    }

    // ---- Harness ----

    private async Task<Result<Committed, Problem>> CommitAsync(params CompletedTrainingSession[] sessions)
    {
        using var context = _database.CreateContext();
        return await new InterpretationCommitter(
                context, _embeddings, new PersonalRecordDetector(context, TimeProvider.System))
            .CommitAsync(new ResolvedInterpretation([.. sessions], [], []));
    }

    private async Task<IReadOnlyList<PersonalRecord>> StandingAsync() =>
        [.. (await AllAsync()).Where(record => record.IsStanding)];

    private async Task<IReadOnlyList<PersonalRecord>> ChainAsync(Func<PersonalRecord, bool> ofKind) =>
        [.. (await AllAsync()).Where(ofKind)];

    private async Task<IReadOnlyList<PersonalRecord>> AllAsync()
    {
        using var context = _database.CreateContext();
        return await context.PersonalRecords.AsNoTracking().ToListAsync();
    }

    private CompletedTrainingSession SessionOf(DateTimeOffset performed, params SetEntry[] sets) =>
        new(sets, new Instant(performed), new EffortNotReported(), new Decorated(Some.Decoration))
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = _database.UserId,
            Notes = "texted",
            Provenance = Provenance.AiAssisted,
            Membership = new Standalone(),
            CreatedAt = Some.Clock,
        };

    private static LiftSet Lift(Guid movementDataId, double weight, int reps) =>
        new(movementDataId, reps, weight, WeightUnit.Pounds, Rpe.Unrated)
        {
            Source = Some.TextedSource,
            Facets = [],
        };

    private static TrailRunSet TrailRun(double kilometers) =>
        new(kilometers, DistanceUnit.Kilometers, TimeSpan.FromMinutes(kilometers * 8), 400, Rpe.Unrated)
        {
            Source = Some.TextedSource,
            Facets = [],
        };

    private async Task<Guid> PlantMovementAsync()
    {
        var movementDataId = Guid.NewGuid();
        var equipment = new Equipment
        {
            EquipmentID = Guid.NewGuid(),
            Name = "Barbell",
            UserID = _database.UserId,
        };
        var movementBase = new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = "Bench Press",
            UserID = _database.UserId,
            Description = string.Empty,
            MuscleGroups = [],
        };

        using var context = _database.CreateContext();
        context.MovementDatas.Add(new MovementData
        {
            MovementDataID = movementDataId,
            UserID = _database.UserId,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase,
        });
        await context.SaveChangesAsync();

        return movementDataId;
    }
}
