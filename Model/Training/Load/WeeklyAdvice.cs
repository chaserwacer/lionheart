using System.Text.Json.Serialization;
using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training.Load;

/// <summary>
/// The one piece of advice a calendar week carries: chosen by fixed rules from the state of the
/// weeks before its Monday, so it reads the same every day of the week.
/// </summary>
/// <remarks>
/// <para>
/// Deterministic server rules, never the model, so a page that shows it spends nothing and the chat
/// is told the same words. One per week, because a banner that changed daily would be a forecast
/// rather than advice; that is also why it reads only the weeks that ended before the Monday,
/// plus what was already true on the Monday — an injury dated that day or before, a deload
/// suggested for the week.
/// </para>
/// <para>
/// A closed set of cases, each carrying what its words were made from, because the cases are what
/// a reader branches on (an injury is said differently to a ramp) and a free string would lose it.
/// When several hold, the first in this order wins: an injury, a deload, a steep ramp, big
/// everyday days, the balance of the last week, a stalled priority, keep building.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ManageInjury), "manageInjury")]
[JsonDerivedType(typeof(TakeDeload), "takeDeload")]
[JsonDerivedType(typeof(EaseRamp), "easeRamp")]
[JsonDerivedType(typeof(EaseAfterEverydayLoad), "easeAfterEverydayLoad")]
[JsonDerivedType(typeof(EmphasiseCardio), "emphasiseCardio")]
[JsonDerivedType(typeof(EmphasiseLifting), "emphasiseLifting")]
[JsonDerivedType(typeof(PriorityStalled), "priorityStalled")]
[JsonDerivedType(typeof(KeepBuilding), "keepBuilding")]
public abstract record WeeklyAdvice
{
    private protected WeeklyAdvice() { }

    /// <summary>The Monday of the week the advice is for.</summary>
    public abstract DateOnly WeekStarting { get; }

    /// <summary>The advice, in a short line.</summary>
    public abstract string Headline { get; }

    /// <summary>Why, from the weeks it was read from.</summary>
    public abstract IReadOnlyList<string> Reasons { get; }

    /// <summary>An active injury touches a family: hold its volume while it is managed, then ramp back.</summary>
    public sealed record ManageInjury(
        DateOnly WeekStarting, string Injury, IReadOnlyList<PatternFamily> Families, string Headline, IReadOnlyList<string> Reasons)
        : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>A deload is suggested for the week: fewer sets, the same weights.</summary>
    public sealed record TakeDeload(DateOnly WeekStarting, int BuildingWeeks, string Headline, IReadOnlyList<string> Reasons) : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>Last week's cardio or running rose steeply over the four before it: hold it this week.</summary>
    public sealed record EaseRamp(DateOnly WeekStarting, RampedMeasure Measure, LoadRatio.Compared LastWeek, string Headline, IReadOnlyList<string> Reasons)
        : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>Last week held days of everyday load far above his usual: start this one easy.</summary>
    public sealed record EaseAfterEverydayLoad(DateOnly WeekStarting, int FarAboveDays, string Headline, IReadOnlyList<string> Reasons)
        : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>Last week's lifting met its targets and its cardio fell well short: more cardio this week.</summary>
    public sealed record EmphasiseCardio(DateOnly WeekStarting, string Headline, IReadOnlyList<string> Reasons) : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>Last week's cardio met its targets and its lifting fell well short: more lifting this week.</summary>
    public sealed record EmphasiseLifting(DateOnly WeekStarting, string Headline, IReadOnlyList<string> Reasons) : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>A priority lift trained every week of the last four without a new best.</summary>
    public sealed record PriorityStalled(DateOnly WeekStarting, PatternFamily Family, string Lift, string Headline, IReadOnlyList<string> Reasons)
        : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>Nothing above holds: build on the weeks before, the target furthest behind named.</summary>
    public sealed record KeepBuilding(DateOnly WeekStarting, string Headline, IReadOnlyList<string> Reasons) : WeeklyAdvice
    {
        public override DateOnly WeekStarting { get; } = WeekStarting;
        public override string Headline { get; } = Headline;
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }
}

/// <summary>Which cardio figure ran steep.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RampedMeasure>))]
public enum RampedMeasure
{
    [JsonStringEnumMemberName("running")] Running,
    [JsonStringEnumMemberName("cardioMinutes")] CardioMinutes,
}
