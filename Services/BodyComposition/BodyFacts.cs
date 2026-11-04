using System.Globalization;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.BodyComposition;

/// <summary>
/// The athlete's body as the chat model reads it: on the context card, the latest dated bodyweight
/// and the latest body-composition scan against the one before it; through the scans tool, each
/// whole sheet.
/// </summary>
/// <remarks>
/// Every figure carries its date, because a bodyweight from three months ago reasoned with as
/// today's is the error this replaces — the profile's undated weight. The analyser's percentages
/// are named as its own reference so the model does not read them as a clinical judgement, and
/// every figure is written with its unit, so a pound is never read as a kilogram.
/// </remarks>
public static class BodyFacts
{
    /// <param name="latest">The newest scan first, then the one before it when there is one.</param>
    public static IReadOnlyList<string> Lines(
        BodyweightMeasurement? bodyweight, IReadOnlyList<BodyCompositionScan> latest, int scansOnRecord) =>
    [
        Bodyweight(bodyweight),
        .. latest switch
        {
            [] => ["Body composition: no scan recorded."],
            [var only] => Scan(only, scansOnRecord, previous: null),
            [var newest, var previous, ..] => Scan(newest, scansOnRecord, previous),
        },
    ];

    private static string Bodyweight(BodyweightMeasurement? measurement) =>
        measurement is null
            ? "Bodyweight: none logged."
            : $"Bodyweight: {Mass(measurement.Bodyweight.Value, measurement.Bodyweight.Unit)} on {measurement.Day:yyyy-MM-dd} (latest logged measurement).";

    private static IEnumerable<string> Scan(BodyCompositionScan scan, int scansOnRecord, BodyCompositionScan? previous)
    {
        var sheet = scan.Sheet;
        var unit = sheet.Unit;

        yield return
            $"Body composition ({sheet.Device} scan {scan.Day:yyyy-MM-dd}, {Plural(scansOnRecord, "scan")} on record; height {Number(sheet.Height.Value)} {Abbreviation(sheet.Height.Unit)}): " +
            $"weight {Mass(sheet.Weight, unit)}, skeletal muscle mass {Mass(sheet.SkeletalMuscleMass, unit)}, " +
            $"body fat mass {Mass(sheet.BodyFatMass, unit)} ({Number(sheet.PercentBodyFat)}% body fat), " +
            $"lean body mass {Mass(sheet.LeanBodyMass, unit)}, BMI {Number(sheet.BodyMassIndex)}.";

        if (sheet.Section<SheetSection.SegmentalLean>() is { } lean)
            yield return $"Segmental lean, {unit.Abbreviation()} (% of the analyser's reference): {Segmental(lean.Segments)}.";

        if (sheet.Section<SheetSection.SegmentalFat>() is { } fat)
            yield return $"Segmental fat, {unit.Abbreviation()} (% of the analyser's reference): {Segmental(fat.Segments)}.";

        if (Others(sheet) is { Length: > 0 } others)
            yield return $"Also on that sheet: {others}.";

        if (previous is not null && previous.Sheet.Unit == unit)
            yield return Change(previous, sheet);
    }

