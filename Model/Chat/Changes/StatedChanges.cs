using lionheart.Functional;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Chat.Changes;

/// <summary>
/// What the model sent, read into the request the page's service takes, or refused naming the
/// field — the model's cue to call again with it put right. Only what the arguments alone can show;
/// whatever needs the athlete's data (his movements, his sessions, today) is checked where the
/// change is drafted.
/// </summary>
public static class StatedChanges
{
    public const int MostSetsPerLift = 10;
    public const int MostRepsPerSet = 50;
    public const int MostLineLength = 200;
    public const int MostNotesLength = 500;

    /// <summary>A planned session's day, prescriptions and notes. It has to say what it is: a lift or a line.</summary>
    public static Result<StatedPlan, Problem> Plan(PlannedSessionArguments? arguments) =>
        Present(arguments, "Plan", "Give the plan.")
            .Bind(plan => Present(plan.Day, nameof(plan.Day), "A planned session names its day.")
                .Bind(day => (plan.Lifts ?? []).Traverse(Lift)
                    .Bind(lifts => (plan.Freeform ?? []).Traverse(Line)
                        .Ensure(
                            lines => lifts.Count > 0 || lines.Count > 0,
                            _ => new Problem.Invalid("Lifts", "Say what the session is: at least one lift or one freeform line."))
                        .Bind(lines => Notes(plan.Notes)
                            .Map(notes => new StatedPlan(
                                day,
                                [.. lifts.SelectMany(sets => sets), .. lines.Select(line => new FreeformPrescriptionCommand(line))],
                                notes))))));

    public static Result<MarkNonTrainingDayRequest, Problem> NonTrainingDay(NonTrainingDayArguments arguments) =>
        Repeat(arguments.Repeat)
            .Map(repeat => new MarkNonTrainingDayRequest(arguments.Day, repeat, arguments.Until, arguments.Note))
            // The page's own request checks the schedule and the note, so a refusal reads the same from either door.
            .Bind(request => request.ToSchedule().Bind(_ => request.ToNote()).Map(_ => request));

    /// <param name="today">The athlete's today, the block's first day when the model leaves it out.</param>
    public static Result<StatedPriority, Problem> PriorityLift(PriorityLiftArguments arguments, DateOnly today) =>
        Family(arguments.Family)
            .Bind(family => Present(arguments.MovementDataId, nameof(arguments.MovementDataId), "Name the lift by its movementDataId.")
                .Bind(movementDataId => Present(arguments.Ends, nameof(arguments.Ends), "Give the block's last day.")
                    .Bind(ends => Present(arguments.SessionsPerWeek, nameof(arguments.SessionsPerWeek), "Give the sessions a week.")
                        .Bind(sessionsPerWeek => Target(arguments.TargetWeightLb, arguments.TargetReps)
                            .Map(target => new StatedPriority(family, new PinFocusRequest(
                                movementDataId, arguments.Starts ?? today, ends, sessionsPerWeek, target, family)))))));

    public static Result<CreateInjuryRequest, Problem> Injury(InjuryArguments arguments) =>
        Words(arguments.Name, nameof(arguments.Name), "Name the injury.")
            .Bind(name => Present(arguments.Day, nameof(arguments.Day), "Give the day it started.")
                .Bind(day => Notes(arguments.Notes)
                    .Map(notes => new CreateInjuryRequest
                    {
                        Name = name,
                        InjuryDate = day.ToDateTime(TimeOnly.MinValue),
                        Notes = notes,
                    })));

    public static Result<CreateInjuryEventRequest, Problem> InjuryCheckIn(InjuryCheckInArguments arguments) =>
        Present(arguments.InjuryId, nameof(arguments.InjuryId), "Name the injury by its injuryID.")
            .Bind(injuryId => Present(arguments.PainLevel, nameof(arguments.PainLevel), "Give his pain, 0 to 10.")
                .Ensure(pain => pain is >= 0 and <= 10, _ => new Problem.Invalid(nameof(arguments.PainLevel), "Pain is 0 to 10."))
                .Bind(pain => Notes(arguments.Notes)
                    .Map(notes => new CreateInjuryEventRequest
                    {
                        InjuryID = injuryId,
                        PainLevel = pain,
                        InjuryType = arguments.Treatment ? InjuryEventType.treatment : InjuryEventType.checkin,
                        Notes = notes,
                        MovementIDs = [.. (arguments.MovementDataIds ?? []).Distinct()],
                    })));

    // ---- Parts ----

