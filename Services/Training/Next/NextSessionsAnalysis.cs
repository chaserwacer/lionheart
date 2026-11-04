using System.Globalization;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Next;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Score;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Balance;

namespace lionheart.Services.Training.Next;

/// <summary>Everything the recommendation is read from, already read.</summary>
/// <param name="Priorities">Each family's priority lift over the window ending on the day.</param>
/// <param name="Score">The score of the seven days ending on the day: the week's measurements and their targets.</param>
/// <param name="RecentCardio">The cardio sessions of the two weeks ending on the day, as the cardio report counted them.</param>
public sealed record NextInputs(
    DateOnly Day,
    TrainingLog Log,
    IReadOnlyList<FamilyPriority> Priorities,
    TrainingScore Score,
    IReadOnlyList<CardioSession> RecentCardio,
    RecoveryRead Recovery);

/// <summary>
/// The next lifting session, the next cardio session and which comes first, as a function of the
/// log, the week's score and the night's recovery.
/// </summary>
/// <remarks>
/// Pure, so every rule is tested without a database. The rules, each from
/// <c>docs/training-science.md</c>:
/// <list type="bullet">
/// <item>The lift is the family trained longest ago (the volume screen's up-next), with its
/// priority lift one step past its last session's best set.</item>
/// <item>The cardio is a quality session when the week's quality target is unmet, none was done in
/// the last two days, and recovery is not low; otherwise zone 2. Hard sessions are spaced 48 hours
/// apart, the easy days between them easy (Seiler 2010).</item>
/// <item>First: low recovery goes to easy aerobic work; one kind already done today goes to the
/// other; hard intervals wait 24–48 hours after legs and legs wait after intervals, the
/// interference the concurrent-training literature measures (Doma and Deakin 2013; Wilson et al.
/// 2012); otherwise whichever part of the week is further behind.</item>
/// </list>
/// </remarks>
public static class NextSessionsAnalysis
{
    /// <summary>Days a quality session is kept from the one before it.</summary>
    public const int QualitySpacingDays = 2;

    /// <summary>The shortest night that is not read as short: six hours.</summary>
    public const int ShortNightMinutes = 360;

    /// <summary>The fewest nights a usual HRV is drawn from.</summary>
    public const int FewestHrvNights = 14;

    public static NextSessions Of(NextInputs inputs)
    {
        var recovery = RecoveryNotes(inputs.Recovery);
        var lift = Lift(inputs);
        var cardio = Cardio(inputs, recovery);
        return new NextSessions(inputs.Day, First(inputs, lift, cardio, recovery), lift, cardio, inputs.Recovery);
    }

    // ---- Recovery ----

    /// <summary>What reads low about this morning, as reasons; empty when nothing does.</summary>
    public static IReadOnlyList<string> RecoveryNotes(RecoveryRead recovery)
    {
        var notes = new List<string>();
        if (recovery.Readiness is ReadinessNote.Low low)
            notes.Add($"Oura readiness {low.OuraScore}, under your usual {low.UsualLow}–{low.UsualHigh}.");
        if (recovery is { Hrv: { } hrv, UsualHrv: { } usual, HrvSpread: { } spread } && hrv < usual - spread)
            notes.Add(Invariant($"HRV {hrv} ms last night against a usual {usual:0}."));
        if (recovery.SleepMinutes is { } slept && slept < ShortNightMinutes)
            notes.Add($"{slept / 60} h {slept % 60:00} min asleep last night.");
        return notes;
    }

    // ---- The lift ----

    private static NextLift Lift(NextInputs inputs)
    {
        var volume = TrainingVolumeAnalysis.Build(inputs.Log, inputs.Day, inputs.Recovery.Readiness);
        if (volume.UpNext is not UpNext.Train due)
            return new NextLift.NothingLogged(["No classified lifting in the log yet."]);

        var reasons = new List<string>
        {
            due.LastTrained is { } last
                ? $"{Sentence(Families(due.Families))} last trained {Ago(inputs.Day, last)}"
                  + (Others(due.Families, volume) is { Length: > 0 } others ? $"; {others} since." : ".")
                : $"{Sentence(Families(due.Families))} not trained yet.",
        };
        foreach (var family in inputs.Score.Lifting.Families.Where(family => due.Families.Contains(family.Of.Family)))
            reasons.Add(Invariant($"{Sentence(PatternWords.Of(family.Of.Family))} {family.Of.HardSets} of {family.Of.Target.Target:0} hard sets this week")
                        + (family.Of.HardSets >= family.Of.Target.Target ? ", target met." : "."));
        if (due.TrainedToday) reasons.Add("Today already has hard sets, so this is the session after it.");

        return new NextLift.Train(
            due.Families,
            due.LastTrained,
            [
                .. inputs.Priorities
                    .Where(priority => due.Families.Contains(priority.Family))
                    .Select(priority => priority.Lift is PriorityLift.Chosen chosen
                        ? new PriorityAim(priority.Family, chosen.MovementDataId, chosen.Name, AimOf(chosen))
                        : null)
                    .OfType<PriorityAim>()
            ],
            reasons);
    }

