using lionheart.Functional;

namespace lionheart.Model.Health;

/// <summary>One fix of a recorded route: when, where, and how high where the device knew.</summary>
/// <param name="Altitude">Metres above sea level, or absent where the fix carried no valid altitude.</param>
public sealed record RoutePoint(DateTimeOffset At, double Latitude, double Longitude, double? Altitude)
{
    /// <summary>
    /// A millionth of a degree, about 11 cm — finer than any phone's fix, and what keeps a stored
    /// coordinate to ten characters rather than seventeen.
    /// </summary>
    private const int CoordinateDecimals = 6;

    /// <summary>A tenth of a metre, finer than the altitude a phone reports.</summary>
    private const int AltitudeDecimals = 1;

    /// <summary>A fix as a device stated it, kept to the precision a fix has, or why it is no place on Earth.</summary>
    public static Result<RoutePoint, string> Fix(DateTimeOffset at, double latitude, double longitude, double? altitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180
        && (altitude is null || double.IsFinite(altitude.Value))
            ? Result<RoutePoint, string>.Success(new RoutePoint(
                at,
                Math.Round(latitude, CoordinateDecimals),
                Math.Round(longitude, CoordinateDecimals),
                altitude is { } metres ? Math.Round(metres, AltitudeDecimals) : null))
            : Result<RoutePoint, string>.Failure($"The fix at {at:O} is not a place on Earth.");

    public DateTimeOffset At { get; init; } = At;

    public double Latitude { get; init; } = Latitude is >= -90 and <= 90
        ? Latitude
        : throw new ArgumentOutOfRangeException(nameof(Latitude), Latitude, "A latitude lies within ±90°.");

    public double Longitude { get; init; } = Longitude is >= -180 and <= 180
        ? Longitude
        : throw new ArgumentOutOfRangeException(nameof(Longitude), Longitude, "A longitude lies within ±180°.");

    public double? Altitude { get; init; } = Altitude is { } altitude ? Require.Finite(altitude, nameof(Altitude)) : null;
}

/// <summary>
/// The path a workout took, oldest fix first, as the app that recorded it wrote it — to Apple Health,
/// or in the Strava archive.
/// </summary>
/// <remarks>
/// <b>This is the athlete's precise location</b> — a run that starts at his door says where he
/// lives. It is stored in his own database and drawn only in his own app; nothing that reaches the
/// chat model carries a coordinate, only what was derived from them (distance, splits, climb).
/// <para>
/// An empty route is <see cref="None"/>: a workout recorded without GPS, which is a fact about the
/// workout rather than a missing field. One fix is no path either, so <see cref="HasPath"/> asks
/// for two.
/// </para>
/// </remarks>
public sealed record WorkoutRoute(IReadOnlyList<RoutePoint> Points)
{
    public IReadOnlyList<RoutePoint> Points { get; init; } = Require.Present(Points, nameof(Points));

    public static WorkoutRoute None { get; } = new([]);

    public bool HasPath => Points.Count >= 2;

    /// <summary>Compared by the fixes it holds, not by the list holding them.</summary>
    public bool Equals(WorkoutRoute? other) => other is not null && Points.SequenceEqual(other.Points);

    public override int GetHashCode() => Points.Count;
}
