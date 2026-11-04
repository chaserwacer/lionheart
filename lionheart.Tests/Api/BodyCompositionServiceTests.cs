using lionheart.Functional;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Model.Profile;
using lionheart.Model.Training.Sets;
using lionheart.Services.BodyComposition;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// Body-composition scans: recorded from a reviewed sheet, corrected whole, and joined to the
/// bodyweight series through the day they were taken.
/// </summary>
public class BodyCompositionServiceTests : IDisposable
{
    private static readonly DateOnly ScanDay = new(2026, 9, 14);
    private static readonly TimeOnly ScanTime = new(7, 41);

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    private static ScanRequest Scan(BodyCompositionSheet? sheet = null, bool recordsBodyweight = true, DateOnly? day = null) =>
        new(day ?? ScanDay, ScanTime, sheet ?? ASheet.InBody570(), recordsBodyweight);

    private Task<Result<ScanView, Problem>> RecordAsync(ScanRequest request, IdentityUser? athlete = null) =>
        _athletes.BodyCompositionAsync(service => service.RecordAsync(athlete ?? _athletes.Owner, request, default));

    private async Task<IReadOnlyList<ScanView>> ScansAsync(IdentityUser? of = null) =>
        ResultAssert.Succeeded(await _athletes.BodyCompositionAsync(service => service.GetAllAsync(of ?? _athletes.Owner, default)));

