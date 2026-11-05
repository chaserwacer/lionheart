using System.Globalization;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Next;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Score;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Focus;

namespace lionheart.Services.Training.Next;

/// <summary>Everything the recommendation is read from, already read.</summary>
/// <param name="Score">
/// The score of the seven days ending on the day: the week's measurements, its expectations and each
/// priority lift's next top set.
/// </param>
/// <param name="RecentCardio">The cardio sessions of the two weeks ending on the day, as the cardio report counted them.</param>
public sealed record NextInputs(
    DateOnly Day,
    TrainingLog Log,
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
/// priority lift's next top set from the week's expectations.</item>
/// <item>The cardio is a quality session when the week's quality target is unmet, none was done in
/// the last two days, and recovery is not low; otherwise zone 2. Hard sessions are spaced 48 hours
/// apart, the easy days between them easy (Seiler 2010).</item>
/// <item>Targets are the week's adjusted expectations, so a week with days marked off asks the
/// next session for the share those days leave; a day itself marked off is said first.</item>
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
        var first = First(inputs, lift, cardio, recovery);
        return new NextSessions(inputs.Day, first with { Reasons = [.. DayOff(inputs), .. first.Reasons] }, lift, cardio, inputs.Recovery);
    }

    /// <summary>That the day is marked non-training, when it is, so the rest reads as the next training day's.</summary>
    private static IReadOnlyList<string> DayOff(NextInputs inputs) =>
        inputs.Score.Expectations.Availability.Days.LastOrDefault() is DayAvailability.NonTraining off && off.Day == inputs.Day
            ? [$"Today is marked non-training{(off.Note.Length > 0 ? $" ({off.Note})" : "")}: this is for your next training day."]
            : [];

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
        var volume = TrainingVolumeAnalysis.Build(inputs.Log, inputs.Day, inputs.Recovery.Readiness, inputs.Score.Expectations.Families);
        if (volume.UpNext is not UpNext.Train due)
            return new NextLift.NothingLogged(["No classified lifting in the log yet."]);

        var reasons = new List<string>
        {
            due.LastTrained is { } last
                ? $"{Sentence(Families(due.Families))} last trained {Ago(inputs.Day, last)}"
                  + (Others(due.Families, volume) is { Length: > 0 } others ? $"; {others} since." : ".")
                : $"{Sentence(Families(due.Families))} not trained yet.",
        };
        foreach (var family in inputs.Score.Expectations.Families.Where(family => due.Families.Contains(family.Family)))
            reasons.Add(Invariant($"{Sentence(PatternWords.Of(family.Family))} {family.Sets.Done:0} of {family.Sets.Adjusted.Target:0} hard sets this week")
                        + (family.Sets.Done >= family.Sets.Adjusted.Target ? ", target met." : "."));
        if (due.TrainedToday) reasons.Add("Today already has hard sets, so this is the session after it.");

        return new NextLift.Train(
            due.Families,
            due.LastTrained,
            [.. inputs.Score.Expectations.Priorities.Where(priority => due.Families.Contains(priority.Family))],
            reasons);
    }

    /// <summary>The families trained since the due ones, in words, or empty.</summary>
    private static string Others(IReadOnlyList<PatternFamily> due, TrainingVolume volume) =>
        Families([.. volume.Families.Where(family => !due.Contains(family.Family) && family.LastTrained is not null).Select(family => family.Family)]);

    // ---- The cardio ----

    private static NextCardio Cardio(NextInputs inputs, IReadOnlyList<string> recovery)
    {
        var zone2 = Band(inputs.Score.Expectations, ZoneBand.Zone2, ScoreRules.Zone2);
        var quality = Band(inputs.Score.Expectations, ZoneBand.Quality, ScoreRules.Quality);
        var lastQuality = inputs.RecentCardio
            .Where(session => session.QualityMinutes > 0)
            .Select(session => (DateOnly?)LocalDay(session.Start))
            .Max();

        var reasons = new List<string>();
        var qualityLeft = quality.Target.Target - quality.Minutes;
        reasons.Add(Invariant($"This week: quality at {quality.Minutes:0} of {quality.Target.Target:0} min, zone 2 at {zone2.Minutes:0} of {zone2.Target.Target:0}."));
        if (inputs.Score.Cardio.Points is null) reasons.Add("This week's cardio could not be read from heart rate.");

        var spaced = lastQuality is not { } lastDay || inputs.Day.DayNumber - lastDay.DayNumber >= QualitySpacingDays;
        var legsToday = LegsLast(inputs.Score) == inputs.Day;
        var read = inputs.Score.Cardio.Points is not null;
        if (qualityLeft > 0 && spaced && recovery.Count is 0 && !legsToday && read)
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
        if (qualityLeft > 0 && legsToday) reasons.Add("Legs today: intervals wait 24–48 hours after legs.");
        if (qualityLeft > 0 && !read) reasons.Add("With the week's heart rate unread, nothing says intervals are owed: keep it aerobic.");

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

    /// <summary>A band's minutes and adjusted target this week; with cardio unknown, nothing against the floor.</summary>
    private static ZoneTarget Band(WeekExpectations expectations, ZoneBand band, Standard standard) =>
        expectations.Cardio.Zones
            .Where(zone => zone.Band == band)
            .Select(zone => new ZoneTarget(band, zone.Minutes.Done, zone.Minutes.Adjusted))
            .FirstOrDefault()
        ?? new ZoneTarget(band, 0, new WeeklyTarget(standard.Floor, standard.Committed, standard.Floor, null));

    private static DateOnly? LegsLast(TrainingScore score) =>
        score.Lifting.Families.FirstOrDefault(family => family.Of.Family == PatternFamily.Legs)?.Of.LastTrained;

    // ---- Which first ----

    /// <remarks>
    /// The interference rules come before "the kind not done today", so legs and intervals are kept
    /// apart on the same day as well as across two.
    /// </remarks>
    private static FirstChoice First(NextInputs inputs, NextLift lift, NextCardio cardio, IReadOnlyList<string> recovery)
    {
        if (recovery.Count > 0)
            return new FirstChoice(NextKind.Cardio, [.. recovery, "Easy aerobic work first; lift heavy once recovery is back in range."]);

        var liftedToday = inputs.Log.Between(inputs.Day, inputs.Day).Any(session => session.HardSets.Count > 0);
        var cardioToday = inputs.RecentCardio.Any(session => LocalDay(session.Start) == inputs.Day);

        if (cardio is NextCardio.Quality && LegsLast(inputs.Score) is { } legs && inputs.Day.DayNumber - legs.DayNumber <= 1)
            return new FirstChoice(NextKind.Lift, [$"Legs trained {Ago(inputs.Day, legs)}: intervals on tired legs lose their quality. Lift first; intervals 24–48 hours after legs."]);

        var lastQuality = inputs.RecentCardio.Where(session => session.QualityMinutes > 0).Select(session => (DateOnly?)LocalDay(session.Start)).Max();
        if (lift is NextLift.Train { Families: var families } && families.Contains(PatternFamily.Legs)
            && lastQuality is { } hard && inputs.Day.DayNumber - hard.DayNumber <= 1)
            return cardioToday
                ? new FirstChoice(NextKind.Lift, [$"Quality session {Ago(inputs.Day, hard)}: legs are next, but tomorrow rather than straight after the intervals."])
                : new FirstChoice(NextKind.Cardio, [$"Quality session {Ago(inputs.Day, hard)}: heavy legs straight after intervals blunt both. Easy cardio now, legs next."]);

        if (liftedToday && !cardioToday) return new FirstChoice(NextKind.Cardio, ["You lifted today; cardio is the session not yet done."]);
        if (cardioToday && !liftedToday) return new FirstChoice(NextKind.Lift, ["You did cardio today; lifting is the session not yet done."]);

        if (inputs.Score.Cardio.Points is not { } cardioPoints)
            return Longer(inputs, ["This week's cardio could not be read, so the week's balance cannot decide."]);

        var behind = Invariant($"Lifting {inputs.Score.Lifting.Points:0} of {inputs.Score.Lifting.OutOf}, cardio {cardioPoints:0} of {inputs.Score.Cardio.OutOf} this week");
        return Share(cardioPoints, inputs.Score.Cardio.OutOf) < Share(inputs.Score.Lifting.Points, inputs.Score.Lifting.OutOf)
            ? new FirstChoice(NextKind.Cardio, [$"{behind}: cardio is further behind."])
            : new FirstChoice(NextKind.Lift, [$"{behind}: lifting is further behind."]);
    }

    /// <summary>Whichever kind was done longer ago, lifting on a tie.</summary>
    private static FirstChoice Longer(NextInputs inputs, IReadOnlyList<string> reasons)
    {
        var lastLift = inputs.Log.Sessions.Where(session => session.HardSets.Count > 0).Select(session => (DateOnly?)session.Day).Max();
        var lastCardio = inputs.RecentCardio.Select(session => (DateOnly?)LocalDay(session.Start)).Max();
        return (lastCardio ?? DateOnly.MinValue) < (lastLift ?? DateOnly.MinValue)
            ? new FirstChoice(NextKind.Cardio, [.. reasons, "Cardio was done longer ago."])
            : new FirstChoice(NextKind.Lift, [.. reasons, "Lifting was done longer ago."]);
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
