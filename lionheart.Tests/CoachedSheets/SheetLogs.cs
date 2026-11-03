using System.Globalization;
using System.Text.Json;
using lionheart.Data;
using lionheart.Services.CoachedSheets;
using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Support;

namespace lionheart.Tests.CoachedSheets;

/// <summary>
/// training-os set logs for the coached-programme import tests, written row by row so each test's
/// premise is on the page, with the decisions and movement map a test states.
/// </summary>
/// <remarks>The rows follow training-os's own layout: the columns <c>derived/sets.tsv</c> has, in its order.</remarks>
internal static class SheetLogs
{
    public const string Header =
        "set_id\tdate\tsession_id\tworkout\texercise\texercise_raw\tset_idx\tset_type\tweight\treps\trpe\tseconds\tdistance\tset_note\tsource";

    /// <summary>Where the athlete was training; tests pin it rather than read the machine's.</summary>
    public static TimeZoneInfo NewYork => TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    /// <summary>One set from a programme sheet, under training-os's session id for its date, sheet and day.</summary>
    public static string Set(
        string date, string sheet, int week, string day, string exercise, string asWritten, double weight, int reps,
        string rpe = "", string note = "") =>
        string.Join("\t",
            Guid.NewGuid().ToString("N")[..16],
            date,
            $"{date}-{sheet[..Math.Min(16, sheet.Length)]}-{day.ToLowerInvariant().Replace(' ', '-')}",
            day,
            exercise,
            asWritten,
            "1",
            "",
            weight.ToString(CultureInfo.InvariantCulture),
            reps.ToString(CultureInfo.InvariantCulture),
            rpe,
            "",
            "",
            note,
            $"sheet:{sheet}#w{week}");

    /// <summary>A row of the Strong history the log carries beside the sheets.</summary>
    public static string StrongRow(string date) =>
        string.Join("\t", "a1b2c3", date, $"{date}-strong", "Afternoon Workout", "squat-barbell", "Squat (Barbell)", "1", "", "225", "5", "", "", "", "", "strong:2026-07-31-strong.csv");

    /// <summary>The two programmes the tests' rows belong to, and whatever not-performed rows a test states.</summary>
    public static SheetDecisions Decisions(params object[] notPerformed) =>
        ResultAssert.Succeeded(SheetDecisions.Parse(JsonSerializer.Serialize(new
        {
            programs = new object[]
            {
                new { sheet = "mk-2", title = "Perfect Powerlifting — MK.2", summary = "Coached by Blake Rossi.", tags = new[] { "coached" } },
                new { sheet = "your-program-rpe", title = "Jamal V5 — Your Program (RPE)", summary = "RPE template.", tags = new[] { "coached" } },
            },
            templateText = new[] { "WEIGHT WILL BE AUTO GENERATED" },
            notPerformed,
        })));

    /// <summary>A not-performed entry as the decisions file states one.</summary>
    public static object NotPerformed(
        string source, string date, string exercise, double weight, int reps, int sets, string kind = "computed") =>
        new { source, date, exercise, weight, reps, sets, kind, reason = "stated by the test" };

    /// <summary>A movement map holding exactly the entries a test states.</summary>
    public static MovementMap Map(params (string Slug, string Base, string Equipment, string Modifier)[] entries) =>
        ResultAssert.Succeeded(MovementMap.Parse(
            JsonSerializer.Serialize(entries.Select(entry => new
            {
                slug = entry.Slug,
                @base = entry.Base,
                equipment = entry.Equipment,
                modifier = entry.Modifier,
            })),
            SheetMovementMap.File));

    public static MovementMap SquatAndBench => Map(
        ("squat-barbell", "Squat", "Barbell", ""),
        ("bench-press-barbell", "Bench Press", "Barbell", ""),
        ("paused-deadlift", "Deadlift", "Barbell", "paused"));

    public static CoachedSheetImporter Importer(ModelContext context, MovementMap map, SheetDecisions decisions) =>
        new(context,
            new PersonalRecordRebuild(context, new PersonalRecordDetector(context, TimeProvider.System)),
            map,
            decisions,
            NewYork,
            TimeProvider.System);
}

/// <summary>A hand-written set log on disk for the length of one test.</summary>
internal sealed class LogFile : IDisposable
{
    public LogFile(params string[] rows)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sets-{Guid.NewGuid():N}.tsv");
        File.WriteAllLines(Path, [SheetLogs.Header, .. rows]);
    }

    public string Path { get; }

    public void Dispose() => File.Delete(Path);
}