    private static Result<IReadOnlyList<SetPrescriptionCommand>, Problem> Lift(PlannedLift lift) =>
        Present(lift.MovementDataId, nameof(lift.MovementDataId), "Each lift names its movementDataId.")
            .Bind(movementDataId => Present(lift.Sets, nameof(lift.Sets), "Each lift gives its sets.")
                .Ensure(sets => sets is >= 1 and <= MostSetsPerLift, _ => new Problem.Invalid(nameof(lift.Sets), $"Sets are 1 to {MostSetsPerLift}."))
                .Bind(sets => Present(lift.Reps, nameof(lift.Reps), "Each lift gives its reps.")
                    .Ensure(reps => reps is >= 1 and <= MostRepsPerSet, _ => new Problem.Invalid(nameof(lift.Reps), $"Reps are 1 to {MostRepsPerSet}."))
                    .Bind(reps => Present(lift.WeightLb, nameof(lift.WeightLb), "Each lift gives its weight in pounds; 0 for bodyweight.")
                        .Ensure(weight => double.IsFinite(weight) && weight >= 0, _ => new Problem.Invalid(nameof(lift.WeightLb), "A weight is 0 lb or more."))
                        .Bind(weight => Repeated(
                            new LiftPrescriptionCommand(movementDataId, reps, weight, WeightUnit.Pounds, lift.Rpe), sets)))));

    /// <summary>The same set, as many times as asked, once the page's own check has passed it.</summary>
    private static Result<IReadOnlyList<SetPrescriptionCommand>, Problem> Repeated(LiftPrescriptionCommand set, int times) =>
        set.ToPrescription().Map(_ => (IReadOnlyList<SetPrescriptionCommand>)[.. Enumerable.Repeat<SetPrescriptionCommand>(set, times)]);

    private static Result<string, Problem> Line(string line) =>
        Words(line, "Freeform", "A freeform line says something.")
            .Ensure(words => words.Length <= MostLineLength, _ => new Problem.Invalid("Freeform", $"A freeform line is at most {MostLineLength} characters."));

    private static Result<string, Problem> Notes(string? notes)
    {
        var trimmed = notes?.Trim() ?? string.Empty;
        return trimmed.Length <= MostNotesLength
            ? Result<string, Problem>.Success(trimmed)
            : Result<string, Problem>.Failure(new Problem.Invalid("Notes", $"Notes are at most {MostNotesLength} characters."));
    }

    private static Result<NonTrainingRepeat, Problem> Repeat(string? repeat) => repeat?.Trim().ToLowerInvariant() switch
    {
        null or "" or "once" => Result<NonTrainingRepeat, Problem>.Success(NonTrainingRepeat.Once),
        "weekly" => Result<NonTrainingRepeat, Problem>.Success(NonTrainingRepeat.Weekly),
        _ => Result<NonTrainingRepeat, Problem>.Failure(new Problem.Invalid("Repeat", "Repeat is \"once\" or \"weekly\".")),
    };

    private static Result<PatternFamily, Problem> Family(string? family) => family?.Trim().ToLowerInvariant() switch
    {
        "push" => Result<PatternFamily, Problem>.Success(PatternFamily.Push),
        "pull" => Result<PatternFamily, Problem>.Success(PatternFamily.Pull),
        "legs" => Result<PatternFamily, Problem>.Success(PatternFamily.Legs),
        _ => Result<PatternFamily, Problem>.Failure(new Problem.Invalid("Family", "A priority leads \"push\", \"pull\" or \"legs\".")),
    };

    private static Result<LiftTargetRequest?, Problem> Target(double? weight, int? reps) => (weight, reps) switch
    {
        (null, null) => Result<LiftTargetRequest?, Problem>.Success(null),
        ({ } pounds, { } count) => Result<LiftTargetRequest?, Problem>.Success(new LiftTargetRequest(pounds, WeightUnit.Pounds, count)),
        _ => Result<LiftTargetRequest?, Problem>.Failure(new Problem.Invalid("TargetReps", "A target gives both its weight and its reps, or neither.")),
    };

    private static Result<string, Problem> Words(string? text, string field, string message) =>
        string.IsNullOrWhiteSpace(text)
            ? Result<string, Problem>.Failure(new Problem.Invalid(field, message))
            : Result<string, Problem>.Success(text.Trim());

    private static Result<T, Problem> Present<T>(T? value, string field, string message) where T : struct =>
        value is { } present
            ? Result<T, Problem>.Success(present)
            : Result<T, Problem>.Failure(new Problem.Invalid(field, message));

    private static Result<T, Problem> Present<T>(T? value, string field, string message) where T : class =>
        value is { } present
            ? Result<T, Problem>.Success(present)
            : Result<T, Problem>.Failure(new Problem.Invalid(field, message));
}

/// <summary>A priority pin as the model stated it: the family it leads, and the page's request to pin it.</summary>
public sealed record StatedPriority(PatternFamily Family, PinFocusRequest Request);

/// <summary>A plan as the model stated it, checked: its day, its prescriptions, its notes.</summary>
public sealed record StatedPlan(DateOnly Day, IReadOnlyList<SetPrescriptionCommand> Prescriptions, string Notes)
{
    public PlanSessionRequest ToPlanRequest() => new(Day, Prescriptions, Notes, TrainingProgramId: null);

    public RevisePlanRequest ToReviseRequest() => new(Day, Prescriptions, Notes);
}
