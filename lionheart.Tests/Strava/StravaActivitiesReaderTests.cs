using lionheart.Functional;
using lionheart.Model.Training.Sets;
using lionheart.Services.Strava;
using lionheart.Tests.Support;
using static lionheart.Tests.Strava.StravaArchives;

namespace lionheart.Tests.Strava;

/// <summary>Reading the archive's activities.csv: Strava's columns, its repeats, its blanks, and what refuses it.</summary>
public class StravaActivitiesReaderTests
{
    private static readonly DateTimeOffset Start = new(2024, 7, 9, 22, 15, 30, TimeSpan.Zero);

    [Fact]
    public void A_row_reads_as_the_activity_it_sums_up_with_its_start_in_UTC()
    {
        using var archive = new ArchiveFolder(Row(
            1001, Start, type: "Ride", name: "Hill loop", elapsedSeconds: 5400, movingSeconds: 5100, meters: 30125.4,
            climb: "412.5", description: "windy, \"steady\" effort", privateNote: "new saddle", exertion: "6.0",
            averageHeartRate: "141.0", maxHeartRate: "176.0", file: "activities/1001.gpx"));

        var activity = Assert.Single(ResultAssert.Succeeded(StravaActivitiesReader.Read(archive.Path)));

        Assert.Equal(
            new StravaActivity(
                1001, Start, "Hill loop", "Ride", TimeSpan.FromSeconds(5400), TimeSpan.FromSeconds(5100), 30125.4, 412.5,
                "windy, \"steady\" effort", "new saddle", Rpe.Of(6), 141, 176, "activities/1001.gpx"),
            activity);
    }

    [Fact]
    public void The_distance_is_Stravas_second_column_in_metres_not_its_first_in_kilometres()
    {
        using var archive = new ArchiveFolder(Row(1001, Start, meters: 4864.7));

        Assert.Equal(4864.7, Assert.Single(ResultAssert.Succeeded(StravaActivitiesReader.Read(archive.Path))).DistanceMeters);
    }

    [Fact]
    public void Blank_and_zero_measures_read_as_none()
    {
        using var archive = new ArchiveFolder(Row(1001, Start, type: "Weight Training", movingSeconds: 0, meters: 0));

        var activity = Assert.Single(ResultAssert.Succeeded(StravaActivitiesReader.Read(archive.Path)));

        Assert.Equal(
            (TimeSpan.Zero, 0.0, (double?)null, Rpe.Unrated, (int?)null, string.Empty, string.Empty),
            (activity.Moving, activity.DistanceMeters, activity.ElevationGainMeters, activity.PerceivedExertion, activity.AverageHeartRate,
             activity.Description, activity.File));
    }

    [Fact]
    public void A_date_written_with_a_narrow_no_break_space_before_the_hour_marker_reads_the_same()
    {
        using var archive = new ArchiveFolder(Row(1001, Start).Replace(" PM\"", " PM\""));

        Assert.Equal(Start, Assert.Single(ResultAssert.Succeeded(StravaActivitiesReader.Read(archive.Path))).Start);
    }

    [Fact]
    public void Every_unreadable_row_is_named_and_nothing_is_read()
    {
        using var archive = new ArchiveFolder(
            Row(1001, Start),
            Row(1002, Start).Replace("\"Jul 9, 2024, 10:15:30 PM\"", "\"someday\""),
            Row(1003, Start, exertion: "11.0"));

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StravaActivitiesReader.Read(archive.Path)));

        Assert.Equal(["line 3", "line 4"], problem.Fields.Select(field => field.Field));
    }

    [Fact]
    public void An_archive_listing_one_activity_twice_is_refused()
    {
        using var archive = new ArchiveFolder(Row(1001, Start), Row(1001, Start.AddDays(1)));

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StravaActivitiesReader.Read(archive.Path)));

        Assert.Contains("activity 1001 more than once", problem.Description);
    }

    [Fact]
    public void A_folder_without_activities_csv_is_not_an_archive()
    {
        var empty = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StravaActivitiesReader.Read(empty)));
            Assert.Contains("not a Strava archive folder", problem.Description);
        }
        finally
        {
            Directory.Delete(empty);
        }
    }
}
