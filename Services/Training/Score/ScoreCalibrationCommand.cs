using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Score;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Load;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    private const string Usage =
        "usage: dotnet run -- score-calibration --user <athlete-guid> [--weeks <from> <to> | --sessions <from> <to> | --next <day> | --load <from> <to> | --everyday <from> <to>]";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [Verb, "--user", var user, ..] || !Guid.TryParse(user, out var userId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();
        var map = MovementPatternMap.Shipped;
        var cardio = new CardioReportService(context);
        var goingIn = new GoingIn.GoingInService(
            context,
            new Chat.Today.TodayFactsService(context, cardio, map, TimeProvider.System, NullLogger<Chat.Today.TodayFactsService>.Instance),
            new Balance.TrainingVolumeService(context, map, TimeProvider.System));
        var service = new TrainingScoreService(context, cardio, map, goingIn, TimeProvider.System);
        var identity = new IdentityUser { Id = userId.ToString() };

        if (args is [_, _, _, "--sessions", var from, var to])
        {
            var report = await new CardioReportService(context).GetReportAsync(
                identity, new DayRange(DateOnly.Parse(from), DateOnly.Parse(to)), ct);
            return report.Match(DescribeSessions, Refuse);
        }

        if (args is [_, _, _, "--next", var asOf])
        {
            var next = await new Next.NextSessionsService(context, service, new CardioReportService(context), MovementPatternMap.Shipped)
                .GetNextAsync(identity, DateOnly.Parse(asOf), ct);
            return next.Match(
                found =>
                {
                    Console.WriteLine(string.Join("\n", Next.NextSessionsService.Lines(found)));
                    return 0;
                },
                Refuse);
        }

        if (args is [_, _, _, "--load", var loadFrom, var loadTo])
        {
            // Each Sunday, then the Monday after it, so the deload and the advice the next week
            // would have been offered are printed beside the week they were read from.
            foreach (var sunday in TrainingScoreService.Sundays(new DayRange(DateOnly.Parse(loadFrom), DateOnly.Parse(loadTo))))
            {
                var week = await service.GetScoreAsync(identity, sunday, ct);
                var next = await service.GetScoreAsync(identity, sunday.AddDays(1), ct);
                Console.WriteLine(week.Match(
                    scored => next.Match(following => LoadLine(scored, following), problem => $"{sunday:yyyy-MM-dd} next refused: {problem.Description}"),
                    problem => $"{sunday:yyyy-MM-dd} refused: {problem.Description}"));
            }
            return 0;
        }

        if (args is [_, _, _, "--everyday", var everydayFrom, var everydayTo])
        {
            var range = new DayRange(DateOnly.Parse(everydayFrom), DateOnly.Parse(everydayTo));
            var everyday = await context.EverydayAsync(userId, range, DateOnly.FromDateTime(DateTime.Today), ct);
            foreach (var day in everyday.Days) Console.WriteLine(EverydayLine(day));
            var judged = everyday.Days.Where(day => day.Standing is EverydayStanding.Judged).ToList();
            Console.WriteLine(
                $"days {everyday.Days.Count}: above {judged.Count(day => day.IsAbove && !day.IsFarAbove)}, far above {judged.Count(day => day.IsFarAbove)}");
            return 0;
        }

        if (args is [_, _, _, "--weeks", var weeksFrom, var weeksTo])
        {
            // Each Sunday scored on its own, so every measurement and its target can be printed.
            foreach (var sunday in TrainingScoreService.Sundays(new DayRange(DateOnly.Parse(weeksFrom), DateOnly.Parse(weeksTo))))
            {
                var scored = await service.GetScoreAsync(identity, sunday, ct);
                Console.WriteLine(scored.Match(Detail, problem => $"{sunday:yyyy-MM-dd} refused: {problem.Description}"));
            }
            return 0;
        }

        var first = (await context.Sessions.OfType<CompletedTrainingSession>().AsNoTracking()
                .Where(session => session.UserId == userId).ToListAsync(ct))
            .Select(session => session.Date).DefaultIfEmpty(DateOnly.FromDateTime(DateTime.Today)).Min();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // A year at a time, the most one call scores; read in turn so one database context is
        // never asked two things at once.
        var chunks = new List<Result<IReadOnlyList<WeeklyScore>, Problem>>();
        foreach (var range in Years(first, today))
        {
            chunks.Add(await service.GetWeeksAsync(identity, range, ct));
        }

        return chunks.Traverse(chunk => chunk)
            .Map(IReadOnlyList<WeeklyScore> (years) => [.. years.SelectMany(year => year)])
            .Match(Describe, Refuse);
    }

    private static IEnumerable<DayRange> Years(DateOnly first, DateOnly today) =>
        Enumerable.Range(0, (today.DayNumber - first.DayNumber) / (7 * TrainingScoreService.MostWeeks) + 1)
            .Select(year => first.AddDays(7 * TrainingScoreService.MostWeeks * year))
            .Select(from => new DayRange(from, Min(from.AddDays(7 * TrainingScoreService.MostWeeks - 1), today)));

    private static int Describe(IReadOnlyList<WeeklyScore> weeks)
    {
        Report(weeks);
        return 0;
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"calibration refused: {problem.Description}");
        return 1;
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
        $"{week.Window.To:yyyy-MM-dd} {week.Value,3}/{week.OutOf}  lifting {week.LiftingPoints,4:0.0}  cardio {(week.CardioPoints is { } cardio ? $"{cardio,4:0.0}" : "   ?")}"
        + $"  progress {week.ProgressPoints,4:0.0}  consistency {week.ConsistencyPoints,4:0.0}";

    /// <summary>One week's score with every measurement against this week's target, "measured/target".</summary>
    private static string Detail(TrainingScore score) =>
        $"{score.Window.To:yyyy-MM-dd} {score.Value,3}/{score.OutOf}  sets "
        + string.Join(" ", score.Lifting.Families.Select(family => $"{family.Of.HardSets}/{family.Of.Target.Target:0}"))
        + "  " + (score.Cardio.Zones.Count is 0
            ? $"cardio {score.Cardio.Measure}"
            : string.Join("  ", score.Cardio.Zones.Select(zone => $"{zone.Of.Band} {zone.Of.Minutes:0}/{zone.Of.Target.Target:0}")))
        + $"  lift {score.Lifting.Points:0.0} cardio {score.Cardio.Points?.ToString("0.0") ?? "?"} progress {score.Progress.Points:0.0} consistency {score.Consistency.Points:0.0}";

    /// <summary>
    /// One week's load and the next week's offer: hard sets against the four before, the cardio and
    /// running ramps, far-above everyday days, then the Monday's deload and advice.
    /// </summary>
    private static string LoadLine(TrainingScore week, TrainingScore monday)
    {
        var load = week.Expectations.Load;
        var cardio = load.Cardio switch
        {
            CardioLoad.Read read => $"cardio {Ratio(read.Minutes.Ratio)} {read.Minutes.Standing}  run {Ratio(read.RunningMeters.Ratio, 1609.344)}mi {read.RunningMeters.Standing}",
            _ => "cardio unread",
        };
        var deload = monday.Expectations.Load.Lifting.Deload switch
        {
            Deload.Suggested suggested => $"DELOAD after {suggested.BuildingWeeks}",
            var other => $"building {other.BuildingWeeks}",
        };
        return $"{week.Window.To:yyyy-MM-dd} sets {Ratio(load.Lifting.HardSets)} {load.Lifting.Ramp}  {cardio}"
               + $"  far-above days {load.Everyday.FarAboveDays}  next: {deload} | {monday.Expectations.Advice.Headline}";
    }

    private static string Ratio(LoadRatio ratio, double unit = 1) => ratio switch
    {
        LoadRatio.Compared compared => $"{compared.Acute / unit:0.#}/{compared.Chronic / unit:0.#}={compared.Ratio:0.00}",
        _ => $"{ratio.Acute / unit:0.#}/-",
    };

    private static string EverydayLine(EverydayDay day) =>
        $"{day.Day:yyyy-MM-dd} {day.Day.DayOfWeek,-9} {day.MetMinutes,6:0} MET-min  "
        + (day.Standing is EverydayStanding.Judged judged
            ? $"usual {judged.Usual,5:0} high {judged.High,5:0}  {day.Ratio:0.00}x  {judged.Band}"
            : "unjudged");

    private static int DescribeSessions(Model.Cardio.CardioReport report)
    {
        Console.WriteLine($"maximum {report.MaxHeartRate}");
        foreach (var session in report.Sessions) Console.WriteLine($"  {SessionLine(session)}");
        Console.WriteLine("declined:");
        foreach (var declined in report.Declined) Console.WriteLine($"  {SessionLine(declined.Session)} {declined.Reason}");
        return 0;
    }

    private static string SessionLine(Model.Cardio.CardioSession session) =>
        $"{session.Start:yyyy-MM-dd HH:mm} {session.Minutes,4:0}m {session.Source} "
        + (session.HeartRate is Model.Cardio.SessionHeartRate.Recorded recorded
            ? $"avg {recorded.AverageBpm} peak {recorded.PeakBpm} cov {recorded.Coverage:0.00} {recorded.Zones}"
            : "no heart rate");

    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
}
