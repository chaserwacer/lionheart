using System.Globalization;
using lionheart.Model;
using lionheart.Model.Training;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.GoingIn;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Score;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Focus;

namespace lionheart.Services.Training.Expectations;

/// <summary>
/// A priority lift's next top set: one step past last time's best set, unless what the athlete has
/// said about his last sessions, or how ready he is today, says hold it.
/// </summary>
/// <remarks>
/// Pure, so each signal is a test. One rule, kept to two outcomes so it can be read at a glance:
/// <list type="bullet">
/// <item>Hold last time's set when a deload is suggested for the week, when today's going-in rating
/// is under his usual day (under 65), when
/// the last top set was at RPE 9.5 or more, when he rated the last session of the lift 5 of 5 for
/// difficulty, or when the latest session of the week was rated 4 of 5 or more for accumulated
/// fatigue.</item>
/// <item>Otherwise step: five pounds or 2.5 kilograms, or a rep where the load is bodyweight. An easy
/// last session (RPE 8 or under, or difficulty 3 or under) is named as the reason.</item>
/// </list>
/// A pinned target is stated beside the aim — met, or how far — and a pin's sessions a week is the
/// lift's frequency, prorated to the days available like every other expectation.
/// </remarks>
public static class LiftAimAnalysis
{
    public const double HardRpe = 9.5;
    public const double EasyRpe = 8;
    public const int HardDifficulty = 5;
    public const int EasyDifficulty = 3;
    public const int TiredFatigue = 4;

    public static PriorityAim Of(
        TrainingLog log, PatternFamily family, PriorityLift.Chosen lift, DateOnly day, GoingInRating goingIn,
        AvailabilityCalendar week, Deload deload)
    {
        var (aim, reasons) = AimOf(log, lift, day, goingIn, week, deload);
        return new PriorityAim(
            family, lift.MovementDataId, lift.Name, aim, Frequency(log, lift, week), [.. reasons, .. TargetReasons(lift, aim)]);
    }

    private static (LiftAim Aim, IReadOnlyList<string> Reasons) AimOf(
        TrainingLog log, PriorityLift.Chosen lift, DateOnly day, GoingInRating goingIn, AvailabilityCalendar week, Deload deload)
    {
        if (lift.Series is not [.., var last])
            return (new LiftAim.Establish(), ["Nothing in the last twelve weeks to build on: work up to a top set of five."]);

        var unit = PriorityAnalysis.UnitOf(log, lift.MovementDataId, day);
        var onSystemLoad = PriorityAnalysis.OnSystemLoad(log, lift.MovementDataId, unit);
        var lastTime = LastTime(log, lift.MovementDataId, last);
        var holds = HoldReasons(goingIn, lastTime, LatestFatigue(log, week), deload).ToList();

        if (holds.Count > 0)
            return (new LiftAim.Hold(last.Load, last.Reps, unit, last.EstimatedOneRepMax, onSystemLoad), holds);

        var (load, reps) = onSystemLoad ? (last.Load, last.Reps + 1) : (last.Load + Step(unit), last.Reps);
        return (
            new LiftAim.Beat(
                load, reps, unit, new LiftPerformance(load, unit, reps).EstimatedOneRepMax,
                lift.Series.Max(best => best.EstimatedOneRepMax), onSystemLoad),
            [StepReason(lastTime, onSystemLoad)]);
    }

    /// <summary>What the athlete said about the last session of the lift: its top set's RPE and the session's difficulty.</summary>
    private sealed record LastSession(Rpe TopSetRpe, int? Difficulty);

    private static LastSession LastTime(TrainingLog log, Guid movementDataId, SessionBest best)
    {
        var sessions = log.Between(best.Day, best.Day)
            .Where(session => session.HardSets.Any(set => set.MovementDataId == movementDataId))
            .ToList();
        var rpe = sessions
            .SelectMany(session => session.HardSets)
            .Where(set => set.MovementDataId == movementDataId && set.Reps == best.Reps && set.Rpe.IsRated
                          && Math.Abs(log.LoadOf(set, best.Day) - best.Load) < 1e-6)
            .Select(set => set.Rpe)
            .DefaultIfEmpty(Rpe.Unrated)
            .MaxBy(rpe => rpe.IsRated ? rpe.Value : -1);
        var difficulty = sessions
            .Select(session => session.Effort is EffortReported reported ? reported.Ratings.DifficultyRating : null)
            .Max();
        return new LastSession(rpe, difficulty);
    }

