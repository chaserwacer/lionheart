using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Load;

/// <summary>
/// One day's everyday load: the movement the ring caught outside training, in MET-minutes, and how
/// it stands against the athlete's own usual day.
/// </summary>
/// <remarks>
/// Fatigue, never credit: a day on his feet costs recovery whatever earned it, but only training
/// earns the score's points. Measured against his own days, so an ordinary active day moves
/// nothing; only a day far outside his usual has any effect, and a small one.
/// </remarks>
/// <param name="MetMinutes">Oura's low, medium and high activity outside any training session, as MET-minutes above rest.</param>
public sealed record EverydayDay(DateOnly Day, double MetMinutes, EverydayStanding Standing)
{
    /// <summary>The day's MET-minutes over his usual day's; zero when there is no usual to compare it with.</summary>
    [JsonIgnore]
    public double Ratio => Standing is EverydayStanding.Judged { Usual: > 0 } judged ? MetMinutes / judged.Usual : 0;

    [JsonIgnore]
    public bool IsFarAbove => Standing is EverydayStanding.Judged { Band: EverydayBand.FarAbove };

    [JsonIgnore]
    public bool IsAbove => Standing is EverydayStanding.Judged { Band: EverydayBand.Above or EverydayBand.FarAbove };
}

/// <summary>A day's everyday load against his usual, or that there were too few days to know his usual.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Judged), "judged")]
[JsonDerivedType(typeof(Unjudged), "unjudged")]
public abstract record EverydayStanding
{
    private protected EverydayStanding() { }

    /// <param name="Usual">The median of his complete days in the 90 before.</param>
    /// <param name="High">Their 90th percentile: a day over it is above his usual.</param>
    public sealed record Judged(double Usual, double High, EverydayBand Band) : EverydayStanding;

    /// <summary>Fewer than four weeks of complete days before it to say what usual is.</summary>
    public sealed record Unjudged : EverydayStanding;
}

/// <summary>Where a day's everyday load falls against his own days.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EverydayBand>))]
public enum EverydayBand
{
    /// <summary>At or under the 90th percentile of his days: nothing to say.</summary>
    [JsonStringEnumMemberName("usual")] Usual,

    /// <summary>Over his 90th percentile: a busy day, stated but with almost no effect.</summary>
    [JsonStringEnumMemberName("above")] Above,

    /// <summary>
    /// Far above: over his 90th percentile by half again. The band whose days ease the week and
    /// lean the next session easier.
    /// </summary>
    [JsonStringEnumMemberName("farAbove")] FarAbove,
}

/// <summary>The everyday load of a run of days, each day read on its own; a day without a ring reading is absent.</summary>
public sealed record EverydayLoad(IReadOnlyList<EverydayDay> Days)
{
    public static readonly EverydayLoad None = new([]);

    public EverydayDay? On(DateOnly day) => Days.FirstOrDefault(known => known.Day == day);

    public IReadOnlyList<EverydayDay> Between(DateOnly from, DateOnly to) =>
        [.. Days.Where(day => day.Day >= from && day.Day <= to)];
}
