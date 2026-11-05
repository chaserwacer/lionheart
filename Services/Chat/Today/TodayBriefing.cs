using System.Diagnostics;
using System.Globalization;
using System.Text;
using lionheart.Model;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;

namespace lionheart.Services.Chat.Today;

/// <summary>
/// The briefing as the model reads it: the athlete's date and time, his training today and
/// yesterday, his last two nights, today's recovery, injuries, and the week so far with its score.
/// </summary>
/// <remarks>
/// The facts a coach needs before answering almost anything, so the model answers "should I train
/// today" without a tool call and never has to infer what today is. Every line is dated in his own
/// days and names absence outright ("not synced yet", "nothing logged yet"), because a fact left out
/// is a fact the model will guess at. Terse by design: it is sent with every message.
/// </remarks>
public static class TodayBriefing
{
    public const string Heading = "TODAY";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// The briefing when nothing but the clock could be read: still the date and time, and an
    /// instruction to fetch the rest rather than assume it.
    /// </summary>
    public static string ClockOnly(AthleteNow now) =>
        new StringBuilder(Opening(now))
            .Append("The rest of today's briefing could not be read: fetch what the question needs with the tools, and do not assume what he did today.")
            .ToString();

    public static string Render(TodayFacts facts)
    {
        var now = facts.Now;
        var text = new StringBuilder(Opening(now))
            .AppendLine(Invariant, $"Trained today: {Day(facts, now.Today, detailed: true, nothing: "nothing logged yet")}")
            .AppendLine(Invariant, $"Yesterday ({now.Yesterday:dddd d MMMM}): {Day(facts, now.Yesterday, detailed: true, nothing: "no training logged")}")
            .AppendLine(Load(facts.Load))
            .AppendLine(Everyday(facts))
            .AppendLine(Zones(facts.Cardio))
            .AppendLine(Nights(facts))
            .AppendLine(Readiness(facts.Readiness, facts.Means))
            .AppendLine(Wellness(facts.Wellness, now.Today))
            .AppendLine(Injuries(facts.Injuries))
            .AppendLine(Week(facts));

        if (facts.Planned.Count > 0)
        {
            text.AppendLine(Invariant, $"Planned: {string.Join("; ", facts.Planned.Select(plan => $"{On(plan.Day, "ddd d MMM")} {string.Join(", ", plan.Movements)}"))}.");
        }

        if (facts.NonTraining.Count > 0)
        {
            text.AppendLine(Invariant, $"Marked non-training (he has said he will not train): {string.Join(", ", facts.NonTraining.Select(day => NonTrainingDay(day, now.Today)))}.");
        }

        text.Append("Training score, seven days ending today (exactly what GetTrainingScore returns; call it only for other windows): ")
            .Append(string.IsNullOrWhiteSpace(facts.Score) ? "not available." : facts.Score.Trim());

        if (!string.IsNullOrWhiteSpace(facts.Next))
        {
            text.AppendLine().Append("Up next, as of today (exactly what GetNextSessions returns): ").Append(facts.Next.Trim());
        }

        return text.ToString();
    }

    private static string Opening(AthleteNow now) =>
        new StringBuilder()
            .AppendLine(Invariant, $"{Heading} (written by Tempo with this message; it is current and outranks any date or state earlier in the conversation)")
            .AppendLine(Invariant, $"Now: {now.Local:dddd d MMMM yyyy, HH:mm} ({Offset(now.Local)}). This week runs Monday {now.WeekStart:d MMMM} to Sunday {now.WeekStart.AddDays(6):d MMMM}.")
            .AppendLine(Units)
            .ToString();

    /// <summary>
    /// He reads every figure in imperial units, and a tool's raw fields (meters, kilograms, °C) are
    /// not what he would say; the briefing's own figures are already converted.
    /// </summary>
    public const string Units =
        "Units: he reads pounds, miles, pace in min/mi, speed in mph, elevation in feet and temperature deviation in °F. State every figure that way; where a tool gives meters, kilometres, kilograms or °C, convert before saying it.";

    // ---- Training ----

