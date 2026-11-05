using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Chat.Changes;
using lionheart.Services.Training.Availability;
using lionheart.Services.Training.Patterns;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatChanges;

/// <summary>What the chat reads to name what it proposes: the days ahead with their ids, and his movements.</summary>
public sealed class ChatPlanLookupsTests : IDisposable
{
    private static readonly DateOnly Wednesday = ChangeDesk.Today.AddDays(7);

    private readonly ChangeDesk _desk = new();

    public void Dispose() => _desk.Dispose();

    private async Task<IReadOnlyList<string>> LookAsync(Func<IChatPlanLookups, Task<IReadOnlyList<string>>> look)
    {
        await using var context = _desk.Athletes.CreateContext();
        return await look(new ChatPlanLookups(context, new NonTrainingDayService(context, ChangeDesk.Clock), MovementPatternMap.Shipped));
    }

    [Fact]
    public async Task Each_day_reads_with_its_plans_who_made_them_and_the_ids_that_change_them()
    {
        var tempo = ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.PlanAssistedAsync(
            _desk.Athletes.Owner, new PlanSessionRequest(Wednesday, [new FreeformPrescriptionCommand("Easy run")], "with friends", null), default)));
        var his = ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.PlanAsync(
            _desk.Athletes.Owner, new PlanSessionRequest(Wednesday.AddDays(1), [], "", null), default)));
        var off = ResultAssert.Succeeded(await _desk.Athletes.NonTrainingDaysAsync(ChangeDesk.Clock, service => service.MarkAsync(
            _desk.Athletes.Owner, new MarkNonTrainingDayRequest(Wednesday.AddDays(2), NonTrainingRepeat.Weekly, Note: "travel"), default)));

        var days = await LookAsync(lookups => lookups.GetPlannedDaysAsync(_desk.Athletes.Owner, new DayRange(Wednesday, Wednesday.AddDays(3))));

        Assert.Equal(
            [
                $"2026-11-11 Wed: planned. Session {tempo.TrainingSessionId}, planned by Tempo: Easy run; Note: with friends.",
                $"2026-11-12 Thu: planned. Session {his.TrainingSessionId}, planned by him: nothing prescribed.",
                $"2026-11-13 Fri: non-training (travel), every week; nonTrainingDayId {off.NonTrainingDayId}.",
                "2026-11-14 Sat: open.",
            ],
            days);
    }

    [Fact]
    public async Task A_range_longer_than_six_weeks_is_refused_in_words_the_model_can_act_on()
    {
        var days = await LookAsync(lookups => lookups.GetPlannedDaysAsync(_desk.Athletes.Owner, new DayRange(Wednesday, Wednesday.AddDays(60))));

        Assert.Equal(["At most 42 days are read at once."], days);
    }

    [Fact]
    public async Task Movements_are_found_by_every_word_of_the_query_with_the_family_they_train()
    {
        var squat = await _desk.Athletes.PlantMovementAsync(_desk.Athletes.OwnerId, "Hack Squat");
        await _desk.Athletes.PlantMovementAsync(_desk.Athletes.OwnerId, "Bench Press");
        await _desk.Athletes.PlantMovementAsync(_desk.Athletes.StrangerId, "Hack Squat");

        var found = await LookAsync(lookups => lookups.FindMovementsAsync(_desk.Athletes.Owner, "hack squat"));

        Assert.Equal([$"{squat}: Hack Squat (Barbell) (legs)"], found);
    }
}
