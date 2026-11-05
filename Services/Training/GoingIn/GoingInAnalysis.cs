using lionheart.Model.Training.Balance;
using lionheart.Model.Training.GoingIn;
using lionheart.Model.Training.Load;
using lionheart.Services.Chat.Today;

namespace lionheart.Services.Training.GoingIn;

/// <summary>
/// Builds the going-in state from the facts the chat's briefing reads, the week's muscle volume and
/// the active injuries. Pure, so every rule below is a test.
/// </summary>
/// <remarks>
/// The method, which the client's info sheet states in words:
/// <list type="bullet">
/// <item>Every part is scored 0–100, his usual day near 75, so the parts can be averaged.</item>
/// <item>Readiness is Oura's own score, unchanged.</item>
/// <item>Autonomic reads last night's HRV and lowest heart rate against the nights before: each
/// per cent of HRV above or below the baseline moves its half 1.25 points from 75, and each beat
/// of lowest heart rate above or below moves it 8 points the other way.</item>
/// <item>Sleep is Oura's sleep score for last night and the night before, two to one.</item>
/// <item>Load starts at 100 and loses 1.5 per hard set since yesterday, 1 per minute at zone 4 or
/// above, 0.15 per minute of zone 2, 0.5 per minute of a cardio session with no heart rate to
/// zone, 50 per unit the week's hard sets run above 1.3 times the four weeks before them
/// (<see cref="LoadRatio"/>), and 0.03 per MET-minute of everyday load yesterday and today over
/// his usual high. It is not scored when the cardio could not be read: training it cannot see is
/// not training not done.</item>
/// <item>Wellness is today's 1–5 overall rating on the same scale; absent when not rated today.</item>
/// </list>
/// The rating is the parts' weighted mean over those that could be scored. It is made only when at
/// least one of readiness, autonomic or sleep was: the other two say nothing about recovery.
/// </remarks>
public static class GoingInAnalysis
{
    public const double ReadinessWeight = 0.30;
    public const double AutonomicWeight = 0.25;
    public const double SleepWeight = 0.20;
    public const double LoadWeight = 0.15;
    public const double WellnessWeight = 0.10;

    /// <summary>Where a part sits on his usual day.</summary>
    private const double Usual = 75;

    /// <summary>The fewest nights a baseline is read from; fewer and a night is compared with noise.</summary>
    public const int FewestBaselineNights = 14;


    public static GoingInState Of(
        TodayFacts facts, IReadOnlyList<MuscleVolume> muscles, IReadOnlyList<InjuryNote> injuries)
    {
        var today = facts.Now.Today;
        var yesterday = facts.Now.Yesterday;
        var lastNight = facts.Nights.FirstOrDefault(night => night.Day == today);
        var nightBefore = facts.Nights.FirstOrDefault(night => night.Day == yesterday);
        var wellness = facts.Wellness is { } rated && rated.Date == today ? rated : null;

        var scores = new Dictionary<Part, double?>
        {
            [Part.Readiness] = facts.Readiness?.Score,
            [Part.Autonomic] = AutonomicScore(lastNight, facts.Baseline),
            [Part.Sleep] = SleepScore(lastNight, nightBefore),
            [Part.Load] = LoadScore(facts, yesterday),
            [Part.Wellness] = wellness is null ? null : (wellness.Overall - 1) / 4 * 100,
        };
        var weighed = scores.Where(score => score.Value is not null).Sum(score => Weight(score.Key));

        PartScore Scored(Part part) => scores[part] is { } score
            ? new PartScore.Scored(Clamp(score), Weight(part) / weighed)
            : new PartScore.NotScored();

        return new GoingInState(
            facts.Now.Local,
            Rating(scores, weighed),
            new ReadinessPart(
                Scored(Part.Readiness),
                facts.Readiness?.Score,
                facts.Readiness?.UsualLow,
                facts.Readiness?.UsualHigh,
                facts.Readiness?.HrvBalance,
                facts.Readiness?.RestingHeartRate,
                facts.Readiness?.TemperatureDeviation),
            new AutonomicPart(
                Scored(Part.Autonomic),
                lastNight?.AverageHrv,
                facts.Baseline.AverageHrv,
                lastNight?.LowestHeartRate,
                facts.Baseline.LowestHeartRate,
                facts.Baseline.Nights),
            new SleepPart(Scored(Part.Sleep), Night(lastNight), Night(nightBefore)),
            new LoadPart(
                Scored(Part.Load),
                SinceYesterday(facts, yesterday).Sum(session => session.HardSets),
                facts.Cardio is CardioDays.Read ? CardioSinceYesterday(facts, yesterday).Sum(c => c.ZoneFourPlus ?? 0) : null,
                facts.Cardio is CardioDays.Read ? CardioSinceYesterday(facts, yesterday).Sum(c => c.ZoneTwo ?? 0) : null,
                (int)facts.Load.HardSets.Acute,
                facts.Load.HardSets is LoadRatio.Compared compared ? compared.Chronic : 0,
                EverydaySinceYesterday(facts, yesterday)),
            new WellnessPart(
                Scored(Part.Wellness),
                wellness is null
                    ? null
                    : new WellnessNote(wellness.Overall, wellness.Mood, wellness.Energy, wellness.Stress, wellness.Motivation)),
            injuries,
            [.. facts.Planned.Where(plan => plan.Day == today).Select(plan => new PlannedNote(plan.Movements))],
            [.. muscles
                .Where(muscle => muscle.LastTrained is { } day && day >= yesterday)
                .OrderByDescending(muscle => muscle.LastTrained)
                .ThenByDescending(muscle => muscle.Sets)
                .Select(muscle => new RecentMuscle(muscle.Muscle, muscle.LastTrained!.Value))]);
    }

