using System.Text.Json;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;

namespace lionheart.Tests.Serialization;

/// <summary>
/// The Apple Health shapes the native client writes and decodes. Literal expectations, because the
/// client is written against these bytes.
/// </summary>
public class HealthWireTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Fact]
    public void A_workout_as_the_client_writes_it_reads_into_an_import()
    {
        const string body = """
            {
              "workoutId": "8d0c3a52-6c1e-4d7b-9a43-2f7d1e6b5c90",
              "activity": "running",
              "indoor": false,
              "appName": "Strava",
              "appBundleId": "com.strava.stravaride",
              "start": "2026-09-20T07:00:00+02:00",
              "end": "2026-09-20T07:30:00+02:00",
              "duration": "00:29:40",
              "distanceMeters": 5012.5,
              "elevationAscendedMeters": null,
              "route": [{ "at": "2026-09-20T05:00:00Z", "latitude": 46.5, "longitude": 8.25, "altitude": 400.2 }],
              "heartRate": [{ "at": "2026-09-20T05:00:01Z", "bpm": 131 }]
            }
            """;

        var workout = JsonSerializer.Deserialize<HealthWorkoutImport>(body, Wire)!;

        var stated = ResultAssert.Succeeded(workout.Stated());
        Assert.Equal(Guid.Parse("8d0c3a52-6c1e-4d7b-9a43-2f7d1e6b5c90"), stated.Source.WorkoutId);
        Assert.Equal(("Strava", "com.strava.stravaride"), (stated.Source.RecordedBy.Name, stated.Source.RecordedBy.BundleId));
        Assert.Equal((TimeSpan.FromSeconds(1780), 5012.5), (stated.Duration, stated.DistanceMeters!.Value));
        Assert.Equal(400.2, Assert.Single(stated.Route.Points).Altitude);
        Assert.Equal(131, Assert.Single(stated.HeartRate.Samples).Bpm);
    }

    [Fact]
    public void A_session_goes_out_naming_its_source()
    {
        var workoutId = Guid.NewGuid();
        var session = Some.CompletedSession(Guid.NewGuid()) with { Source = AHealthRun.Source(workoutId) };

        var source = JsonDocument.Parse(JsonSerializer.Serialize(TrainingSessionView.Of(session), Wire)).RootElement
            .GetProperty("source");

        Assert.Equal("appleHealth", source.GetProperty("$type").GetString());
        Assert.Equal(workoutId, source.GetProperty("workoutId").GetGuid());
        Assert.Equal("Strava", source.GetProperty("recordedBy").GetProperty("name").GetString());
    }

    [Fact]
    public void A_session_from_the_athletes_log_says_so()
    {
        var source = JsonDocument.Parse(JsonSerializer.Serialize(TrainingSessionView.Of(Some.CompletedSession(Guid.NewGuid())), Wire))
            .RootElement.GetProperty("source");

        Assert.Equal("athleteLog", source.GetProperty("$type").GetString());
    }

    [Theory]
    [InlineData("alreadyImported")]
    [InlineData("notImported")]
    public void Each_import_outcome_goes_out_named_by_its_discriminator(string discriminator)
    {
        HealthImportOutcome outcome = discriminator == "alreadyImported"
            ? new HealthImportOutcome.AlreadyImported(Guid.NewGuid())
            : new HealthImportOutcome.NotImported(HealthWorkoutExclusion.SameAsLoggedRun, Guid.NewGuid());

        var root = JsonDocument.Parse(JsonSerializer.Serialize(outcome, Wire)).RootElement;

        Assert.Equal(discriminator, root.GetProperty("$type").GetString());
    }

    [Fact]
    public void A_reason_a_workout_was_left_out_goes_out_as_its_word()
    {
        var root = JsonDocument.Parse(JsonSerializer.Serialize<HealthImportOutcome>(
            new HealthImportOutcome.NotImported(HealthWorkoutExclusion.ReadDirectly, null), Wire)).RootElement;

        Assert.Equal("readDirectly", root.GetProperty("reason").GetString());
    }
}
