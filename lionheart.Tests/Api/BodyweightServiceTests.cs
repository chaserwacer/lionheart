using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// The athlete's dated bodyweight: one measurement a day, restated by logging the day again.
/// </summary>
public class BodyweightServiceTests : IDisposable
{
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    [Fact]
    public async Task Logging_a_day_answers_with_its_bodyweight()
    {
        var view = ResultAssert.Succeeded(await LogAsync(Monday, 175.4));

        Assert.Equal(new BodyweightView(Monday, 175.4, WeightUnit.Pounds), view);
    }

    [Fact]
    public async Task Logging_the_same_day_again_replaces_its_measurement()
    {
        await LogAsync(Monday, 175);

        ResultAssert.Succeeded(await LogAsync(Monday, 79.5, WeightUnit.Kilograms));

        var held = Assert.Single(await StoredAsync());
        Assert.Equal(new Bodyweight(79.5, WeightUnit.Kilograms), held.Bodyweight);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-175)]
    [InlineData(1000.5)]
    [InlineData(double.NaN)]
    public async Task A_weight_no_one_weighs_is_refused_on_the_weight_and_nothing_is_written(double weight)
    {
        var refusal = ResultAssert.Failed(await LogAsync(Monday, weight));

        Assert.Equal("Weight", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task A_thousand_is_still_a_bodyweight() =>
        ResultAssert.Succeeded(await LogAsync(Monday, 1000));

    [Fact]
    public async Task A_unit_that_names_no_member_is_refused_rather_than_thrown()
    {
        var refusal = ResultAssert.Failed(await LogAsync(Monday, 175, (WeightUnit)99));

        Assert.Equal("Unit", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
    }

    [Fact]
    public async Task An_account_with_no_profile_cannot_log_a_bodyweight()
    {
        var refusal = ResultAssert.Failed(await _athletes.BodyweightAsync(service => service.LogAsync(
            _athletes.Unprofiled, Monday, new BodyweightRequest(175, WeightUnit.Pounds), default)));

        Assert.IsType<Problem.Conflict>(refusal);
    }

    [Fact]
    public async Task A_range_reads_the_athletes_own_days_inside_it_oldest_first()
    {
        await LogAsync(Monday.AddDays(2), 176);
        await LogAsync(Monday, 175);
        await LogAsync(Monday.AddDays(5), 177);
        await _athletes.BodyweightAsync(service => service.LogAsync(
            _athletes.Stranger, Monday.AddDays(1), new BodyweightRequest(150, WeightUnit.Pounds), default));

        var range = ResultAssert.Succeeded(await _athletes.BodyweightAsync(service =>
            service.GetRangeAsync(_athletes.Owner, new DayRange(Monday, Monday.AddDays(2)), default)));

        Assert.Equal([Monday, Monday.AddDays(2)], range.Select(view => view.Day));
    }

    [Fact]
    public async Task A_range_that_runs_backwards_is_refused()
    {
        var refusal = ResultAssert.Failed(await _athletes.BodyweightAsync(service =>
            service.GetRangeAsync(_athletes.Owner, new DayRange(Monday, Monday.AddDays(-1)), default)));

        Assert.IsType<Problem.Invalid>(refusal);
    }

    [Fact]
    public async Task The_latest_is_the_newest_day_logged()
    {
        await LogAsync(Monday.AddDays(3), 176);
        await LogAsync(Monday, 175);

        var latest = ResultAssert.Succeeded(await _athletes.BodyweightAsync(service =>
            service.GetLatestAsync(_athletes.Owner, default)));

        Assert.Equal(Monday.AddDays(3), latest.Day);
    }

    [Fact]
    public async Task With_nothing_logged_there_is_no_latest()
    {
        await _athletes.BodyweightAsync(service => service.LogAsync(
            _athletes.Stranger, Monday, new BodyweightRequest(150, WeightUnit.Pounds), default));

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.BodyweightAsync(service =>
            service.GetLatestAsync(_athletes.Owner, default))));
    }

    [Fact]
    public async Task Deleting_a_day_removes_its_measurement()
    {
        await LogAsync(Monday, 175);

        ResultAssert.Succeeded(await _athletes.BodyweightAsync(service =>
            service.DeleteAsync(_athletes.Owner, Monday, default)));

        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task Deleting_a_day_with_no_measurement_of_the_athletes_is_not_found()
    {
        await _athletes.BodyweightAsync(service => service.LogAsync(
            _athletes.Stranger, Monday, new BodyweightRequest(150, WeightUnit.Pounds), default));

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.BodyweightAsync(service =>
            service.DeleteAsync(_athletes.Owner, Monday, default))));

        using var context = _athletes.CreateContext();
        Assert.Equal(1, await context.BodyweightMeasurements.CountAsync());
    }

    private Task<Result<BodyweightView, Problem>> LogAsync(DateOnly day, double weight, WeightUnit unit = WeightUnit.Pounds) =>
        _athletes.BodyweightAsync(service =>
            service.LogAsync(_athletes.Owner, day, new BodyweightRequest(weight, unit), default));

    private async Task<IReadOnlyList<BodyweightMeasurement>> StoredAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.BodyweightMeasurements.AsNoTracking().Where(m => m.UserId == _athletes.OwnerId).ToListAsync();
    }
}
