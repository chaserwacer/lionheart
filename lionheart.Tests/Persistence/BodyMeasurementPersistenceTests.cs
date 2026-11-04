using lionheart.Model.BodyMeasurements;
using lionheart.Model.Training;
using lionheart.Model.Training.Sets;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// The two value objects this round adds to existing rows and tables — a day's bodyweight and a
/// movement's setup — through real SQLite, each as columns of the row that holds it.
/// </summary>
public sealed class BodyMeasurementPersistenceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void A_bodyweight_measurement_round_trips_with_its_unit()
    {
        using (var context = _db.CreateContext())
        {
            context.BodyweightMeasurements.Add(
                BodyweightMeasurement.Measured(_db.UserId, Day, new Bodyweight(175.4, WeightUnit.Pounds)));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.BodyweightMeasurements.Single();
        Assert.Equal((_db.UserId, Day, new Bodyweight(175.4, WeightUnit.Pounds)), (loaded.UserId, loaded.Day, loaded.Bodyweight));
        Assert.Equal("2026-11-03", _db.ExecuteScalar("SELECT Day FROM BodyweightMeasurements"));
    }

    [Fact]
    public void A_second_measurement_on_the_same_day_is_refused_by_the_key()
    {
        using (var context = _db.CreateContext())
        {
            context.BodyweightMeasurements.Add(
                BodyweightMeasurement.Measured(_db.UserId, Day, new Bodyweight(175, WeightUnit.Pounds)));
            context.SaveChanges();
        }

        using var second = _db.CreateContext();
        second.BodyweightMeasurements.Add(BodyweightMeasurement.Measured(_db.UserId, Day, new Bodyweight(80, WeightUnit.Kilograms)));

        Assert.Throws<DbUpdateException>(() => second.SaveChanges());
    }

    [Fact]
    public void A_restated_measurement_is_saved_in_place()
    {
        using (var context = _db.CreateContext())
        {
            context.BodyweightMeasurements.Add(
                BodyweightMeasurement.Measured(_db.UserId, Day, new Bodyweight(175, WeightUnit.Pounds)));
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            context.BodyweightMeasurements.Single().Restate(new Bodyweight(79.5, WeightUnit.Kilograms));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(new Bodyweight(79.5, WeightUnit.Kilograms), fresh.BodyweightMeasurements.Single().Bodyweight);
    }

    [Fact]
    public void A_movement_setup_round_trips_and_a_new_movement_has_none()
    {
        var movementDataId = PlantMovement();

        using (var context = _db.CreateContext())
        {
            Assert.Equal(MovementSetup.None, context.MovementDatas.Single().Setup);
            context.MovementDatas.Single().ReplaceSetup(new MovementSetup("seat 4, pin 7", AddsToBodyweight: true));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(new MovementSetup("seat 4, pin 7", true), fresh.MovementDatas.Single(m => m.MovementDataID == movementDataId).Setup);
        Assert.Equal(1L, _db.ExecuteScalar("SELECT AddsToBodyweight FROM MovementDatas"));
    }

    private Guid PlantMovement()
    {
        using var context = _db.CreateContext();
        var equipment = new Equipment { EquipmentID = Guid.NewGuid(), Name = "Bodyweight", UserID = _db.UserId };
        var movementBase = new MovementBase
        {
            MovementBaseID = Guid.NewGuid(), Name = "Dip", UserID = _db.UserId, Description = string.Empty,
        };
        var movement = new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = _db.UserId,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase,
        };
        context.MovementDatas.Add(movement);
        context.SaveChanges();
        return movement.MovementDataID;
    }
}