    private enum Part { Readiness, Autonomic, Sleep, Load, Wellness }

    private static double Weight(Part part) => part switch
    {
        Part.Readiness => ReadinessWeight,
        Part.Autonomic => AutonomicWeight,
        Part.Sleep => SleepWeight,
        Part.Load => LoadWeight,
        Part.Wellness => WellnessWeight,
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, null),
    };

    private static GoingInRating Rating(IReadOnlyDictionary<Part, double?> scores, double weighed)
    {
        var readsRecovery = scores[Part.Readiness] is not null || scores[Part.Autonomic] is not null
                            || scores[Part.Sleep] is not null;
        if (!readsRecovery) return new GoingInRating.NotRated();

        var score = Clamp(scores.Where(part => part.Value is not null)
            .Sum(part => Weight(part.Key) * Math.Clamp(part.Value!.Value, 0, 100)) / weighed);
        return new GoingInRating.Rated(score, BandOf(score));
    }

    public static GoingInBand BandOf(int score) => score switch
    {
        >= 80 => GoingInBand.High,
        >= 65 => GoingInBand.Normal,
        >= 50 => GoingInBand.Reduced,
        _ => GoingInBand.Low,
    };

    /// <summary>
    /// HRV and lowest heart rate, each against the nights before the last two, averaged over the
    /// halves that can be read. Absent without last night or without enough nights to compare.
    /// </summary>
    private static double? AutonomicScore(NightFact? lastNight, NightBaseline baseline)
    {
        if (lastNight is null || baseline.Nights < FewestBaselineNights) return null;

        double? hrv = lastNight.AverageHrv is { } tonight && baseline.AverageHrv is { } usual and > 0
            ? Usual + 125 * (tonight / usual - 1)
            : null;
        double? heartRate = lastNight.LowestHeartRate is { } lowest && baseline.LowestHeartRate is { } usualLowest
            ? Usual - 8 * (lowest - usualLowest)
            : null;

        double?[] halves = [hrv, heartRate];
        var read = halves.OfType<double>().Select(half => Math.Clamp(half, 0, 100)).ToList();
        return read.Count == 0 ? null : read.Average();
    }

    /// <summary>Oura's sleep score for last night and the night before, two to one; absent without last night's.</summary>
    private static double? SleepScore(NightFact? lastNight, NightFact? nightBefore) =>
        (lastNight?.SleepScore, nightBefore?.SleepScore) switch
        {
            (null, _) => null,
            ({ } last, null) => last,
            ({ } last, { } before) => (2.0 * last + before) / 3,
        };

    /// <summary>
    /// The load since yesterday, or absent when the cardio could not be read: a run the report
    /// could not see would otherwise cost nothing and the part would read fresher than he is.
    /// </summary>
    private static double? LoadScore(TodayFacts facts, DateOnly yesterday)
    {
        if (facts.Cardio is CardioDays.Unread) return null;

        var hardSets = SinceYesterday(facts, yesterday).Sum(session => session.HardSets);
        var cardio = CardioSinceYesterday(facts, yesterday).ToList();
        var ratio = facts.Load.HardSets is LoadRatio.Compared compared ? compared.Ratio : 0;

        return 100
               - 1.5 * hardSets
               - 1.0 * cardio.Sum(session => session.ZoneFourPlus ?? 0)
               - 0.15 * cardio.Sum(session => session.ZoneTwo ?? 0)
               - UnzonedMinuteCost * cardio.Where(IsUnzoned).Sum(session => session.Minutes)
               - 50 * Math.Max(0, ratio - LoadRatio.SteepRatio)
               - EverydayCost(EverydaySinceYesterday(facts, yesterday));
    }

    /// <summary>
    /// What a MET-minute of everyday load over his usual high (the 90th percentile of his days)
    /// costs: about a fifth of what a zone-2 minute's six or so MET-minutes do, so an ordinary active
    /// day costs nothing and a day far above it costs the part points in single figures.
    /// </summary>
    public const double EverydayMetMinuteCost = 0.03;

    private static double EverydayCost(IReadOnlyList<EverydayDay> days) =>
        EverydayMetMinuteCost * days.Sum(day => day.Standing is EverydayStanding.Judged judged ? Math.Max(0, day.MetMinutes - judged.High) : 0);

    private static IReadOnlyList<EverydayDay> EverydaySinceYesterday(TodayFacts facts, DateOnly yesterday) =>
        facts.Everyday.Between(yesterday, facts.Now.Today);

    /// <summary>
    /// What a minute of cardio with no heart rate to zone costs: between zone 2 and zone 4, since
    /// the session happened and how hard it was is unknown.
    /// </summary>
    public const double UnzonedMinuteCost = 0.5;

    private static bool IsUnzoned(CardioFact session) => session.ZoneTwo is null && session.ZoneFourPlus is null;

    private static IEnumerable<SessionFact> SinceYesterday(TodayFacts facts, DateOnly yesterday) =>
        facts.Sessions.Where(session => session.Day >= yesterday);

    private static IEnumerable<CardioFact> CardioSinceYesterday(TodayFacts facts, DateOnly yesterday) =>
        facts.Cardio.Known.Where(session => session.Day >= yesterday);

    private static NightNote? Night(NightFact? night) =>
        night is null ? null : new NightNote(night.Day, night.TotalSleepSeconds, night.Efficiency, night.SleepScore);

    private static int Clamp(double score) => (int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero);
}
