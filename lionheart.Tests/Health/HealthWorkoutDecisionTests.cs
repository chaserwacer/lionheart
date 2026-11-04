using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Health.Import;
using lionheart.Tests.Support;

namespace lionheart.Tests.Health;

/// <summary>
/// Which Apple Health workouts become training, and as what: the sources left out, the kinds taken
/// and left out, and a workout already in the log under another name.
/// </summary>
public class HealthWorkoutDecisionTests
{
    private static readonly Guid Athlete = Guid.NewGuid();
    private static readonly NewSessionIdentity Identity = new(Guid.NewGuid(), Athlete, Some.Clock);

    // ---- What a run becomes ----

    [Fact]
    public void An_outdoor_run_becomes_a_completed_trail_run_in_kilometres_with_its_recording_beside_it()
    {
        var workout = AHealthRun.Outdoor(meters: 5000) with { ElevationAscendedMeters = 64 };

        var import = Assert.IsType<HealthWorkoutDecision.Import>(Decide(workout));

        var run = Assert.IsType<TrailRunSet>(Assert.Single(import.Session.Sets));
        Assert.Equal((5.0, DistanceUnit.Kilometers, workout.Duration, 64.0), (run.Distance, run.Unit, run.Duration, run.ElevationGainMeters));
        Assert.Equal(new Interval(workout.Start, workout.End), import.Session.Period);
        Assert.Equal(new SessionSource.AppleHealth(workout.WorkoutId, new RecordingApp("Strava", AHealthRun.StravaBundle)), import.Session.Source);
        Assert.Equal((Identity.TrainingSessionId, Athlete, Provenance.User), (import.Session.TrainingSessionId, import.Session.UserId, import.Session.Provenance));
        Assert.Equal(Identity.TrainingSessionId, import.Recording.TrainingSessionId);
        Assert.Equal(workout.Route.Count, import.Recording.Route.Points.Count);
        Assert.True(import.Recording.HeartRate.IsRecorded);
    }

    [Fact]
    public void A_run_with_no_stated_climb_takes_the_climb_its_route_shows()
    {
        var start = AHealthRun.Start;
        var workout = AHealthRun.Outdoor(meters: 1800) with
        {
            Route = AHealthRun.North(start, 1800, 3, altitude: index => index <= 60 ? 400 + index : 460),
        };

        var import = Assert.IsType<HealthWorkoutDecision.Import>(Decide(workout));

        Assert.Equal(60, Assert.IsType<TrailRunSet>(Assert.Single(import.Session.Sets)).ElevationGainMeters, precision: 6);
    }

    [Fact]
    public void A_run_the_app_stated_no_distance_for_takes_its_routes_length()
    {
        var workout = AHealthRun.Outdoor(meters: 3000) with { DistanceMeters = null };

        var import = Assert.IsType<HealthWorkoutDecision.Import>(Decide(workout));

        Assert.Equal(3.0, Assert.IsType<TrailRunSet>(Assert.Single(import.Session.Sets)).Distance, precision: 2);
    }

    [Theory]
    [InlineData("cycling", false, EnduranceActivity.Ride)]
    [InlineData("cycling", true, EnduranceActivity.IndoorRide)]
    [InlineData("hiking", false, EnduranceActivity.Hike)]
    [InlineData("walking", false, EnduranceActivity.Walk)]
    [InlineData("walking", true, EnduranceActivity.Walk)]
    [InlineData("running", true, EnduranceActivity.IndoorRun)]
    public void Every_other_activity_taken_becomes_an_endurance_set_of_its_kind(string activity, bool indoor, EnduranceActivity expected)
    {
        var workout = AHealthRun.Outdoor(meters: 8000) with { Activity = activity, Indoor = indoor, ElevationAscendedMeters = 120 };

        var import = Assert.IsType<HealthWorkoutDecision.Import>(Decide(workout));

        var set = Assert.IsType<EnduranceSet>(Assert.Single(import.Session.Sets));
        Assert.Equal((expected, 8.0, DistanceUnit.Kilometers, workout.Duration, 120.0), (set.Activity, set.Distance, set.Unit, set.Duration, set.ElevationGainMeters));
    }

