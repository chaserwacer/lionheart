using lionheart.Model.Health;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Health.Recordings;
using lionheart.Tests.Support;

namespace lionheart.Tests.Health;

/// <summary>
/// What a route says: its length, its time in motion, its climb, and its splits — over invented
/// routes whose answers are known in advance.
/// </summary>
public class RouteAnalysisTests
{
    private const double Kilometre = 1000;
    private const double Mile = 1609.344;

    private static readonly DateTimeOffset Start = AHealthRun.Start;

    // ---- Length ----

    [Fact]
    public void A_hundredth_of_a_degree_of_latitude_measures_its_arc_on_the_sphere()
    {
        var route = Route(AHealthRun.North(Start, meters: 1111.95, metersPerSecond: 3));

        Assert.Equal(1111.95, RouteAnalysis.Figures(route).Meters, precision: 1);
    }

    [Fact]
    public void One_fix_is_no_path_and_says_nothing()
    {
        var route = new WorkoutRoute([new RoutePoint(Start, 46.5, 8.25, 400)]);

        Assert.Equal(new RouteFigures(0, TimeSpan.Zero, null, null), RouteAnalysis.Figures(route));
        Assert.Empty(RouteAnalysis.Splits(route, 0, Kilometre, []));
    }

    // ---- Moving time ----

    [Fact]
    public void A_stop_on_the_route_is_elapsed_time_and_not_moving_time()
    {
        // Two kilometres at 3 m/s with a two-minute stop at the first: the phone's fixes stay put.
        var out_ = AHealthRun.North(Start, 1000, 3);
        var stopped = out_[^1].At.AddMinutes(2);
        var back = AHealthRun.North(stopped, 1000, 3)
            .Select(fix => fix with { Latitude = fix.Latitude + 1000 / AHealthRun.MetresPerDegree });
        var route = Route([.. out_, .. back]);

        var figures = RouteAnalysis.Figures(route);

        Assert.Equal(2000 / 3.0, figures.Moving.TotalSeconds, precision: 0);
        var splits = RouteAnalysis.Splits(route, 2000, Kilometre, []);
        Assert.Equal(
            (route.Points[^1].At - route.Points[0].At).TotalSeconds,
            splits.Sum(split => split.Elapsed.TotalSeconds),
            precision: 3);
    }

    // ---- Splits ----

    [Fact]
    public void A_run_is_cut_into_whole_kilometres_and_what_is_left()
    {
        var route = Route(AHealthRun.North(Start, 2500, 3));

        var splits = RouteAnalysis.Splits(route, 2500, Kilometre, []);

        Assert.Equal([1000, 1000, 500], splits.Select(split => Math.Round(split.DistanceMeters, 3)));
        Assert.Equal([1, 2, 3], splits.Select(split => split.Number));
        // An even 3 m/s is 5:33 a kilometre in every split, the short last one included.
        Assert.All(splits, split => Assert.Equal(1000 / 3.0, split.Pace.TotalSeconds, precision: 3));
    }

    [Fact]
    public void Splits_add_up_to_the_stated_distance_when_the_route_measures_short()
    {
        var route = Route(AHealthRun.North(Start, 2000, 3));

        var splits = RouteAnalysis.Splits(route, distanceMeters: 2100, Kilometre, []);

        Assert.Equal(2100, splits.Sum(split => split.DistanceMeters), precision: 6);
        Assert.Equal(3, splits.Count);
    }

    [Fact]
    public void A_remainder_under_ten_metres_is_not_a_split()
    {
        var route = Route(AHealthRun.North(Start, 2008, 3));

        Assert.Equal(2, RouteAnalysis.Splits(route, 2008, Kilometre, []).Count);
    }

    [Fact]
    public void Mile_splits_are_cut_at_each_mile()
    {
        var route = Route(AHealthRun.North(Start, 5000, 3));

        var splits = RouteAnalysis.Splits(route, 5000, Mile, []);

        Assert.Equal([Mile, Mile, Mile, 5000 - 3 * Mile], splits.Select(split => split.DistanceMeters), new Close(1e-6));
        Assert.Equal(Mile / 3, splits[0].Pace.TotalSeconds, precision: 3);
    }

    [Fact]
    public void A_split_averages_the_heart_rate_inside_its_own_time()
    {
        var route = Route(AHealthRun.North(Start, 2000, 2));
        // 500 s per kilometre: 140 through the first, 170 through the second.
        IReadOnlyList<HeartRateSample> heartRate =
        [
            .. Enumerable.Range(0, 100).Select(index => Beat(Start.AddSeconds(5 * index), 140)),
            .. Enumerable.Range(100, 100).Select(index => Beat(Start.AddSeconds(5 * index), 170)),
        ];

        var splits = RouteAnalysis.Splits(route, 2000, Kilometre, heartRate);

        Assert.Equal([140, 170], splits.Select(split => split.AverageBpm));
    }

    [Fact]
    public void A_split_with_no_heart_rate_inside_it_has_no_average()
    {
        var route = Route(AHealthRun.North(Start, 1000, 3));

        Assert.Null(Assert.Single(RouteAnalysis.Splits(route, 1000, Kilometre, [])).AverageBpm);
    }

    // ---- Climb ----

    [Fact]
    public void Altitude_jitter_on_the_flat_is_not_climb()
    {
        var route = Route(AHealthRun.North(Start, 2000, 3, altitude: index => 400 + (index % 2 == 0 ? 2 : -0.5)));

        var figures = RouteAnalysis.Figures(route);

        Assert.Equal((0.0, 0.0), (figures.ClimbMeters!.Value, figures.DescentMeters!.Value));
    }

    [Fact]
    public void A_steady_climb_and_descent_are_counted_whole()
    {
        // Up a metre a fix for sixty fixes, then down a metre a fix for sixty.
        var route = Route(AHealthRun.North(Start, 1800, 3, secondsPerFix: 5,
            altitude: index => index <= 60 ? 400 + index : 460 - (index - 60)));

        var figures = RouteAnalysis.Figures(route);

        Assert.Equal(60, figures.ClimbMeters!.Value, precision: 6);
        Assert.Equal(60, figures.DescentMeters!.Value, precision: 6);
    }

    [Fact]
    public void The_splits_climb_adds_up_to_the_routes()
    {
        var route = Route(AHealthRun.North(Start, 3300, 3, altitude: index => 400 + 25 * Math.Sin(index / 15.0)));

        var climb = RouteAnalysis.Figures(route).ClimbMeters!.Value;
        var splits = RouteAnalysis.Splits(route, 3300, Kilometre, []);

        Assert.True(climb > 0);
        Assert.Equal(climb, splits.Sum(split => split.ClimbMeters!.Value), precision: 6);
    }

    [Fact]
    public void A_route_with_no_altitude_has_no_climb_rather_than_a_climb_of_zero()
    {
        var route = Route(AHealthRun.North(Start, 2000, 3, altitude: _ => null));

        Assert.Null(RouteAnalysis.Figures(route).ClimbMeters);
        Assert.All(RouteAnalysis.Splits(route, 2000, Kilometre, []), split => Assert.Null(split.ClimbMeters));
    }

    // ---- Helpers ----

    private static WorkoutRoute Route(IReadOnlyList<Model.Health.RoutePointCommand> fixes) => AHealthRun.Route(fixes);

    private static HeartRateSample Beat(DateTimeOffset at, int bpm) => new(at, bpm, RecordedHeartRate.SampleSource);

    private sealed class Close(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;
        public int GetHashCode(double obj) => 0;
    }
}