    /// <summary>
    /// A day's training: each session with its lifting and the cardio the report counted that day.
    /// When the report could not be read, the day says so rather than reading as a day without cardio.
    /// </summary>
    private static string Day(TodayFacts facts, DateOnly day, bool detailed, string nothing)
    {
        var parts = facts.Sessions.Where(session => session.Day == day).Select(session => Session(session, detailed))
            .Concat(facts.Cardio.Known.Where(cardio => cardio.Day == day).Select(Cardio))
            .ToList();
        var listed = parts.Count == 0 ? nothing : string.Join("; ", parts);
        return facts.Cardio switch
        {
            CardioDays.Read => $"{listed}.",
            CardioDays.Unread => $"{listed} (cardio could not be read).",
            _ => throw new UnreachableException(),
        };
    }

    private static string Session(SessionFact session, bool detailed)
    {
        var when = $"{At(session.Start)}, {Minutes(session.Duration.TotalMinutes)}";
        var rated = Rated(session);

        if (session.Lifts.Count == 0)
        {
            return session.HasCardioSets ? $"logged cardio session at {when}{rated}" : $"logged session at {when} with no hard sets{rated}";
        }

        var families = Families(session.Lifts);
        var lifts = detailed
            ? $": {string.Join(", ", session.Lifts.Select(lift => $"{lift.Movement} {lift.HardSets} {(lift.HardSets == 1 ? "set" : "sets")} (top {lift.TopSet})"))}"
            : $" ({string.Join(", ", session.Lifts.Take(3).Select(lift => lift.Movement))}{(session.Lifts.Count > 3 ? ", …" : "")})";
        return $"lifting at {when}, {session.HardSets} hard sets{families}{lifts}{rated}";
    }

    /// <summary>His own ratings from finishing the session, on the app's 1-5 scales.</summary>
    private static string Rated(SessionFact session)
    {
        var ratings = new[]
            {
                session.Difficulty is { } difficulty ? $"difficulty {difficulty}/5" : null,
                session.Fatigue is { } fatigue ? $"accumulated fatigue {fatigue}/5" : null,
            }
            .OfType<string>()
            .ToList();
        return ratings.Count == 0 ? "" : $"; he rated it {string.Join(", ", ratings)}";
    }

    /// <summary>Hard sets by family, so "upper" or "legs" is stated rather than inferred from movement names.</summary>
    private static string Families(IReadOnlyList<LiftFact> lifts)
    {
        var byFamily = lifts
            .GroupBy(lift => lift.Family)
            .OrderBy(group => group.Key is null ? int.MaxValue : (int)group.Key)
            .Select(group => $"{Family(group.Key)} {group.Sum(lift => lift.HardSets)}")
            .ToList();
        return byFamily.Count == 0 ? "" : $" ({string.Join(", ", byFamily)})";
    }

    private static string Family(PatternFamily? family) => family switch
    {
        PatternFamily.Push => "push",
        PatternFamily.Pull => "pull",
        PatternFamily.Legs => "legs",
        PatternFamily.Core => "core",
        _ => "unclassified",
    };

    private static string Cardio(CardioFact cardio)
    {
        var heart = cardio.AverageBpm is { } bpm
            ? $", avg {bpm} bpm, zone 2 {Minutes(cardio.ZoneTwo ?? 0)}, zone 4-5 {Minutes(cardio.ZoneFourPlus ?? 0)}"
            : ", no heart rate recorded";
        return $"{cardio.Activity} at {At(cardio.Start)}, {Minutes(cardio.Minutes)}{heart}";
    }

    private static string Load(LoadFact load) =>
        string.Create(Invariant,
            $"Load: {load.HardSets.Acute:0} hard sets in {load.Sessions.Acute:0} sessions over the last 7 days, against {Usual(load)}{Ratio(load.HardSets)}.");

    private static string Usual(LoadFact load) => (load.HardSets, load.Sessions) switch
    {
        (LoadRatio.Compared sets, LoadRatio.Compared sessions) =>
            string.Create(Invariant, $"a weekly mean of {sets.Chronic:0} hard sets in {sessions.Chronic:0.#} sessions over the four weeks before"),
        _ => "nothing in the four weeks before",
    };

