using lionheart.Model.Analysis;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;
using lionheart.Services.Strava;
using lionheart.Services.Strava.Files;
using lionheart.Tests.Support;

namespace lionheart.Tests.Strava;

/// <summary>
/// The rule for what an archived activity becomes: which types are taken and as what, how the
/// session is measured and placed, and when an activity is already in the log.
/// </summary>
public class StravaActivityDecisionTests
{
    private static readonly Guid Athlete = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2023, 6, 10, 22, 30, 0, TimeSpan.Zero);

    private static StravaActivity AnActivity(
        string type = "Run",
        string name = "Morning Run",
        double meters = 8000,
        TimeSpan? elapsed = null,
        TimeSpan? moving = null,
        double? climb = 55.2,
        Rpe? exertion = null,
        long id = 4242,
        DateTimeOffset? start = null) =>
        new(id, start ?? Start, name, type, elapsed ?? TimeSpan.FromMinutes(45), moving ?? TimeSpan.FromMinutes(42), meters, climb,
            string.Empty, string.Empty, exertion ?? Rpe.Unrated, null, null, string.Empty);

    private static ArchiveLog Log(
        IReadOnlyList<CompletedTrainingSession>? logged = null, IReadOnlySet<Guid>? held = null, IReadOnlySet<long>? deleted = null) =>
        new(logged ?? [], held ?? new HashSet<Guid>(), deleted ?? new HashSet<long>(),
            new ArchivePlacement(Athlete, StravaArchives.NewYork, Some.Clock));

    private static StravaActivityDecision Decide(StravaActivity activity, ArchiveLog? log = null, ActivityTrack? track = null) =>
        StravaActivityDecisions.Decide(activity, track ?? ActivityTrack.None, log ?? Log());

    // ---- What each type becomes ----

    [Fact]
    public void A_run_becomes_a_trail_run_of_Stravas_distance_moving_time_and_climb()
    {
        var added = Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity(exertion: Rpe.Of(7))));

        var run = Assert.IsType<TrailRunSet>(Assert.Single(added.Session.Sets));
        Assert.Equal((8.0, DistanceUnit.Kilometers, TimeSpan.FromMinutes(42), 55.2, Rpe.Of(7)),
            (run.Distance, run.Unit, run.Duration, run.ElevationGainMeters, run.Rpe));
        Assert.Equal(new SessionSource.StravaArchive(4242), added.Session.Source);
    }

    [Theory]
    [InlineData("Ride", EnduranceActivity.Ride)]
    [InlineData("Mountain Bike Ride", EnduranceActivity.Ride)]
    [InlineData("Virtual Ride", EnduranceActivity.IndoorRide)]
    [InlineData("E-Bike Ride", EnduranceActivity.EBikeRide)]
    [InlineData("Hike", EnduranceActivity.Hike)]
    [InlineData("Walk", EnduranceActivity.Walk)]
    [InlineData("Virtual Run", EnduranceActivity.IndoorRun)]
    [InlineData("Kayaking", EnduranceActivity.Paddle)]
    public void A_distance_activity_becomes_an_endurance_set_of_its_kind(string type, EnduranceActivity activity)
    {
        var added = Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity(type)));

        Assert.Equal(activity, Assert.IsType<EnduranceSet>(Assert.Single(added.Session.Sets)).Activity);
    }

    [Fact]
    public void A_weight_training_session_becomes_a_freeform_set_saying_what_it_was_with_its_heart_rate_in_the_notes()
    {
        var activity = AnActivity("Weight Training", "Evening Weight Training", meters: 0, elapsed: TimeSpan.FromMinutes(74)) with
        {
            AverageHeartRate = 112,
            MaxHeartRate = 158,
        };

        var added = Assert.IsType<StravaActivityDecision.AddedLifting>(Decide(activity));

        var set = Assert.IsType<FreeformSet>(Assert.Single(added.Session.Sets));
        Assert.Equal("Weight training, 1:14:00, recorded in Strava", Assert.IsType<Texted>(set.Source).RawText);
        Assert.Equal("Heart rate (Strava): average 112 bpm, peak 158 bpm.", added.Session.Notes);
    }

    [Fact]
    public void A_type_Lionheart_does_not_take_is_left_out()
    {
        Assert.Equal(new StravaActivityDecision.LeftOut(StravaExclusion.NotTaken), Decide(AnActivity("Swim")));
    }

    [Fact]
    public void A_distance_activity_with_no_distance_and_no_route_is_left_out()
    {
        Assert.Equal(new StravaActivityDecision.LeftOut(StravaExclusion.NoDistance), Decide(AnActivity(meters: 0)));
    }

    [Fact]
    public void A_distance_activity_with_no_stated_distance_is_measured_by_its_route()
    {
        var route = AHealthRun.Route(AHealthRun.North(Start, 2000, 3));
        var track = new ActivityTrack(route, RecordedHeartRate.None, RecordedMetrics.None);

        var added = Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity(meters: 0), track: track));

        Assert.Equal(2.0, Assert.IsType<TrailRunSet>(Assert.Single(added.Session.Sets)).Distance, precision: 2);
        Assert.Equal(route, added.Recording.Route);
    }

    // ---- How the session is placed and noted ----

    [Fact]
    public void The_session_spans_the_elapsed_time_on_its_day_in_the_athletes_zone()
    {
        var added = Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity(elapsed: TimeSpan.FromMinutes(50))));

        var period = Assert.IsType<Interval>(added.Session.Period);
        Assert.Equal(new DateTimeOffset(2023, 6, 10, 18, 30, 0, TimeSpan.FromHours(-4)), period.Start);
        Assert.Equal(TimeSpan.FromMinutes(50), period.Duration);
        Assert.Equal(new DateOnly(2023, 6, 10), added.Session.Date);
    }

    [Fact]
    public void A_name_the_athlete_gave_and_what_he_wrote_go_into_the_notes_and_Stravas_default_name_does_not()
    {
        var named = AnActivity(name: "Bridge loop") with { Description = "felt easy", PrivateNote = "left knee fine" };

        var added = Assert.IsType<StravaActivityDecision.Added>(Decide(named));
        var unnamed = Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity(name: "Afternoon Run")));

        Assert.Equal("Bridge loop\nfelt easy\nleft knee fine", added.Session.Notes);
        Assert.Equal(string.Empty, unnamed.Session.Notes);
    }

    [Fact]
    public void An_activity_gets_the_same_session_id_on_every_run()
    {
        var first = Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity()));
        var second = Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity()));

        Assert.Equal(first.Session.TrainingSessionId, second.Session.TrainingSessionId);
        Assert.NotEqual(first.Session.TrainingSessionId, Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity(id: 4243))).Session.TrainingSessionId);
    }

    // ---- Already in the log ----

    [Fact]
    public void An_activity_this_import_already_made_a_session_is_left_as_it_is()
    {
        var id = StravaSessions.SessionIdFor(4242, Athlete);

        Assert.Equal(new StravaActivityDecision.AlreadyImported(id), Decide(AnActivity(), Log(held: new HashSet<Guid> { id })));
    }

    [Fact]
    public void An_activity_whose_session_the_athlete_deleted_stays_deleted()
    {
        Assert.Equal(new StravaActivityDecision.LeftOut(StravaExclusion.Deleted), Decide(AnActivity(), Log(deleted: new HashSet<long> { 4242 })));
    }

    [Fact]
    public void Stravas_own_copy_of_the_run_in_Health_is_the_session_it_already_is()
    {
        var health = Session(Start, Start.AddMinutes(45), new SessionSource.AppleHealth(Guid.NewGuid(), new RecordingApp("Strava", "com.strava.stravaride")), Some.TrailRunSet);

        var decision = Decide(AnActivity(), Log(logged: [health]));

        Assert.Equal(new StravaActivityDecision.Matched(new SessionMatch.LoggedRun(health.TrainingSessionId)), decision);
    }

    [Fact]
    public void A_weight_training_activity_inside_a_logged_lifting_session_is_that_session()
    {
        var lifting = Session(Start.AddMinutes(-5), Start.AddMinutes(80), new SessionSource.AthleteLog(), Some.LiftSet);

        var decision = Decide(AnActivity("Weight Training", meters: 0, elapsed: TimeSpan.FromMinutes(70)), Log(logged: [lifting]));

        Assert.Equal(new StravaActivityDecision.Matched(new SessionMatch.LoggedLifting(lifting.TrainingSessionId)), decision);
    }

    [Fact]
    public void An_activity_of_no_length_at_the_moment_of_a_held_one_of_no_length_is_that_session()
    {
        var health = Session(Start, Start, new SessionSource.AppleHealth(Guid.NewGuid(), new RecordingApp("Strava", "com.strava.stravaride")), Some.TrailRunSet);

        var decision = Decide(AnActivity(elapsed: TimeSpan.Zero, moving: TimeSpan.Zero), Log(logged: [health]));

        Assert.Equal(new StravaActivityDecision.Matched(new SessionMatch.LoggedRun(health.TrainingSessionId)), decision);
    }

    [Fact]
    public void An_activity_less_than_half_inside_a_held_session_is_a_session_of_its_own()
    {
        var health = Session(Start.AddMinutes(30), Start.AddMinutes(90), new SessionSource.AppleHealth(Guid.NewGuid(), new RecordingApp("Connect", "com.garmin.connect.mobile")), Some.RideSet);

        Assert.IsType<StravaActivityDecision.Added>(Decide(AnActivity("Ride"), Log(logged: [health])));
    }

    private static CompletedTrainingSession Session(DateTimeOffset start, DateTimeOffset end, SessionSource source, SetEntry set) =>
        Some.CompletedSession(Athlete) with { Sets = [set], Period = new Interval(start, end), Source = source };
}
