using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Score;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Training.Score;

/// <summary>
/// The command line the score is calibrated with:
/// <c>dotnet run -- score-calibration --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// Scores every Monday-to-Sunday week from the first logged session to today and prints the
/// distribution, so a change to <see cref="ScoreRules"/> can be judged against the athlete's own
/// history before it ships. Reads only; it writes nothing.
/// </remarks>
internal static class ScoreCalibrationCommand
{
    public const string Verb = "score-calibration";

    private const string Usage = "usage: dotnet run -- score-calibration --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [Verb, "--user", var user] || !Guid.TryParse(user, out var userId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();
        var service = new TrainingScoreService(context, new CardioReportService(context), MovementPatternMap.Shipped);
        var identity = new IdentityUser { Id = userId.ToString() };

        var first = (await context.Sessions.OfType<CompletedTrainingSession>().AsNoTracking()
                .Where(session => session.UserId == userId).ToListAsync(ct))
            .Select(session => session.Date).DefaultIfEmpty(DateOnly.FromDateTime(DateTime.Today)).Min();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var weeks = new List<WeeklyScore>();
        for (var from = first; from <= today; from = from.AddDays(7 * TrainingScoreService.MostWeeks))
        {
            var to = Min(from.AddDays(7 * TrainingScoreService.MostWeeks - 1), today);
            var scored = await service.GetWeeksAsync(identity, new DayRange(from, to), ct);
            if (scored.Match(chunk => { weeks.AddRange(chunk); return false; }, problem =>
                {
                    Console.Error.WriteLine($"calibration refused: {problem.Description}");
                    return true;
                }))
                return 1;
        }

        Report(weeks);
        return 0;
    }

    private static void Report(IReadOnlyList<WeeklyScore> weeks)
    {
        var trained = weeks.Where(week => week.ConsistencyPoints > 0 && week.LiftingPoints > 0).ToList();
        Distribution("every week", weeks);
        Distribution("weeks with lifting", trained);
        Distribution("weeks with lifting and cardio known", [.. trained.Where(week => week.CardioPoints is not null)]);

        Console.WriteLine("\nweeks at 90 or above:");
        foreach (var week in weeks.Where(week => week.Value >= 90)) Console.WriteLine($"  {Line(week)}");

        Console.WriteLine("\nthe last 16 weeks:");
        foreach (var week in weeks.TakeLast(16)) Console.WriteLine($"  {Line(week)}");
    }

    private static void Distribution(string label, IReadOnlyList<WeeklyScore> weeks)
    {
        if (weeks.Count is 0)
        {
            Console.WriteLine($"{label}: none");
            return;
        }

        var values = weeks.Select(week => week.Value).Order().ToList();
        int At(double fraction) => values[(int)Math.Floor(fraction * (values.Count - 1))];
        var bands = values.GroupBy(value => Math.Min(90, value / 10 * 10)).OrderBy(band => band.Key)
            .Select(band => $"{band.Key}s:{band.Count()}");

        Console.WriteLine(
            $"{label} (n={values.Count}): p10 {At(0.1)} · p25 {At(0.25)} · median {At(0.5)} · p75 {At(0.75)} · p90 {At(0.9)} · max {values[^1]}");
        Console.WriteLine($"  {string.Join("  ", bands)}");
    }

    private static string Line(WeeklyScore week) =>
        $"{week.Window.To:yyyy-MM-dd} {week.Value,3}  lifting {week.LiftingPoints,4:0.0}  cardio {(week.CardioPoints is { } cardio ? $"{cardio,4:0.0}" : "   ?")}"
        + $"  progress {week.ProgressPoints,4:0.0}  consistency {week.ConsistencyPoints,4:0.0}";

    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
}
