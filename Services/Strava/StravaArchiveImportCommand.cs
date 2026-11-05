using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Cardio;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Strava;

/// <summary>
/// The command line the Strava archive import runs behind:
/// <c>dotnet run -- import-strava &lt;export-folder&gt; --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// Packaged like the other history imports, for their reasons: a filesystem path, an athlete named
/// on the command line, and no bulk-write route to keep out of production. The folder is the one
/// Strava's "Download your data" archive unzips to, holding <c>activities.csv</c> and
/// <c>activities/</c>. Strava writes every time in UTC; a session is placed on its day in this
/// machine's time zone, as it applied on that date.
/// </remarks>
internal static class StravaArchiveImportCommand
{
    public const string Verb = "import-strava";

    private const string Usage = "usage: dotnet run -- import-strava <strava-export-folder> --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [Verb, var archive, "--user", var user] || !Guid.TryParse(user, out var userId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();
        var clock = TimeProvider.System;
        var rebuild = new PersonalRecordRebuild(context, new PersonalRecordDetector(context, clock));

        var imported = await new StravaArchiveImporter(context, rebuild, TimeZoneInfo.Local, clock).ImportAsync(archive, userId, ct);

        return imported.Match(Describe, Refuse);
    }

    // ---- Reporting ----

    private static int Describe(StravaArchiveImportReport report)
    {
        Console.WriteLine($"Sessions placed on their day in {TimeZoneInfo.Local.Id}.");
        Console.WriteLine($"{report.ActivitiesInArchive} activities in the archive; {report.AlreadyImported} already imported by an earlier run.");

        Console.WriteLine($"\n{report.Added.Count} added:");
        foreach (var group in report.Added.GroupBy(added => (added.Activity.Type, added.Became)).OrderBy(group => group.Key.Type, StringComparer.Ordinal))
            Console.WriteLine($"  {group.Count(),4} {group.Key.Type} -> {group.Key.Became}");

        Console.WriteLine($"\n{report.Matched.Count} already in the log, not added:");
        foreach (var group in report.Matched.GroupBy(matched => (matched.Activity.Type, Held: Held(matched))).OrderBy(group => group.Key.Type, StringComparer.Ordinal))
            Console.WriteLine($"  {group.Count(),4} {group.Key.Type} -> {group.Key.Held}");

        Console.WriteLine($"\n{report.Enriched.Count} held sessions given what their recording lacked:");
        Console.WriteLine($"  {report.Enriched.Count(session => session.Fill.Route)} a route, {report.Enriched.Count(session => session.Fill.HeartRate)} a heart rate, "
                          + $"{report.Enriched.Count(session => session.Fill.Metrics)} metrics.");
        if (report.ClocklessRoutesKept > 0)
            Console.WriteLine($"  {report.ClocklessRoutesKept} held routes have no clock where the archive's has one; kept as held, so they give no splits.");

        Console.WriteLine($"\n{report.LeftOut.Count} left out:");
        foreach (var group in report.LeftOut.GroupBy(left => (left.Reason, left.Activity.Type)).OrderBy(group => group.Key.Reason))
            Console.WriteLine($"  {group.Count(),4} {group.Key.Type}: {Reason(group.Key.Reason)}");

        Console.WriteLine($"\n{report.UnreadableFiles.Count} files could not be read (their activities were decided on the summary alone):");
        foreach (var file in report.UnreadableFiles)
            Console.WriteLine($"  {file.Activity.File}: {file.Reason}");

        if (report.NearSessions.Count > 0)
        {
            Console.WriteLine($"\n{report.NearSessions.Count} added activities lie partly inside a session already held (less than half, so both stand):");
            foreach (var near in report.NearSessions)
                Console.WriteLine($"  {near.Activity.Start:yyyy-MM-dd HH:mm}Z {near.Activity.Type} ({near.Activity.Id}): {near.Share:P1} inside session {near.HeldSessionId}");
        }

        Console.WriteLine(report.Records switch
        {
            RecordsOutcome.Rebuilt rebuilt => $"\nRecords rebuilt over the whole history. {RebuildRecordsCommand.Summary(rebuilt.Report)}",
            RecordsOutcome.Untouched => "\nNo session added, so the records were left as they were.",
            _ => throw new UnreachableException(),
        });

        return 0;
    }

    private static string Held(MatchedActivity matched) =>
        (matched.Match, matched.HeldAs) switch
        {
            (SessionMatch.LoggedLifting, _) => "inside a logged lifting session",
            (_, SessionSource.AppleHealth health) => $"Apple Health session recorded by {health.RecordedBy.Name}",
            (_, SessionSource.StravaArchive) => "a session this import added for another activity",
            (_, SessionSource.AthleteLog) => "a logged session",
            _ => throw new UnreachableException(),
        };

    private static string Reason(StravaExclusion reason) => reason switch
    {
        StravaExclusion.Deleted => "its session was deleted",
        StravaExclusion.NotTaken => "a type Lionheart does not take",
        StravaExclusion.NoDistance => "no distance and no route",
        _ => throw new UnreachableException(),
    };

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"import refused: {problem.Description}");
        return 1;
    }
}
