using System.Text.Json.Serialization;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training.Next;

/// <summary>
/// What to train next: the next lifting session, the next cardio session, and which of the two
/// makes more sense first, each with its reasons so the athlete can disagree with them.
/// </summary>
/// <remarks>
/// A recommendation read from the log, the week's score and the night's recovery, never an
/// instruction: every choice carries the facts it was made from.
/// </remarks>
/// <param name="Day">The day the recommendation is for: the window's last day, which may already hold a session.</param>
public sealed record NextSessions(DateOnly Day, FirstChoice First, NextLift Lift, NextCardio Cardio, RecoveryRead Recovery);

/// <summary>Which of the two to do next.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NextKind>))]
public enum NextKind
{
    [JsonStringEnumMemberName("lift")] Lift,
    [JsonStringEnumMemberName("cardio")] Cardio,
}

public sealed record FirstChoice(NextKind Session, IReadOnlyList<string> Reasons);

/// <summary>The next lifting session: the family or families due, and each one's priority lift with an aim.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Train), "train")]
[JsonDerivedType(typeof(NothingLogged), "nothingLogged")]
public abstract record NextLift
{
    private protected NextLift() { }

    public abstract IReadOnlyList<string> Reasons { get; }

    /// <param name="Families">The families due, trained longest ago, together when last trained the same day.</param>
    /// <param name="Priorities">Each due family's priority lift, where it has one.</param>
    public sealed record Train(
        IReadOnlyList<PatternFamily> Families,
        DateOnly? LastTrained,
        IReadOnlyList<PriorityAim> Priorities,
        IReadOnlyList<string> Reasons) : NextLift
    {
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>No classified lifting in the history, so there is no order to read.</summary>
    public sealed record NothingLogged(IReadOnlyList<string> Reasons) : NextLift
    {
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }
}

/// <summary>The next cardio session: aerobic base, or a quality session with its structure.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Zone2), "zone2")]
[JsonDerivedType(typeof(Quality), "quality")]
public abstract record NextCardio
{
    private protected NextCardio() { }

    public abstract IReadOnlyList<string> Reasons { get; }

    /// <summary>Continuous work at 60–70 % of maximum heart rate for the minutes given.</summary>
    public sealed record Zone2(int Minutes, IReadOnlyList<string> Reasons) : NextCardio
    {
        public override IReadOnlyList<string> Reasons { get; } = Reasons;
    }

    /// <summary>
    /// Intervals at zone 4–5 — 80 % of maximum heart rate and above — with easy recoveries between,
    /// after a warm-up.
    /// </summary>
    public sealed record Quality(int Bouts, int BoutMinutes, int RecoveryMinutes, IReadOnlyList<string> Reasons) : NextCardio
    {
        public override IReadOnlyList<string> Reasons { get; } = Reasons;

        /// <summary>The minutes of work the session holds.</summary>
        public int WorkMinutes => Bouts * BoutMinutes;
    }
}

/// <summary>Last night and this morning as the recommendation read them.</summary>
/// <param name="SleepMinutes">The main sleep filed under the day; absent when none is synced.</param>
/// <param name="Hrv">That night's average HRV in milliseconds.</param>
/// <param name="UsualHrv">The mean of the 28 nights before it.</param>
/// <param name="HrvSpread">Their standard deviation, which a low night is read against.</param>
public sealed record RecoveryRead(ReadinessNote Readiness, int? SleepMinutes, int? Hrv, double? UsualHrv, double? HrvSpread)
{
    public static readonly RecoveryRead NotRead = new(new ReadinessNote.NotRead(), null, null, null, null);
}
