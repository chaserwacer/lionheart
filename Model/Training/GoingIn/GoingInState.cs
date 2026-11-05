using System.Text.Json.Serialization;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training.GoingIn;

/// <summary>
/// How ready the athlete is for the session he is about to train: one rating built from five parts,
/// each carrying the figures it was read from, and what is worth knowing before the first set.
/// </summary>
/// <remarks>
/// Lionheart's own reading, not Oura's: readiness is one part of it, beside the night's HRV and
/// lowest heart rate against his own nights, the last two nights' sleep, the training since
/// yesterday and how he rated himself today. Every part is on the same 0–100 scale with his usual
/// day near 75, so a part reads the same whichever figure it came from. Injuries and the muscles
/// trained since yesterday are stated rather than scored: pain in one joint, or sore quads, says
/// what to train today rather than how ready he is to train at all.
/// </remarks>
/// <param name="AsOf">The moment on the athlete's clock the state was read.</param>
/// <param name="PlannedToday">Planned sessions dated today, each as the movements it prescribes.</param>
/// <param name="RecentMuscles">Muscle groups last trained as a primary mover yesterday or today.</param>
public sealed record GoingInState(
    DateTimeOffset AsOf,
    GoingInRating Rating,
    ReadinessPart Readiness,
    AutonomicPart Autonomic,
    SleepPart Sleep,
    LoadPart Load,
    WellnessPart Wellness,
    IReadOnlyList<InjuryNote> Injuries,
    IReadOnlyList<PlannedNote> PlannedToday,
    IReadOnlyList<RecentMuscle> RecentMuscles);

/// <summary>The going-in rating, or that there is nothing about his body today to rate it from.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Rated), "rated")]
[JsonDerivedType(typeof(NotRated), "notRated")]
public abstract record GoingInRating
{
    private protected GoingInRating() { }

    /// <param name="Score">0–100, the parts' weighted mean over the parts that could be scored.</param>
    public sealed record Rated(int Score, GoingInBand Band) : GoingInRating;

    /// <summary>
    /// None of readiness, autonomic or sleep could be scored: training load and wellness alone say
    /// nothing about recovery, so no rating is made from them.
    /// </summary>
    public sealed record NotRated : GoingInRating;
}

/// <summary>Where a rating falls, so the screen can say it in a word.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GoingInBand>))]
public enum GoingInBand
{
    /// <summary>80 and above: better than his usual day.</summary>
    [JsonStringEnumMemberName("high")] High,
    /// <summary>65 to 79: his usual day.</summary>
    [JsonStringEnumMemberName("normal")] Normal,
    /// <summary>50 to 64: under his usual day.</summary>
    [JsonStringEnumMemberName("reduced")] Reduced,
    /// <summary>Under 50.</summary>
    [JsonStringEnumMemberName("low")] Low,
}

/// <summary>A part's score, or that it had nothing to be scored from.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Scored), "scored")]
[JsonDerivedType(typeof(NotScored), "notScored")]
public abstract record PartScore
{
    private protected PartScore() { }

    /// <param name="Score">0–100, his usual day near 75.</param>
    /// <param name="Share">The share of the rating this part carried, once the parts that could not be scored were left out.</param>
    public sealed record Scored(int Score, double Share) : PartScore;

    public sealed record NotScored : PartScore;
}

/// <summary>Oura's readiness for today, with the contributors that bear on recovery.</summary>
/// <remarks>Every figure here is one of Oura's 0–100 ratings, never a measurement.</remarks>
/// <param name="UsualLow">The tenth percentile of his last 90 days' readiness, when there are enough days.</param>
/// <param name="TemperatureDeviation">Degrees Celsius from his baseline, Oura's own deviation.</param>
public sealed record ReadinessPart(
    PartScore Score,
    int? OuraScore,
    int? UsualLow,
    int? UsualHigh,
    int? HrvBalance,
    int? RestingHeartRate,
    double? TemperatureDeviation);

/// <summary>Last night's HRV and lowest heart rate against the nights before.</summary>
/// <param name="Hrv">Last night's average HRV in milliseconds; absent when the night is not synced.</param>
/// <param name="HrvBaseline">The mean of the nights before the last two.</param>
/// <param name="LowestHeartRate">Last night's lowest heart rate in beats per minute.</param>
/// <param name="BaselineNights">How many nights the baselines are drawn from.</param>
public sealed record AutonomicPart(
    PartScore Score,
    int? Hrv,
    double? HrvBaseline,
    int? LowestHeartRate,
    double? LowestHeartRateBaseline,
    int BaselineNights);

/// <summary>Last night and the night before, as Oura filed them.</summary>
public sealed record SleepPart(PartScore Score, NightNote? LastNight, NightNote? NightBefore);

/// <param name="Day">The day Oura filed the night under: the day he woke.</param>
/// <param name="SleepScore">Oura's 0–100 rating of the night.</param>
public sealed record NightNote(DateOnly Day, int TotalSleepSeconds, int Efficiency, int? SleepScore);

/// <summary>The training since the start of yesterday, the week against the four before it, and the everyday load since yesterday.</summary>
/// <param name="HardSets">Hard sets in sessions completed yesterday or today.</param>
/// <param name="HighIntensityMinutes">Minutes at zone 4 or above in cardio since yesterday; absent when the cardio could not be read.</param>
/// <param name="ZoneTwoMinutes">Minutes in zone 2 in cardio since yesterday; absent when the cardio could not be read.</param>
/// <param name="HardSetsChronic">The mean weekly hard sets of the four seven-day blocks before the last seven days; zero with none read.</param>
/// <param name="Everyday">Yesterday's and today's everyday load, where the ring read them: only what runs over his usual high costs.</param>
public sealed record LoadPart(
    PartScore Score,
    int HardSets,
    double? HighIntensityMinutes,
    double? ZoneTwoMinutes,
    int HardSetsLast7,
    double HardSetsChronic,
    IReadOnlyList<EverydayDay> Everyday);

/// <summary>How he rated himself today, when he has.</summary>
public sealed record WellnessPart(PartScore Score, WellnessNote? Today);

/// <param name="Overall">The mean of the four 1–5 ratings, stress inverted.</param>
public sealed record WellnessNote(double Overall, int Mood, int Energy, int Stress, int Motivation);

/// <summary>An active injury, and the movements its check-ins name.</summary>
/// <param name="LastPain">The pain level of the newest check-in, when there is one.</param>
public sealed record InjuryNote(Guid InjuryId, string Name, int? LastPain, IReadOnlyList<Guid> MovementDataIds);

/// <param name="Movements">The names of the lifts it prescribes, in order.</param>
public sealed record PlannedNote(IReadOnlyList<string> Movements);

public sealed record RecentMuscle(MuscleGroup Muscle, DateOnly LastTrained);
