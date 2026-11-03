using lionheart.Functional;
using lionheart.Model.Training.Sets;
using lionheart.Services.Strong;
using lionheart.Tests.Support;
using static lionheart.Tests.Strong.StrongExports;

namespace lionheart.Tests.Strong;

/// <summary>
/// Reading Strong's CSV into workouts: what each column means, and what refuses the export.
/// </summary>
public class StrongExportReaderTests
{
    // ---- The real export ----

    [Fact]
    public void Rows_sharing_a_date_and_name_become_one_workout_with_its_sets_in_order()
    {
        var workouts = Read(FourWorkouts);

        Assert.Equal(
            [("Push Workout", 20), ("RIT Legs", 13), ("Evening Workout", 8), ("Afternoon Workout", 12)],
            workouts.Select(workout => (workout.Key.Name, workout.Sets.Count)));

        var push = workouts[0];
        Assert.Equal(
            ["Bench Press (Barbell)", "Bench Press (Barbell)", "Bench Press (Barbell)"],
            push.Sets.Take(3).Select(set => set.Exercise));
        Assert.Equal([155.0, 185.0, 185.0], push.Sets.Take(3).Select(set => set.Weight));
    }

    [Fact]
    public void A_rest_timer_row_is_counted_and_not_read_as_a_set()
    {
        // Strong writes the rest after a set as a row of its own, with the rest in Seconds and zero
        // weight and reps. Read as a set it would be a 0 x 0 lift after every real one.
        var evening = Read(FourWorkouts)[2];

        Assert.Equal(8, evening.Sets.Count);
        Assert.Equal(8, evening.RestTimers);
        Assert.DoesNotContain(evening.Sets, set => set.Reps == 0);
    }

    [Fact]
    public void Warm_up_drop_and_failure_rows_keep_the_kind_Strong_marked_them_with()
    {
        var workouts = Read(FourWorkouts);

        Assert.Equal(StrongSetKind.WarmUp, workouts[0].Sets[0].Kind);
        Assert.Equal(
            [StrongSetKind.Working, StrongSetKind.Working, StrongSetKind.Failure, StrongSetKind.Failure, StrongSetKind.Failure],
            workouts[1].Sets.Take(5).Select(set => set.Kind));
    }

    [Fact]
    public void An_exercise_name_loses_the_trailing_space_Strong_kept_on_it()
    {
        var evening = Read(FourWorkouts)[2];

        Assert.Contains(evening.Sets, set => set.Exercise == "shoulder press dumbell");
    }

    [Fact]
    public void Workout_notes_keep_the_line_breaks_Strong_escaped()
    {
        var evening = Read(FourWorkouts)[2];

        Assert.Equal(
            "horizontal pulling movement \nvertical pressing movement \nhamstring curl?\noverhead tricep",
            evening.Notes);
    }

