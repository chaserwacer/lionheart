using System.Globalization;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Oura;

/// <summary>
/// The command line a live Oura pull runs behind, for a range wider than the app asks for:
/// <c>dotnet run -- sync-oura --user &lt;athlete-id&gt; --from &lt;yyyy-MM-dd&gt; --to &lt;yyyy-MM-dd&gt;</c>.
/// </summary>
/// <remarks>
/// Packaged like the archive import, for its reasons: the athlete is named rather than signed in,
/// and nothing is added to the web host. It pulls with the credential the athlete stored, so a
/// backfill needs neither his password nor the token in hand. The range is pulled a month at a
/// time, so one month Oura refuses ends the run with every earlier month already stored.
/// </remarks>
internal static class OuraSyncCommand
{
    public const string Verb = "sync-oura";

    private const string Usage =
        "usage: dotnet run -- sync-oura --user <athlete-guid> --from <yyyy-MM-dd> --to <yyyy-MM-dd>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (Arguments.Parse(args) is not { } arguments)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        using var http = new HttpClient();
        var service = new OuraSyncService(scope.ServiceProvider.GetRequiredService<ModelContext>(), http);

        var synced = await Months(arguments.Range)
            .TraverseAsync(month => service.SyncAthleteAsync(arguments.UserId, month, ct)
                .MapAsync(report => Describe(report)));

        return synced.Match(_ => 0, Refuse);
    }

    /// <summary>The range cut at calendar-month boundaries, oldest first.</summary>
    internal static IReadOnlyList<DayRange> Months(DayRange range) =>
        [.. Enumerable.Range(0, int.MaxValue)
            .Select(offset => new DateOnly(range.From.Year, range.From.Month, 1).AddMonths(offset))
            .TakeWhile(start => start <= range.To)
            .Select(start => new DayRange(
                start < range.From ? range.From : start,
                start.AddMonths(1).AddDays(-1) is var end && end > range.To ? range.To : end))];

    private sealed record Arguments(Guid UserId, DayRange Range)
    {
        public static Arguments? Parse(string[] args) =>
            args is [Verb, "--user", var user, "--from", var from, "--to", var to]
            && Guid.TryParse(user, out var userId)
            && DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)
            && DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last)
            && first <= last
                ? new Arguments(userId, new DayRange(first, last))
                : null;
    }

    private static OuraSyncReport Describe(OuraSyncReport report)
    {
        Console.WriteLine($"{report.Range.From:yyyy-MM-dd}..{report.Range.To:yyyy-MM-dd}  {report.Stored} stored, {report.Rejected} rejected"
            + string.Concat(report.Collections.Where(tally => tally.Received > 0)
                .Select(tally => $"  {tally.Collection} {tally.Stored}/{tally.Received}")));
        foreach (var rejection in report.Collections.SelectMany(tally => tally.Rejected.Select(reason => $"    {tally.Collection}: {reason}")))
        {
            Console.WriteLine(rejection);
        }

        return report;
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"sync refused: {problem.Description}");
        return 1;
    }
}
