using lionheart.Functional;
using lionheart.Model.Training.Programs;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// Program routes: the Planned → Active → Completed lifecycle, its illegal moves, and what a
/// delete has to refuse because sessions point at programs without a foreign key to enforce it.
/// </summary>
public class TrainingProgramServiceTests : IDisposable
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 10, 27);

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    [Fact]
    public async Task Planning_a_program_stores_it_planned()
    {
        var program = ResultAssert.Succeeded(await PlanAsync());

        var view = Assert.IsType<PlannedProgramView>(program);
        Assert.Equal("Fall block", view.Title);
        Assert.Equal(Start, view.StartDate);
        Assert.Equal(["strength", "hypertrophy"], view.Tags);

        Assert.IsType<PlannedTrainingProgram>(await LoadAsync(view.TrainingProgramId));
    }

    [Fact]
    public async Task A_program_that_ends_before_it_starts_is_refused()
    {
        var refusal = ResultAssert.Failed(await _athletes.ProgramsAsync(service => service.PlanAsync(
            _athletes.Owner, new PlanProgramRequest("Backwards", End, Start, []), default)));

        Assert.IsType<Problem.Invalid>(refusal);
        Assert.Empty(await AllProgramsAsync());
    }

    [Fact]
    public async Task A_planned_program_activates_keeping_everything_but_its_state()
    {
        var planned = ResultAssert.Succeeded(await PlanAsync());

        var active = ResultAssert.Succeeded(await ActivateAsync(planned.TrainingProgramId));

        var view = Assert.IsType<ActiveProgramView>(active);
        Assert.Equal(planned.TrainingProgramId, view.TrainingProgramId);
        Assert.Equal(planned.Title, view.Title);
        Assert.Equal(planned.Tags, view.Tags);
        Assert.Single(await AllProgramsAsync());
    }

    [Fact]
    public async Task An_active_program_completes_with_the_summary_only_that_state_can_carry()
    {
        var active = await ProgramInAsync(Lifecycle.Active);

        var completed = ResultAssert.Succeeded(await CompleteAsync(active));

        Assert.Equal("Hit every main-lift target.", Assert.IsType<CompletedProgramView>(completed).Summary);
    }

    [Theory]
    [InlineData(Lifecycle.Active)]
    [InlineData(Lifecycle.Completed)]
    public async Task Only_a_planned_program_can_be_activated(string state)
    {
        var programId = await ProgramInAsync(state);

        var refusal = ResultAssert.Failed(await ActivateAsync(programId));

        Assert.IsType<Problem.Conflict>(refusal);
        await AssertUnchangedAsync(programId, state);
    }

    [Theory]
    [InlineData(Lifecycle.Planned)]
    [InlineData(Lifecycle.Completed)]
    public async Task Only_an_active_program_can_be_completed(string state)
    {
        var programId = await ProgramInAsync(state);

        var refusal = ResultAssert.Failed(await CompleteAsync(programId));

        Assert.IsType<Problem.Conflict>(refusal);
        await AssertUnchangedAsync(programId, state);
    }

    [Theory]
    [InlineData(Lifecycle.Planned)]
    [InlineData(Lifecycle.Active)]
    [InlineData(Lifecycle.Completed)]
    public async Task Retitling_a_program_leaves_its_lifecycle_state_alone(string state)
    {
        var programId = await ProgramInAsync(state);
        var before = await LoadAsync(programId);

        var updated = ResultAssert.Succeeded(await _athletes.ProgramsAsync(service => service.UpdateAsync(
            _athletes.Owner,
            programId,
            new UpdateProgramRequest("Renamed block", Start, End, ["deload"]),
            default)));

        Assert.Equal("Renamed block", updated.Title);
        Assert.Equal(["deload"], updated.Tags);

        var after = await LoadAsync(programId);
        Assert.Equal(before.GetType(), after.GetType());
    }

    [Fact]
    public async Task An_update_that_reverses_the_dates_is_refused()
    {
        var programId = await ProgramInAsync(Lifecycle.Planned);

        var refusal = ResultAssert.Failed(await _athletes.ProgramsAsync(service => service.UpdateAsync(
            _athletes.Owner, programId, new UpdateProgramRequest("Backwards", End, Start, []), default)));

        Assert.IsType<Problem.Invalid>(refusal);
        Assert.Equal("Fall block", (await LoadAsync(programId)).Title);
    }

    [Fact]
    public async Task Another_athletes_program_is_missing_rather_than_forbidden_on_every_route()
    {
        var planted = Some.PlannedProgram(_athletes.StrangerId);
        await _athletes.PlantAsync(context => context.Programs.Add(planted));
        var id = planted.TrainingProgramId;

        Problem[] refusals =
        [
            ResultAssert.Failed(await _athletes.ProgramsAsync(s => s.GetAsync(_athletes.Owner, id, default))),
            ResultAssert.Failed(await ActivateAsync(id)),
            ResultAssert.Failed(await CompleteAsync(id)),
            ResultAssert.Failed(await _athletes.ProgramsAsync(s => s.UpdateAsync(
                _athletes.Owner, id, new UpdateProgramRequest("mine now", Start, End, []), default))),
            ResultAssert.Failed(await _athletes.ProgramsAsync(s => s.DeleteAsync(_athletes.Owner, id, default))),
        ];

        Assert.All(refusals, refusal => Assert.IsType<Problem.NotFound>(refusal));
        Assert.Equal(planted.Title, (await LoadAsync(id)).Title);
    }

    [Fact]
    public async Task Listing_programs_never_reaches_across_athletes()
    {
        await _athletes.PlantAsync(context => context.Programs.Add(Some.ActiveProgram(_athletes.StrangerId)));
        var mine = ResultAssert.Succeeded(await PlanAsync());

        var listed = ResultAssert.Succeeded(await _athletes.ProgramsAsync(service =>
            service.GetAllAsync(_athletes.Owner, default)));

        Assert.Equal(mine.TrainingProgramId, Assert.Single(listed).TrainingProgramId);
    }

    [Fact]
    public async Task A_program_with_no_sessions_deletes()
    {
        var programId = await ProgramInAsync(Lifecycle.Planned);

        ResultAssert.Succeeded(await _athletes.ProgramsAsync(service =>
            service.DeleteAsync(_athletes.Owner, programId, default)));

        Assert.Empty(await AllProgramsAsync());
    }

    /// <summary>
    /// Membership persists as a plain Guid with Guid.Empty standing for Standalone, so there is no
    /// foreign key to stop a delete from orphaning the sessions that name the program. Refusing is
    /// what keeps a session from pointing at a program that no longer exists.
    /// </summary>
    [Fact]
    public async Task A_program_that_still_has_sessions_refuses_to_be_deleted()
    {
        var programId = await ProgramInAsync(Lifecycle.Planned);
        await _athletes.PlantAsync(context => context.Sessions.Add(
            Some.PlannedSession(_athletes.OwnerId) with { Membership = new InProgram(programId) }));

        var refusal = ResultAssert.Failed(await _athletes.ProgramsAsync(service =>
            service.DeleteAsync(_athletes.Owner, programId, default)));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Single(await AllProgramsAsync());
    }

    [Fact]
    public async Task Another_athletes_session_does_not_block_a_delete()
    {
        var programId = await ProgramInAsync(Lifecycle.Planned);
        await _athletes.PlantAsync(context => context.Sessions.Add(
            Some.PlannedSession(_athletes.StrangerId) with { Membership = new InProgram(programId) }));

        ResultAssert.Succeeded(await _athletes.ProgramsAsync(service =>
            service.DeleteAsync(_athletes.Owner, programId, default)));

        Assert.Empty(await AllProgramsAsync());
    }

    [Fact]
    public async Task An_account_with_no_profile_is_refused_rather_than_violating_the_foreign_key()
    {
        var refusal = ResultAssert.Failed(await _athletes.ProgramsAsync(service =>
            service.PlanAsync(
                _athletes.Unprofiled,
                new PlanProgramRequest("Fall block", Start, End, []),
                default)));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Empty(await AllProgramsAsync());
    }

    // ---- Fixtures ----

    private static class Lifecycle
    {
        public const string Planned = "planned";
        public const string Active = "active";
        public const string Completed = "completed";
    }

    private Task<Result<TrainingProgramView, Problem>> PlanAsync() =>
        _athletes.ProgramsAsync(service => service.PlanAsync(
            _athletes.Owner,
            new PlanProgramRequest("Fall block", Start, End, ["strength", "hypertrophy"]),
            default));

    private Task<Result<TrainingProgramView, Problem>> ActivateAsync(Guid programId) =>
        _athletes.ProgramsAsync(service => service.ActivateAsync(_athletes.Owner, programId, default));

    private Task<Result<TrainingProgramView, Problem>> CompleteAsync(Guid programId) =>
        _athletes.ProgramsAsync(service => service.CompleteAsync(
            _athletes.Owner, programId, new CompleteProgramRequest("Hit every main-lift target."), default));

    private async Task<Guid> ProgramInAsync(string state)
    {
        var programId = ResultAssert.Succeeded(await PlanAsync()).TrainingProgramId;
        if (state is Lifecycle.Planned) return programId;

        ResultAssert.Succeeded(await ActivateAsync(programId));
        if (state is Lifecycle.Active) return programId;

        ResultAssert.Succeeded(await CompleteAsync(programId));
        return programId;
    }

    private async Task<TrainingProgram> LoadAsync(Guid programId)
    {
        using var context = _athletes.CreateContext();
        return await context.Programs.SingleAsync(program => program.TrainingProgramId == programId);
    }

    private async Task<IReadOnlyList<TrainingProgram>> AllProgramsAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.Programs.Where(p => p.UserId == _athletes.OwnerId).ToListAsync();
    }

    private async Task AssertUnchangedAsync(Guid programId, string state)
    {
        var stored = await LoadAsync(programId);
        Assert.Equal(state, stored switch
        {
            PlannedTrainingProgram => Lifecycle.Planned,
            ActiveTrainingProgram => Lifecycle.Active,
            CompletedTrainingProgram => Lifecycle.Completed,
            _ => "unknown"
        });
    }
}
