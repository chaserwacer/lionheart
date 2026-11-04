using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Health.Recordings;
using lionheart.Tests.Support;

namespace lionheart.Tests.Health;

/// <summary>The context card's line about the workouts imported from Apple Health.</summary>
public class WorkoutFactsTests
{
    [Fact]
    public void A_window_with_no_imported_workout_says_so()
    {
        Assert.Equal("Workouts from Apple Health: none in the last 28 days.", WorkoutFacts.Recent([], days: 28));
    }

    [Fact]
    public void The_line_counts_each_activity_sums_its_distance_and_reads_its_latest_in_miles_and_feet()
    {
        var start = new DateTimeOffset(2026, 10, 3, 7, 0, 0, TimeSpan.FromHours(-4));
        IReadOnlyList<CompletedTrainingSession> runs =
        [
            Run(start.AddDays(4), kilometres: 10, minutes: 50, climb: 140),
            Run(start, kilometres: 5, minutes: 30, climb: 20),
            Ride(start.AddDays(2), kilometres: 45, minutes: 90, climb: 300),
        ];

        Assert.Equal(
            "Workouts from Apple Health in the last 28 days: 2 runs 9.32 mi, 1 ride 27.96 mi; "
            + "latest run 2026-10-07, 6.21 mi in 50:00, pace 8:03/mi, climb 459 ft; "
            + "latest ride 2026-10-05, 27.96 mi in 1:30:00, speed 18.6 mph, climb 984 ft.",
            WorkoutFacts.Recent(runs, days: 28));
    }

    private static CompletedTrainingSession Run(DateTimeOffset start, double kilometres, double minutes, double climb) =>
        Imported(start, minutes, new TrailRunSet(kilometres, DistanceUnit.Kilometers, TimeSpan.FromMinutes(minutes), climb, Rpe.Unrated)
        {
            Source = new Formed(),
            Facets = [],
        });

    private static CompletedTrainingSession Ride(DateTimeOffset start, double kilometres, double minutes, double climb) =>
        Imported(start, minutes, new EnduranceSet(EnduranceActivity.Ride, kilometres, DistanceUnit.Kilometers, TimeSpan.FromMinutes(minutes), climb, Rpe.Unrated)
        {
            Source = new Formed(),
            Facets = [],
        });

    private static CompletedTrainingSession Imported(DateTimeOffset start, double minutes, SetEntry set) =>
        Some.CompletedSession(Guid.NewGuid()) with
        {
            Sets = [set],
            Period = new Interval(start, start.AddMinutes(minutes)),
            Source = AHealthRun.Source(),
        };
}
