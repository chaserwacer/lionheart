using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Load;

/// <summary>
/// A week's load against what the athlete is used to: the window's figure over the mean weekly
/// figure of the four seven-day blocks before it — the acute:chronic ratio, and the one definition
/// of load every reader uses.
/// </summary>
/// <remarks>
/// Uncoupled — the chronic blocks exclude the window — because a coupled ratio counts the week
/// against itself and understates every spike (Windt and Gabbett 2019; Impellizzeri et al. 2020).
/// The chronic figure is the same one the weekly targets grow from (<c>Progression</c>), so a
/// target and the load it is judged by never disagree about what "usual" was. Two cases, because a
/// figure with nothing read before it has no ratio: a first week back is not a ratio of zero.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Compared), "compared")]
[JsonDerivedType(typeof(NothingBefore), "nothingBefore")]
public abstract record LoadRatio
{
    private protected LoadRatio() { }

    /// <summary>The window's own figure.</summary>
    public abstract double Acute { get; }

    /// <param name="Chronic">The mean weekly figure of the blocks before that could be read; zero when they held nothing.</param>
    public sealed record Compared(double Acute, double Chronic) : LoadRatio
    {
        public override double Acute { get; } = Acute;

        /// <summary>Acute over chronic; zero when there was nothing before, which the ramp reads by its increase instead.</summary>
        public double Ratio => Chronic > 0 ? Acute / Chronic : 0;

        public override RampStanding Standing(double leastIncrease, double leastChronic = 0) => (Acute - Chronic) switch
        {
            _ when Chronic < leastChronic => RampStanding.Unjudged,
            var increase when increase < leastIncrease => RampStanding.Within,
            _ when Chronic <= 0 || Ratio >= SpikeRatio => RampStanding.Spike,
            _ when Ratio >= SteepRatio => RampStanding.Steep,
            _ => RampStanding.Within,
        };
    }

    /// <summary>No block before the window could be read, so there is nothing to be used to.</summary>
    public sealed record NothingBefore(double Acute) : LoadRatio
    {
        public override double Acute { get; } = Acute;

        public override RampStanding Standing(double leastIncrease, double leastChronic = 0) => RampStanding.Unjudged;
    }

    /// <summary>
    /// The ratio at which a week reads steep: the top of the 0.8–1.3 band Gabbett (2016) found
    /// injury risk lowest in.
    /// </summary>
    public const double SteepRatio = 1.3;

    /// <summary>The ratio above which Gabbett's "danger zone" begins: a spike rather than a steep week.</summary>
    public const double SpikeRatio = 1.5;

    /// <summary>The window's figure against the blocks before it, newest first; an unread block is absent.</summary>
    public static LoadRatio Of(double acute, IEnumerable<double?> before)
    {
        var read = before.OfType<double>().ToList();
        return read.Count is 0 ? new NothingBefore(acute) : new Compared(acute, read.Average());
    }

    /// <summary>
    /// How steep the window is, given the least increase that is more than noise for the measure and
    /// the least usual worth judging a rise against.
    /// </summary>
    public abstract RampStanding Standing(double leastIncrease, double leastChronic = 0);
}

/// <summary>How steeply a week rose over the four before it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RampStanding>))]
public enum RampStanding
{
    /// <summary>Nothing before to compare it with, or too little for a rise over it to mean anything.</summary>
    [JsonStringEnumMemberName("unjudged")] Unjudged,

    /// <summary>Under 1.3 times the usual, or up by less than the measure's noise.</summary>
    [JsonStringEnumMemberName("within")] Within,

    /// <summary>1.3 to 1.5 times the usual.</summary>
    [JsonStringEnumMemberName("steep")] Steep,

    /// <summary>1.5 times the usual or more, or a real amount after four weeks of nothing.</summary>
    [JsonStringEnumMemberName("spike")] Spike,
}
