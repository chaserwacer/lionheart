using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Load;

/// <summary>
/// The week's load as the expectations manage it: the lifting against his usual and whether a
/// deload is suggested, the cardio's ramp, and the everyday load of its days.
/// </summary>
/// <remarks>
/// Three parts because they move the plan three ways: lifting suggests a lighter week, cardio holds
/// a target back after a spike, and everyday load eases the week and the next session a little.
/// Every one reads load the same way (<see cref="LoadRatio"/>) against the same four weeks the
/// targets grow from.
/// </remarks>
public sealed record WeekLoad(LiftingLoad Lifting, CardioLoad Cardio, EverydayWeek Everyday);

/// <summary>The window's hard sets against the four weeks before, and the deload the calendar week is offered.</summary>
/// <param name="HardSets">Every hard set of the window, classified or not, against the four blocks before.</param>
public sealed record LiftingLoad(LoadRatio HardSets, RampStanding Ramp, Deload Deload);

/// <summary>
/// Whether the calendar week holding the window's last day is offered a deload: a lighter week
/// after a run of building ones.
/// </summary>
/// <remarks>
/// Only ever suggested, never applied: nothing is planned or changed for him. What a suggestion
/// does is say, in the week's adjusted targets, that a lighter week is the plan, so following it
/// reads as doing the plan rather than falling short. Whether he followed is read from the week's
/// hard sets so far, so it can change through the week; the suggestion itself is decided from the
/// weeks before the Monday and cannot.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NotSuggested), "notSuggested")]
[JsonDerivedType(typeof(Suggested), "suggested")]
public abstract record Deload
{
    private protected Deload() { }

    /// <summary>Building weeks in a row before the Monday, from the newest back.</summary>
    public abstract int BuildingWeeks { get; }

    public abstract IReadOnlyList<string> Reasons { get; }

    public sealed record NotSuggested(int BuildingWeeks, IReadOnlyList<string> Reasons) : Deload
    {
        public override int BuildingWeeks { get; } = BuildingWeeks;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <param name="WeekStarting">The Monday of the week the deload is suggested for.</param>
    /// <param name="Ceiling">The most hard sets the week holds and still reads as a deload: 70 % of his usual week.</param>
    /// <param name="SoFar">Hard sets from the Monday through the window's last day.</param>
    public sealed record Suggested(
        DateOnly WeekStarting, int BuildingWeeks, double Ceiling, int SoFar, IReadOnlyList<string> Reasons) : Deload
    {
        public override int BuildingWeeks { get; } = BuildingWeeks;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;

        /// <summary>Whether the week so far is within the deload: what "following it" means.</summary>
        public bool Following => SoFar <= Ceiling;
    }
}

/// <summary>The window's cardio against the four weeks before: all of its minutes, and its running distance.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Read), "read")]
[JsonDerivedType(typeof(Unread), "unread")]
public abstract record CardioLoad
{
    private protected CardioLoad() { }

    /// <param name="Minutes">Every cardio session's minutes.</param>
    /// <param name="RunningMeters">The distance of runs: logged runs, indoor runs and the ring's runs.</param>
    public sealed record Read(LoadRamp Minutes, LoadRamp RunningMeters) : CardioLoad;

    /// <summary>The window's cardio could not be read, so its ramp cannot be either.</summary>
    public sealed record Unread : CardioLoad;
}

/// <summary>A figure against the four weeks before, and how steep that is for the measure.</summary>
public sealed record LoadRamp(LoadRatio Ratio, RampStanding Standing);

/// <summary>The everyday load of the window's days, and how much of a day the far-above ones took off the targets.</summary>
/// <param name="Days">The window's days with a ring reading, oldest first.</param>
/// <param name="EasedDays">
/// Half a day for each day far above his usual, once two or more are: what the adjusted targets
/// prorate away. Zero for a single big day, which moves nothing but the next session.
/// </param>
public sealed record EverydayWeek(IReadOnlyList<EverydayDay> Days, double EasedDays)
{
    public static readonly EverydayWeek None = new([], 0);

    [JsonIgnore]
    public int FarAboveDays => Days.Count(day => day.IsFarAbove);
}
