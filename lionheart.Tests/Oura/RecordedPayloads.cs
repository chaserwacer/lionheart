using System.Text.Json;
using lionheart.Model.Oura.Wire;

namespace lionheart.Tests.Oura;

/// <summary>
/// The Oura payloads under <c>Oura/Archive</c>, read a line at a time.
/// </summary>
/// <remarks>
/// Invented values in exactly the shape Oura sends: its field names and nesting, the nulls and
/// zeros it writes where a measure is missing, its timestamp formats and sample runs. The shape is
/// what matters, for one reason: the live pull cannot be run without the athlete's credential, and
/// the archive import and the pull share one mapping, so a test that maps one of these payloads is
/// the only evidence available about what the pull would do with Oura's.
/// </remarks>
internal static class RecordedPayloads
{
    /// <summary>The miniature archive: a few representative records per collection.</summary>
    public static string ArchiveDirectory => Path.Combine(AppContext.BaseDirectory, "Oura", "Archive");

    /// <summary>Records broken on purpose, for what the importer does with input it cannot read.</summary>
    public static string DamagedDirectory => Path.Combine(AppContext.BaseDirectory, "Oura", "Damaged");

    public static IReadOnlyList<string> Lines(string collection) =>
        [.. File.ReadAllLines(Path.Combine(ArchiveDirectory, $"{collection}.jsonl"))
            .Where(line => !string.IsNullOrWhiteSpace(line))];

    /// <summary>One record, deserialized to its wire type.</summary>
    public static T Record<T>(string collection, int index) =>
        JsonSerializer.Deserialize<T>(Lines(collection)[index])
        ?? throw new InvalidOperationException($"{collection}.jsonl line {index + 1} deserialized to null.");

    public static WireDailyActivity Activity(int index) => Record<WireDailyActivity>("daily_activity", index);
    public static WireDailyReadiness Readiness(int index) => Record<WireDailyReadiness>("daily_readiness", index);
    public static WireDailySleep DailySleep(int index) => Record<WireDailySleep>("daily_sleep", index);
    public static WireDailySpo2 Spo2(int index) => Record<WireDailySpo2>("daily_spo2", index);
    public static WireDailyStress Stress(int index) => Record<WireDailyStress>("daily_stress", index);
    public static WireSleep Sleep(int index) => Record<WireSleep>("sleep", index);
    public static WireWorkout Workout(int index) => Record<WireWorkout>("workout", index);
    public static WireSession Session(int index) => Record<WireSession>("session", index);
    public static WireTag Tag(int index) => Record<WireTag>("tag", index);
}
