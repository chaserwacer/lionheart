using System.Globalization;
using lionheart.Model.Training.Sets;

namespace lionheart.Model;

/// <summary>
/// The units the athlete reads a figure in — pounds, miles, feet and degrees Fahrenheit — for the
/// text the chat model is handed and repeats to him.
/// </summary>
/// <remarks>
/// Stored values keep the unit they were measured or logged in: Oura and Apple Health in meters and
/// °C, a set in the unit it was logged in. This converts on the way to words only, so no record
/// changes with it. The iOS client's <c>Imperial</c> is the same rule for the screens.
/// </remarks>
public static class Imperial
{
    public const double MetersPerMile = 1609.344;
    public const double MetersPerFoot = 0.3048;
    public const double MetersPerInch = 0.0254;
    public const double KilogramsPerPound = 0.45359237;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>A distance in meters as miles to two places: "6.21 mi".</summary>
    public static string Miles(double meters) => $"{(meters / MetersPerMile).ToString("0.00", Invariant)} mi";

    /// <summary>A climb or a height in meters as whole feet: "459 ft".</summary>
    public static string Feet(double meters) => $"{(meters / MetersPerFoot).ToString("0", Invariant)} ft";

    /// <summary>A time per kilometre as the time per mile.</summary>
    public static TimeSpan PerMile(TimeSpan perKilometre) => perKilometre * (MetersPerMile / 1000);

    /// <summary>A pace as minutes and seconds per mile: "8:03/mi".</summary>
    public static string Pace(TimeSpan perMile)
    {
        var seconds = (int)Math.Round(perMile.TotalSeconds);
        return $"{seconds / 60}:{seconds % 60:00}/mi";
    }

    /// <summary>A speed from the time a mile takes: "18.6 mph".</summary>
    public static string Speed(TimeSpan perMile) => $"{(3600 / perMile.TotalSeconds).ToString("0.0", Invariant)} mph";

    /// <summary>A speed in meters per second as miles an hour: "18.6 mph".</summary>
    public static string Speed(double metersPerSecond) =>
        $"{(metersPerSecond * 3600 / MetersPerMile).ToString("0.0", Invariant)} mph";

    /// <summary>A weight in pounds, whichever unit it was logged in.</summary>
    public static double Pounds(double weight, WeightUnit unit) => unit switch
    {
        WeightUnit.Pounds => weight,
        WeightUnit.Kilograms => weight / KilogramsPerPound,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>A weight as pounds to a tenth at most: "225 lb", "220.5 lb".</summary>
    public static string Weight(double weight, WeightUnit unit) => $"{Number(Pounds(weight, unit))} lb";

    /// <summary>
    /// A logged distance in the unit a US runner reads it in: kilometres as miles, and the rest as
    /// logged — a 400 m repeat is a 400, not a quarter of a mile.
    /// </summary>
    public static string Distance(double distance, DistanceUnit unit) => unit switch
    {
        DistanceUnit.Kilometers => Miles(distance * 1000),
        DistanceUnit.Miles => $"{distance.ToString("0.##", Invariant)} mi",
        DistanceUnit.Meters => $"{distance.ToString("0.#", Invariant)} m",
        DistanceUnit.Yards => $"{distance.ToString("0.#", Invariant)} yd",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>
    /// A temperature <i>change</i> from Celsius to Fahrenheit: the scale's 9/5 and no offset, since a
    /// deviation from baseline of 0 °C is a deviation of 0 °F.
    /// </summary>
    public static double FahrenheitChange(double celsiusChange) => celsiusChange * 9 / 5;

    private static string Number(double value) => value.ToString("#,0.#", Invariant);
}
