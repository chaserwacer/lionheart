using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Health.Recordings;
using lionheart.Tests.Support;

namespace lionheart.Tests.Health;

/// <summary>The context card's line about the runs imported from Apple Health.</summary>
public class WorkoutFactsTests
{
    [Fact]
    public void A_window_with_no_imported_run_says_so()
    {
        Assert.Equal("Runs from Apple Health: none in the last 28 days.", WorkoutFacts.Recent([], days: 28));
    }

    [Fact]
    public void The_line_counts_the_runs_sums_their_distance_and_reads_the_latest()
    {
        var start = new DateTimeOffset(2026, 10, 3, 7, 0, 0, TimeSpan.FromHours(-4));
        IReadOnlyList<CompletedTrainingSession> runs =
        [
            Run(start.AddDays(4), kilometres: 10, minutes: 50, climb: 140),
            Run(start, kilometres: 5, minutes: 30, climb: 20),
        ];

        Assert.Equal(
            "Runs from Apple Health: 2 in the last 28 days, 15.00 km in all; latest 2026-10-07, 10.00 km in 50:00, pace 5:00/km, climb 140 m.",
            WorkoutFacts.Recent(runs, days: 28));
    }

    private static CompletedTrainingSession Run(DateTimeOffset start, double kilometres, double minutes, double climb) =>
        Some.CompletedSession(Guid.NewGuid()) with
        {
            Sets =
            [
                new TrailRunSet(kilometres, DistanceUnit.Kilometers, TimeSpan.FromMinutes(minutes), climb, Rpe.Unrated)
                {
                    Source = new Formed(),
                    Facets = [],
                },
            ],
            Period = new Interval(start, start.AddMinutes(minutes)),
            Source = AHealthRun.Source(),
        };
}