    /// <summary>The families trained since the due ones, in words, or empty.</summary>
    private static string Others(IReadOnlyList<PatternFamily> due, TrainingVolume volume) =>
        Families([.. volume.Families.Where(family => !due.Contains(family.Family) && family.LastTrained is not null).Select(family => family.Family)]);

    /// <summary>
    /// The priority's next top set: its last session's best set one step on — five pounds or 2.5
    /// kilograms, or a rep where the load is bodyweight — against the best a new best must pass.
    /// </summary>
    public static LiftAim AimOf(PriorityLift.Chosen lift)
    {
        if (lift.Series is not [.., var last]) return new LiftAim.Establish();

        var (unit, onSystemLoad) = lift.Progress switch
        {
            LiftProgress.FirstBack back => (back.Unit, back.OnSystemLoad),
            LiftProgress.Compared compared => (compared.Unit, compared.OnSystemLoad),
            _ => (WeightUnit.Pounds, false),
        };
        var (load, reps) = onSystemLoad ? (last.Load, last.Reps + 1) : (last.Load + Step(unit), last.Reps);
        return new LiftAim.Beat(
            load, reps, unit, new LiftPerformance(load, unit, reps).EstimatedOneRepMax,
            lift.Series.Max(best => best.EstimatedOneRepMax), onSystemLoad);
    }

