using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Training;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// Context routes: the two kinds, the window over the stamped StartTime column, and the same
/// missing-rather-than-forbidden rule sessions and programs follow.
/// </summary>
public class ContextServiceTests : IDisposable
{
    private static readonly DateTimeOffset Logged = new(2026, 9, 10, 22, 0, 0, TimeSpan.FromHours(-4));

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    [Fact]
    public async Task An_exertion_context_keeps_the_effort_a_state_context_has_no_place_for()
    {
        var created = ResultAssert.Succeeded(await CreateExertionAsync());

        var view = Assert.IsType<ExertionContextView>(created);
        Assert.IsType<EffortReported>(view.Effort);
        Assert.Equal(new Interval(Logged, Logged.AddHours(3)), view.Period);

        Assert.IsType<ExertionContext>(await LoadAsync(view.ContextId));
    }

    [Fact]
    public async Task A_state_context_is_stored_under_its_own_kind()
    {
        var created = ResultAssert.Succeeded(await CreateStateAsync());

        Assert.IsType<StateContextView>(created);
        Assert.Equal(new Instant(Logged), created.Period);
        Assert.IsType<StateContext>(await LoadAsync(created.ContextId));
    }

    /// <summary>
    /// Contexts are decorated at creation (docs/data-remodel.md), but the interpretation engine
    /// decorates *text* — a context entered as structure has no utterance to parse, and nothing
    /// else can annotate one. So a structured context is written undecorated and never reaches the
    /// projection retrieval reads. Flip both assertions when the re-decoration path lands.
    /// </summary>
    [Fact]
    public async Task A_structured_context_is_currently_undecorated_and_unprojected()
    {
        var created = ResultAssert.Succeeded(await CreateStateAsync());

        Assert.IsType<NotDecoratedView>(created.Decoration);

        using var context = _athletes.CreateContext();
        Assert.IsType<NotDecorated>((await context.Contexts.SingleAsync()).Decoration);
        Assert.Empty(await context.AnalyzableEntries.ToListAsync());
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_is_refused()
    {
        var refusal = ResultAssert.Failed(await _athletes.ContextsAsync(service => service.CreateStateAsync(
            _athletes.Owner,
            new CreateStateContextRequest(new PeriodCommand(Logged, Logged.AddHours(-3)), "backwards"),
            default)));

        Assert.IsType<Problem.Invalid>(refusal);
        Assert.Empty(await AllContextsAsync());
    }

    [Fact]
    public async Task Effort_with_nothing_in_it_is_not_reported_rather_than_an_empty_report()
    {
        var created = ResultAssert.Succeeded(await _athletes.ContextsAsync(service =>
            service.CreateExertionAsync(
                _athletes.Owner,
                new CreateExertionContextRequest(new PeriodCommand(Logged, null), "moved a couch", null),
                default)));

        Assert.IsType<EffortNotReported>(Assert.IsType<ExertionContextView>(created).Effort);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Notes_are_editable_without_changing_what_kind_of_context_it_is(bool exertion)
    {
        var created = ResultAssert.Succeeded(exertion ? await CreateExertionAsync() : await CreateStateAsync());
        var before = await LoadAsync(created.ContextId);

        var updated = ResultAssert.Succeeded(await _athletes.ContextsAsync(service => service.UpdateNotesAsync(
            _athletes.Owner, created.ContextId, new ContextNotesRequest("worse than it looked"), default)));

        Assert.Equal("worse than it looked", updated.Notes);
        Assert.Equal(created.GetType(), updated.GetType());

        var after = await LoadAsync(created.ContextId);
        Assert.Equal(before.GetType(), after.GetType());
        Assert.Equal("worse than it looked", after.Notes);
    }

    [Fact]
    public async Task Another_athletes_context_is_missing_rather_than_forbidden_on_every_route()
    {
        var planted = Some.StateContext(_athletes.StrangerId);
        await _athletes.PlantAsync(context => context.Contexts.Add(planted));
        var id = planted.ContextId;

        Problem[] refusals =
        [
            ResultAssert.Failed(await _athletes.ContextsAsync(s => s.GetAsync(_athletes.Owner, id, default))),
            ResultAssert.Failed(await _athletes.ContextsAsync(s => s.UpdateNotesAsync(
                _athletes.Owner, id, new ContextNotesRequest("mine now"), default))),
            ResultAssert.Failed(await _athletes.ContextsAsync(s => s.DeleteAsync(_athletes.Owner, id, default))),
        ];

        Assert.All(refusals, refusal => Assert.IsType<Problem.NotFound>(refusal));
        Assert.Equal(planted.Notes, (await LoadAsync(id)).Notes);
    }

    [Fact]
    public async Task The_window_matches_on_the_stamped_start_time_and_stops_at_the_athlete()
    {
        await _athletes.PlantAsync(context => context.Contexts.Add(
            Some.StateContext(_athletes.StrangerId) with { Period = new Instant(Logged) }));
        var mine = ResultAssert.Succeeded(await CreateStateAsync());
        var outside = ResultAssert.Succeeded(await _athletes.ContextsAsync(service => service.CreateStateAsync(
            _athletes.Owner,
            new CreateStateContextRequest(new PeriodCommand(Logged.AddDays(40), null), "much later"),
            default)));

        var found = ResultAssert.Succeeded(await _athletes.ContextsAsync(service => service.GetWindowAsync(
            _athletes.Owner, new TimeWindow(Logged.AddDays(-1), Logged.AddDays(1)), default)));

        Assert.Equal(mine.ContextId, Assert.Single(found).ContextId);
        Assert.DoesNotContain(outside.ContextId, found.Select(view => view.ContextId));
    }

    [Fact]
    public async Task A_window_that_ends_before_it_starts_is_refused()
    {
        var refusal = ResultAssert.Failed(await _athletes.ContextsAsync(service => service.GetWindowAsync(
            _athletes.Owner, new TimeWindow(Logged, Logged.AddDays(-1)), default)));

        Assert.IsType<Problem.Invalid>(refusal);
    }

    [Fact]
    public async Task Deleting_a_context_takes_its_analysis_projection_with_it()
    {
        var created = ResultAssert.Succeeded(await CreateStateAsync());
        await _athletes.PlantAsync(context => context.AnalyzableEntries.Add(
            Some.AnalyzableEntry(_athletes.OwnerId) with { EntryId = created.ContextId, Kind = EntryKind.Context }));

        ResultAssert.Succeeded(await _athletes.ContextsAsync(service =>
            service.DeleteAsync(_athletes.Owner, created.ContextId, default)));

        using var verify = _athletes.CreateContext();
        Assert.Empty(await verify.Contexts.ToListAsync());
        Assert.Empty(await verify.AnalyzableEntries.ToListAsync());
    }

    [Fact]
    public async Task An_account_with_no_profile_is_refused_rather_than_violating_the_foreign_key()
    {
        var refusal = ResultAssert.Failed(await _athletes.ContextsAsync(service =>
            service.CreateStateAsync(
                _athletes.Unprofiled,
                new CreateStateContextRequest(new PeriodCommand(Logged, null), "terrible sleep"),
                default)));

        Assert.IsType<Problem.Conflict>(refusal);
        Assert.Empty(await AllContextsAsync());
    }

    // ---- Fixtures ----

    private Task<Result<ContextView, Problem>> CreateExertionAsync() =>
        _athletes.ContextsAsync(service => service.CreateExertionAsync(
            _athletes.Owner,
            new CreateExertionContextRequest(
                new PeriodCommand(Logged, Logged.AddHours(3)),
                "pressure-washed the driveway",
                new PerceivedEffortCommand(6, 7, 4, 2)),
            default));

    private Task<Result<ContextView, Problem>> CreateStateAsync() =>
        _athletes.ContextsAsync(service => service.CreateStateAsync(
            _athletes.Owner,
            new CreateStateContextRequest(new PeriodCommand(Logged, null), "terrible sleep last night"),
            default));

    private async Task<Context> LoadAsync(Guid contextId)
    {
        using var context = _athletes.CreateContext();
        return await context.Contexts.SingleAsync(c => c.ContextId == contextId);
    }

    private async Task<IReadOnlyList<Context>> AllContextsAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.Contexts.Where(c => c.UserId == _athletes.OwnerId).ToListAsync();
    }
}
