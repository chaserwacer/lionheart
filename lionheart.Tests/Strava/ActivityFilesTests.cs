using System.Text;
using lionheart.Functional;
using lionheart.Services.Strava.Files;
using lionheart.Tests.Support;
using static lionheart.Tests.Strava.FitFile;
using static lionheart.Tests.Strava.StravaArchives;

namespace lionheart.Tests.Strava;

/// <summary>An activity's file read as its track: GPX as Strava writes it, gzipped FIT, and the files it cannot read.</summary>
public class ActivityFilesTests
{
    private static readonly DateTimeOffset Start = new(2025, 6, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_GPX_track_becomes_the_route_with_its_times_and_elevations()
    {
        using var archive = new ArchiveFolder().With("activities/1.gpx", Gpx(Start, meters: 3000));

        var track = ResultAssert.Succeeded(ActivityFiles.Read(archive.Path, "activities/1.gpx"));

        var expected = AHealthRun.North(Start, 3000, 3);
        Assert.Equal(expected.Count, track.Route.Points.Count);
        Assert.Equal((Start, AHealthRun.Latitude, 400.0), (track.Route.Points[0].At, track.Route.Points[0].Latitude, track.Route.Points[0].Altitude!.Value));
        Assert.Equal(expected[^1].At, track.Route.Points[^1].At);
        Assert.False(track.HeartRate.IsRecorded);
    }

    [Fact]
    public void A_GPX_heart_rate_in_Garmins_extension_becomes_the_recorded_heart_rate()
    {
        using var archive = new ArchiveFolder().With("activities/1.gpx", Gpx(Start, meters: 600, bpm: 151));

        var track = ResultAssert.Succeeded(ActivityFiles.Read(archive.Path, "activities/1.gpx"));

        Assert.Equal(track.Route.Points.Count, track.HeartRate.Samples.Count);
        Assert.All(track.HeartRate.Samples, sample => Assert.Equal(151, sample.Bpm));
    }

    [Fact]
    public void A_track_point_with_no_time_refuses_the_file()
    {
        const string gpx = """
            <gpx creator="StravaGPX" version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
             <trk><trkseg><trkpt lat="46.5" lon="8.25"><ele>400.0</ele></trkpt></trkseg></trk>
            </gpx>
            """;
        using var archive = new ArchiveFolder().With("activities/1.gpx", gpx);

        Assert.Equal("A track point has no time.", Refusal(ActivityFiles.Read(archive.Path, "activities/1.gpx")));
    }

    [Fact]
    public void A_gzipped_FIT_file_is_read_through_its_compression()
    {
        var fit = new FitFile()
            .Define(0, 20, [(253, 4, 0x86), (0, 4, 0x85), (1, 4, 0x85), (3, 1, 0x02)])
            .Data(0, [.. U32(Timestamp(Start)), .. S32(Semicircles(AHealthRun.Latitude)), .. S32(Semicircles(AHealthRun.Longitude)), 97])
            .Data(0, [.. U32(Timestamp(Start.AddSeconds(1))), .. S32(Semicircles(AHealthRun.Latitude + 0.0001)), .. S32(Semicircles(AHealthRun.Longitude)), 99])
            .Bytes();
        using var archive = new ArchiveFolder().With("activities/2.fit.gz", fit);

        var track = ResultAssert.Succeeded(ActivityFiles.Read(archive.Path, "activities/2.fit.gz"));

        Assert.Equal(2, track.Route.Points.Count);
        Assert.Equal([97, 99], track.HeartRate.Samples.Select(sample => sample.Bpm));
    }

    [Fact]
    public void A_reading_that_cannot_be_a_heart_rate_is_left_out_of_the_track()
    {
        var fit = new FitFile()
            .Define(0, 20, [(253, 4, 0x86), (3, 1, 0x02)])
            .Data(0, [.. U32(Timestamp(Start)), 0])
            .Data(0, [.. U32(Timestamp(Start.AddSeconds(1))), 120])
            .Bytes();
        using var archive = new ArchiveFolder().With("activities/3.fit", fit);

        var track = ResultAssert.Succeeded(ActivityFiles.Read(archive.Path, "activities/3.fit"));

        Assert.Equal([120], track.HeartRate.Samples.Select(sample => sample.Bpm));
    }

    [Fact]
    public void An_activity_with_no_file_has_no_track_and_that_is_no_fault() =>
        Assert.Same(ActivityTrack.None, ResultAssert.Succeeded(ActivityFiles.Read(Path.GetTempPath(), string.Empty)));

    [Theory]
    [InlineData("activities/9.gpx", "The archive has no such file.")]
    [InlineData("../outside.gpx", "The file's path leads outside the archive.")]
    [InlineData("activities/4.tcx", "Files of type '.tcx' are not read.")]
    public void A_file_the_archive_cannot_give_is_refused_with_why(string file, string reason)
    {
        using var archive = new ArchiveFolder().With("activities/4.tcx", Encoding.UTF8.GetBytes("<TrainingCenterDatabase/>"));

        Assert.Equal(reason, Refusal(ActivityFiles.Read(archive.Path, file)));
    }

    private static string Refusal(Result<ActivityTrack, string> read) => Assert.IsType<Failure<ActivityTrack, string>>(read).Problem;
}
