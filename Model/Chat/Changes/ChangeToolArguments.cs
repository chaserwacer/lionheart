using System.ComponentModel;

namespace lionheart.Model.Chat.Changes;

// What the chat model sends to the propose tools. Shaped for a model rather than for a client: flat,
// in the athlete's units (lb), every field described, and every value it may leave out optional so a
// missing one is refused by name instead of binding to a default that means something else. A choice
// among words is a string, parsed where the change is drafted: the schema generator describes an enum
// as integers, which is not what a model should be asked to send. The descriptions are part of the
// prompt the model reads.

/// <summary>A planned session as the chat states it: the day, what is planned on it, and a note.</summary>
public sealed record PlannedSessionArguments
{
    [Description("The day the session is for, yyyy-MM-dd, on the athlete's calendar. Today or later.")]
    public DateOnly? Day { get; init; }

    [Description("Lifts to prescribe, in the order they are trained, each the same set repeated: list a movement again for different sets, such as a top set then back-off sets. Each needs the movementDataId of one of the athlete's own movements (FindMovements).")]
    public IReadOnlyList<PlannedLift>? Lifts { get; init; }

    [Description("What the session is, in words, one line each, for anything that is not a lift with numbers: \"Easy run, 4-5 miles, conversational\", \"Zone 2 ride, 60 minutes\", \"4 x 4 minute intervals at zone 4-5\". Short and specific.")]
    public IReadOnlyList<string>? Freeform { get; init; }

    [Description("The session's name on Train and Today, a few words: \"Group run with friends\", \"Legs before travel\". Optional; without it the session is named by its day.")]
    public string? Notes { get; init; }
}

/// <summary>One lift on a planned session: the same set prescribed a number of times.</summary>
/// <remarks>A top set and back-off sets are two entries for the same movement.</remarks>
public sealed record PlannedLift
{
    [Description("The movement's movementDataId.")]
    public Guid? MovementDataId { get; init; }

    [Description("How many sets of it, 1-10.")]
    public int? Sets { get; init; }

    [Description("Target reps per set, 1-50.")]
    public int? Reps { get; init; }

    [Description("Target weight per set in pounds; for a weighted bodyweight lift such as a dip or pull-up, the added load; 0 for bodyweight alone.")]
    public double? WeightLb { get; init; }

    [Description("Target RPE, 6-10 in half steps. Optional.")]
    public double? Rpe { get; init; }
}

/// <summary>A change to a planned session: which one, and the whole plan it becomes.</summary>
public sealed record PlanChangeArguments
{
    [Description("The planned session's trainingSessionId (GetPlannedDays). Only a session Tempo planned can be changed this way.")]
    public Guid? TrainingSessionId { get; init; }

    [Description("The whole plan as it should read afterwards, unchanged parts restated: a field left out is cleared.")]
    public PlannedSessionArguments? Plan { get; init; }
}

/// <summary>A non-training day as the chat states it.</summary>
public sealed record NonTrainingDayArguments
{
    [Description("The day, yyyy-MM-dd; for a weekly repeat, the first day it covers, whose weekday repeats.")]
    public DateOnly? Day { get; init; }

    [Description("\"once\" for that one day, \"weekly\" for its weekday every week from it.")]
    public string? Repeat { get; init; }

    [Description("For a weekly repeat only, the last day it covers, yyyy-MM-dd. Leave out for an open repeat.")]
    public DateOnly? Until { get; init; }

    [Description("What the athlete said about it, in a few words: \"travel\", \"sick\", \"exams\". At most 200 characters.")]
    public string? Note { get; init; }
}

/// <summary>A priority lift as the chat states it.</summary>
public sealed record PriorityLiftArguments
{
    [Description("The family it leads: \"push\", \"pull\" or \"legs\".")]
    public string? Family { get; init; }

    [Description("The lift's movementDataId (FindMovements).")]
    public Guid? MovementDataId { get; init; }

    [Description("The first day of its block, yyyy-MM-dd. Today when left out.")]
    public DateOnly? Starts { get; init; }

    [Description("The last day of its block, yyyy-MM-dd. Blocks usually run 4-8 weeks.")]
    public DateOnly? Ends { get; init; }

    [Description("Sessions a week to train it, 1-7.")]
    public int? SessionsPerWeek { get; init; }

    [Description("A target set to reach by the block's end, in pounds. Give it with targetReps, or leave both out.")]
    public double? TargetWeightLb { get; init; }

    [Description("The reps of the target set.")]
    public int? TargetReps { get; init; }
}

/// <summary>A new injury as the chat states it.</summary>
public sealed record InjuryArguments
{
    [Description("A short name for it, as the athlete would say it: \"left knee\", \"right shoulder impingement\".")]
    public string? Name { get; init; }

    [Description("The day it started, yyyy-MM-dd.")]
    public DateOnly? Day { get; init; }

    [Description("What happened and how it feels, in a sentence or two, in his words. Optional.")]
    public string? Notes { get; init; }
}

/// <summary>A check-in on an injury already tracked, as the chat states it.</summary>
public sealed record InjuryCheckInArguments
{
    [Description("The injury's injuryID (GetAllUserInjuries).")]
    public Guid? InjuryId { get; init; }

    [Description("Pain now, 0 (none) to 10 (worst), as the athlete rated it. Ask him if he has not said.")]
    public int? PainLevel { get; init; }

    [Description("True when this records a treatment (physio, massage, rehab work) rather than how it feels.")]
    public bool Treatment { get; init; }

    [Description("What he said about it, in a sentence. Optional.")]
    public string? Notes { get; init; }

    [Description("movementDataIds of movements that aggravated it, if he named any. Optional.")]
    public IReadOnlyList<Guid>? MovementDataIds { get; init; }
}