    private static double Step(WeightUnit unit) => unit switch
    {
        WeightUnit.Pounds => 5,
        WeightUnit.Kilograms => 2.5,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    // ---- The cardio ----

    private static NextCardio Cardio(NextInputs inputs, IReadOnlyList<string> recovery)
    {
        var zone2 = Band(inputs.Score, ZoneBand.Zone2, ScoreRules.Zone2);
        var quality = Band(inputs.Score, ZoneBand.Quality, ScoreRules.Quality);
        var lastQuality = inputs.RecentCardio
            .Where(session => session.QualityMinutes > 0)
            .Select(session => (DateOnly?)LocalDay(session.Start))
            .Max();

        var reasons = new List<string>();
        var qualityLeft = quality.Target.Target - quality.Minutes;
        reasons.Add(Invariant($"Quality {quality.Minutes:0} of {quality.Target.Target:0} min this week, zone 2 {zone2.Minutes:0} of {zone2.Target.Target:0}."));
        if (inputs.Score.Cardio.Points is null) reasons.Add("This week's cardio could not be read from heart rate.");

        var spaced = lastQuality is not { } lastDay || inputs.Day.DayNumber - lastDay.DayNumber >= QualitySpacingDays;
        if (qualityLeft > 0 && spaced && recovery.Count is 0)
        {
            var firstBack = quality.Target.Chronic is not > 0;
            var (bouts, boutMinutes, rest) = Structure(firstBack ? QualityWork.LeastSessionMinutes : qualityLeft);
            reasons.Add(lastQuality is { } day
                ? $"Last quality session {Ago(inputs.Day, day)}."
                : "No quality session in the last two weeks.");
            if (firstBack) reasons.Add("No quality work in the four weeks before: start at the shortest session that counts.");
            reasons.Add("Run, ride or row; each bout counts once heart rate holds 80 % of maximum for two minutes.");
            return new NextCardio.Quality(bouts, boutMinutes, rest, reasons);
        }

        if (qualityLeft > 0 && !spaced && lastQuality is { } recent)
            reasons.Add($"Quality session {Ago(inputs.Day, recent)}: keep 48 hours between hard sessions.");
        if (recovery.Count > 0) reasons.Add("Recovery reads low: easy aerobic work, no intervals.");

        var left = zone2.Target.Target - zone2.Minutes;
        var minutes = recovery.Count > 0
            ? 30
            : (int)Math.Clamp(Math.Ceiling(left / 5) * 5, 30, 75);
        return new NextCardio.Zone2(minutes, reasons);
    }

    /// <summary>
    /// The interval structure whose work is nearest the minutes wanted, the shorter on a tie: five
    /// two-minute bouts for the least that counts, then 4 × 4 (Helgerud et al. 2007), 3 × 8 and
    /// 4 × 8 (Seiler et al. 2013).
    /// </summary>
    public static (int Bouts, int BoutMinutes, int RecoveryMinutes) Structure(double workMinutes) =>
        Structures.MinBy(structure => Math.Abs(structure.Bouts * structure.BoutMinutes - workMinutes));

    private static readonly IReadOnlyList<(int Bouts, int BoutMinutes, int RecoveryMinutes)> Structures =
        [(5, 2, 2), (4, 4, 3), (3, 8, 2), (4, 8, 2)];

    /// <summary>A band's minutes and target this week; with cardio unknown, nothing against the floor.</summary>
    private static ZoneTarget Band(TrainingScore score, ZoneBand band, Standard standard) =>
        score.Cardio.Zones.Select(zone => zone.Of).FirstOrDefault(zone => zone.Band == band)
        ?? new ZoneTarget(band, 0, new WeeklyTarget(standard.Floor, standard.Committed, standard.Floor, null));

    // ---- Which first ----

    private static FirstChoice First(NextInputs inputs, NextLift lift, NextCardio cardio, IReadOnlyList<string> recovery)
    {
        if (recovery.Count > 0)
            return new FirstChoice(NextKind.Cardio, [.. recovery, "Easy aerobic work first; lift heavy once recovery is back in range."]);

        var liftedToday = inputs.Log.Between(inputs.Day, inputs.Day).Any(session => session.HardSets.Count > 0);
        var cardioToday = inputs.RecentCardio.Any(session => LocalDay(session.Start) == inputs.Day);
        if (liftedToday && !cardioToday) return new FirstChoice(NextKind.Cardio, ["You lifted today; cardio is the session not yet done."]);
        if (cardioToday && !liftedToday) return new FirstChoice(NextKind.Lift, ["You did cardio today; lifting is the session not yet done."]);

        var legsLast = inputs.Score.Lifting.Families.FirstOrDefault(family => family.Of.Family == PatternFamily.Legs)?.Of.LastTrained;
        if (cardio is NextCardio.Quality && legsLast is { } legs && inputs.Day.DayNumber - legs.DayNumber <= 1)
            return new FirstChoice(NextKind.Lift, [$"Legs trained {Ago(inputs.Day, legs)}: intervals on tired legs lose their quality. Lift first; intervals 24–48 hours after legs."]);

        var lastQuality = inputs.RecentCardio.Where(session => session.QualityMinutes > 0).Select(session => (DateOnly?)LocalDay(session.Start)).Max();
        if (lift is NextLift.Train { Families: var families } && families.Contains(PatternFamily.Legs)
            && lastQuality is { } hard && inputs.Day.DayNumber - hard.DayNumber <= 1)
            return new FirstChoice(NextKind.Cardio, [$"Quality session {Ago(inputs.Day, hard)}: heavy legs straight after intervals blunt both. Easy cardio now, legs next."]);

        var liftShare = Share(inputs.Score.Lifting.Points, inputs.Score.Lifting.OutOf);
        var cardioShare = Share(inputs.Score.Cardio.Points ?? 0, inputs.Score.Cardio.OutOf);
        var behind = Invariant($"Lifting {inputs.Score.Lifting.Points:0} of {inputs.Score.Lifting.OutOf}, cardio {inputs.Score.Cardio.Points ?? 0:0} of {inputs.Score.Cardio.OutOf} this week");
        return cardioShare < liftShare
            ? new FirstChoice(NextKind.Cardio, [$"{behind}: cardio is further behind."])
            : new FirstChoice(NextKind.Lift, [$"{behind}: lifting is further behind."]);
    }

    private static double Share(double points, int outOf) => outOf > 0 ? points / outOf : 0;

    // ---- Words ----

    private static string Families(IReadOnlyList<PatternFamily> families) =>
        string.Join(" and ", families.Select(PatternWords.Of));

    private static string Sentence(string words) =>
        words.Length is 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];

    private static string Ago(DateOnly today, DateOnly day) => (today.DayNumber - day.DayNumber) switch
    {
        0 => "today",
        1 => "yesterday",
        var days => $"{days} days ago",
    };

    private static DateOnly LocalDay(DateTimeOffset instant) => DateOnly.FromDateTime(instant.DateTime);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