    [Fact]
    public void An_indoor_ride_with_no_route_takes_the_distance_the_app_stated()
    {
        var workout = AHealthRun.Outdoor(meters: 20_000) with { Activity = "cycling", Indoor = true, Route = [] };

        var import = Assert.IsType<HealthWorkoutDecision.Import>(Decide(workout));

        Assert.Equal(20.0, Assert.IsType<EnduranceSet>(Assert.Single(import.Session.Sets)).Distance, precision: 6);
        Assert.False(import.Recording.Route.HasPath);
    }

    [Fact]
    public void What_else_the_app_measured_goes_onto_the_recording()
    {
        var workout = AHealthRun.Outdoor() with
        {
            Activity = "cycling",
            Metrics =
            [
                new MetricSeriesCommand("power", [new(AHealthRun.Start, 212.4), new(AHealthRun.Start.AddSeconds(1), 230.6)]),
                new MetricSeriesCommand("cadence", [new(AHealthRun.Start, 88)]),
            ],
        };

        var import = Assert.IsType<HealthWorkoutDecision.Import>(Decide(workout));

        Assert.Equal([212.0, 231.0], import.Recording.Metrics.Of(WorkoutMetric.Power).Select(sample => sample.Value));
        Assert.Equal([88.0], import.Recording.Metrics.Of(WorkoutMetric.Cadence).Select(sample => sample.Value));
    }

    // ---- What is left out ----

    [Theory]
    [InlineData("com.ouraring.oura")]
    [InlineData("io.strongapp.strong")]
    public void A_workout_from_an_app_Lionheart_reads_directly_is_left_out(string bundle)
    {
        var workout = AHealthRun.Outdoor() with { AppBundleId = bundle };

        Assert.Equal(Excluded(HealthWorkoutExclusion.ReadDirectly), Decide(workout));
    }

    [Fact]
    public void An_activity_Lionheart_does_not_take_is_left_out()
    {
        Assert.Equal(Excluded(HealthWorkoutExclusion.OtherActivity), Decide(AHealthRun.Outdoor() with { Activity = "activity-16" }));
    }

    [Fact]
    public void A_run_with_neither_a_distance_nor_a_route_is_left_out()
    {
        var workout = AHealthRun.Outdoor() with { DistanceMeters = null, Route = [] };

        Assert.Equal(Excluded(HealthWorkoutExclusion.NoDistance), Decide(workout));
    }

    // ---- Counted once ----

    [Fact]
    public void A_run_lying_inside_a_logged_run_is_that_run()
    {
        var workout = AHealthRun.Outdoor(meters: 5400);
        var logged = Logged(workout.Start.AddMinutes(-5), minutes: 40, Some.TrailRunSet);

        Assert.Equal(Excluded(HealthWorkoutExclusion.SameAsLoggedRun, logged.TrainingSessionId), Decide(workout, logged));
    }

    [Fact]
    public void A_run_lying_inside_a_logged_lifting_session_is_that_session_as_another_device_saw_it()
    {
        var workout = AHealthRun.Outdoor(meters: 1800);
        var lifting = Logged(workout.Start.AddMinutes(-30), minutes: 90, Some.LiftSet);

        Assert.Equal(Excluded(HealthWorkoutExclusion.InsideLoggedLifting, lifting.TrainingSessionId), Decide(workout, lifting));
    }

    [Fact]
    public void The_same_run_written_by_two_apps_is_taken_once()
    {
        var first = AHealthRun.Outdoor(meters: 5400);
        var taken = Assert.IsType<HealthWorkoutDecision.Import>(Decide(first)).Session;
        var second = AHealthRun.Outdoor(meters: 5400, start: first.Start.AddSeconds(20)) with { AppName = "Runna", AppBundleId = "com.runna.app" };

        Assert.Equal(Excluded(HealthWorkoutExclusion.SameAsLoggedRun, taken.TrainingSessionId), Decide(second, taken));
    }

    [Fact]
    public void The_same_ride_written_by_Strava_and_a_watch_is_taken_once()
    {
        var strava = AHealthRun.Outdoor(meters: 30_000, metersPerSecond: 8) with { Activity = "cycling" };
        var taken = Assert.IsType<HealthWorkoutDecision.Import>(Decide(strava)).Session;
        var watch = AHealthRun.Outdoor(meters: 30_000, metersPerSecond: 8, start: strava.Start.AddSeconds(-40)) with
        {
            Activity = "cycling",
            AppName = "Apple Watch",
            AppBundleId = "com.apple.health.00000000-0000-0000-0000-00000000000A",
        };

        Assert.Equal(Excluded(HealthWorkoutExclusion.SameAsLoggedRun, taken.TrainingSessionId), Decide(watch, taken));
    }

