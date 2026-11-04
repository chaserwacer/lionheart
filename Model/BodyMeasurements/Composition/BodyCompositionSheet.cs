using lionheart.Model.Training.Sets;

namespace lionheart.Model.BodyMeasurements.Composition;

/// <summary>One body-composition result sheet, as the analyser printed it.</summary>
/// <remarks>
/// Every InBody prints the muscle-fat and obesity figures held here directly, so a sheet always
/// has them. Everything else depends on the model of analyser — a 270 prints no water split, no
/// segmental fat and one impedance frequency; a 570 prints all of those; a 770 prints more — so
/// it arrives as <see cref="Sections"/>: the panels this sheet carried, each at most once. A panel
/// the analyser did not print, or that could not be read off the paper, is simply not there.
/// <para>
/// Masses are in <see cref="Unit"/>, the unit the sheet printed, and never convert, for the reason
/// a lift's weight does not. The percentages of reference are the analyser's own: its reference
/// for the athlete's height and weight, not a clinical norm.
/// </para>
/// </remarks>
public sealed record BodyCompositionSheet(
    string Device,
    WeightUnit Unit,
    Height Height,
    double Weight,
    double SkeletalMuscleMass,
    double BodyFatMass,
    double LeanBodyMass,
    double PercentBodyFat,
    double BodyMassIndex,
    IReadOnlyList<SheetSection> Sections)
{
    /// <summary>The panel of this kind, when the sheet carried one.</summary>
    public T? Section<T>() where T : SheetSection => Sections.OfType<T>().FirstOrDefault();
}

// Member order is load-bearing: persisted as ints in the sheet's JSON.
public enum LengthUnit
{
    Inches,
    Centimetres
}

/// <summary>The height the analyser was given, which is what its BMI and references are computed from.</summary>
public sealed record Height(double Value, LengthUnit Unit);

/// <summary>A reading for each of the five segments an analyser measures, in the order it prints them.</summary>
public sealed record Segments<T>(T RightArm, T LeftArm, T Trunk, T RightLeg, T LeftLeg)
{
    public IEnumerable<(string Name, T Reading)> Named() =>
    [
        (nameof(RightArm), RightArm),
        (nameof(LeftArm), LeftArm),
        (nameof(Trunk), Trunk),
        (nameof(RightLeg), RightLeg),
        (nameof(LeftLeg), LeftLeg),
    ];
}

/// <summary>
/// One segment's mass, and that mass as a percentage of the analyser's own reference for it.
/// </summary>
public sealed record SegmentReading(double Mass, double PercentOfReference);

/// <summary>One frequency's row of segmental impedance, in ohms.</summary>
public sealed record ImpedanceRow(double Kilohertz, Segments<double> Ohms);
