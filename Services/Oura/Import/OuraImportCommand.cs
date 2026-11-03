using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Oura.Import;

/// <summary>
/// The command line the archive import runs behind:
/// <c>dotnet run -- import-oura &lt;directory&gt; --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// A command rather than a development-only endpoint, for three reasons. It takes a filesystem
/// path, which is an argument rather than something a request body should carry. It needs no
/// identity story — the athlete is named on the command line instead of being inferred from a
/// bearer token. And it adds no route that would have to be kept out of production, which a write
/// endpoint that bulk-loads from disk certainly would.
/// <para>
/// It builds a host with configuration and the database only. The web host refuses to start
/// without LM Studio configured, and an import has no use for a language model.
/// </para>
/// </remarks>
internal static class OuraImportCommand
{
    public const string Verb = "import-oura";

    private const string Usage =
        "usage: dotnet run -- import-oura <archive-directory> --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (Arguments.Parse(args) is not { } arguments)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();

        var imported = await new OuraArchiveImporter(context)
            .ImportAsync(arguments.Directory, arguments.UserId, ct);

        return imported.Match(Describe, Refuse);
    }

    private sealed record Arguments(string Directory, Guid UserId)
    {
        /// <summary>The arguments, or nothing if they do not form a runnable command.</summary>
        public static Arguments? Parse(string[] args)
        {
            if (args is not [Verb, var directory, "--user", var user]) return null;

            return Guid.TryParse(user, out var userId) ? new Arguments(directory, userId) : null;
        }
    }

    // ---- Reporting ----

    private static int Describe(OuraImportReport report)
    {
        foreach (var collection in report.Collections)
        {
            Console.WriteLine(collection.Outcome switch
            {
                OuraCollectionOutcome.Read read =>
                    $"{collection.Collection,-24} {read.Stored,6} stored of {read.Lines,6} lines"
                    + (read.Rejected.Count is 0 ? string.Empty : $", {read.Rejected.Count} rejected"),
                OuraCollectionOutcome.NoArchiveFile missing =>
                    $"{collection.Collection,-24}      - no {missing.ExpectedName} in the archive",
                _ => throw new UnreachableException()
            });
        }

        if (report.UncollectedFiles.Count > 0)
        {
            Console.WriteLine($"\nnot imported (not a collection this application stores): {string.Join(", ", report.UncollectedFiles)}");
        }

        if (report.Rejected > 0)
        {
            Console.WriteLine($"\n{report.Rejected} rejected record(s):");
            foreach (var rejection in report.Rejections)
            {
                Console.WriteLine($"  {rejection.File}:{rejection.Line}  {rejection.Reason}");
            }
        }

        Console.WriteLine($"\n{report.Stored} documents stored, {report.Rejected} rejected.");
        return 0;
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"import refused: {problem.Description}");
        return 1;
    }
}
