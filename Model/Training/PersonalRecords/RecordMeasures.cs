using System.Text.Json.Serialization;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// One performed lift set, as the record that it holds remembers it.
/// </summary>
/// <remarks>
/// Three facets of the same set are records in their own right — the weight, the weight at a rep
/// count, and the weight times the reps — so the set is one named cluster the three
/// <see cref="Achievement"/> cases share rather than three case-local copies of weight-unit-reps.
/// <para>
/// <see cref="Volume"/> and <see cref="EstimatedOneRepMax"/> are functions of the members above
/// them and never reach storage: a stored copy could disagree with what it came from. The wire
/// gets them restated by the view, because the client must not re-derive them either.
/// </para>
/// </remarks>
public sealed record LiftPerformance(double Weight, WeightUnit Unit, int Reps)
{
    /// <summary>What the set moved in total, in <see cref="Unit"/>.</summary>
    [JsonIgnore]
    public double Volume => Weight * Reps;

    /// <summary>
    /// Epley's estimate — <c>weight × (1 + reps / 30)</c> — of the single rep this set implies.
    /// </summary>
    /// <remarks>
    /// Epley is the formula this app reports, chosen because it is the one the lifting apps the
    /// athlete compares against use, so the number reads the same here as it does there. At one
    /// rep the formula answers 1.033 × the weight, which is nonsense — a single at 225 implies a
    /// single at 225 — so one rep or fewer answers with the weight itself.
    /// </remarks>
    [JsonIgnore]
    public double EstimatedOneRepMax => Reps <= 1 ? Weight : Weight * (1 + Reps / 30.0);
}

/// <summary>
/// Everything one movement was loaded with across a single session, which is a record no
/// individual set holds.
/// </summary>
/// <remarks>
/// Not a <see cref="LiftPerformance"/> with the reps summed: a session total has no one weight and
/// no one rep count, and inventing either would make the figure read as a set that happened.
/// </remarks>
public sealed record SessionVolume(double Volume, WeightUnit Unit);

/// <summary>
/// One performed distance-and-time effort — a treadmill interval, a logged run, a trail run.
/// </summary>
/// <remarks>
/// Distance records compare in meters, so an effort logged in miles and one logged in kilometers
/// rank against each other. That is deliberately unlike weight, which never converts: the four
/// distance units include two spellings of the same metric scale, and refusing to relate 5 km to
/// 5000 m would be a defect rather than a safeguard. The athlete's own unit is kept so the figure
/// reads back as it was logged.
/// </remarks>
public sealed record DistanceEffort(double Distance, DistanceUnit Unit, TimeSpan Duration)
{
    [JsonIgnore]
    public double DistanceInMeters => Distance * Unit.MetersEach();

    /// <summary>
    /// How long one meter took, which is what one pace ranks against another by.
    /// </summary>
    /// <remarks>
    /// A non-positive distance or duration has no pace, and answering with infinity rather than
    /// throwing keeps the member total: a corrupt row then ranks last instead of making every
    /// record on the movement unreadable.
    /// </remarks>
    [JsonIgnore]
    public double SecondsPerMeter => DistanceInMeters > 0 && Duration > TimeSpan.Zero
        ? Duration.TotalSeconds / DistanceInMeters
        : double.PositiveInfinity;

    /// <summary>
    /// The distance this effort's fastest-time record is held at, as whole meters.
    /// </summary>
    /// <remarks>
    /// Bracketing on the distance actually covered, rather than on standard race distances, is
    /// what keeps the figure honest: a 10 km run carries no 5 km split, so crediting one from its
    /// total time would report a time the athlete never ran. Two efforts over the same route
    /// therefore share a bracket, and 5.1 km is a different question from 5 km.
    /// </remarks>
    [JsonIgnore]
    public int BracketMeters => (int)Math.Round(DistanceInMeters, MidpointRounding.AwayFromZero);

    [JsonIgnore]
    public TimeSpan PacePerKilometer => PaceOver(1000);

    [JsonIgnore]
    public TimeSpan PacePerMile => PaceOver(DistanceUnit.Miles.MetersEach());

    private TimeSpan PaceOver(double meters) => double.IsFinite(SecondsPerMeter)
        ? TimeSpan.FromSeconds(SecondsPerMeter * meters)
        : TimeSpan.Zero;
}
