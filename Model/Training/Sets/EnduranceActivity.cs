using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Sets;

/// <summary>What an <see cref="EnduranceSet"/> was.</summary>
/// <remarks>
/// Indoor is part of the activity rather than a flag beside it, because it changes what the
/// session is evidence of: a trainer's or a treadmill's distance is the machine's estimate rather
/// than ground covered, so an indoor ride or run counts as training and never as a record. There is
/// no indoor hike, and a walk sets no record wherever it was, so neither has an indoor case.
/// <para>
/// An e-bike ride is its own activity rather than a ride, for the indoor ride's reason: the motor
/// covered part of the distance, so it is training and never a riding record. A paddle — a kayak, a
/// canoe, a board — is training too, and competes on nothing: water carries the boat, and a
/// handful of outings would rank only against each other.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<EnduranceActivity>))]
public enum EnduranceActivity
{
    [JsonStringEnumMemberName("ride")] Ride,
    [JsonStringEnumMemberName("indoorRide")] IndoorRide,
    [JsonStringEnumMemberName("hike")] Hike,
    [JsonStringEnumMemberName("walk")] Walk,
    [JsonStringEnumMemberName("indoorRun")] IndoorRun,
    [JsonStringEnumMemberName("eBikeRide")] EBikeRide,
    [JsonStringEnumMemberName("paddle")] Paddle,
}

public static class EnduranceSets
{
    extension(IEnumerable<SetEntry> sets)
    {
        /// <summary>
        /// The activity of the first endurance set, or none for sets holding no endurance set — a
        /// session imported from Health holds one set, so this is what the session was.
        /// </summary>
        public EnduranceActivity? EnduranceActivity =>
            sets.OfType<EnduranceSet>().Select(set => (EnduranceActivity?)set.Activity).FirstOrDefault();
    }
}