    private static string Ratio(LoadRatio ratio) =>
        ratio is LoadRatio.Compared { Chronic: > 0 } compared
            ? string.Create(Invariant, $" (acute:chronic {compared.Ratio:0.00})")
            : "";

    /// <summary>
    /// Yesterday's and today's movement outside training against his usual day: fatigue that earns
    /// no credit, so the model weighs a day on his feet without counting it as training.
    /// </summary>
    private static string Everyday(TodayFacts facts)
    {
        var days = new[] { (facts.Now.Yesterday, "yesterday"), (facts.Now.Today, "today so far") }
            .Select(day => (Read: facts.Everyday.Between(day.Item1, day.Item1).FirstOrDefault(), Label: day.Item2))
            .Where(day => day.Read is not null)
            .Select(day => $"{day.Label} {EverydayDay(day.Read!)}")
            .ToList();
        return days.Count is 0
            ? "Everyday load (movement outside training, fatigue but never training credit): not read from the ring."
            : $"Everyday load (movement outside training, fatigue but never training credit): {string.Join("; ", days)}.";
    }

    private static string EverydayDay(Model.Training.Load.EverydayDay day) => day.Standing switch
    {
        EverydayStanding.Judged judged => string.Create(Invariant,
            $"{day.MetMinutes:0} MET-min, {day.Ratio:0.0}x his usual day ({judged.Band switch { EverydayBand.FarAbove => "far above usual", EverydayBand.Above => "above usual", _ => "usual" }})"),
        EverydayStanding.Unjudged => string.Create(Invariant, $"{day.MetMinutes:0} MET-min (too few days yet to know his usual)"),
        _ => throw new UnreachableException(),
    };

    /// <summary>The zones in beats per minute, as the cardio report and the score draw them, so a target can be given in bpm.</summary>
    private static string Zones(CardioDays cardio) => cardio switch
    {
        CardioDays.Read { MaxHeartRate: { } max } => Zones(max),
        CardioDays.Read => "Heart-rate zones: no maximum heart rate on record (no usable age), so no zones.",
        CardioDays.Unread => "Heart-rate zones: the cardio report could not be read; GetCardioReport states them.",
        _ => throw new UnreachableException(),
    };

    private static string Zones(MaxHeartRate max)
    {
        var bpm = ZoneMinutes.LowerEdges.Select(edge => (int)Math.Round(edge * max.Bpm)).ToList();
        var basis = max switch
        {
            MaxHeartRate.AgePredicted { ObservedPeak: { } peak } => $"age-predicted; the ring's cardio samples over the last year reach {peak} at the 99th percentile",
            MaxHeartRate.AgePredicted => "age-predicted",
            MaxHeartRate.Observed => "the ring's own peak, above the age prediction",
            _ => "estimated",
        };
        return $"Heart-rate zones (maximum {max.Bpm} bpm, {basis}): zone 2 {bpm[1]}-{bpm[2] - 1}, zone 3 {bpm[2]}-{bpm[3] - 1}, zone 4 {bpm[3]}-{bpm[4] - 1}, zone 5 {bpm[4]}+ bpm.";
    }

    private static string Week(TodayFacts facts)
    {
        var days = Enumerable.Range(0, facts.Now.Today.DayNumber - facts.Now.WeekStart.DayNumber + 1)
            .Select(offset => facts.Now.WeekStart.AddDays(offset))
            .Select(day => $"{On(day, "ddd")} {Day(facts, day, detailed: false, nothing: "nothing").TrimEnd('.')}");
        return $"This week so far: {string.Join("; ", days)}.";
    }

    // ---- Recovery ----

