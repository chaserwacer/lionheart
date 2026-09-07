using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using Programs = lionheart.Model.Training.Programs;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// The structured session routes: every lifecycle transition including the ones the domain has to
/// refuse, whole-list replacement, and the rule that another athlete's session is missing rather
/// than forbidden.
/// </summary>
public class TrainingSessionServiceTests : IDisposable
{
    private static readonly DateTimeOffset Started = new(2026, 9, 10, 17, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateOnly PlannedFor = new(2026, 9, 10);

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    // ---- Creation ----

    [Fact]
    public async Task Planning_a_session_stores_it_planned_with_its_prescriptions()
    {
        var planned = ResultAssert.Succeeded(await PlanAsync());

        var view = Assert.IsType<PlannedSessionView>(planned);
        Assert.Equal(PlannedFor, view.PlannedFor);
        Assert.Equal(PlannedFor, view.Date);
        Assert.Equal(2, view.Prescriptions.Count);
        Assert.Null(view.TrainingProgramId);

        var stored = await LoadAsync(view.TrainingSessionId);
        Assert.IsType<PlannedTrainingSession>(stored);
    }

    [Fact]
    public async Task A_created_session_is_stamped_with_user_provenance()
    {
        // Provenance is absent from every request record, so this can only assert that the server
        // picks it — which is the point: a client able to claim AiAssisted, or to deny it, would
        // make the engine's own authorship unauditable.
        var planned = ResultAssert.Succeeded(await PlanAsync());

        Assert.Equal(Provenance.User, planned.Provenance);
    }

    [Fact]
    public async Task An_unplanned_session_starts_active_with_no_sets()
    {
        var started = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.StartUnplannedAsync(
                _athletes.Owner, new StartUnplannedSessionRequest(Started, "just started", null), default)));

