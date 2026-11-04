using System.Text.Json;
using lionheart.Model.Health;
using lionheart.Model.Oura.Readings;
using lionheart.Model.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace lionheart.Data;

/// <summary>
/// EF mapping for what Apple Health workouts bring beyond the sessions they become — the recording
/// app's route, heart rate and other metrics — and for the workouts whose sessions were deleted.
/// </summary>
/// <remarks>
/// <b>Stored column by column, not fix by fix.</b> A route is read and written whole and an hour of
/// it is three to four thousand fixes, so it is one JSON value — but as parallel arrays under one
/// start instant (<c>{"start":…,"ticks":[…],"latitude":[…],"longitude":[…],"altitude":[…]}</c>)
/// rather than an object per fix, which repeats four property names and a full timestamp three
/// thousand times. That is under half the size: roughly 150 KB for an hour's run at one fix a
/// second instead of 350. The heart rate and each metric are stored the same way. The domain never sees the storage
/// form; the converters here are the only code that does.
/// <para>
/// No foreign key to the session. A session is rewritten by delete-then-insert whenever it is
/// corrected, and a cascade would take the recording with it on every correction; the session's
/// delete removes the recording explicitly instead, the way it removes its analysis projection.
/// </para>
/// </remarks>
public static class HealthConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkoutRecording>(recording =>
        {
            recording.ToTable("WorkoutRecordings");
            recording.HasKey(r => r.TrainingSessionId);
            recording.HasOne<LionheartUser>().WithMany().HasForeignKey(r => r.UserId);
            recording.HasIndex(r => r.UserId);

            recording.Property(r => r.Route).HasStoredForm(StoredRoute.Of, stored => stored.ToRoute());
            recording.Property(r => r.HeartRate).HasStoredForm(StoredHeartRate.Of, stored => stored.ToHeartRate());
            recording.Property(r => r.Metrics).HasStoredForm(StoredMetric.Of, StoredMetric.ToMetrics).HasDefaultValueSql("'[]'");
        });

        modelBuilder.Entity<DeletedHealthWorkout>(deleted =>
        {
            deleted.ToTable("DeletedHealthWorkouts");
            deleted.HasKey(d => new { d.UserId, d.WorkoutId });
            deleted.HasOne<LionheartUser>().WithMany().HasForeignKey(d => d.UserId);
        });
    }

    /// <summary>A value written as its storage form's JSON and read back through it.</summary>
    private static PropertyBuilder<T> HasStoredForm<T, TStored>(
        this PropertyBuilder<T> property, Func<T, TStored> toStored, Func<TStored, T> fromStored)
    {
        // Delegates rather than local functions: a converter is an expression tree, which can
        // invoke a captured delegate but cannot name a local function.
        Func<T, string> write = value => JsonSerializer.Serialize(toStored(value), ValueConversions.StorageJson);
        Func<string, T> read = json => fromStored(JsonSerializer.Deserialize<TStored>(json, ValueConversions.StorageJson)
            ?? throw new JsonException($"Stored JSON deserialized to null for {typeof(T).Name}."));

        var comparer = new ValueComparer<T>(
            (left, right) => Equals(left, right),
            value => value == null ? 0 : value.GetHashCode(),
            value => value);

        return property.HasConversion(value => write(value), json => read(json), comparer);
    }

    /// <summary>A route as parallel arrays from its first fix.</summary>
    private sealed record StoredRoute(
        DateTimeOffset Start, long[] Ticks, double[] Latitude, double[] Longitude, double?[] Altitude)
    {
        public static StoredRoute Of(WorkoutRoute route)
        {
            var start = route.Points.Count > 0 ? route.Points[0].At : DateTimeOffset.UnixEpoch;
            return new StoredRoute(
                start,
                [.. route.Points.Select(point => (point.At - start).Ticks)],
                [.. route.Points.Select(point => point.Latitude)],
                [.. route.Points.Select(point => point.Longitude)],
                [.. route.Points.Select(point => point.Altitude)]);
        }

        public WorkoutRoute ToRoute() =>
            new([.. Ticks.Select((ticks, index) =>
                new RoutePoint(Start.AddTicks(ticks), Latitude[index], Longitude[index], Altitude[index]))]);
    }

    /// <summary>Each metric's series as parallel arrays from its first sample.</summary>
    private sealed record StoredMetric(WorkoutMetric Metric, DateTimeOffset Start, long[] Ticks, double[] Values)
    {
        public static IReadOnlyList<StoredMetric> Of(RecordedMetrics metrics) =>
        [
            .. metrics.Series.Select(series => new StoredMetric(
                series.Metric,
                series.Samples[0].At,
                [.. series.Samples.Select(sample => (sample.At - series.Samples[0].At).Ticks)],
                [.. series.Samples.Select(sample => sample.Value)])),
        ];

        public static RecordedMetrics ToMetrics(IReadOnlyList<StoredMetric> stored) =>
            RecordedMetrics.Of(
                from series in stored
                from index in Enumerable.Range(0, series.Ticks.Length)
                select (series.Metric, series.Start.AddTicks(series.Ticks[index]), series.Values[index]));
    }

    /// <summary>A heart-rate trace as parallel arrays from its first sample.</summary>
    private sealed record StoredHeartRate(DateTimeOffset Start, long[] Ticks, int[] Bpm)
    {
        public static StoredHeartRate Of(RecordedHeartRate heartRate)
        {
            var start = heartRate.Samples.Count > 0 ? heartRate.Samples[0].At : DateTimeOffset.UnixEpoch;
            return new StoredHeartRate(
                start,
                [.. heartRate.Samples.Select(sample => (sample.At - start).Ticks)],
                [.. heartRate.Samples.Select(sample => sample.Bpm)]);
        }

        public RecordedHeartRate ToHeartRate() =>
            new([.. Ticks.Select((ticks, index) =>
                new HeartRateSample(Start.AddTicks(ticks), Bpm[index], RecordedHeartRate.SampleSource))]);
    }
}
