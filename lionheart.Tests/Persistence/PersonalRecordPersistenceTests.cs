using System.Text.Json;
using lionheart.Data;
using lionheart.Model.Training;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// How a record reaches the database and comes back: the achievement, the standing and the lineage
/// as JSON, the subject as a single scalar, and the lookup columns stamped off all three.
/// </summary>
public class PersonalRecordPersistenceTests : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_record_round_trips_with_its_achievement_intact()
    {
        var saved = await SaveAsync(Heaviest(225, WeightUnit.Pounds, 5));

        var loaded = await LoadAsync(saved.PersonalRecordId);

        Assert.Equal(
            new LiftPerformance(225, WeightUnit.Pounds, 5),
            Assert.IsType<Achievement.HeaviestSet>(loaded.Achievement).Lift);
        Assert.Equal(new RecordSubject.Movement(MovementId(saved)), loaded.Subject);
        Assert.IsType<RecordStanding.Current>(loaded.Standing);
        Assert.IsType<RecordLineage.FirstOfItsKind>(loaded.Lineage);
    }

    [Theory]
    [InlineData("heaviestSet")]
    [InlineData("repMax")]
    [InlineData("bestSessionVolume")]
    [InlineData("longestDistance")]
    [InlineData("fastestTime")]
    [InlineData("bestPace")]
    public async Task Every_kind_of_achievement_survives_the_round_trip(string kindName)
    {
        var record = await SaveAsync(OfKind(kindName));

        var loaded = await LoadAsync(record.PersonalRecordId);

        Assert.Equal(record.Achievement, loaded.Achievement);
        Assert.Equal(kindName, loaded.Kind.Name);
    }

    [Fact]
    public async Task Trail_running_persists_as_the_empty_movement_rather_than_a_null()
    {
        var record = PersonalRecord.FirstOf(
            Guid.CreateVersion7(),
            _database.UserId,
            new RecordSubject.TrailRunning(),
            new Achievement.LongestDistance(
                new DistanceEffort(8, DistanceUnit.Kilometers, TimeSpan.FromMinutes(70))),
            Some.Clock);

        using (var context = _database.CreateContext())
        {
            context.PersonalRecords.Add(record);
            await context.SaveChangesAsync();
        }

        Assert.Equal(
            Guid.Empty.ToString().ToUpperInvariant(),
            _database.ExecuteScalar(
                $"select MovementDataId from PersonalRecords where PersonalRecordId = '{RemodelDatabase.SqlGuid(record.PersonalRecordId)}'"));
        Assert.IsType<RecordSubject.TrailRunning>((await LoadAsync(record.PersonalRecordId)).Subject);
    }

    [Fact]
    public async Task The_lookup_columns_are_stamped_off_the_record_rather_than_set_by_hand()
    {
        var record = await SaveAsync(new Achievement.RepMax(new LiftPerformance(225, WeightUnit.Pounds, 5)));

        var id = RemodelDatabase.SqlGuid(record.PersonalRecordId);
        Assert.Equal("repMax", _database.ExecuteScalar($"select KindName from PersonalRecords where PersonalRecordId = '{id}'"));
        Assert.Equal(5L, _database.ExecuteScalar($"select KindBracket from PersonalRecords where PersonalRecordId = '{id}'"));
        Assert.Equal(1L, _database.ExecuteScalar($"select Stands from PersonalRecords where PersonalRecordId = '{id}'"));
    }

    [Fact]
    public async Task Beating_a_record_restamps_the_standing_column_of_the_one_it_beat()
    {
        var first = await SaveAsync(Heaviest(225, WeightUnit.Pounds, 5));

        using (var context = _database.CreateContext())
        {
            var tracked = await context.PersonalRecords.FindAsync(first.PersonalRecordId);
            context.PersonalRecords.Add(PersonalRecord.Succeeding(
                Guid.CreateVersion7(), Heaviest(245, WeightUnit.Pounds, 5), Some.Clock.AddDays(7), tracked!));
            await context.SaveChangesAsync();
        }

        Assert.Equal(
            0L,
            _database.ExecuteScalar(
                $"select Stands from PersonalRecords where PersonalRecordId = '{RemodelDatabase.SqlGuid(first.PersonalRecordId)}'"));
    }

    /// <summary>
    /// The hand-over writes both halves, so one kind can only ever have one standing record.
    /// </summary>
    /// <remarks>
    /// The invariant this aggregate is built on, and the reason the only way to add a record over
    /// an existing one is the factory that demotes it in the same breath. It is not a unique
    /// index: SQLite checks one statement at a time with no deferred form, so the insert would be
    /// refused while the row it replaces is still standing.
    /// </remarks>
    [Fact]
    public async Task A_hand_over_leaves_exactly_one_record_of_its_kind_standing()
    {
        var movementDataId = await PlantMovementAsync();
        var first = await SaveAsync(Heaviest(225, WeightUnit.Pounds, 5), movementDataId);

        using (var context = _database.CreateContext())
        {
            var tracked = await context.PersonalRecords.FindAsync(first.PersonalRecordId);
            context.PersonalRecords.Add(PersonalRecord.Succeeding(
                Guid.CreateVersion7(), Heaviest(245, WeightUnit.Pounds, 5), Some.Clock.AddDays(7), tracked!));
            await context.SaveChangesAsync();
        }

        using var reading = _database.CreateContext();
        var all = await reading.PersonalRecords.AsNoTracking().ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(
            245,
            Assert.IsType<Achievement.HeaviestSet>(
                Assert.Single(all, record => record.IsStanding).Achievement).Lift.Weight);
    }

    [Fact]
    public async Task Two_rep_maxes_at_different_rep_counts_both_stand()
    {
        var movementDataId = await PlantMovementAsync();

        await SaveAsync(new Achievement.RepMax(new LiftPerformance(225, WeightUnit.Pounds, 5)), movementDataId);
        await SaveAsync(new Achievement.RepMax(new LiftPerformance(255, WeightUnit.Pounds, 3)), movementDataId);

        using var context = _database.CreateContext();
        Assert.Equal(2, await context.PersonalRecords.CountAsync());
    }

    /// <summary>
    /// The JSON the remodelling migration writes by hand is the JSON the serializer would have
    /// written.
    /// </summary>
    /// <remarks>
    /// A migration has no application to call, so it spells the payloads out. Nothing else would
    /// notice them drifting apart — the rows would simply fail to deserialize on the day the
    /// athlete next opened the records screen.
    /// </remarks>
    [Theory]
    [InlineData("heaviestSet")]
    [InlineData("bestSetVolume")]
    public void The_migrations_hand_written_achievement_matches_the_serializers(string kindName)
    {
        var migrated =
            $"{{\"$type\":\"{kindName}\",\"Lift\":{{\"Weight\":210.0,\"Unit\":1,\"Reps\":4}}}}";

        var deserialized = JsonSerializer.Deserialize<Achievement>(migrated, ValueConversions.StorageJson);

        Assert.NotNull(deserialized);
        Assert.Equal(kindName, deserialized.Kind.Name);
        Assert.Equal(
            new LiftPerformance(210, WeightUnit.Pounds, 4),
            deserialized switch
            {
                Achievement.HeaviestSet heaviest => heaviest.Lift,
                Achievement.BestSetVolume best => best.Lift,
                _ => throw new Xunit.Sdk.XunitException("Unexpected achievement.")
            });
    }

    [Fact]
    public void The_migrations_hand_written_standing_and_lineage_match_the_serializers()
    {
        var beaten = JsonSerializer.Deserialize<RecordStanding>(
            "{\"$type\":\"beaten\",\"At\":\"2026-03-23T16:30:32.1085981\"}", ValueConversions.StorageJson);
        var replaced = JsonSerializer.Deserialize<RecordLineage>(
            "{\"$type\":\"replaced\",\"PersonalRecordId\":\"352089b3-0000-0000-0000-000000000000\"," +
            "\"AchievedAt\":\"2026-03-23T16:30:32.1085981\"}", ValueConversions.StorageJson);

        Assert.Equal(new DateTime(2026, 3, 23, 16, 30, 32).AddTicks(1085981), Assert.IsType<RecordStanding.Beaten>(beaten).At);
        Assert.Equal(
            Guid.Parse("352089b3-0000-0000-0000-000000000000"),
            Assert.IsType<RecordLineage.Replaced>(replaced).PersonalRecordId);
        Assert.IsType<RecordStanding.Current>(
            JsonSerializer.Deserialize<RecordStanding>("{\"$type\":\"standing\"}", ValueConversions.StorageJson));
        Assert.IsType<RecordLineage.FirstOfItsKind>(
            JsonSerializer.Deserialize<RecordLineage>("{\"$type\":\"first\"}", ValueConversions.StorageJson));
    }

    /// <summary>
    /// A stored achievement carries its measurement and nothing else — for every kind.
    /// </summary>
    /// <remarks>
    /// Asserting the exact key set rather than the absence of a few names, because that is the
    /// claim: a volume, an estimated max, a pace or a distance bracket stored beside the members
    /// it was computed from is a second copy that can come to disagree with them. Every case needs
    /// its own row, since <c>[JsonIgnore]</c> does not inherit through an override — watching only
    /// the heaviest set, dropping the attribute from <see cref="DistanceEffort"/>'s pace left the
    /// whole suite green.
    /// </remarks>
    [Theory]
    [InlineData("heaviestSet", "Lift", new[] { "Weight", "Unit", "Reps" })]
    [InlineData("bestSetVolume", "Lift", new[] { "Weight", "Unit", "Reps" })]
    [InlineData("repMax", "Lift", new[] { "Weight", "Unit", "Reps" })]
    [InlineData("bestSessionVolume", "Total", new[] { "Volume", "Unit" })]
    [InlineData("longestDistance", "Effort", new[] { "Distance", "Unit", "Duration" })]
    [InlineData("fastestTime", "Effort", new[] { "Distance", "Unit", "Duration" })]
    [InlineData("bestPace", "Effort", new[] { "Distance", "Unit", "Duration" })]
    public void A_stored_achievement_carries_its_measurement_and_no_figure_read_off_it(
        string kindName, string measure, string[] measured)
    {
        var stored = JsonSerializer.Serialize(OfKind(kindName), ValueConversions.StorageJson);

        using var payload = JsonDocument.Parse(stored);
        Assert.Equal(
            ["$type", measure],
            payload.RootElement.EnumerateObject().Select(member => member.Name));
        Assert.Equal(
            measured,
            payload.RootElement.GetProperty(measure).EnumerateObject().Select(member => member.Name));
    }

    private static Achievement Heaviest(double weight, WeightUnit unit, int reps) =>
        new Achievement.HeaviestSet(new LiftPerformance(weight, unit, reps));

    private static Achievement OfKind(string kindName)
    {
        var lift = new LiftPerformance(225, WeightUnit.Pounds, 5);
        var effort = new DistanceEffort(5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(22));

        return kindName switch
        {
            "heaviestSet" => new Achievement.HeaviestSet(lift),
            "bestSetVolume" => new Achievement.BestSetVolume(lift),
            "repMax" => new Achievement.RepMax(lift),
            "bestSessionVolume" => new Achievement.BestSessionVolume(new SessionVolume(12400, WeightUnit.Pounds)),
            "longestDistance" => new Achievement.LongestDistance(effort),
            "fastestTime" => new Achievement.FastestTime(effort),
            "bestPace" => new Achievement.BestPace(effort),
            _ => throw new Xunit.Sdk.XunitException($"No achievement for '{kindName}'.")
        };
    }

    private async Task<PersonalRecord> SaveAsync(Achievement achievement, Guid? movementDataId = null)
    {
        var subject = new RecordSubject.Movement(movementDataId ?? await PlantMovementAsync());
        var record = PersonalRecord.FirstOf(
            Guid.CreateVersion7(), _database.UserId, subject, achievement, Some.Clock);

        using var context = _database.CreateContext();
        context.PersonalRecords.Add(record);
        await context.SaveChangesAsync();

        return record;
    }

    private async Task<PersonalRecord> LoadAsync(Guid personalRecordId)
    {
        using var context = _database.CreateContext();
        return (await context.PersonalRecords.AsNoTracking()
            .FirstAsync(record => record.PersonalRecordId == personalRecordId));
    }

    private static Guid MovementId(PersonalRecord record) =>
        ((RecordSubject.Movement)record.Subject).MovementDataId;

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