    [Fact]
    public void Blank_lines_are_not_read_as_rows_and_do_not_shift_the_line_numbers_after_them()
    {
        using var export = new ExportFile(
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "1", 225, 5),
            "",
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "X", 225, 5),
            "",
            "");

        Assert.Equal("line 4", Refused(export.Path).Single().Field);
    }

    // ---- Time ----

    [Fact]
    public void A_start_is_placed_at_the_offset_the_zone_kept_on_that_date()
    {
        // The same wall-clock hour in August and in December is four hours behind UTC in one and
        // five in the other. Today's offset applied to both would move one of them by an hour.
        using var export = new ExportFile(
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "1", 225, 5, workout: "Summer"),
            Set("2025-12-12 18:30:00", "Squat (Barbell)", "1", 225, 5, workout: "Winter"));

        var workouts = Read(export.Path);

        Assert.Equal(TimeSpan.FromHours(-4), workouts[0].Period.Start.Offset);
        Assert.Equal(TimeSpan.FromHours(-5), workouts[1].Period.Start.Offset);
        Assert.Equal(new DateTime(2025, 12, 12, 23, 30, 0), workouts[1].Period.Start.UtcDateTime);
    }

    [Theory]
    [InlineData("1h 5m", 65)]
    [InlineData("47m", 47)]
    [InlineData("1h", 60)]
    [InlineData("49h 43m", 2983)]
    public void The_duration_sets_where_the_workout_ends(string duration, int minutes)
    {
        using var export = new ExportFile(Set("2025-08-12 18:30:00", "Squat (Barbell)", "1", 225, 5, duration: duration));

        Assert.Equal(TimeSpan.FromMinutes(minutes), Read(export.Path)[0].Period.Duration);
    }

    [Fact]
    public void A_duration_in_seconds_is_read_as_seconds()
    {
        using var export = new ExportFile(Set("2024-08-15 10:26:24", "Bicep Curl (Dumbbell)", "1", 30, 6, duration: "27s"));

        Assert.Equal(TimeSpan.FromSeconds(27), Read(export.Path)[0].Period.Duration);
    }

    [Fact]
    public void A_start_in_the_hour_the_clocks_skipped_is_refused_as_never_having_happened()
    {
        using var export = new ExportFile(Set("2025-03-09 02:30:00", "Squat (Barbell)", "1", 225, 5));

        Assert.Contains("never happened", Refused(export.Path).Single().Message);
    }

    [Fact]
    public void A_start_in_the_hour_the_clocks_repeated_is_refused_as_ambiguous()
    {
        using var export = new ExportFile(Set("2025-11-02 01:30:00", "Squat (Barbell)", "1", 225, 5));

        Assert.Contains("happened twice", Refused(export.Path).Single().Message);
    }

    // ---- Values ----

    [Fact]
    public void An_RPE_is_carried_and_an_empty_one_is_unrated()
    {
        using var export = new ExportFile(
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "1", 225, 5, rpe: "8.5"),
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "2", 225, 5));

        var sets = Read(export.Path)[0].Sets;

        Assert.Equal(Rpe.Of(8.5), sets[0].Rpe);
        Assert.False(sets[1].Rpe.IsRated);
    }

    // ---- Refusals: all or nothing, every bad line named ----

    [Fact]
    public void Every_unreadable_line_is_named_and_nothing_is_returned()
    {
        // One refusal naming all of them, so a damaged export takes one round of fixes, not one
        // per line.
        using var export = new ExportFile(
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "1", 225, 5),
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "X", 225, 5),
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "2", 225, 5.5),
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "3", 225, 5, rpe: "11"),
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "4", -40, 5));

        Assert.Equal(["line 3", "line 4", "line 5", "line 6"], Refused(export.Path).Select(field => field.Field));
    }

    [Fact]
    public void A_set_carrying_a_time_or_a_distance_is_refused_rather_than_stored_as_a_lift()
    {
        using var export = new ExportFile(
            Set("2025-08-12 18:30:00", "Plank", "1", 0, 0, seconds: 60),
            Set("2025-08-12 18:30:00", "Rowing (Machine)", "1", 0, 0, distance: 2000));

        Assert.All(Refused(export.Path), field => Assert.Contains("only lifts are imported", field.Message));
    }

    [Fact]
    public void Two_rows_of_one_workout_disagreeing_on_its_duration_are_refused()
    {
        using var export = new ExportFile(
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "1", 225, 5, duration: "1h"),
            Set("2025-08-12 18:30:00", "Squat (Barbell)", "2", 225, 5, duration: "2h"));

        Assert.Equal("line 3", Refused(export.Path).Single().Field);
    }

    [Fact]
    public void A_file_without_Strong_columns_is_refused_as_not_an_export()
    {
        using var export = ExportFile.Headed("Date,Exercise,Weight", "2025-08-12,Squat,225");

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StrongExportReader.Read(export.Path, NewYork)));
        Assert.Contains("not a Strong export", problem.Description);
    }

    [Fact]
    public void A_path_with_no_file_is_refused()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid()}.csv");

        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StrongExportReader.Read(missing, NewYork)));
    }

    // ---- Helpers ----

    private static IReadOnlyList<StrongWorkout> Read(string path) =>
        ResultAssert.Succeeded(StrongExportReader.Read(path, NewYork));

    private static IReadOnlyList<InvalidField> Refused(string path) =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StrongExportReader.Read(path, NewYork))).Fields;
}