    /// <summary>The accumulated fatigue of the latest session in the week that rated it.</summary>
    private static int? LatestFatigue(TrainingLog log, AvailabilityCalendar week) =>
        log.Between(week.Range.From, week.Range.To)
            .Select(session => session.Effort is EffortReported reported ? reported.Ratings.AccumulatedFatigue : null)
            .LastOrDefault(fatigue => fatigue is not null);

    private static IEnumerable<string> HoldReasons(GoingInRating goingIn, LastSession lastTime, int? fatigue, Deload deload)
    {
        if (deload is Deload.Suggested)
            yield return "Deload week suggested: hold the top set and do fewer sets.";
        if (goingIn is GoingInRating.Rated { Band: GoingInBand.Low or GoingInBand.Reduced } rated)
            yield return $"Going-in rating {rated.Score}, under your usual day: hold the weight.";
        if (lastTime.TopSetRpe is { IsRated: true } rpe && rpe.Value >= HardRpe)
            yield return $"Last top set at RPE {Figure(rpe.Value)}: hold it until it moves easier.";
        if (lastTime.Difficulty is >= HardDifficulty)
            yield return $"Last session of it rated {lastTime.Difficulty} of 5 for difficulty: hold the weight.";
        if (fatigue is >= TiredFatigue)
            yield return $"Accumulated fatigue rated {fatigue} of 5 this week: hold the weight.";
    }

    private static string StepReason(LastSession lastTime, bool onSystemLoad)
    {
        var step = onSystemLoad ? "one more rep" : "one step on";
        if (lastTime.TopSetRpe is { IsRated: true } rpe && rpe.Value <= EasyRpe)
            return $"Last top set at RPE {Figure(rpe.Value)}: {step}.";
        if (lastTime.Difficulty is { } difficulty and <= EasyDifficulty)
            return $"Last session of it rated {difficulty} of 5 for difficulty: {step}.";
        return $"Nothing says hold: {step} from last time's best set.";
    }

    /// <summary>A pinned lift target, met by the aim or how far off; nothing without one.</summary>
    /// <remarks>
    /// Only compared on plate weight in the same unit: on system load the aim carries bodyweight the
    /// target does not, and weights never convert.
    /// </remarks>
    private static IEnumerable<string> TargetReasons(PriorityLift.Chosen lift, LiftAim aim)
    {
        if (lift.Choice is not PriorityChoice.ByPin { Pin.Target: FocusTarget.Lift target }) yield break;

        var stated = $"Pinned target {target.Reps} × {Imperial.Weight(target.Weight, target.Unit)}";
        var aimed = aim switch
        {
            LiftAim.Beat { OnSystemLoad: false } beat when beat.Unit == target.Unit => (beat.Load, beat.Reps),
            LiftAim.Hold { OnSystemLoad: false } hold when hold.Unit == target.Unit => (hold.Load, hold.Reps),
            _ => ((double Load, int Reps)?)null,
        };
        yield return aimed switch
        {
            { } set when set.Load >= target.Weight && set.Reps >= target.Reps => $"{stated}: this set meets it.",
            { } set when set.Reps == target.Reps => $"{stated}: {Imperial.Weight(target.Weight - set.Load, target.Unit)} to go.",
            _ => $"{stated}.",
        };
    }

    /// <summary>Sessions of the week that trained the lift, against the pin's sessions a week where it asks.</summary>
    private static PriorityFrequency Frequency(TrainingLog log, PriorityLift.Chosen lift, AvailabilityCalendar week)
    {
        var sessions = log.Between(week.Range.From, week.Range.To)
            .Count(session => session.HardSets.Any(set => set.MovementDataId == lift.MovementDataId));
        if (lift.Choice is not PriorityChoice.ByPin pinned) return new PriorityFrequency.NotAsked(sessions);

        var standard = new Standard(pinned.Pin.SessionsPerWeek, pinned.Pin.SessionsPerWeek, 1);
        var raw = Progression.Fixed(standard);
        var adjusted = Progression.Prorated(raw, standard, week.AvailableDays);
        return new PriorityFrequency.Asked(new Expectation(
            sessions, raw, adjusted, [$"Pinned for {pinned.Pin.SessionsPerWeek} session{(pinned.Pin.SessionsPerWeek is 1 ? "" : "s")} a week.", .. ExpectationReasons.Adjusted(raw, adjusted, week, EverydayWeek.None)]));
    }

    private static double Step(WeightUnit unit) => unit switch
    {
        WeightUnit.Pounds => 5,
        WeightUnit.Kilograms => 2.5,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Figure(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
