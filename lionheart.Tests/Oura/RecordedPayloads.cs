using System.Text.Json;
using lionheart.Model.Oura.Wire;

namespace lionheart.Tests.Oura;

/// <summary>
/// The recorded Oura payloads under <c>Oura/Archive</c>, read a line at a time.
/// </summary>
/// <remarks>
/// These are real responses from the athlete's own account, kept as files so the bytes are exactly
/// what Oura sent. They matter more than a hand-written fixture would for one reason: the live pull
/// cannot be run without his credential, and the archive import and the pull share one mapping, so
/// a test that maps a recorded payload is the only evidence available about what the pull would do
/// with it.
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

    /// <summary>One recorded record, deserialized to its wire type.</summary>
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