        var view = Assert.IsType<ActiveSessionView>(started);
        Assert.Equal(Started, view.StartedAt);
        Assert.Empty(view.Sets);
    }

    [Fact]
    public async Task A_session_can_be_logged_after_the_fact_without_passing_through_the_lifecycle()
    {
        var logged = ResultAssert.Succeeded(await LogCompletedAsync());

        var view = Assert.IsType<CompletedSessionView>(logged);
        Assert.Equal(2, view.Sets.Count);
        Assert.Equal(new Interval(Started, Started.AddMinutes(75)), view.Period);
        Assert.IsType<EffortReported>(view.Effort);
    }

    [Fact]
    public async Task A_session_joins_a_program_the_athlete_owns()
    {
        var program = await PlanProgramAsync();

        var planned = ResultAssert.Succeeded(await PlanAsync(program.TrainingProgramId));

        Assert.Equal(program.TrainingProgramId, planned.TrainingProgramId);

        var stored = await LoadAsync(planned.TrainingSessionId);
        var membership = Assert.IsType<InProgram>(stored.Membership);
        Assert.Equal(program.TrainingProgramId, membership.TrainingProgramId);
    }

    [Fact]
    public async Task A_session_cannot_join_another_athletes_program()
    {
        var stranger = Some.PlannedProgram(_athletes.StrangerId);
        await _athletes.PlantAsync(context => context.Programs.Add(stranger));

        var refusal = ResultAssert.Failed(await PlanAsync(stranger.TrainingProgramId));

        Assert.IsType<Problem.NotFound>(refusal);
        Assert.Empty(await AllSessionsAsync());
    }

    // ---- Transitions ----

    [Fact]
    public async Task A_planned_session_starts_and_carries_its_identity_across()
    {
        var program = await PlanProgramAsync();
        var planned = ResultAssert.Succeeded(await PlanAsync(program.TrainingProgramId));

        var started = ResultAssert.Succeeded(await StartAsync(planned.TrainingSessionId));

        var view = Assert.IsType<ActiveSessionView>(started);
        Assert.Equal(planned.TrainingSessionId, view.TrainingSessionId);
        Assert.Equal(planned.Notes, view.Notes);
        Assert.Equal(planned.CreatedAt, view.CreatedAt);
        Assert.Equal(program.TrainingProgramId, view.TrainingProgramId);
        Assert.Equal(Started, view.StartedAt);

        // The transition replaces the row rather than adding one: a session has one identity.
        Assert.Single(await AllSessionsAsync());
    }

    [Fact]
    public async Task An_active_session_completes_carrying_the_sets_it_accumulated()
    {
        var active = await SessionInAsync(Lifecycle.Active);
        await ReplaceSetsAsync(active, [ALift(5, 225)]);

        var completed = ResultAssert.Succeeded(await CompleteAsync(active));

        var view = Assert.IsType<CompletedSessionView>(completed);
        Assert.Single(view.Sets);
        Assert.Equal(active, view.TrainingSessionId);
    }

    [Fact]
    public async Task A_planned_session_skips_keeping_the_date_it_was_planned_for()
    {
        var planned = await SessionInAsync(Lifecycle.Planned);

        var skipped = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.SkipAsync(_athletes.Owner, planned, default)));

        var view = Assert.IsType<SkippedSessionView>(skipped);
        Assert.Equal(PlannedFor, view.PlannedFor);
        Assert.Equal(PlannedFor, view.Date);
    }

    [Theory]
    [InlineData(Lifecycle.Active)]
    [InlineData(Lifecycle.Completed)]
    [InlineData(Lifecycle.Skipped)]
    public async Task Only_a_planned_session_can_be_started(string state)
    {
        var sessionId = await SessionInAsync(state);

        var refusal = ResultAssert.Failed(await StartAsync(sessionId));

        Assert.IsType<Problem.Conflict>(refusal);
        await AssertUnchangedAsync(sessionId, state);
    }

    [Theory]
    [InlineData(Lifecycle.Planned)]
    [InlineData(Lifecycle.Completed)]
    [InlineData(Lifecycle.Skipped)]
    public async Task Only_an_active_session_can_be_completed(string state)
    {
        var sessionId = await SessionInAsync(state);

        var refusal = ResultAssert.Failed(await CompleteAsync(sessionId));

        Assert.IsType<Problem.Conflict>(refusal);
        await AssertUnchangedAsync(sessionId, state);
    }

    [Theory]
    [InlineData(Lifecycle.Active)]
    [InlineData(Lifecycle.Completed)]
    [InlineData(Lifecycle.Skipped)]
    public async Task Only_a_planned_session_can_be_skipped(string state)
    {
        var sessionId = await SessionInAsync(state);

        var refusal = ResultAssert.Failed(await _athletes.SessionsAsync(service =>
            service.SkipAsync(_athletes.Owner, sessionId, default)));

        Assert.IsType<Problem.Conflict>(refusal);
        await AssertUnchangedAsync(sessionId, state);
    }

    /// <summary>
    /// Completing is where a session would be decorated (docs/data-remodel.md), but the
    /// interpretation engine parses *text* and only ever creates rows — nothing can annotate an
    /// already-structured session. Until the re-decoration path lands, a session completed through
    /// this route is undecorated and never reaches the analysis projection. Flip both assertions
    /// when that path arrives.
    /// </summary>
    [Fact]
    public async Task A_session_completed_through_this_route_is_currently_undecorated_and_unprojected()
    {
        var active = await SessionInAsync(Lifecycle.Active);

        var completed = ResultAssert.Succeeded(await CompleteAsync(active));

        Assert.IsType<NotDecoratedView>(Assert.IsType<CompletedSessionView>(completed).Decoration);

        using var context = _athletes.CreateContext();
        var stored = Assert.IsType<CompletedTrainingSession>(await context.Sessions.SingleAsync());
        Assert.IsType<NotDecorated>(stored.Decoration);
        Assert.Empty(await context.AnalyzableEntries.ToListAsync());
    }

    // ---- Whole-list replacement ----

    [Fact]
    public async Task Replacing_sets_replaces_the_whole_list_rather_than_appending()
    {
        var active = await SessionInAsync(Lifecycle.Active);
        await ReplaceSetsAsync(active, [ALift(5, 225), ALift(5, 235)]);

        var replaced = ResultAssert.Succeeded(await ReplaceSetsAsync(active, [ALift(3, 245)]));

        var sets = Assert.IsType<ActiveSessionView>(replaced).Sets;
        var only = Assert.IsType<LiftSet>(Assert.Single(sets));
        Assert.Equal(3, only.Reps);
        Assert.Equal(245, only.Weight);
    }

    [Fact]
    public async Task Replacing_prescriptions_replaces_the_whole_list_rather_than_appending()
    {
        var planned = await SessionInAsync(Lifecycle.Planned);

        var replaced = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.ReplacePrescriptionsAsync(
                _athletes.Owner,
                planned,
                new ReplacePrescriptionsRequest([new FreeformPrescriptionCommand("coach's choice")]),
                default)));

        var prescriptions = Assert.IsType<PlannedSessionView>(replaced).Prescriptions;
        Assert.Equal("coach's choice", Assert.IsType<FreeformPrescription>(Assert.Single(prescriptions)).Text);
    }

    [Fact]
    public async Task A_replaced_set_arrives_formed_and_unfaceted_because_a_client_states_neither()
    {
        var active = await SessionInAsync(Lifecycle.Active);

        var replaced = ResultAssert.Succeeded(await ReplaceSetsAsync(active, [ALift(5, 225)]));

        var set = Assert.Single(Assert.IsType<ActiveSessionView>(replaced).Sets);
        Assert.IsType<Formed>(set.Source);
        Assert.Empty(set.Facets);
    }

    [Theory]
    [InlineData(Lifecycle.Planned)]
    [InlineData(Lifecycle.Completed)]
    [InlineData(Lifecycle.Skipped)]
    public async Task Only_an_active_session_can_have_its_sets_replaced(string state)
    {
        var sessionId = await SessionInAsync(state);

        var refusal = ResultAssert.Failed(await ReplaceSetsAsync(sessionId, [ALift(5, 225)]));

        Assert.IsType<Problem.Conflict>(refusal);
        await AssertUnchangedAsync(sessionId, state);
    }

    [Theory]
    [InlineData(Lifecycle.Active)]
    [InlineData(Lifecycle.Completed)]
    [InlineData(Lifecycle.Skipped)]
    public async Task Only_a_planned_session_can_have_its_prescriptions_replaced(string state)
    {
        var sessionId = await SessionInAsync(state);

        var refusal = ResultAssert.Failed(await _athletes.SessionsAsync(service =>
            service.ReplacePrescriptionsAsync(
                _athletes.Owner,
                sessionId,
                new ReplacePrescriptionsRequest([new FreeformPrescriptionCommand("anything")]),
                default)));

        Assert.IsType<Problem.Conflict>(refusal);
        await AssertUnchangedAsync(sessionId, state);
    }

    // ---- Notes ----

    [Theory]
    [InlineData(Lifecycle.Planned)]
    [InlineData(Lifecycle.Active)]
    [InlineData(Lifecycle.Completed)]
    [InlineData(Lifecycle.Skipped)]
    public async Task Notes_are_editable_in_every_state_and_change_none_of_them(string state)
    {
        var sessionId = await SessionInAsync(state);
        var before = await LoadAsync(sessionId);

        var updated = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.UpdateNotesAsync(_athletes.Owner, sessionId, new SessionNotesRequest("felt heavy"), default)));

        Assert.Equal("felt heavy", updated.Notes);

        var after = await LoadAsync(sessionId);
        Assert.Equal(before.GetType(), after.GetType());
        Assert.Equal("felt heavy", after.Notes);
    }

    // ---- Ownership ----

    [Fact]
    public async Task Another_athletes_session_is_missing_rather_than_forbidden_on_every_route()
    {
        var planted = Some.PlannedSession(_athletes.StrangerId);
        await _athletes.PlantAsync(context => context.Sessions.Add(planted));
        var id = planted.TrainingSessionId;

        Problem[] refusals =
        [
            ResultAssert.Failed(await _athletes.SessionsAsync(s => s.GetAsync(_athletes.Owner, id, default))),
            ResultAssert.Failed(await StartAsync(id)),
            ResultAssert.Failed(await CompleteAsync(id)),
            ResultAssert.Failed(await _athletes.SessionsAsync(s => s.SkipAsync(_athletes.Owner, id, default))),
            ResultAssert.Failed(await ReplaceSetsAsync(id, [ALift(5, 225)])),
            ResultAssert.Failed(await _athletes.SessionsAsync(s => s.ReplacePrescriptionsAsync(
                _athletes.Owner, id, new ReplacePrescriptionsRequest([]), default))),
            ResultAssert.Failed(await _athletes.SessionsAsync(s => s.UpdateNotesAsync(
                _athletes.Owner, id, new SessionNotesRequest("mine now"), default))),
            ResultAssert.Failed(await _athletes.SessionsAsync(s => s.DeleteAsync(_athletes.Owner, id, default))),
        ];

        // Not Forbidden: telling a stranger that a record exists but is not theirs is the
        // disclosure the 404 rule exists to prevent.
        Assert.All(refusals, refusal => Assert.IsType<Problem.NotFound>(refusal));
    }

    [Fact]
    public async Task A_refused_route_leaves_another_athletes_session_exactly_as_it_was()
    {
        var planted = Some.PlannedSession(_athletes.StrangerId);
        await _athletes.PlantAsync(context => context.Sessions.Add(planted));

        await _athletes.SessionsAsync(s => s.UpdateNotesAsync(
            _athletes.Owner, planted.TrainingSessionId, new SessionNotesRequest("mine now"), default));
        await _athletes.SessionsAsync(s => s.DeleteAsync(_athletes.Owner, planted.TrainingSessionId, default));

        var survivor = await LoadAsync(planted.TrainingSessionId);
        Assert.Equal(planted.Notes, survivor.Notes);
        Assert.Equal(_athletes.StrangerId, survivor.UserId);
    }

    [Fact]
    public async Task A_window_never_reaches_across_athletes()
    {
        await _athletes.PlantAsync(context => context.Sessions.Add(
            Some.CompletedSession(_athletes.StrangerId) with { Period = new Interval(Started, Started.AddHours(1)) }));
        await LogCompletedAsync();

        var mine = ResultAssert.Succeeded(await WindowAsync(Started.AddDays(-30), Started.AddDays(30)));

        Assert.Single(mine);
    }

    // ---- Reads ----

    [Fact]
    public async Task The_window_matches_each_lifecycle_state_on_its_own_time_truth()
    {
        var inside = new List<Guid>
        {
            await SessionInAsync(Lifecycle.Planned),
            await SessionInAsync(Lifecycle.Active),
            await SessionInAsync(Lifecycle.Completed),
            await SessionInAsync(Lifecycle.Skipped),
        };

        var found = ResultAssert.Succeeded(await WindowAsync(Started.AddDays(-1), Started.AddDays(1)));

        Assert.Equal(inside.Order(), found.Select(view => view.TrainingSessionId).Order());
    }

    [Fact]
    public async Task A_session_outside_the_window_is_not_returned()
    {
        await SessionInAsync(Lifecycle.Completed);

        var found = ResultAssert.Succeeded(await WindowAsync(Started.AddDays(30), Started.AddDays(60)));

        Assert.Empty(found);
    }

    [Fact]
    public async Task A_window_that_ends_before_it_starts_is_refused()
    {
        var refusal = ResultAssert.Failed(await WindowAsync(Started, Started.AddDays(-1)));

        Assert.IsType<Problem.Invalid>(refusal);
    }

    // ---- Delete ----

    [Fact]
    public async Task Deleting_a_session_takes_its_analysis_projection_with_it()
    {
        var sessionId = await SessionInAsync(Lifecycle.Completed);
        await _athletes.PlantAsync(context => context.AnalyzableEntries.Add(
            Some.AnalyzableEntry(_athletes.OwnerId) with { EntryId = sessionId }));

        ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.DeleteAsync(_athletes.Owner, sessionId, default)));

        using var verify = _athletes.CreateContext();
        Assert.Empty(await verify.Sessions.ToListAsync());
        Assert.Empty(await verify.AnalyzableEntries.ToListAsync());
    }

    // ---- Boundary guards ----

    [Fact]
    public async Task An_account_with_no_profile_is_refused_rather_than_violating_the_foreign_key()
    {
        // Registering through the Identity API creates no LionheartUser, and every session row has
        // a foreign key to one. Without the guard this is a constraint violation escaping as a 500
        // on the very first thing a new account tries to do.
        var refusal = ResultAssert.Failed(await _athletes.SessionsAsync(service =>
            service.LogCompletedAsync(
                _athletes.Unprofiled, ALoggedSession([ALift(5, 225)]), default)));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Empty(await AllSessionsAsync());
    }

    [Fact]
    public async Task An_rpe_outside_its_range_is_refused_rather_than_thrown()
    {
        var refusal = ResultAssert.Failed(await _athletes.SessionsAsync(service =>
            service.LogCompletedAsync(_athletes.Owner, ALoggedSession([ALift(5, 225, rpe: 11)]), default)));

        var invalid = Assert.IsType<Problem.Invalid>(refusal);
        Assert.Equal("Rpe", Assert.Single(invalid.Fields).Field);
        Assert.Empty(await AllSessionsAsync());
    }

    [Fact]
    public async Task A_weight_unit_that_names_no_member_is_refused_rather_than_thrown()
    {
        var set = new LiftSetCommand(Guid.NewGuid(), 5, 225, (WeightUnit)99, null);

        var refusal = ResultAssert.Failed(await _athletes.SessionsAsync(service =>
            service.LogCompletedAsync(_athletes.Owner, ALoggedSession([set]), default)));

        Assert.Equal("Unit", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
        Assert.Empty(await AllSessionsAsync());
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_is_refused()
    {
        var request = ALoggedSession([ALift(5, 225)]) with
        {
            Period = new PeriodCommand(Started, Started.AddHours(-2))
        };

        var refusal = ResultAssert.Failed(await _athletes.SessionsAsync(service =>
            service.LogCompletedAsync(_athletes.Owner, request, default)));

        Assert.IsType<Problem.Invalid>(refusal);
        Assert.Empty(await AllSessionsAsync());
    }

    [Fact]
    public async Task An_instant_period_is_accepted_where_no_end_is_stated()
    {
        var request = ALoggedSession([ALift(5, 225)]) with { Period = new PeriodCommand(Started, null) };

        var logged = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.LogCompletedAsync(_athletes.Owner, request, default)));

        Assert.Equal(new Instant(Started), Assert.IsType<CompletedSessionView>(logged).Period);
    }

    [Fact]
    public async Task Effort_with_nothing_in_it_is_not_reported_rather_than_an_empty_report()
    {
        var request = ALoggedSession([ALift(5, 225)]) with { Effort = null };

        var logged = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.LogCompletedAsync(_athletes.Owner, request, default)));

        Assert.IsType<EffortNotReported>(Assert.IsType<CompletedSessionView>(logged).Effort);
    }

    // ---- Fixtures ----

    private static class Lifecycle
    {
        public const string Planned = "planned";
        public const string Active = "active";
        public const string Completed = "completed";
        public const string Skipped = "skipped";
    }

    private static LiftSetCommand ALift(int reps, double weight, double? rpe = 8) =>
        new(Guid.NewGuid(), reps, weight, WeightUnit.Pounds, rpe);

    private static LogCompletedSessionRequest ALoggedSession(IReadOnlyList<SetCommand> sets) =>
        new(sets,
            new PeriodCommand(Started, Started.AddMinutes(75)),
            new PerceivedEffortCommand(6, 8, 7, 3),
            "logged after the fact",
            null);

    private Task<Result<TrainingSessionView, Problem>> PlanAsync(Guid? programId = null) =>
        _athletes.SessionsAsync(service => service.PlanAsync(
            _athletes.Owner,
            new PlanSessionRequest(
                PlannedFor,
                [
                    new LiftPrescriptionCommand(Guid.NewGuid(), 5, 225, WeightUnit.Pounds, 8),
                    new FreeformPrescriptionCommand("finisher, coach's choice"),
                ],
                "push day",
                programId),
            default));

    private Task<Result<TrainingSessionView, Problem>> LogCompletedAsync() =>
        _athletes.SessionsAsync(service => service.LogCompletedAsync(
            _athletes.Owner, ALoggedSession([ALift(5, 225), ALift(5, 235)]), default));

    private Task<Result<TrainingSessionView, Problem>> StartAsync(Guid sessionId) =>
        _athletes.SessionsAsync(service => service.StartAsync(
            _athletes.Owner, sessionId, new StartSessionRequest(Started), default));

    private Task<Result<TrainingSessionView, Problem>> CompleteAsync(Guid sessionId) =>
        _athletes.SessionsAsync(service => service.CompleteAsync(
            _athletes.Owner,
            sessionId,
            new CompleteSessionRequest(
                new PeriodCommand(Started, Started.AddMinutes(75)),
                new PerceivedEffortCommand(6, 8, 7, 3)),
            default));

    private Task<Result<TrainingSessionView, Problem>> ReplaceSetsAsync(
        Guid sessionId, IReadOnlyList<SetCommand> sets) =>
        _athletes.SessionsAsync(service => service.ReplaceSetsAsync(
            _athletes.Owner, sessionId, new ReplaceSetsRequest(sets), default));

    private Task<Result<IReadOnlyList<TrainingSessionView>, Problem>> WindowAsync(
        DateTimeOffset from, DateTimeOffset to) =>
        _athletes.SessionsAsync(service => service.GetWindowAsync(
            _athletes.Owner, new TimeWindow(from, to), default));

    private Task<Programs.TrainingProgramView> PlanProgramAsync() =>
        _athletes.ProgramsAsync(async service => ResultAssert.Succeeded(await service.PlanAsync(
            _athletes.Owner,
            new Programs.PlanProgramRequest(
                "Fall block", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 27), ["strength"]),
            default)));

    /// <summary>A session in one lifecycle state, reached only through the routes that lead there.</summary>
    private async Task<Guid> SessionInAsync(string state)
    {
        switch (state)
        {
            case Lifecycle.Planned:
                return ResultAssert.Succeeded(await PlanAsync()).TrainingSessionId;

            case Lifecycle.Active:
                var toStart = ResultAssert.Succeeded(await PlanAsync()).TrainingSessionId;
                return ResultAssert.Succeeded(await StartAsync(toStart)).TrainingSessionId;

            case Lifecycle.Completed:
                return ResultAssert.Succeeded(await LogCompletedAsync()).TrainingSessionId;

            case Lifecycle.Skipped:
                var toSkip = ResultAssert.Succeeded(await PlanAsync()).TrainingSessionId;
                return ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
                    service.SkipAsync(_athletes.Owner, toSkip, default))).TrainingSessionId;

            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown lifecycle state.");
        }
    }

    private async Task<TrainingSession> LoadAsync(Guid sessionId)
    {
        using var context = _athletes.CreateContext();
        return await context.Sessions.SingleAsync(session => session.TrainingSessionId == sessionId);
    }

    private async Task<IReadOnlyList<TrainingSession>> AllSessionsAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.Sessions.Where(s => s.UserId == _athletes.OwnerId).ToListAsync();
    }

    private async Task AssertUnchangedAsync(Guid sessionId, string state)
    {
        var stored = await LoadAsync(sessionId);
        Assert.Equal(state, stored switch
        {
            PlannedTrainingSession => Lifecycle.Planned,
            ActiveTrainingSession => Lifecycle.Active,
            CompletedTrainingSession => Lifecycle.Completed,
            SkippedTrainingSession => Lifecycle.Skipped,
            _ => "unknown"
        });
    }
}
