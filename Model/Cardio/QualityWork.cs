using System.Text.Json.Serialization;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Cardio;

/// <summary>What kind of movement a cardio session was, as far as where its effort came from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CardioModality>))]
public enum CardioModality
{
    /// <summary>Running, riding, rowing, swimming, skiing, a machine: effort the athlete chooses and holds.</summary>
    [JsonStringEnumMemberName("endurance")] Endurance,

    /// <summary>Walks and hikes, where the terrain and the pack set the effort.</summary>
    [JsonStringEnumMemberName("onFoot")] OnFoot,

    /// <summary>A game or dance, where play sets the effort.</summary>
    [JsonStringEnumMemberName("game")] Game,

    /// <summary>Nobody named it.</summary>
    [JsonStringEnumMemberName("unnamed")] Unnamed,
}

/// <summary>
/// The rule that decides which high heart rate earns intensity credit: work bouts, in a session of a
/// modality built for intervals and tempo, adding up to a session's worth.
/// </summary>
/// <remarks>
/// <para>
/// Heart rate alone cannot tell a stimulus from a circumstance. Heat, dehydration, caffeine,
/// stress and the excitement of a crowd all raise it with no matching rise in work (Achten and
/// Jeukendrup 2003), and a night of dancing can hold zone 4 for longer than an interval session.
/// What separates training is intent and structure, which is how coaches count intensity: by the
/// session's goal rather than seconds in a zone (Seiler and Kjerland 2006; Sylta, Tønnessen and
/// Seiler 2014).
/// </para>
/// <para>
/// So quality work is counted only where all three hold: the session is an endurance modality
/// (<see cref="CardioModality.Endurance"/>), its heart rate shows work bouts (<see cref="WorkBouts"/>),
/// and those bouts add up to at least <see cref="LeastSessionMinutes"/>. Ten minutes is the least
/// a prescribed hard session accumulates — five bouts of two minutes, or a ten-minute threshold
/// block; the work that adapts is accumulated time near 90 % of maximum heart rate, 16 to 32
/// minutes a session in the trials (Helgerud et al. 2007; Seiler et al. 2013).
/// </para>
/// </remarks>
public static class QualityWork
{
    public const double LeastSessionMinutes = 10;

    /// <summary>The modality of a session, from what the log or the ring called it.</summary>
    public static CardioModality ModalityOf(CardioSessionSource source) => source switch
    {
        CardioSessionSource.LoggedSession { IsTrailRun: true } => CardioModality.Endurance,
        CardioSessionSource.LoggedSession { Activity: EnduranceActivity.Hike or EnduranceActivity.Walk } => CardioModality.OnFoot,
        CardioSessionSource.LoggedSession => CardioModality.Endurance,
        CardioSessionSource.RingWorkout { Activity: "hiking" } => CardioModality.OnFoot,
        CardioSessionSource.RingWorkout ring => ring.Category switch
        {
            WorkoutCategory.Endurance => CardioModality.Endurance,
            WorkoutCategory.Walk => CardioModality.OnFoot,
            WorkoutCategory.Sport => CardioModality.Game,
            WorkoutCategory.Strength or WorkoutCategory.Incidental or WorkoutCategory.Unspecified
                or WorkoutCategory.Unrecognised => CardioModality.Unnamed,
            _ => throw new System.Diagnostics.UnreachableException(),
        },
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>The minutes of a session that earn intensity credit: its bouts, or nothing.</summary>
    public static double MinutesOf(CardioSessionSource source, SessionHeartRate heartRate)
    {
        if (ModalityOf(source) is not CardioModality.Endurance) return 0;
        if (heartRate is not SessionHeartRate.Recorded recorded) return 0;

        var work = recorded.Bouts.Sum(bout => bout.Minutes);
        return work >= LeastSessionMinutes ? work : 0;
    }
}
