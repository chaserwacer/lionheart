using System.Text.Json;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.BodyMeasurements.Composition;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.BodyComposition;

/// <summary>
/// Records a scan from a JSON file holding a <see cref="ScanRequest"/>, the body
/// <c>POST /api/body-composition/scans</c> takes:
/// <c>dotnet run -- add-body-composition-scan &lt;scan.json&gt; --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// For a sheet already transcribed and checked by hand, so entering it does not depend on how well
/// a photo of it reads. It goes through the same service as the route — the same checks, the same
/// bodyweight rule — and a scan already recorded at that minute is refused, so running it twice
/// writes once.
/// </remarks>
internal static class AddScanCommand
{
    public const string Verb = "add-body-composition-scan";

    private const string Usage =
        "usage: dotnet run -- add-body-composition-scan <scan.json> --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [Verb, var path, "--user", var user] || !Guid.TryParse(user, out var userId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var service = new BodyCompositionService(scope.ServiceProvider.GetRequiredService<ModelContext>());

        var recorded = await Read(path)
            .BindAsync(request => scope.ServiceProvider.GetRequiredService<ModelContext>().KnownAthleteAsync(userId, ct)
                .BindAsync(_ => service.RecordAsync(new IdentityUser { Id = userId.ToString() }, request, ct)));

        return recorded.Match(Describe, Refuse);
    }

    private static Result<ScanRequest, Problem> Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ScanRequest>(File.ReadAllText(path), JsonSerializerOptions.Web) is { Sheet: not null } request
                ? Result<ScanRequest, Problem>.Success(request)
                : Result<ScanRequest, Problem>.Failure(new Problem.Invalid("File", $"{path} holds no scan."));
        }
        // NotSupportedException is the serializer's answer to a panel with no "$type".
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return Result<ScanRequest, Problem>.Failure(new Problem.Invalid("File", $"{path}: {e.Message}"));
        }
    }

    private static int Describe(ScanView scan)
    {
        var sheet = scan.Sheet;
        Console.WriteLine($"Recorded {sheet.Device} scan {scan.Day:yyyy-MM-dd} {scan.Time:HH\\:mm} as {scan.Id}.");
        Console.WriteLine($"  weight {sheet.Weight}, SMM {sheet.SkeletalMuscleMass}, PBF {sheet.PercentBodyFat}%, {sheet.Sections.Count} panels.");
        return 0;
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"scan refused: {problem.Description}");
        return 1;
    }
}