    /// <summary>One whole sheet as text, every panel it carried included.</summary>
    public static string Sheet(BodyCompositionScan scan)
    {
        var sheet = scan.Sheet;
        var unit = sheet.Unit;
        IEnumerable<string?> parts =
        [
            $"{sheet.Device} scan {scan.Day:yyyy-MM-dd} {scan.Time:HH\\:mm}, height {Number(sheet.Height.Value)} {Abbreviation(sheet.Height.Unit)}: " +
            $"weight {Mass(sheet.Weight, unit)}, skeletal muscle mass {Mass(sheet.SkeletalMuscleMass, unit)}, " +
            $"body fat mass {Mass(sheet.BodyFatMass, unit)} ({Number(sheet.PercentBodyFat)}% body fat), " +
            $"lean body mass {Mass(sheet.LeanBodyMass, unit)}, BMI {Number(sheet.BodyMassIndex)}.",
            sheet.Section<SheetSection.BodyWater>() is { } water
                ? $"Body water: intracellular {Mass(water.Intracellular, unit)}, extracellular {Mass(water.Extracellular, unit)}, total {Mass(water.Total, unit)}."
                : null,
            sheet.Section<SheetSection.DryLeanMass>() is { } dry ? $"Dry lean mass {Mass(dry.Mass, unit)}." : null,
            sheet.Section<SheetSection.SegmentalLean>() is { } lean
                ? $"Segmental lean, {unit.Abbreviation()} (% of the analyser's reference): {Segmental(lean.Segments)}."
                : null,
            sheet.Section<SheetSection.SegmentalFat>() is { } fat
                ? $"Segmental fat, {unit.Abbreviation()} (% of the analyser's reference): {Segmental(fat.Segments)}."
                : null,
            Others(sheet) is { Length: > 0 } others ? $"{char.ToUpperInvariant(others[0])}{others[1..]}." : null,
            sheet.Section<SheetSection.MassControl>() is { } control
                ? $"The analyser's suggested change: fat {SignedMass(control.BodyFat, unit)}, lean {SignedMass(control.LeanBody, unit)}."
                : null,
            sheet.Section<SheetSection.Impedance>() is { } impedance
                ? $"Impedance, ohms: {string.Join("; ", impedance.Rows.Select(Impedance))}."
                : null,
        ];
        return string.Join(" ", parts.OfType<string>());
    }

    private static string Impedance(ImpedanceRow row) =>
        $"{row.Kilohertz.ToString("0.#", CultureInfo.InvariantCulture)} kHz right arm {Number(row.Ohms.RightArm)}, left arm {Number(row.Ohms.LeftArm)}, " +
        $"trunk {Number(row.Ohms.Trunk)}, right leg {Number(row.Ohms.RightLeg)}, left leg {Number(row.Ohms.LeftLeg)}";

    private static string SignedMass(double value, WeightUnit unit) => $"{Signed(value)} {unit.Abbreviation()}";

    private static string Change(BodyCompositionScan previous, BodyCompositionSheet sheet) =>
        $"Since the {previous.Day:yyyy-MM-dd} scan: weight {Signed(sheet.Weight - previous.Sheet.Weight)}, " +
        $"skeletal muscle mass {Signed(sheet.SkeletalMuscleMass - previous.Sheet.SkeletalMuscleMass)}, " +
        $"body fat mass {Signed(sheet.BodyFatMass - previous.Sheet.BodyFatMass)} {sheet.Unit.Abbreviation()}; " +
        $"body fat {Signed(sheet.PercentBodyFat - previous.Sheet.PercentBodyFat)} percentage points.";

    private static string Others(BodyCompositionSheet sheet) =>
        string.Join(", ", new[]
        {
            sheet.Section<SheetSection.ExtracellularWaterRatio>() is { } ratio ? $"ECW/TBW {ratio.Value.ToString("0.000", CultureInfo.InvariantCulture)}" : null,
            sheet.Section<SheetSection.FitnessScore>() is { } score ? $"the analyser's fitness score {score.Points}" : null,
            sheet.Section<SheetSection.BasalMetabolicRate>() is { } rate ? $"basal metabolic rate {rate.Kilocalories} kcal" : null,
            sheet.Section<SheetSection.VisceralFatLevel>() is { } level ? $"visceral fat level {level.Level} (analyser's 1-20 scale)" : null,
        }.OfType<string>());

    private static string Segmental(Segments<SegmentReading> segments) =>
        $"right arm {Reading(segments.RightArm)}, left arm {Reading(segments.LeftArm)}, trunk {Reading(segments.Trunk)}, " +
        $"right leg {Reading(segments.RightLeg)}, left leg {Reading(segments.LeftLeg)}";

    private static string Reading(SegmentReading reading) =>
        $"{Number(reading.Mass)} ({Number(reading.PercentOfReference)}%)";

    private static string Mass(double value, WeightUnit unit) => $"{Number(value)} {unit.Abbreviation()}";

    private static string Number(double value) => value.ToString("0.0#", CultureInfo.InvariantCulture);

    private static string Signed(double value) => value.ToString("+0.0#;-0.0#;0.0", CultureInfo.InvariantCulture);

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Abbreviation(LengthUnit unit) => unit is LengthUnit.Centimetres ? "cm" : "in";
}