    private static string Nights(TodayFacts facts)
    {
        var today = facts.Now.Today;
        var lastNight = facts.Nights.FirstOrDefault(night => night.Day == today);
        var before = facts.Nights.FirstOrDefault(night => night.Day == today.AddDays(-1));
        var baseline = facts.Baseline.Nights > 0
            ? string.Create(Invariant, $" Baseline over the {facts.Baseline.Nights} nights before: HRV {facts.Baseline.AverageHrv:0} ms, lowest heart rate {facts.Baseline.LowestHeartRate:0} bpm.")
            : "";
        return $"Sleep: last night {Night(lastNight)}; the night before {Night(before)}.{baseline}";
    }

    private static string Night(NightFact? night) =>
        night is null
            ? "not synced yet"
            : $"{At(night.Bedtime)}-{At(night.Wake)}, {Hours(night.TotalSleepSeconds)} asleep, efficiency {night.Efficiency}%"
                + (night.LowestHeartRate is { } low ? $", lowest heart rate {low} bpm" : "")
                + (night.AverageHrv is { } hrv ? $", HRV {hrv} ms" : "")
                + (night.SleepScore is { } score ? $", sleep score {score}" : "");

    private static string Readiness(ReadinessFact? readiness, WeekMeans means)
    {
        var week = means.Days > 0
            ? string.Create(Invariant, $" 7-day means: readiness {Score(means.Readiness)}, sleep score {Score(means.SleepScore)} ({means.Days} days).")
            : "";

        if (readiness?.Score is not { } score) return $"Readiness today: not synced yet.{week}";

        var usual = readiness is { UsualLow: { } low, UsualHigh: { } high } ? $" (usual {low}-{high} over 90 days)" : "";
        var parts = new List<string> { $"{score}/100{usual}" };
        if (readiness.HrvBalance is { } hrv) parts.Add($"HRV balance {hrv}/100");
        if (readiness.RestingHeartRate is { } rhr) parts.Add($"resting heart rate score {rhr}/100");
        if (readiness.TemperatureDeviation is { } temperature) parts.Add(string.Create(Invariant, $"temperature {Imperial.FahrenheitChange(temperature):+0.0;-0.0;0.0} °F"));
        return $"Readiness today (Oura's 1-100 scores, not measurements): {string.Join(", ", parts)}.{week}";
    }

    private static string Wellness(WellnessFact? wellness, DateOnly today) =>
        wellness is null
            ? "Wellness check-in: none in the last 7 days."
            : string.Create(Invariant,
                $"Wellness check-in ({(wellness.Date == today ? "today" : On(wellness.Date, "ddd d MMM"))}): overall {wellness.Overall:0.0}, mood {wellness.Mood}, energy {wellness.Energy}, stress {wellness.Stress}, motivation {wellness.Motivation}.");

    private static string Injuries(IReadOnlyList<InjuryFact> injuries) =>
        injuries.Count == 0
            ? "Active injuries: none."
            : $"Active injuries: {string.Join("; ", injuries.Select(injury => injury.LastPain is { } pain ? $"{injury.Name} (last pain {pain}/10)" : injury.Name))}.";

    // ---- Units ----

    private static string Offset(DateTimeOffset local) =>
        $"UTC{(local.Offset < TimeSpan.Zero ? "-" : "+")}{local.Offset.Duration():hh\\:mm}";

    private static string On(DateOnly day, string format) => day.ToString(format, Invariant);

    /// <summary>"today (travel)", "Sun 8 Nov": the day, and his note where he left one.</summary>
    private static string NonTrainingDay(NonTrainingFact day, DateOnly today) =>
        (day.Day == today ? "today" : On(day.Day, "ddd d MMM")) + (day.Note.Length > 0 ? $" ({day.Note})" : "");

    private static string At(DateTimeOffset instant) => instant.ToString("HH:mm", Invariant);

    private static string Minutes(double minutes) => string.Create(Invariant, $"{Math.Round(minutes):0} min");

    /// <summary>Rounded to the minute, as the app's screens read the same night.</summary>
    private static string Hours(int seconds)
    {
        var minutes = (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero);
        return $"{minutes / 60}h{minutes % 60:00}m";
    }

    private static string Score(double? score) => score is { } value ? string.Create(Invariant, $"{value:0}") : "n/a";
}
