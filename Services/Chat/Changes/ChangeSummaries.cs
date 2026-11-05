using System.Globalization;
using lionheart.Model;
using lionheart.Model.Chat.Changes;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Chat.Changes;

/// <summary>
/// The card for each change, in the athlete's words and units: what it sets, and what it replaces
/// or lets go of. Pure, over names the drafter has already resolved, so the words are a function of
/// the change and nothing the model wrote.
/// </summary>
public static class ChangeSummaries
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ProposalSummary PlanSession(PlanSessionRequest plan, IReadOnlyList<SetPrescription> prescriptions, MovementNameBook names) =>
        new($"Plan a session on {Day(plan.PlannedFor)}", [.. Plan(prescriptions, names), .. NoteLine(plan.Notes)]);

    /// <param name="before">The session as it is planned now.</param>
    public static ProposalSummary RevisePlan(
        PlannedTrainingSession before, RevisePlanRequest after, IReadOnlyList<SetPrescription> prescriptions, MovementNameBook names) =>
        new($"Change the session planned for {Day(before.PlannedFor)}",
        [
            .. before.PlannedFor == after.PlannedFor ? [] : new[] { $"Moves to {Day(after.PlannedFor)}" },
            .. Plan(prescriptions, names),
            .. NoteLine(after.Notes),
            $"Replaces: {string.Join("; ", Plan(before.Prescriptions, names).DefaultIfEmpty("nothing prescribed"))}",
        ]);

    public static ProposalSummary RemovePlan(PlannedTrainingSession planned, MovementNameBook names) =>
        new($"Remove the session planned for {Day(planned.PlannedFor)}", [.. Plan(planned.Prescriptions, names), .. NoteLine(planned.Notes)]);

    public static ProposalSummary MarkNonTrainingDay(NonTrainingSchedule schedule, string note) =>
        new($"Mark {Days(schedule)} as non-training", [.. Span(schedule), .. NoteLine(note)]);

    public static ProposalSummary FreeNonTrainingDay(NonTrainingDay marked) =>
        new($"Free {Days(marked.Schedule)} for training", [.. Span(marked.Schedule), .. NoteLine(marked.Note)]);

    /// <param name="superseded">The family's priority now, which pinning this one lets go of.</param>
    public static ProposalSummary PinPriorityLift(
        PatternFamily family, string lift, FocusTerms terms, string? superseded) =>
        new($"Make {lift} the {family.ToString().ToLowerInvariant()} priority",
        [
            $"Block: {Day(terms.Horizon.Starts)} to {Day(terms.Horizon.Ends)}",
            $"{terms.SessionsPerWeek} {(terms.SessionsPerWeek == 1 ? "session" : "sessions")} a week",
            .. terms.Target is FocusTarget.Lift target
                ? new[] { $"Target: {Imperial.Weight(target.Weight, target.Unit)} x {target.Reps}" }
                : [],
            .. superseded is { } previous ? new[] { $"Lets go of {previous} as the {family.ToString().ToLowerInvariant()} priority" } : [],
        ]);

    public static ProposalSummary LogInjury(string name, DateOnly started, string notes) =>
        new($"Log an injury: {name}", [$"Started {Day(started)}", .. NoteLine(notes)]);

    public static ProposalSummary LogInjuryCheckIn(string injury, int pain, bool treatment, string notes, IReadOnlyList<string> movements) =>
        new(treatment ? $"Log a treatment for {injury}" : $"Check in on {injury}",
        [
            $"Pain {pain} of 10",
            .. NoteLine(notes),
            .. movements.Count == 0 ? [] : new[] { $"Aggravated by {string.Join(", ", movements)}" },
        ]);

    /// <summary>A day as the athlete reads it on the card and in the briefing: "Wed 11 Nov".</summary>
    public static string Day(DateOnly day) => day.ToString("ddd d MMM", Invariant);

    /// <summary>
    /// What a plan prescribes, one line per lift or freeform line. Runs of the same set read as
    /// sets times reps, the way he writes them: "Hack Squat (Machine): 3 x 8 at 230 lb, RPE 8".
    /// </summary>
    public static IReadOnlyList<string> Plan(IReadOnlyList<SetPrescription> prescriptions, MovementNameBook names) =>
        [.. Runs(prescriptions).Select(run => Line(run.Prescription, run.Count, names))];

    private static string Line(SetPrescription prescription, int count, MovementNameBook names) => prescription switch
    {
        LiftPrescription lift =>
            $"{names.NameOf(lift.MovementDataId)}: {count} x {lift.TargetReps} at {Load(lift)}{Rated(lift.TargetRpe)}",
        DistanceTimePrescription distance =>
            $"{names.NameOf(distance.MovementDataId)}: {Imperial.Distance(distance.TargetDistance, distance.Unit)} in {(int)distance.TargetDuration.TotalMinutes} min"
            + (count > 1 ? $", {count} times" : string.Empty),
        FreeformPrescription freeform => freeform.Text,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Load(LiftPrescription lift) =>
        lift.TargetWeight == 0 ? "bodyweight" : Imperial.Weight(lift.TargetWeight, lift.Unit);

    private static string Rated(Rpe rpe) =>
        rpe.IsRated ? $", RPE {rpe.Value.ToString("0.#", Invariant)}" : string.Empty;

    private static IEnumerable<(SetPrescription Prescription, int Count)> Runs(IReadOnlyList<SetPrescription> prescriptions) =>
        prescriptions.Aggregate(
            new List<(SetPrescription Prescription, int Count)>(),
            (runs, next) =>
            {
                if (runs.Count > 0 && runs[^1].Prescription == next) runs[^1] = (next, runs[^1].Count + 1);
                else runs.Add((next, 1));
                return runs;
            });

    private static string Days(NonTrainingSchedule schedule) => schedule switch
    {
        NonTrainingSchedule.OneDay one => Day(one.Day),
        NonTrainingSchedule.EveryWeek every => $"every {every.From.DayOfWeek}",
        NonTrainingSchedule.EveryWeekUntil until => $"every {until.From.DayOfWeek}",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static IReadOnlyList<string> Span(NonTrainingSchedule schedule) => schedule switch
    {
        NonTrainingSchedule.OneDay => [],
        NonTrainingSchedule.EveryWeek every => [$"From {Day(every.From)}, with no last day"],
        NonTrainingSchedule.EveryWeekUntil until => [$"From {Day(until.From)} until {Day(until.Until)}"],
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static IReadOnlyList<string> NoteLine(string note) =>
        string.IsNullOrWhiteSpace(note) ? [] : [$"Note: {note.Trim()}"];
}
