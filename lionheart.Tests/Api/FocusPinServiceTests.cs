using lionheart.Functional;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// The focus list's routes: pinning one of the athlete's own movements, revising and unpinning
/// only while pinned, deleting a mistake, and never reaching another athlete's pins.
/// </summary>
public class FocusPinServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Starts = new(2026, 11, 2);
    private static readonly DateOnly Ends = new(2027, 1, 31);
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 14, 15, 16, TimeSpan.Zero);
    private static readonly LiftTargetRequest PlusSeventyForFive = new(70, WeightUnit.Pounds, 5);

    private readonly TwoAthletes _athletes = new();
    private Guid _dip;

    public async Task InitializeAsync() =>
        _dip = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Weighted Dip");

    public Task DisposeAsync()
    {
        _athletes.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Pinning_stores_the_pin_pinned_with_its_terms_and_the_instant_it_was_made()
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin()));

        var listed = Assert.Single(await ListAsync());
        Assert.Equal(pinned, listed);
        Assert.Equal(_dip, listed.MovementDataId);
        Assert.Equal((Starts, Ends, 2), (listed.Starts, listed.Ends, listed.SessionsPerWeek));
        var target = Assert.IsType<FocusTarget.Lift>(listed.Target);
        Assert.Equal((70d, WeightUnit.Pounds, 5), (target.Weight, target.Unit, target.Reps));
        Assert.IsType<FocusStatus.Pinned>(listed.Status);
        Assert.Equal(Now.UtcDateTime, listed.PinnedAt);
    }

    [Fact]
    public async Task A_pin_with_no_target_is_about_frequency_alone()
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin() with { Target = null }));

        Assert.IsType<FocusTarget.None>(pinned.Target);
        Assert.IsType<FocusTarget.None>(Assert.Single(await ListAsync()).Target);
    }

    [Theory]
    [MemberData(nameof(MalformedPins))]
    public async Task A_malformed_pin_is_refused_naming_the_field_and_writes_nothing(
        PinFocusRequest malformed, string field)
    {
        var refusal = Assert.IsType<Problem.Invalid>(
            ResultAssert.Failed(await PinAsync(malformed with { MovementDataId = _dip })));

        Assert.Equal(field, Assert.Single(refusal.Fields).Field);
        Assert.Empty(await ListAsync());
    }

    public static TheoryData<PinFocusRequest, string> MalformedPins() => new()
    {
        { PinOn(Guid.Empty) with { Starts = Ends, Ends = Starts }, "Ends" },
        { PinOn(Guid.Empty) with { SessionsPerWeek = 0 }, "SessionsPerWeek" },
        { PinOn(Guid.Empty) with { SessionsPerWeek = 8 }, "SessionsPerWeek" },
        { PinOn(Guid.Empty) with { Target = PlusSeventyForFive with { Weight = -5 } }, "Target.Weight" },
        { PinOn(Guid.Empty) with { Target = PlusSeventyForFive with { Weight = double.NaN } }, "Target.Weight" },
        { PinOn(Guid.Empty) with { Target = PlusSeventyForFive with { Reps = 0 } }, "Target.Reps" },
        { PinOn(Guid.Empty) with { Target = PlusSeventyForFive with { Reps = 31 } }, "Target.Reps" },
        { PinOn(Guid.Empty) with { Target = PlusSeventyForFive with { Unit = (WeightUnit)7 } }, "Target.Unit" },
    };

    [Fact]
    public async Task The_edges_of_each_range_are_accepted()
    {
        ResultAssert.Succeeded(await PinAsync(Pin() with
        {
            Ends = Starts, SessionsPerWeek = 1, Target = PlusSeventyForFive with { Weight = 0, Reps = 1 },
        }));
        ResultAssert.Succeeded(await PinAsync(Pin() with
        {
            SessionsPerWeek = 7, Target = PlusSeventyForFive with { Reps = 30 },
        }));

        Assert.Equal(2, (await ListAsync()).Count);
    }

    [Fact]
    public async Task Another_athletes_movement_cannot_be_pinned_and_reads_as_missing()
    {
        var theirs = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Their dip");

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await PinAsync(Pin() with { MovementDataId = theirs })));
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await PinAsync(Pin() with { MovementDataId = Guid.NewGuid() })));
        Assert.Empty(await StoredPinsAsync());
    }

    [Fact]
    public async Task An_account_with_no_profile_is_refused_rather_than_violating_the_foreign_key()
    {
        var refusal = ResultAssert.Failed(await _athletes.FocusAsync(new FixedClock(Now), service =>
            service.PinAsync(_athletes.Unprofiled, Pin(), default)));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Empty(await StoredPinsAsync());
    }

    [Fact]
    public async Task An_account_with_no_profile_reads_an_empty_list()
    {
        var listed = ResultAssert.Succeeded(await _athletes.FocusAsync(new FixedClock(Now), service =>
            service.GetAllAsync(_athletes.Unprofiled, default)));

        Assert.Empty(listed);
    }

    [Fact]
    public async Task Revising_replaces_the_terms_whole_and_nothing_else()
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin()));

        var revised = ResultAssert.Succeeded(await ReviseAsync(
            pinned.FocusPinId, new ReviseFocusRequest(Starts.AddDays(7), Ends.AddDays(28), 3, null)));

        Assert.Equal(
            pinned with { Starts = Starts.AddDays(7), Ends = Ends.AddDays(28), SessionsPerWeek = 3, Target = new FocusTarget.None() },
            revised);
        Assert.Equal(revised, Assert.Single(await ListAsync()));
    }

    [Fact]
    public async Task A_malformed_revision_is_refused_and_leaves_the_pin_as_it_was()
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin()));

        var refusal = ResultAssert.Failed(await ReviseAsync(
            pinned.FocusPinId, new ReviseFocusRequest(Starts, Ends, 9, null)));

        Assert.Equal("SessionsPerWeek", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
        Assert.Equal(pinned, Assert.Single(await ListAsync()));
    }

    [Fact]
    public async Task An_unpinned_pin_is_history_and_cannot_be_revised()
    {
        var unpinned = await UnpinnedPinAsync(Ends);

        var refusal = ResultAssert.Failed(await ReviseAsync(
            unpinned.FocusPinId, new ReviseFocusRequest(Starts, Ends.AddDays(30), 4, null)));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Equal(unpinned, Assert.Single(await ListAsync()));
    }

    [Fact]
    public async Task Unpinning_keeps_the_pin_dated_by_the_day_the_athlete_names()
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin()));
        var on = new DateOnly(2026, 12, 20);

        var unpinned = ResultAssert.Succeeded(await UnpinAsync(pinned.FocusPinId, on));

        Assert.Equal(pinned with { Status = new FocusStatus.Unpinned(on) }, unpinned);
        Assert.Equal(unpinned, Assert.Single(await ListAsync()));
    }

    [Fact]
    public async Task Unpinning_an_unpinned_pin_collides_and_keeps_the_first_date()
    {
        var unpinned = await UnpinnedPinAsync(new DateOnly(2026, 12, 20));

        var refusal = ResultAssert.Failed(await UnpinAsync(unpinned.FocusPinId, new DateOnly(2027, 1, 3)));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Equal(new FocusStatus.Unpinned(new DateOnly(2026, 12, 20)), Assert.Single(await ListAsync()).Status);
    }

    [Fact]
    public async Task A_pin_deletes_whether_it_is_pinned_or_not()
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin()));
        var unpinned = await UnpinnedPinAsync(Ends);

        ResultAssert.Succeeded(await DeleteAsync(pinned.FocusPinId));
        ResultAssert.Succeeded(await DeleteAsync(unpinned.FocusPinId));

        Assert.Empty(await StoredPinsAsync());
    }

    [Fact]
    public async Task Another_athletes_pin_is_missing_rather_than_forbidden_on_every_route()
    {
        var theirMovement = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Their dip");
        var theirs = ResultAssert.Succeeded(await _athletes.FocusAsync(new FixedClock(Now), service =>
            service.PinAsync(_athletes.Stranger, Pin() with { MovementDataId = theirMovement }, default)));
        var id = theirs.FocusPinId;

        Problem[] refusals =
        [
            ResultAssert.Failed(await ReviseAsync(id, new ReviseFocusRequest(Starts, Ends, 5, null))),
            ResultAssert.Failed(await UnpinAsync(id, Ends)),
            ResultAssert.Failed(await DeleteAsync(id)),
        ];

        Assert.All(refusals, refusal => Assert.IsType<Problem.NotFound>(refusal));
        Assert.Empty(await ListAsync());
        Assert.Equal(theirs, Assert.Single(ResultAssert.Succeeded(await _athletes.FocusAsync(
            new FixedClock(Now), service => service.GetAllAsync(_athletes.Stranger, default)))));
    }

    [Fact]
    public async Task A_pin_that_does_not_exist_is_not_found() =>
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await UnpinAsync(Guid.NewGuid(), Ends)));

    [Fact]
    public async Task The_list_reads_pinned_first_then_the_latest_horizon_first()
    {
        var earlyPinned = ResultAssert.Succeeded(await PinAsync(Pin() with { Starts = Starts.AddMonths(-6) }));
        var latePinned = ResultAssert.Succeeded(await PinAsync(Pin() with { Starts = Starts.AddDays(14) }));
        var lateUnpinned = await UnpinnedPinAsync(Ends, startingOn: Starts.AddDays(60));

        Assert.Equal(
            [latePinned.FocusPinId, earlyPinned.FocusPinId, lateUnpinned.FocusPinId],
            (await ListAsync()).Select(pin => pin.FocusPinId));
    }

    // ---- Priorities ----

    [Fact]
    public async Task A_pin_with_no_family_is_for_emphasis()
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin()));

        Assert.IsType<FocusRole.Emphasis>(pinned.Role);
    }

    [Fact]
    public async Task A_new_push_priority_lets_the_standing_one_go_the_day_before_it_starts()
    {
        var bench = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bench Press");
        var dips = ResultAssert.Succeeded(await PinAsync(Pin() with { Priority = PatternFamily.Push }));
        var emphasis = ResultAssert.Succeeded(await PinAsync(Pin()));

        var benchPriority = ResultAssert.Succeeded(await PinAsync(
            PinOn(bench) with { Starts = Starts.AddDays(42), Priority = PatternFamily.Push }));

        var listed = await ListAsync();
        Assert.Equal(new FocusStatus.Unpinned(Starts.AddDays(41)), listed.Single(pin => pin.FocusPinId == dips.FocusPinId).Status);
        Assert.IsType<FocusStatus.Pinned>(listed.Single(pin => pin.FocusPinId == emphasis.FocusPinId).Status);
        Assert.IsType<FocusStatus.Pinned>(listed.Single(pin => pin.FocusPinId == benchPriority.FocusPinId).Status);
        Assert.Equal(PatternFamily.Push, Assert.IsType<FocusRole.Priority>(benchPriority.Role).Family);
    }

    [Fact]
    public async Task A_priority_starting_before_the_standing_one_lets_it_go_on_its_own_first_day()
    {
        var bench = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bench Press");
        var dips = ResultAssert.Succeeded(await PinAsync(Pin() with { Priority = PatternFamily.Push }));

        ResultAssert.Succeeded(await PinAsync(PinOn(bench) with { Starts = Starts.AddDays(-10), Priority = PatternFamily.Push }));

        Assert.Equal(
            new FocusStatus.Unpinned(Starts),
            (await ListAsync()).Single(pin => pin.FocusPinId == dips.FocusPinId).Status);
    }

    [Fact]
    public async Task A_pull_priority_leaves_the_push_priority_standing()
    {
        var row = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Seated Row");
        var dips = ResultAssert.Succeeded(await PinAsync(Pin() with { Priority = PatternFamily.Push }));

        ResultAssert.Succeeded(await PinAsync(PinOn(row) with { Priority = PatternFamily.Pull }));

        Assert.IsType<FocusStatus.Pinned>((await ListAsync()).Single(pin => pin.FocusPinId == dips.FocusPinId).Status);
    }

    [Fact]
    public async Task A_lift_that_trains_another_family_is_refused_as_its_priority_and_nothing_is_let_go()
    {
        var curl = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bicep Curl");
        var dips = ResultAssert.Succeeded(await PinAsync(Pin() with { Priority = PatternFamily.Push }));

        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(
            await PinAsync(PinOn(curl) with { Priority = PatternFamily.Push })));

        Assert.Equal("Priority", Assert.Single(refusal.Fields).Field);
        Assert.Equal(dips, Assert.Single(await ListAsync()));
    }

    [Fact]
    public async Task Core_is_not_a_family_a_priority_is_chosen_for()
    {
        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(
            await PinAsync(Pin() with { Priority = PatternFamily.Core })));

        Assert.Equal("Priority", Assert.Single(refusal.Fields).Field);
        Assert.Empty(await StoredPinsAsync());
    }

    // ---- Fixtures ----

    private static PinFocusRequest PinOn(Guid movementDataId) =>
        new(movementDataId, Starts, Ends, 2, PlusSeventyForFive);

    private PinFocusRequest Pin() => PinOn(_dip);

    private Task<Result<FocusPinView, Problem>> PinAsync(PinFocusRequest request) =>
        _athletes.FocusAsync(new FixedClock(Now), service => service.PinAsync(_athletes.Owner, request, default));

    private Task<Result<FocusPinView, Problem>> ReviseAsync(Guid focusPinId, ReviseFocusRequest request) =>
        _athletes.FocusAsync(new FixedClock(Now), service => service.ReviseAsync(_athletes.Owner, focusPinId, request, default));

    private Task<Result<FocusPinView, Problem>> UnpinAsync(Guid focusPinId, DateOnly on) =>
        _athletes.FocusAsync(new FixedClock(Now), service => service.UnpinAsync(
            _athletes.Owner, focusPinId, new UnpinFocusRequest(on), default));

    private Task<Result<Unit, Problem>> DeleteAsync(Guid focusPinId) =>
        _athletes.FocusAsync(new FixedClock(Now), service => service.DeleteAsync(_athletes.Owner, focusPinId, default));

    private async Task<FocusPinView> UnpinnedPinAsync(DateOnly on, DateOnly? startingOn = null)
    {
        var pinned = ResultAssert.Succeeded(await PinAsync(Pin() with { Starts = startingOn ?? Starts }));
        return ResultAssert.Succeeded(await UnpinAsync(pinned.FocusPinId, on));
    }

    private async Task<IReadOnlyList<FocusPinView>> ListAsync() =>
        ResultAssert.Succeeded(await _athletes.FocusAsync(new FixedClock(Now), service =>
            service.GetAllAsync(_athletes.Owner, default)));

    private async Task<IReadOnlyList<FocusPin>> StoredPinsAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.FocusPins.ToListAsync();
    }
}
