using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// Which routine a session was trained from: stated when it is started or logged, carried by
/// every transition, and dropped rather than refused when the routine is not the athlete's.
/// </summary>
public class SessionOriginTests : IDisposable
{
    private static readonly DateTimeOffset Started = new(2026, 11, 2, 17, 0, 0, TimeSpan.FromHours(-5));

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    [Fact]
    public async Task A_session_started_from_one_of_the_athletes_routines_is_trained_from_it()
    {
        var routine = await RoutineOfAsync(_athletes.OwnerId);

        var view = ResultAssert.Succeeded(await StartAsync(routine));

        Assert.Equal(routine, view.RoutineId);
        Assert.Equal(new RoutineOrigin.FromRoutine(routine), await OriginAsync(view.TrainingSessionId));
    }

    [Fact]
    public async Task A_session_logged_from_one_of_the_athletes_routines_is_trained_from_it()
    {
        var routine = await RoutineOfAsync(_athletes.OwnerId);

        var view = ResultAssert.Succeeded(await LogAsync(routine));

        Assert.Equal(routine, view.RoutineId);
    }

    [Fact]
    public async Task A_session_naming_no_routine_is_ad_hoc()
    {
        var view = ResultAssert.Succeeded(await LogAsync(null));

        Assert.Null(view.RoutineId);
        Assert.IsType<RoutineOrigin.AdHoc>(await OriginAsync(view.TrainingSessionId));
    }

    [Fact]
    public async Task A_routine_that_no_longer_exists_is_dropped_and_the_session_still_lands()
    {
        // A workout started offline from a routine deleted in the meantime still happened.
        var view = ResultAssert.Succeeded(await LogAsync(Guid.NewGuid()));

        Assert.Null(view.RoutineId);
        Assert.IsType<RoutineOrigin.AdHoc>(await OriginAsync(view.TrainingSessionId));
    }

    [Fact]
    public async Task Another_athletes_routine_is_dropped_rather_than_refused_or_recorded()
    {
        var theirs = await RoutineOfAsync(_athletes.StrangerId);

        var view = ResultAssert.Succeeded(await StartAsync(theirs));

        Assert.Null(view.RoutineId);
        Assert.IsType<RoutineOrigin.AdHoc>(await OriginAsync(view.TrainingSessionId));
    }

    [Fact]
    public async Task An_active_session_completed_keeps_the_routine_it_was_started_from()
    {
        var routine = await RoutineOfAsync(_athletes.OwnerId);
        var active = ResultAssert.Succeeded(await StartAsync(routine));

        var completed = ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.CompleteAsync(
            _athletes.Owner, active.TrainingSessionId, new CompleteSessionRequest(new PeriodCommand(Started, null), null), default)));

        Assert.Equal(routine, completed.RoutineId);
        Assert.Equal(new RoutineOrigin.FromRoutine(routine), await OriginAsync(active.TrainingSessionId));
    }

    [Fact]
    public async Task A_session_with_its_notes_edited_keeps_its_routine()
    {
        var routine = await RoutineOfAsync(_athletes.OwnerId);
        var logged = ResultAssert.Succeeded(await LogAsync(routine));

        var edited = ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.UpdateNotesAsync(
            _athletes.Owner, logged.TrainingSessionId, new SessionNotesRequest("felt strong"), default)));

        Assert.Equal(routine, edited.RoutineId);
    }

    [Fact]
    public async Task A_planned_session_is_ad_hoc_through_its_start()
    {
        var planned = ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.PlanAsync(
            _athletes.Owner, new PlanSessionRequest(new DateOnly(2026, 11, 4), [], "push", null), default)));

        var started = ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.StartAsync(
            _athletes.Owner, planned.TrainingSessionId, new StartSessionRequest(Started), default)));

        Assert.Null(started.RoutineId);
        Assert.IsType<RoutineOrigin.AdHoc>(await OriginAsync(started.TrainingSessionId));
    }

    private Task<Result<TrainingSessionView, Problem>> StartAsync(Guid? routineId) =>
        _athletes.SessionsAsync(service => service.StartUnplannedAsync(
            _athletes.Owner, new StartUnplannedSessionRequest(Started, "upper", null, routineId), default));

    private Task<Result<TrainingSessionView, Problem>> LogAsync(Guid? routineId) =>
        _athletes.SessionsAsync(service => service.LogCompletedAsync(
            _athletes.Owner,
            new LogCompletedSessionRequest(
                [new LiftSetCommand(Guid.NewGuid(), 5, 225, WeightUnit.Pounds, null)],
                new PeriodCommand(Started, Started.AddHours(1)),
                null,
                "upper",
                null,
                routineId),
            default));

    private async Task<Guid> RoutineOfAsync(Guid userId)
    {
        var routine = Some.UpperDay(userId);
        await _athletes.PlantAsync(context => context.Routines.Add(routine));
        return routine.RoutineId;
    }

    private async Task<RoutineOrigin> OriginAsync(Guid sessionId)
    {
        using var context = _athletes.CreateContext();
        return (await context.Sessions.AsNoTracking().SingleAsync(s => s.TrainingSessionId == sessionId)).Origin;
    }
}
