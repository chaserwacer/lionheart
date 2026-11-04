using lionheart.Functional;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.BodyMeasurements.Composition;

/// <summary>
/// Whether a stated sheet holds numbers an analyser could have printed, naming every field it
/// objects to at once.
/// </summary>
/// <remarks>
/// A sheet is entered from a review form of some fifty figures, so a refusal lists every field it
/// objects to rather than the first. The bounds are generous: they catch a figure typed into the
/// wrong box or a decimal point lost, not an unusual body. Whether the figures agree with each
/// other — water plus dry lean mass making the lean body mass — is left to the review, because a
/// sheet's own rounding can put them a tenth apart and the paper is the authority.
/// </remarks>
public static class SheetRules
{
    public static Result<BodyCompositionSheet, Problem> Checked(BodyCompositionSheet sheet) =>
        Problems(sheet) is { Count: > 0 } fields
            ? Result<BodyCompositionSheet, Problem>.Failure(new Problem.Invalid(fields))
            : Result<BodyCompositionSheet, Problem>.Success(sheet with { Device = sheet.Device.Trim() });

    public static IReadOnlyList<InvalidField> Problems(BodyCompositionSheet sheet) =>
    [
        .. Device(sheet.Device),
        .. Defined(sheet.Unit, "Sheet.Unit"),
        .. Stature(sheet.Height),
        .. Mass(sheet.Weight, "Sheet.Weight"),
        .. Mass(sheet.SkeletalMuscleMass, "Sheet.SkeletalMuscleMass"),
        .. Mass(sheet.BodyFatMass, "Sheet.BodyFatMass"),
        .. Mass(sheet.LeanBodyMass, "Sheet.LeanBodyMass"),
        .. Within(sheet.PercentBodyFat, 0, 100, "Sheet.PercentBodyFat"),
        .. Within(sheet.BodyMassIndex, 5, 100, "Sheet.BodyMassIndex"),
        .. Sections(sheet.Sections),
    ];

    private const int LongestDevice = 40;

    private static IEnumerable<InvalidField> Device(string? device) =>
        string.IsNullOrWhiteSpace(device) || device.Trim().Length > LongestDevice
            ? [new InvalidField("Sheet.Device", $"The analyser's model, as printed, in at most {LongestDevice} characters.")]
            : [];

    private static IEnumerable<InvalidField> Defined<TEnum>(TEnum value, string field) where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? [] : [new InvalidField(field, $"'{value}' is not a {typeof(TEnum).Name}.")];

    private static IEnumerable<InvalidField> Stature(Height? height) => height switch
    {
        null => [Missing("Sheet.Height")],
        { Unit: LengthUnit.Inches } => Within(height.Value, 36, 96, "Sheet.Height.Value"),
        { Unit: LengthUnit.Centimetres } => Within(height.Value, 90, 245, "Sheet.Height.Value"),
        _ => Defined(height.Unit, "Sheet.Height.Unit"),
    };

    /// <summary>A mass a sheet prints: more than nothing and no more than any bodyweight.</summary>
    private static IEnumerable<InvalidField> Mass(double value, string field) =>
        value is > 0 and <= Bodyweight.Heaviest
            ? []
            : [new InvalidField(field, $"A mass is more than 0 and at most {Bodyweight.Heaviest}.")];

    private static IEnumerable<InvalidField> Within(double value, double least, double most, string field) =>
        value >= least && value <= most
            ? []
            : [new InvalidField(field, $"Expected a figure from {least} to {most}.")];

    private static InvalidField Missing(string field) => new(field, "Required.");

    // ---- Sections ----

    private static IEnumerable<InvalidField> Sections(IReadOnlyList<SheetSection?>? sections) =>
        sections is null
            ? [Missing("Sheet.Sections")]
            : [.. Repeated(sections), .. sections.SelectMany(Section)];

