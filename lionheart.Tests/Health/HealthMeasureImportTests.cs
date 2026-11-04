using lionheart.Model.BodyMeasurements;
using lionheart.Model.Health;
using lionheart.Model.Health.Measurements;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Model.Analysis;
using lionheart.Services.Health.Measurements;
using lionheart.Tests.Api;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Health;

/// <summary>
/// Apple Health's measurements through the real schema: days kept and restated, the weigh-ins
/// that fill bodyweight, and the profile's age from a date of birth. Invented values throughout.
/// </summary>
public class HealthMeasureImportTests : IDisposable
{
    private static readonly DateOnly Day = new(2022, 4, 11);

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    // ---- Days of figures ----

    [Fact]
    public async Task A_new_day_is_recorded_and_reads_back_as_its_figures_series()
    {
        var report = await ImportAsync(Hrv(Day, 52), Hrv(Day.AddDays(1), 47));

        Assert.Equal(new HealthMeasuresImportReport(Recorded: 2, Restated: 0, Unchanged: 0, Unreadable: 0), report);
        var series = Assert.Single(await ReadAsync(_athletes.Owner));
        Assert.Equal((HealthMetric.HeartRateVariabilitySdnn, MeasuringDevice.AppleWatch), (series.Metric, series.Device));
        Assert.Equal([52.0, 47.0], series.Days.Select(day => day.Mean));
    }

    [Fact]
    public async Task A_day_offered_again_restates_itself_when_its_readings_changed_and_is_left_alone_when_not()
    {
        await ImportAsync(Hrv(Day, 52), Hrv(Day.AddDays(1), 47));

        var report = await ImportAsync(Hrv(Day, 52), Hrv(Day.AddDays(1), 49, count: 4));

        Assert.Equal(new HealthMeasuresImportReport(Recorded: 0, Restated: 1, Unchanged: 1, Unreadable: 0), report);
        Assert.Equal([52.0, 49.0], Assert.Single(await ReadAsync(_athletes.Owner)).Days.Select(day => day.Mean));
    }

    [Fact]
    public async Task The_same_day_from_the_watch_and_the_phone_is_two_rows_never_a_blend()
    {
        await ImportAsync(
            new MeasuredDayCommand(HealthMetric.WalkingSpeed, MeasuringDevice.IPhone, Day, 1.30, 1.1, 1.5, 12),
            new MeasuredDayCommand(HealthMetric.WalkingSpeed, MeasuringDevice.AppleWatch, Day, 1.42, 1.2, 1.6, 5));

        var series = await ReadAsync(_athletes.Owner);

        Assert.Equal(2, series.Count);
        Assert.All(series, one => Assert.Single(one.Days));
    }

    [Fact]
    public async Task An_unreadable_day_is_left_out_and_counted_while_the_rest_of_the_batch_is_kept()
    {
        var report = await ImportAsync(
            Hrv(Day, 52),
            new MeasuredDayCommand(HealthMetric.WalkingAsymmetry, MeasuringDevice.IPhone, Day, 4.5, 2, 9, 6));

        Assert.Equal((1, 1), (report.Recorded, report.Unreadable));
    }

    [Fact]
    public async Task Another_athletes_days_are_not_read()
    {
        await ImportAsync(Hrv(Day, 52));

        Assert.Empty(await ReadAsync(_athletes.Stranger));
    }

    // ---- Weigh-ins ----

    [Fact]
    public async Task Weigh_ins_fill_the_days_with_no_bodyweight_and_leave_a_logged_day_as_it_was()
    {
        await SeedBodyweightAsync(Day, new Bodyweight(181, WeightUnit.Pounds));

        var report = await WeighInsAsync(WeighIn(Day, 79), WeighIn(Day.AddDays(-30), 80));

        Assert.Equal((1, 1), (report.Added, report.DaysAlreadyMeasured));
        await using var context = _athletes.CreateContext();
        var held = await context.BodyweightMeasurements.OrderBy(m => m.Day).Select(m => m.Bodyweight).ToListAsync();
        Assert.Equal([new Bodyweight(176.4, WeightUnit.Pounds), new Bodyweight(181, WeightUnit.Pounds)], held);
    }

    [Fact]
    public async Task Offering_the_same_weigh_ins_again_adds_nothing()
    {
        await WeighInsAsync(WeighIn(Day, 79));

        var again = await WeighInsAsync(WeighIn(Day, 79));

        Assert.Equal((0, 1), (again.Added, again.DaysAlreadyMeasured));
    }

