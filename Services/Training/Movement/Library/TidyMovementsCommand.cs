using lionheart.Data;
using lionheart.Functional;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>
/// The command line the library tidy runs behind:
/// <c>dotnet run -- tidy-movements --user &lt;athlete-id&gt; [--dry-run]</c>.
/// </summary>
/// <remarks>
/// Packaged like the imports and the record rebuild: a command, not an endpoint, building a host
/// with configuration and the database only. The connection string is relative, so it tidies the
/// database of the checkout it runs from. Run it with <c>--dry-run</c> first: that prints every
/// change the write would make, record replay included, and rolls it all back.
/// </remarks>
internal static class TidyMovementsCommand
{
    public const string Verb = "tidy-movements";

    private const string Usage = "usage: dotnet run -- tidy-movements --user <athlete-guid> [--dry-run]";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var (parsed, userId, run) = args switch
        {
            [Verb, "--user", var user] when Guid.TryParse(user, out var id) => (true, id, TidyRun.Write),
            [Verb, "--user", var user, "--dry-run"] when Guid.TryParse(user, out var id) => (true, id, TidyRun.DryRun),
            [Verb, "--dry-run", "--user", var user] when Guid.TryParse(user, out var id) => (true, id, TidyRun.DryRun),
            _ => (false, Guid.Empty, TidyRun.DryRun),
        };
        if (!parsed)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();
        var records = new PersonalRecordRebuild(context, new PersonalRecordDetector(context, TimeProvider.System));

        var tidied = await MovementLibraryRules.Shipped()
            .BindAsync(rules => new MovementLibraryTidy(context, rules, records, MovementPatternMap.Shipped, TimeProvider.System)
                .TidyAsync(userId, run, ct));

        return tidied.Match(Describe, Refuse);
    }

    private static int Describe(TidyReport report)
    {
        foreach (var line in TidyReportText.Lines(report)) Console.WriteLine(line);
        return 0;
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"tidy refused: {problem.Description}");
        return 1;
    }
}
