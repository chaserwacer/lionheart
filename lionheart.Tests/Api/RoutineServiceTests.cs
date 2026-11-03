using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// The routine routes: defining one, saving a completed session as one, the last time each was
/// trained, and the rule that another athlete's routine, session or movement is not found.
/// </summary>
public class RoutineServiceTests : IDisposable
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 2, 17, 0, 0, TimeSpan.FromHours(-5));

    private readonly TwoAthletes _athletes = new();
    private Guid _press;
    private Guid _pulldown;

    public void Dispose() => _athletes.Dispose();

    private async Task MovementsAsync()
    {
        _press = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bench Press");
        _pulldown = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Lat Pulldown");
    }

    // ---- Defining ----

    [Fact]
    public async Task A_routine_is_defined_with_its_movements_in_order_and_has_never_been_trained()
    {
        await MovementsAsync();

        var view = ResultAssert.Succeeded(await DefineAsync(UpperDay()));

        Assert.Equal(("Upper day", "pin 7"), (view.Name, view.Notes));
        Assert.Equal([new RoutineMovement(_pulldown, 2), new RoutineMovement(_press, 3)], view.Movements);
        Assert.Null(view.LastSession);
        Assert.Equal(view.RoutineId, Assert.Single(await StoredAsync()).RoutineId);
    }

    [Fact]
    public async Task A_routine_naming_another_athletes_movement_is_not_found_and_nothing_is_written()
    {
        await MovementsAsync();
        var theirs = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Squat");

        var refusal = ResultAssert.Failed(await DefineAsync(
            UpperDay() with { Movements = [new RoutineMovement(_press, 2), new RoutineMovement(theirs, 2)] }));

        Assert.IsType<Problem.NotFound>(refusal);
        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task A_routine_naming_a_movement_that_does_not_exist_is_not_found()
    {
        await MovementsAsync();

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await DefineAsync(
            UpperDay() with { Movements = [new RoutineMovement(Guid.NewGuid(), 2)] })));
    }

    [Fact]
    public async Task An_invalid_routine_is_refused_on_its_field_and_nothing_is_written()
    {
        await MovementsAsync();

        var refusal = ResultAssert.Failed(await DefineAsync(UpperDay() with { Name = "  " }));

        Assert.Equal("Name", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task An_account_with_no_profile_cannot_define_a_routine()
    {
        var refusal = ResultAssert.Failed(await _athletes.RoutinesAsync(service =>
            service.DefineAsync(_athletes.Unprofiled, UpperDay(), default)));

        Assert.IsType<Problem.Conflict>(refusal);
    }

    // ---- From a session ----

    [Fact]
    public async Task A_completed_session_saved_as_a_routine_gives_its_movements_and_working_sets()
    {
        await MovementsAsync();
        var session = await LogAsync(
            null,
            Lift(_pulldown, SetKind.WarmUp), Lift(_pulldown), Lift(_press), Lift(_pulldown, SetKind.Drop),
            Lift(_press, SetKind.Failure), new FreeformSetCommand("stretched"));

        var view = ResultAssert.Succeeded(await FromSessionAsync(session, "Upper day"));

        Assert.Equal([new RoutineMovement(_pulldown, 2), new RoutineMovement(_press, 2)], view.Movements);
        Assert.Equal(("Upper day", string.Empty), (view.Name, view.Notes));
    }

    [Fact]
    public async Task An_ad_hoc_session_saved_as_a_routine_becomes_that_routines_first_session()
    {
        await MovementsAsync();
        var session = await LogAsync(null, Lift(_press));

        var view = ResultAssert.Succeeded(await FromSessionAsync(session, "Upper day"));

        Assert.Equal(session, Assert.IsType<CompletedSessionView>(view.LastSession).TrainingSessionId);
        Assert.Equal(new RoutineOrigin.FromRoutine(view.RoutineId), (await SessionAsync(session)).Origin);
    }

    [Fact]
    public async Task A_session_trained_from_another_routine_keeps_that_origin()
    {
        await MovementsAsync();
        var other = ResultAssert.Succeeded(await DefineAsync(UpperDay()));
        var session = await LogAsync(other.RoutineId, Lift(_press));

        var copy = ResultAssert.Succeeded(await FromSessionAsync(session, "Upper copy"));

        Assert.Null(copy.LastSession);
        Assert.Equal(new RoutineOrigin.FromRoutine(other.RoutineId), (await SessionAsync(session)).Origin);
    }

    [Fact]
    public async Task A_session_still_in_progress_cannot_be_saved_as_a_routine_and_nothing_is_written()
    {
        var active = ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.StartUnplannedAsync(
            _athletes.Owner, new StartUnplannedSessionRequest(Monday, "going", null, null), default)));

        var refusal = ResultAssert.Failed(await FromSessionAsync(active.TrainingSessionId, "Upper day"));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task Another_athletes_session_is_not_found()
    {
        await MovementsAsync();
        var theirs = Some.CompletedSession(_athletes.StrangerId);
        await _athletes.PlantAsync(context => context.Sessions.Add(theirs));

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await FromSessionAsync(theirs.TrainingSessionId, "Mine now")));
    }

    [Fact]
    public async Task A_session_with_no_working_set_on_a_movement_is_refused()
    {
        await MovementsAsync();
        var session = await LogAsync(null, Lift(_press, SetKind.WarmUp), new FreeformSetCommand("mobility"));

        var refusal = ResultAssert.Failed(await FromSessionAsync(session, "Upper day"));

        Assert.Equal("SessionId", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
        Assert.Empty(await StoredAsync());
        Assert.IsType<RoutineOrigin.AdHoc>((await SessionAsync(session)).Origin);
    }

    // ---- Reading ----

    [Fact]
    public async Task Routines_list_oldest_first_each_with_its_most_recently_started_completed_session()
    {
        await MovementsAsync();
        var upper = ResultAssert.Succeeded(await DefineAsync(UpperDay()));
        var lower = ResultAssert.Succeeded(await DefineAsync(UpperDay() with { Name = "Lower day" }));
        var later = await LogAsync(upper.RoutineId, Monday.AddDays(3), Lift(_press));
        await LogAsync(upper.RoutineId, Monday, Lift(_press));
        await _athletes.SessionsAsync(service => service.StartUnplannedAsync(
            _athletes.Owner, new StartUnplannedSessionRequest(Monday.AddDays(5), "in progress", null, upper.RoutineId), default));

        var routines = ResultAssert.Succeeded(await _athletes.RoutinesAsync(service => service.GetAllAsync(_athletes.Owner, default)));

        Assert.Equal([upper.RoutineId, lower.RoutineId], routines.Select(r => r.RoutineId));
        Assert.Equal(later, routines[0].LastSession?.TrainingSessionId);
        Assert.Null(routines[1].LastSession);
    }

    [Fact]
    public async Task One_routine_reads_with_its_last_session()
    {
        await MovementsAsync();
        var upper = ResultAssert.Succeeded(await DefineAsync(UpperDay()));
        var session = await LogAsync(upper.RoutineId, Lift(_press));

        var view = ResultAssert.Succeeded(await _athletes.RoutinesAsync(service =>
            service.GetAsync(_athletes.Owner, upper.RoutineId, default)));

        Assert.Equal(session, view.LastSession?.TrainingSessionId);
    }

    [Fact]
    public async Task Another_athletes_routine_is_not_found_to_read_revise_or_delete()
    {
        await MovementsAsync();
        var mine = ResultAssert.Succeeded(await DefineAsync(UpperDay()));

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.RoutinesAsync(service =>
            service.GetAsync(_athletes.Stranger, mine.RoutineId, default))));
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.RoutinesAsync(service =>
            service.ReviseAsync(_athletes.Stranger, mine.RoutineId, UpperDay(), default))));
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.RoutinesAsync(service =>
            service.DeleteAsync(_athletes.Stranger, mine.RoutineId, default))));
        Assert.Single(await StoredAsync());
    }

    // ---- Revising and deleting ----

    [Fact]
    public async Task Revising_a_routine_replaces_what_it_says_and_keeps_what_it_is()
    {
        await MovementsAsync();
        var upper = ResultAssert.Succeeded(await DefineAsync(UpperDay()));

        var revised = ResultAssert.Succeeded(await _athletes.RoutinesAsync(service => service.ReviseAsync(
            _athletes.Owner, upper.RoutineId,
            new DefineRoutineRequest("Upper B", "new pin", [new RoutineMovement(_press, 4)]), default)));

        Assert.Equal(
            (upper.RoutineId, upper.CreatedAt, "Upper B", "new pin"),
            (revised.RoutineId, revised.CreatedAt, revised.Name, revised.Notes));
        Assert.Equal([new RoutineMovement(_press, 4)], Assert.Single(await StoredAsync()).Movements);
    }

    [Fact]
    public async Task A_revision_that_is_refused_changes_nothing()
    {
        await MovementsAsync();
        var upper = ResultAssert.Succeeded(await DefineAsync(UpperDay()));

        var refusal = ResultAssert.Failed(await _athletes.RoutinesAsync(service => service.ReviseAsync(
            _athletes.Owner, upper.RoutineId,
            new DefineRoutineRequest("Upper B", string.Empty, [new RoutineMovement(_press, 21)]), default)));

        Assert.IsType<Problem.Invalid>(refusal);
        Assert.Equal("Upper day", Assert.Single(await StoredAsync()).Name);
    }

    [Fact]
    public async Task Deleting_a_routine_leaves_its_sessions_naming_it()
    {
        await MovementsAsync();
        var upper = ResultAssert.Succeeded(await DefineAsync(UpperDay()));
        var session = await LogAsync(upper.RoutineId, Lift(_press));

        ResultAssert.Succeeded(await _athletes.RoutinesAsync(service =>
            service.DeleteAsync(_athletes.Owner, upper.RoutineId, default)));

        Assert.Empty(await StoredAsync());
        Assert.Equal(new RoutineOrigin.FromRoutine(upper.RoutineId), (await SessionAsync(session)).Origin);
    }

    // ---- Helpers ----

    private DefineRoutineRequest UpperDay() =>
        new("Upper day", "pin 7", [new RoutineMovement(_pulldown, 2), new RoutineMovement(_press, 3)]);

    private Task<Result<RoutineView, Problem>> DefineAsync(DefineRoutineRequest request) =>
        _athletes.RoutinesAsync(service => service.DefineAsync(_athletes.Owner, request, default));

    private Task<Result<RoutineView, Problem>> FromSessionAsync(Guid sessionId, string name) =>
        _athletes.RoutinesAsync(service =>
            service.FromSessionAsync(_athletes.Owner, sessionId, new RoutineFromSessionRequest(name), default));

    private static LiftSetCommand Lift(Guid movement, SetKind kind = SetKind.Working) =>
        new(movement, 5, 100, WeightUnit.Pounds, null, kind);

    private Task<Guid> LogAsync(Guid? routineId, params SetCommand[] sets) => LogAsync(routineId, Monday, sets);

    private async Task<Guid> LogAsync(Guid? routineId, DateTimeOffset start, params SetCommand[] sets) =>
        ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.LogCompletedAsync(
            _athletes.Owner,
            new LogCompletedSessionRequest([.. sets], new PeriodCommand(start, start.AddHours(1)), null, "logged", null, routineId),
            default))).TrainingSessionId;

    private async Task<IReadOnlyList<Routine>> StoredAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.Routines.AsNoTracking().ToListAsync();
    }

    private async Task<TrainingSession> SessionAsync(Guid sessionId)
    {
        using var context = _athletes.CreateContext();
        return await context.Sessions.AsNoTracking().SingleAsync(s => s.TrainingSessionId == sessionId);
    }
}