    /// <summary>A sheet prints each panel once, so a second of a kind is a form that sent one twice.</summary>
    private static IEnumerable<InvalidField> Repeated(IEnumerable<SheetSection?> sections) =>
        sections.OfType<SheetSection>()
            .GroupBy(section => section.GetType())
            .Where(kind => kind.Count() > 1)
            .Select(kind => new InvalidField($"Sheet.Sections.{kind.Key.Name}", "A sheet carries each panel once."));

    private static IEnumerable<InvalidField> Section(SheetSection? section) => section switch
    {
        null => [Missing("Sheet.Sections")],
        SheetSection.BodyWater water =>
        [
            .. Mass(water.Intracellular, "Sheet.BodyWater.Intracellular"),
            .. Mass(water.Extracellular, "Sheet.BodyWater.Extracellular"),
            .. Mass(water.Total, "Sheet.BodyWater.Total"),
        ],
        SheetSection.DryLeanMass dry => Mass(dry.Mass, "Sheet.DryLeanMass.Mass"),
        SheetSection.SegmentalLean lean => Segmental(lean.Segments, "Sheet.SegmentalLean"),
        SheetSection.SegmentalFat fat => Segmental(fat.Segments, "Sheet.SegmentalFat"),
        SheetSection.ExtracellularWaterRatio ratio =>
            Within(ratio.Value, 0.2, 0.6, "Sheet.ExtracellularWaterRatio.Value"),
        SheetSection.FitnessScore score => Within(score.Points, 0, 200, "Sheet.FitnessScore.Points"),
        SheetSection.MassControl control =>
        [
            .. Within(control.BodyFat, -Bodyweight.Heaviest, Bodyweight.Heaviest, "Sheet.MassControl.BodyFat"),
            .. Within(control.LeanBody, -Bodyweight.Heaviest, Bodyweight.Heaviest, "Sheet.MassControl.LeanBody"),
        ],
        SheetSection.BasalMetabolicRate rate =>
            Within(rate.Kilocalories, 300, 10_000, "Sheet.BasalMetabolicRate.Kilocalories"),
        SheetSection.VisceralFatLevel level => Within(level.Level, 1, 30, "Sheet.VisceralFatLevel.Level"),
        SheetSection.Impedance impedance => Impedance(impedance.Rows),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static IEnumerable<InvalidField> Segmental(Segments<SegmentReading>? segments, string field) =>
        segments is null
            ? [Missing(field)]
            : segments.Named().SelectMany(segment => segment.Reading is null
                ? [Missing($"{field}.{segment.Name}")]
                : (IEnumerable<InvalidField>)
                [
                    .. SegmentMass(segment.Reading.Mass, $"{field}.{segment.Name}.Mass"),
                    .. Within(segment.Reading.PercentOfReference, 1, 1000, $"{field}.{segment.Name}.PercentOfReference"),
                ]);

    /// <summary>A segment can carry very little fat, so its floor is zero rather than above it.</summary>
    private static IEnumerable<InvalidField> SegmentMass(double value, string field) =>
        Within(value, 0, Bodyweight.Heaviest, field);

    private static IEnumerable<InvalidField> Impedance(IReadOnlyList<ImpedanceRow?>? rows) =>
        rows is null || rows.Count == 0
            ? [new InvalidField("Sheet.Impedance.Rows", "An impedance panel has at least one frequency.")]
            :
            [
                .. rows.Count != rows.OfType<ImpedanceRow>().DistinctBy(row => row.Kilohertz).Count()
                    ? [new InvalidField("Sheet.Impedance.Rows", "Each frequency appears once.")]
                    : Array.Empty<InvalidField>(),
                .. rows.SelectMany(row => row is null
                    ? [Missing("Sheet.Impedance.Rows")]
                    : (IEnumerable<InvalidField>)
                    [
                        .. Within(row.Kilohertz, 1, 5000, "Sheet.Impedance.Kilohertz"),
                        .. row.Ohms is null
                            ? [Missing($"Sheet.Impedance.{row.Kilohertz}kHz")]
                            : row.Ohms.Named().SelectMany(segment =>
                                Within(segment.Reading, 1, 5000, $"Sheet.Impedance.{row.Kilohertz}kHz.{segment.Name}")),
                    ]),
            ];
}
