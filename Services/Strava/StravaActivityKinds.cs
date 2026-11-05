using lionheart.Model.Training.Sets;

namespace lionheart.Services.Strava;

/// <summary>What a Strava activity becomes, by its type — or that Lionheart does not take it.</summary>
public abstract record StravaActivityKind
{
    private StravaActivityKind() { }

    /// <summary>A run outdoors: a <see cref="TrailRunSet"/>, competing on trail running.</summary>
    public sealed record TrailRun : StravaActivityKind;

    /// <summary>Anything else measured by distance and time: an <see cref="EnduranceSet"/> of this activity.</summary>
    public sealed record Endurance(EnduranceActivity Activity) : StravaActivityKind;

    /// <summary>A gym session: Strava knows its time and heart rate, never its sets.</summary>
    public sealed record Lifting : StravaActivityKind;

    /// <summary>A type Lionheart does not take from the archive — a swim, yoga, a ski day.</summary>
    public sealed record NotTaken : StravaActivityKind;
}

/// <summary>Strava's activity types, and what each becomes.</summary>
/// <remarks>
/// Strava's own names, as its archive writes them. Variants Strava splits out by terrain or bike
/// (a trail run, a mountain-bike or gravel ride) are what the Health import makes of the same
/// workout: a trail run, a ride. "Virtual" is Strava's word for indoors — a treadmill, a trainer —
/// which competes on nothing, as an indoor run or ride from Health does. A type not listed is left
/// out and counted by name, so a new one is seen rather than guessed at.
/// </remarks>
public static class StravaActivityKinds
{
    private static readonly IReadOnlyDictionary<string, StravaActivityKind> ByType =
        new Dictionary<string, StravaActivityKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["Run"] = new StravaActivityKind.TrailRun(),
            ["Trail Run"] = new StravaActivityKind.TrailRun(),
            ["Virtual Run"] = new StravaActivityKind.Endurance(EnduranceActivity.IndoorRun),
            ["Ride"] = new StravaActivityKind.Endurance(EnduranceActivity.Ride),
            ["Mountain Bike Ride"] = new StravaActivityKind.Endurance(EnduranceActivity.Ride),
            ["Gravel Ride"] = new StravaActivityKind.Endurance(EnduranceActivity.Ride),
            ["Virtual Ride"] = new StravaActivityKind.Endurance(EnduranceActivity.IndoorRide),
            ["E-Bike Ride"] = new StravaActivityKind.Endurance(EnduranceActivity.EBikeRide),
            ["E-Mountain Bike Ride"] = new StravaActivityKind.Endurance(EnduranceActivity.EBikeRide),
            ["Hike"] = new StravaActivityKind.Endurance(EnduranceActivity.Hike),
            ["Walk"] = new StravaActivityKind.Endurance(EnduranceActivity.Walk),
            ["Kayaking"] = new StravaActivityKind.Endurance(EnduranceActivity.Paddle),
            ["Canoeing"] = new StravaActivityKind.Endurance(EnduranceActivity.Paddle),
            ["Stand Up Paddling"] = new StravaActivityKind.Endurance(EnduranceActivity.Paddle),
            ["Weight Training"] = new StravaActivityKind.Lifting(),
        };

    public static StravaActivityKind Of(string type) =>
        ByType.TryGetValue(type.Trim(), out var kind) ? kind : new StravaActivityKind.NotTaken();

    /// <summary>
    /// Whether a name is the one Strava gives an activity nobody named — "Morning Run", "Evening
    /// Weight Training" — which says nothing the session does not already.
    /// </summary>
    /// <remarks>Strava has named older activities by their finer type ("Afternoon Mountain Bike Ride").</remarks>
    public static bool IsDefaultName(string name, string type) =>
        name.Split(' ', 2) is [var part, var named]
        && part is "Morning" or "Lunch" or "Afternoon" or "Evening" or "Night"
        && (string.Equals(named, type.Trim(), StringComparison.Ordinal) || ByType.Keys.Contains(named, StringComparer.Ordinal));
}
