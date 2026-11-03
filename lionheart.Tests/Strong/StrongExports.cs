using System.Globalization;
using lionheart.Data;
using lionheart.Services.Strong;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Support;

namespace lionheart.Tests.Strong;

/// <summary>
/// Strong exports for the import tests: the real miniature under <c>Strong/Exports</c>, and
/// hand-written ones whose premise is stated where the test is.
/// </summary>
/// <remarks>
/// <c>four-workouts.csv</c> is four of the athlete's own workouts copied byte for byte from his
/// export: one with a warm-up, one with sets to failure, one with workout notes and rest timers,
/// and one with a set note. Real rows rather than invented ones, for the reason the Oura fixtures
/// are real: what the importer has to survive is what Strong actually writes.
/// </remarks>
internal static class StrongExports
{
    public const string Header =
        "Date,Workout Name,Duration,Exercise Name,Set Order,Weight,Reps,Distance,Seconds,Notes,Workout Notes,RPE";

    public static string FourWorkouts => Path.Combine(AppContext.BaseDirectory, "Strong", "Exports", "four-workouts.csv");

    /// <summary>Where the athlete was when these were logged; tests pin it rather than read the machine's.</summary>
    public static TimeZoneInfo NewYork => TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    /// <summary>One set row, quoted the way Strong quotes them.</summary>
    public static string Set(
        string date,
        string exercise,
        string order,
        double weight,
        double reps,
        string workout = "Afternoon Workout",
        string duration = "1h 5m",
        string notes = "",
        string workoutNotes = "",
        string rpe = "",
        double distance = 0,
        double seconds = 0) =>
        string.Join(",",
            date, Quoted(workout), duration, Quoted(exercise), order,
            weight.ToString("0.0##", CultureInfo.InvariantCulture),
            reps.ToString("0.0##", CultureInfo.InvariantCulture),
            distance.ToString(CultureInfo.InvariantCulture),
            seconds.ToString("0.0", CultureInfo.InvariantCulture),
            Quoted(notes), Quoted(workoutNotes), rpe);

    public static string Rest(string date, string exercise, string workout = "Afternoon Workout", string duration = "1h 5m") =>
        $"{date},{Quoted(workout)},{duration},{Quoted(exercise)},Rest Timer,0,0.0,0,180.0,,,";

    private static string Quoted(string text) => $"\"{text.Replace("\"", "\"\"")}\"";

    /// <summary>A map holding exactly the entries a test states, so its premise is on the page.</summary>
    public static StrongMovementMap Map(params (string Strong, string Base, string Equipment, string Modifier)[] entries) =>
        ResultAssert.Succeeded(StrongMovementMap.Parse(System.Text.Json.JsonSerializer.Serialize(
            entries.Select(entry => new
            {
                strong = entry.Strong,
                @base = entry.Base,
                equipment = entry.Equipment,
                modifier = entry.Modifier,
            }))));

    public static StrongImporter Importer(ModelContext context, StrongMovementMap map) =>
        new(context, new PersonalRecordDetector(context, TimeProvider.System), map, NewYork, TimeProvider.System);
}

/// <summary>A hand-written export on disk for the length of one test.</summary>
internal sealed class ExportFile : IDisposable
{
    public ExportFile(params string[] rows) : this(StrongExports.Header, rows) { }

    private ExportFile(string header, string[] rows)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"strong-{Guid.NewGuid():N}.csv");
        File.WriteAllLines(Path, [header, .. rows]);
    }

    /// <summary>A file whose first line is the given text rather than Strong's header.</summary>
    public static ExportFile Headed(string header, params string[] rows) => new(header, rows);

    public string Path { get; }

    public void Dispose() => File.Delete(Path);
}
