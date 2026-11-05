using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using lionheart.Services.Training.Availability;
using lionheart.Tests.Support;

namespace lionheart.Tests.Balance;

/// <summary>
/// Which days a non-training day covers, and how a day reads when a date marked off, a planned
/// session and a weekday marked off meet on it.
/// </summary>
public class AvailabilityAnalysisTests
{
    /// <summary>A Sunday, so "every week from it" means every Sunday.</summary>
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    private static readonly DayRange Fortnight = new(Sunday.AddDays(-7), Sunday.AddDays(6));

    private static NonTrainingDay Marked(NonTrainingSchedule schedule, string note = "", int minute = 0) =>
        NonTrainingDay.Mark(Guid.NewGuid(), Guid.NewGuid(), schedule, note, DateTime.UnixEpoch.AddMinutes(minute));

    private static readonly IReadOnlySet<DateOnly> None = new HashSet<DateOnly>();

    private static DayAvailability On(AvailabilityCalendar calendar, DateOnly day) => calendar.Days.Single(entry => entry.Day == day);

    [Fact]
    public void A_weekly_day_covers_its_weekday_from_its_first_day_on_and_not_before()
    {
        var calendar = AvailabilityAnalysis.Of([Marked(new NonTrainingSchedule.EveryWeek(Sunday))], None, None, Fortnight);

        Assert.Equal([Sunday], calendar.Unavailable.Select(day => day.Day));
        Assert.Equal(13, calendar.AvailableDays);
    }

    [Fact]
    public void A_weekly_day_with_a_last_day_covers_nothing_after_it()
    {
        var until = ResultAssert.Succeeded(NonTrainingSchedule.EveryWeekUntil.Of(Sunday.AddDays(-7), Sunday.AddDays(-1)));

        var calendar = AvailabilityAnalysis.Of([Marked(until)], None, None, Fortnight);

        Assert.Equal([Sunday.AddDays(-7)], calendar.Unavailable.Select(day => day.Day));
    }

    [Fact]
    public void A_planned_session_frees_a_weekday_marked_off_every_week()
    {
        var calendar = AvailabilityAnalysis.Of(
            [Marked(new NonTrainingSchedule.EveryWeek(Sunday.AddDays(-7)))], new HashSet<DateOnly> { Sunday }, None, Fortnight);

        Assert.IsType<DayAvailability.Planned>(On(calendar, Sunday));
        Assert.IsType<DayAvailability.NonTraining>(On(calendar, Sunday.AddDays(-7)));
    }

    [Fact]
    public void A_date_marked_off_outranks_a_session_planned_on_it()
    {
        var calendar = AvailabilityAnalysis.Of(
            [Marked(new NonTrainingSchedule.OneDay(Sunday), "travel")], new HashSet<DateOnly> { Sunday }, None, Fortnight);

        Assert.Equal(new DayAvailability.NonTraining(Sunday, calendar.Unavailable[0].NonTrainingDayId, "travel", false), On(calendar, Sunday));
    }

    [Fact]
    public void Where_a_date_and_a_weekday_both_cover_a_day_the_date_names_it()
    {
        var date = Marked(new NonTrainingSchedule.OneDay(Sunday), "wedding", minute: 5);

        var calendar = AvailabilityAnalysis.Of(
            [Marked(new NonTrainingSchedule.EveryWeek(Sunday.AddDays(-7)), minute: 0), date], None, None, Fortnight);

        var sunday = Assert.IsType<DayAvailability.NonTraining>(On(calendar, Sunday));
        Assert.Equal((date.NonTrainingDayId, "wedding", false), (sunday.NonTrainingDayId, sunday.Note, sunday.Repeats));
    }

    [Fact]
    public void A_day_trained_was_available_whatever_marked_it_off()
    {
        var calendar = AvailabilityAnalysis.Of(
            [Marked(new NonTrainingSchedule.OneDay(Sunday)), Marked(new NonTrainingSchedule.EveryWeek(Sunday.AddDays(-7)))],
            None,
            new HashSet<DateOnly> { Sunday, Sunday.AddDays(-7) },
            Fortnight);

        Assert.IsType<DayAvailability.Trained>(On(calendar, Sunday));
        Assert.IsType<DayAvailability.Trained>(On(calendar, Sunday.AddDays(-7)));
        Assert.Equal(14, calendar.AvailableDays);
    }

    [Theory]
    [InlineData(NonTrainingRepeat.Once, null, typeof(NonTrainingSchedule.OneDay))]
    [InlineData(NonTrainingRepeat.Weekly, null, typeof(NonTrainingSchedule.EveryWeek))]
    [InlineData(NonTrainingRepeat.Weekly, 21, typeof(NonTrainingSchedule.EveryWeekUntil))]
    public void A_request_states_one_of_the_three_schedules(NonTrainingRepeat repeat, int? untilInDays, Type schedule)
    {
        var request = new MarkNonTrainingDayRequest(Sunday, repeat, untilInDays is { } days ? Sunday.AddDays(days) : null);

        Assert.IsType(schedule, ResultAssert.Succeeded(request.ToSchedule()));
    }

    [Theory]
    [InlineData(NonTrainingRepeat.Once, 7, "Until")]
    [InlineData(NonTrainingRepeat.Weekly, -1, "Until")]
    public void A_last_day_on_a_single_day_or_before_a_repeat_starts_is_refused_naming_until(
        NonTrainingRepeat repeat, int untilInDays, string field)
    {
        var problem = ResultAssert.Failed(new MarkNonTrainingDayRequest(Sunday, repeat, Sunday.AddDays(untilInDays)).ToSchedule());

        Assert.Equal(field, Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
    }

    [Fact]
    public void An_unknown_repeat_is_refused_naming_repeat()
    {
        var problem = ResultAssert.Failed(new MarkNonTrainingDayRequest(Sunday, (NonTrainingRepeat)7).ToSchedule());

        Assert.Equal("Repeat", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
    }

    [Fact]
    public void A_note_is_trimmed_absent_is_empty_and_past_two_hundred_characters_is_refused()
    {
        Assert.Equal("travel", ResultAssert.Succeeded(new MarkNonTrainingDayRequest(Sunday, Note: "  travel ").ToNote()));
        Assert.Equal("", ResultAssert.Succeeded(new MarkNonTrainingDayRequest(Sunday).ToNote()));
        var problem = ResultAssert.Failed(new MarkNonTrainingDayRequest(Sunday, Note: new string('x', 201)).ToNote());
        Assert.Equal("Note", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
    }
}
