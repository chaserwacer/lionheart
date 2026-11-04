using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Support;

/// <summary>
/// Body-composition sheets for tests: an invented athlete's InBody 570 sheet, in pounds, whose
/// figures agree with each other the way a real sheet's do.
/// </summary>
public static class ASheet
{
    public static BodyCompositionSheet InBody570() => new(
        Device: "InBody570",
        Unit: WeightUnit.Pounds,
        Height: new Height(71.0, LengthUnit.Inches),
        Weight: 183.4,
        SkeletalMuscleMass: 88.2,
        BodyFatMass: 30.2,
        LeanBodyMass: 153.2,
        PercentBodyFat: 16.5,
        BodyMassIndex: 25.6,
        Sections: AllPanels());

    /// <summary>The panels a 270 does not print, taken off a 570's sheet.</summary>
    public static BodyCompositionSheet CoreOnly() => InBody570() with { Device = "InBody270", Sections = [] };

    public static IReadOnlyList<SheetSection> AllPanels() =>
    [
        new SheetSection.BodyWater(Intracellular: 70.1, Extracellular: 42.1, Total: 112.2),
        new SheetSection.DryLeanMass(41.0),
        new SheetSection.SegmentalLean(new Segments<SegmentReading>(
            new(9.10, 118.0), new(9.02, 116.9), new(68.4, 112.2), new(22.90, 106.5), new(22.71, 105.6))),
        new SheetSection.SegmentalFat(new Segments<SegmentReading>(
            new(1.4, 88.0), new(1.5, 90.1), new(15.8, 160.4), new(4.2, 101.3), new(4.2, 100.9))),
        new SheetSection.ExtracellularWaterRatio(0.375),
        new SheetSection.FitnessScore(84),
        new SheetSection.MassControl(BodyFat: -3.1, LeanBody: 0.0),
        new SheetSection.BasalMetabolicRate(1890),
        new SheetSection.VisceralFatLevel(6),
        new SheetSection.Impedance(
        [
            new ImpedanceRow(5, new Segments<double>(290.1, 292.4, 21.9, 262.0, 263.3)),
            new ImpedanceRow(50, new Segments<double>(251.2, 253.0, 18.1, 229.5, 230.2)),
            new ImpedanceRow(500, new Segments<double>(214.4, 216.0, 13.5, 197.3, 198.1)),
        ]),
    ];
}