    [Fact]
    public void A_run_merely_near_a_logged_session_is_a_session_of_its_own()
    {
        var workout = AHealthRun.Outdoor(meters: 5400);
        var before = Logged(workout.Start.AddMinutes(-80), minutes: 75, Some.LiftSet);

        Assert.IsType<HealthWorkoutDecision.Import>(Decide(workout, before));
    }

    // ---- The wire ----

    [Fact]
    public void A_workout_ending_before_it_starts_is_refused_at_the_wire()
    {
        var workout = AHealthRun.Outdoor() with { End = AHealthRun.Start.AddMinutes(-1) };

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(workout.Stated()));
        Assert.Equal(nameof(HealthWorkoutImport.End), Assert.Single(problem.Fields).Field);
    }

    [Fact]
    public void A_fix_that_is_not_a_place_on_Earth_is_refused_at_the_wire()
    {
        var workout = AHealthRun.Outdoor() with { Route = [new RoutePointCommand(AHealthRun.Start, 91, 8, null)] };

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(workout.Stated()));
        Assert.Equal(nameof(HealthWorkoutImport.Route), Assert.Single(problem.Fields).Field);
    }

    [Fact]
    public void A_reading_that_is_not_a_heart_rate_is_left_out_and_the_run_kept()
    {
        var workout = AHealthRun.Outdoor() with
        {
            HeartRate =
            [
                new HeartRateCommand(AHealthRun.Start, 0),
                new HeartRateCommand(AHealthRun.Start.AddSeconds(5), 151),
                new HeartRateCommand(AHealthRun.Start.AddSeconds(10), 1510),
            ],
        };

        var heartRate = ResultAssert.Succeeded(workout.Stated()).HeartRate;

        Assert.Equal([151], heartRate.Samples.Select(sample => sample.Bpm));
    }

    [Fact]
    public void A_metric_this_server_does_not_know_and_a_reading_that_cannot_be_one_are_left_out_and_the_workout_kept()
    {
        var workout = AHealthRun.Outdoor() with
        {
            Metrics =
            [
                new MetricSeriesCommand("groundContactTime", [new(AHealthRun.Start, 240), new(AHealthRun.Start.AddSeconds(5), 24_000)]),
                new MetricSeriesCommand("breathingRate", [new(AHealthRun.Start, 30)]),
                new MetricSeriesCommand("3", [new(AHealthRun.Start, 30)]),
            ],
        };

        var metrics = ResultAssert.Succeeded(workout.Stated()).Metrics;

        var series = Assert.Single(metrics.Series);
        Assert.Equal((WorkoutMetric.GroundContactTime, 240.0), (series.Metric, Assert.Single(series.Samples).Value));
    }

    [Fact]
    public void A_fix_is_held_to_a_millionth_of_a_degree()
    {
        var workout = AHealthRun.Outdoor() with { Route = [new RoutePointCommand(AHealthRun.Start, 46.123456789, 8.987654321, 412.345)] };

        var point = Assert.Single(ResultAssert.Succeeded(workout.Stated()).Route.Points);
        Assert.Equal((46.123457, 8.987654, 412.3), (point.Latitude, point.Longitude, point.Altitude!.Value));
    }

    // ---- Helpers ----

    private static HealthWorkoutDecision Decide(HealthWorkoutImport workout, params CompletedTrainingSession[] logged) =>
        HealthWorkoutDecisions.Decide(ResultAssert.Succeeded(workout.Stated()), logged, Identity);

    private static HealthWorkoutDecision.Excluded Excluded(HealthWorkoutExclusion reason, Guid? matched = null) => new(reason, matched);

    private static CompletedTrainingSession Logged(DateTimeOffset start, double minutes, SetEntry set) =>
        Some.CompletedSession(Athlete) with
        {
            Sets = [set],
            Period = new Interval(start, start.AddMinutes(minutes)),
        };
}