    private async Task<List<BodyweightMeasurement>> BodyweightsAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.BodyweightMeasurements.Where(m => m.UserId == _athletes.OwnerId).ToListAsync();
    }

    // ---- Recording ----

    [Fact]
    public async Task A_recorded_scan_reads_back_with_every_panel_its_sheet_carried()
    {
        var recorded = ResultAssert.Succeeded(await RecordAsync(Scan()));

        var read = Assert.Single(await ScansAsync());
        Assert.Equal((recorded.Id, ScanDay, ScanTime), (read.Id, read.Day, read.Time));
        JsonAssert.Equal(ASheet.InBody570(), read.Sheet);
    }

    [Fact]
    public async Task A_sheet_with_only_the_core_figures_is_a_whole_scan()
    {
        ResultAssert.Succeeded(await RecordAsync(Scan(ASheet.CoreOnly())));

        Assert.Empty(Assert.Single(await ScansAsync()).Sheet.Sections);
    }

    [Fact]
    public async Task Recording_a_scan_logs_its_weight_as_the_days_bodyweight()
    {
        ResultAssert.Succeeded(await RecordAsync(Scan()));

        var measurement = Assert.Single(await BodyweightsAsync());
        Assert.Equal((ScanDay, new Bodyweight(183.4, WeightUnit.Pounds)), (measurement.Day, measurement.Bodyweight));
    }

    [Fact]
    public async Task A_scan_that_records_its_bodyweight_replaces_one_already_logged_that_day()
    {
        await _athletes.BodyweightAsync(service =>
            service.LogAsync(_athletes.Owner, ScanDay, new BodyweightRequest(181.0, WeightUnit.Pounds), default));

        ResultAssert.Succeeded(await RecordAsync(Scan()));

        Assert.Equal(183.4, Assert.Single(await BodyweightsAsync()).Bodyweight.Value);
    }

    [Fact]
    public async Task A_scan_told_to_keep_the_days_bodyweight_leaves_it_as_logged()
    {
        await _athletes.BodyweightAsync(service =>
            service.LogAsync(_athletes.Owner, ScanDay, new BodyweightRequest(181.0, WeightUnit.Pounds), default));

        ResultAssert.Succeeded(await RecordAsync(Scan(recordsBodyweight: false)));

        Assert.Equal(181.0, Assert.Single(await BodyweightsAsync()).Bodyweight.Value);
    }

    [Fact]
    public async Task A_scan_at_a_minute_already_recorded_is_refused_and_written_once()
    {
        ResultAssert.Succeeded(await RecordAsync(Scan()));

        var refusal = ResultAssert.Failed(await RecordAsync(Scan(ASheet.CoreOnly())));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Equal("InBody570", Assert.Single(await ScansAsync()).Sheet.Device);
    }

    [Fact]
    public async Task A_sheet_with_several_wrong_figures_is_refused_on_each_and_nothing_is_written()
    {
        var sheet = ASheet.InBody570() with
        {
            Weight = 0,
            BodyMassIndex = 247,
            Sections = [.. ASheet.AllPanels(), new SheetSection.FitnessScore(91)],
        };

        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await RecordAsync(Scan(sheet))));

        Assert.Equal(
            ["Sheet.BodyMassIndex", "Sheet.Sections.FitnessScore", "Sheet.Weight"],
            refusal.Fields.Select(field => field.Field).Order());
        Assert.Empty(await ScansAsync());
        Assert.Empty(await BodyweightsAsync());
    }

    [Fact]
    public async Task A_unit_that_names_no_member_is_refused_rather_than_thrown()
    {
        var refusal = ResultAssert.Failed(await RecordAsync(Scan(ASheet.InBody570() with { Unit = (WeightUnit)7 })));

        Assert.Contains("Sheet.Unit", Assert.IsType<Problem.Invalid>(refusal).Fields.Select(field => field.Field));
    }

    [Fact]
    public async Task An_account_with_no_profile_cannot_record_a_scan() =>
        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await RecordAsync(Scan(), _athletes.Unprofiled)));

    [Fact]
    public async Task Recording_a_scan_marks_the_chat_cards_recent_state_stale()
    {
        await _athletes.PlantAsync(context => context.AthleteContextCards.Add(new AthleteContextCard
        {
            CardID = Guid.NewGuid(),
            UserID = _athletes.OwnerId,
            CreatedAt = Some.Clock,
            RecentStateAsOf = Some.Clock,
        }));

        ResultAssert.Succeeded(await RecordAsync(Scan()));

        using var context = _athletes.CreateContext();
        Assert.Equal(DateTime.MinValue, (await context.AthleteContextCards.SingleAsync()).RecentStateAsOf);
    }

    // ---- Correcting ----

    [Fact]
    public async Task Restating_a_scan_replaces_its_sheet_and_the_bodyweight_it_measured()
    {
        var recorded = ResultAssert.Succeeded(await RecordAsync(Scan()));
        var corrected = ASheet.InBody570() with { Weight = 184.3 };

        ResultAssert.Succeeded(await _athletes.BodyCompositionAsync(service =>
            service.RestateAsync(_athletes.Owner, recorded.Id, Scan(corrected), default)));

        Assert.Equal(184.3, Assert.Single(await ScansAsync()).Sheet.Weight);
        Assert.Equal(184.3, Assert.Single(await BodyweightsAsync()).Bodyweight.Value);
    }

    [Fact]
    public async Task Correcting_a_scans_day_moves_the_bodyweight_it_wrote()
    {
        var recorded = ResultAssert.Succeeded(await RecordAsync(Scan(day: ScanDay.AddDays(1))));

        ResultAssert.Succeeded(await _athletes.BodyCompositionAsync(service =>
            service.RestateAsync(_athletes.Owner, recorded.Id, Scan(), default)));

        Assert.Equal(ScanDay, Assert.Single(await BodyweightsAsync()).Day);
    }

    [Fact]
    public async Task Correcting_a_scans_day_leaves_a_reading_logged_apart_from_it()
    {
        var wrongDay = ScanDay.AddDays(1);
        ResultAssert.Succeeded(await _athletes.BodyweightAsync(service =>
            service.LogAsync(_athletes.Owner, wrongDay, new BodyweightRequest(181.0, WeightUnit.Pounds), default)));
        var recorded = ResultAssert.Succeeded(await RecordAsync(Scan(day: wrongDay, recordsBodyweight: false)));

        ResultAssert.Succeeded(await _athletes.BodyCompositionAsync(service =>
            service.RestateAsync(_athletes.Owner, recorded.Id, Scan(recordsBodyweight: false), default)));

        Assert.Equal((wrongDay, 181.0), Assert.Single(await BodyweightsAsync()) is var kept ? (kept.Day, kept.Bodyweight.Value) : default);
    }

    [Fact]
    public async Task Restating_a_scan_onto_the_minute_of_another_is_refused()
    {
        ResultAssert.Succeeded(await RecordAsync(Scan()));
        var later = ResultAssert.Succeeded(await RecordAsync(Scan(day: ScanDay.AddDays(30))));

        var refusal = ResultAssert.Failed(await _athletes.BodyCompositionAsync(service =>
            service.RestateAsync(_athletes.Owner, later.Id, Scan(), default)));

        Assert.IsType<Problem.Conflict>(refusal);
    }

    [Fact]
    public async Task Another_athletes_scan_cannot_be_restated_or_deleted()
    {
        var theirs = ResultAssert.Succeeded(await RecordAsync(Scan(), _athletes.Stranger));

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.BodyCompositionAsync(service =>
            service.RestateAsync(_athletes.Owner, theirs.Id, Scan(), default))));
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.BodyCompositionAsync(service =>
            service.DeleteAsync(_athletes.Owner, theirs.Id, default))));
        Assert.Single(await ScansAsync(_athletes.Stranger));
        Assert.Empty(await ScansAsync());
    }

    // ---- Deleting and reading ----

    [Fact]
    public async Task Deleting_a_scan_keeps_the_bodyweight_it_measured()
    {
        var recorded = ResultAssert.Succeeded(await RecordAsync(Scan()));

        ResultAssert.Succeeded(await _athletes.BodyCompositionAsync(service =>
            service.DeleteAsync(_athletes.Owner, recorded.Id, default)));

        Assert.Empty(await ScansAsync());
        Assert.Single(await BodyweightsAsync());
    }

    [Fact]
    public async Task Scans_read_oldest_first()
    {
        ResultAssert.Succeeded(await RecordAsync(Scan(day: ScanDay.AddDays(42))));
        ResultAssert.Succeeded(await RecordAsync(Scan()));

        Assert.Equal([ScanDay, ScanDay.AddDays(42)], (await ScansAsync()).Select(scan => scan.Day));
    }

    [Fact]
    public async Task The_chat_tool_reads_only_the_athletes_own_scans()
    {
        ResultAssert.Succeeded(await RecordAsync(Scan()));
        ResultAssert.Succeeded(await RecordAsync(Scan(day: ScanDay.AddDays(1)), _athletes.Stranger));

        var scans = await _athletes.BodyCompositionAsync(service =>
            ((BodyCompositionService)service).GetBodyCompositionScansForChatAsync(_athletes.Owner));

        Assert.StartsWith("InBody570 scan 2026-09-14", Assert.Single(scans));
    }
}