    [Fact]
    public async Task The_report_counts_the_weighted_dips_whose_system_load_now_reads_an_earlier_bodyweight()
    {
        await SeedBodyweightAsync(Day, new Bodyweight(181, WeightUnit.Pounds));
        var dip = await SeedMovementAsync(addsToBodyweight: true);
        var press = await SeedMovementAsync(addsToBodyweight: false);
        await SeedSessionAsync(Day.AddDays(-20), Lift(dip) with { Kind = SetKind.WarmUp }, Lift(dip), Lift(dip), Lift(press));
        await SeedSessionAsync(Day.AddDays(5), Lift(dip));

        var report = await WeighInsAsync(WeighIn(Day.AddDays(-40), 80));

        // The two working dips before the logged day read 176.4 instead of the 181 that stood in for
        // them; the warm-up is no hard set, the press adds no bodyweight, and the dip after the
        // logged day reads it either way.
        Assert.Equal(2, report.SetsReweighed);
    }

    // ---- Date of birth ----

    [Fact]
    public async Task A_date_of_birth_sets_an_unset_age_and_says_nothing_changed_the_second_time()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        var birth = new HealthBirthDate(new DateOnly(1990, 5, 20));

        var first = await BirthDateAsync(clock, birth);
        var second = await BirthDateAsync(clock, birth);

        Assert.Equal((new ProfileAgeView(36, true), new ProfileAgeView(36, false)), (first, second));
        await using var context = _athletes.CreateContext();
        Assert.Equal(36, (await context.LionheartUsers.SingleAsync(u => u.UserID == _athletes.OwnerId)).Age);
    }

    // ---- Helpers ----

    private static MeasuredDayCommand Hrv(DateOnly day, double mean, int count = 3) =>
        new(HealthMetric.HeartRateVariabilitySdnn, MeasuringDevice.AppleWatch, day, mean, mean - 10, mean + 10, count);

    private static WeighInCommand WeighIn(DateOnly day, double kilograms) =>
        new(day, new DateTimeOffset(day.ToDateTime(new TimeOnly(7, 15)), TimeSpan.FromHours(-5)), kilograms);

    private static LiftSet Lift(Guid movementDataId) =>
        Some.LiftSet with { MovementDataId = movementDataId, Reps = 8, Weight = 25 };

    private async Task<HealthMeasuresImportReport> ImportAsync(params MeasuredDayCommand[] days)
    {
        await using var context = _athletes.CreateContext();
        return ResultAssert.Succeeded(await new HealthMeasureService(context).ImportAsync(_athletes.Owner, new HealthMeasuresImport(days), default));
    }

    private async Task<IReadOnlyList<HealthMeasureSeries>> ReadAsync(IdentityUser user)
    {
        await using var context = _athletes.CreateContext();
        return ResultAssert.Succeeded(await new HealthMeasureService(context).GetRangeAsync(
            user, new DayRange(new DateOnly(2014, 1, 1), new DateOnly(2030, 1, 1)), default));
    }

    private async Task<HealthBodyweightImportReport> WeighInsAsync(params WeighInCommand[] weighIns)
    {
        await using var context = _athletes.CreateContext();
        return ResultAssert.Succeeded(await new HealthBodyweightImportService(context).ImportAsync(
            _athletes.Owner, new HealthBodyweightsImport(weighIns), default));
    }

    private async Task<ProfileAgeView> BirthDateAsync(TimeProvider clock, HealthBirthDate birth)
    {
        await using var context = _athletes.CreateContext();
        return ResultAssert.Succeeded(await new HealthBirthDateService(context, clock).ApplyAsync(_athletes.Owner, birth, default));
    }

    private async Task SeedBodyweightAsync(DateOnly day, Bodyweight bodyweight)
    {
        await using var context = _athletes.CreateContext();
        context.BodyweightMeasurements.Add(BodyweightMeasurement.Measured(_athletes.OwnerId, day, bodyweight));
        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedMovementAsync(bool addsToBodyweight)
    {
        await using var context = _athletes.CreateContext();
        var equipment = new Equipment { EquipmentID = Guid.NewGuid(), Name = "Bodyweight", UserID = _athletes.OwnerId };
        var movementBase = new MovementBase
        {
            MovementBaseID = Guid.NewGuid(), Name = addsToBodyweight ? "Dip" : "Press", UserID = _athletes.OwnerId,
            Description = string.Empty,
        };
        var movement = new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = _athletes.OwnerId,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase,
        };
        movement.ReplaceSetup(new MovementSetup(string.Empty, addsToBodyweight));
        context.MovementDatas.Add(movement);
        await context.SaveChangesAsync();
        return movement.MovementDataID;
    }

    private async Task SeedSessionAsync(DateOnly day, params SetEntry[] sets)
    {
        var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(17, 0)), TimeSpan.FromHours(-5));
        await using var context = _athletes.CreateContext();
        context.Sessions.Add(Some.CompletedSession(_athletes.OwnerId) with
        {
            Sets = sets,
            Period = new Interval(start, start.AddMinutes(60)),
        });
        await context.SaveChangesAsync();
    }
}
