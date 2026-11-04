using System.Text.Json.Serialization;

namespace lionheart.Model.BodyMeasurements.Composition;

/// <summary>A panel a body-composition sheet may carry, depending on the analyser that printed it.</summary>
/// <remarks>
/// A closed set of panels rather than a record of nullable fields: which panels a sheet has is the
/// fact that varies between analysers, and a panel's figures are printed together or not at all.
/// A new analyser's panel is a new case.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(BodyWater), "bodyWater")]
[JsonDerivedType(typeof(DryLeanMass), "dryLeanMass")]
[JsonDerivedType(typeof(SegmentalLean), "segmentalLean")]
[JsonDerivedType(typeof(SegmentalFat), "segmentalFat")]
[JsonDerivedType(typeof(ExtracellularWaterRatio), "extracellularWaterRatio")]
[JsonDerivedType(typeof(FitnessScore), "fitnessScore")]
[JsonDerivedType(typeof(MassControl), "massControl")]
[JsonDerivedType(typeof(BasalMetabolicRate), "basalMetabolicRate")]
[JsonDerivedType(typeof(VisceralFatLevel), "visceralFatLevel")]
[JsonDerivedType(typeof(Impedance), "impedance")]
public abstract record SheetSection
{
    private protected SheetSection() { }

    /// <summary>The body's water, split into what is inside the cells and what is outside them.</summary>
    public sealed record BodyWater(double Intracellular, double Extracellular, double Total) : SheetSection;

    /// <summary>Lean mass that is not water: protein and mineral together.</summary>
    public sealed record DryLeanMass(double Mass) : SheetSection;

    /// <summary>Lean mass by segment, against the analyser's reference for the athlete's height.</summary>
    public sealed record SegmentalLean(Segments<SegmentReading> Segments) : SheetSection;

    /// <summary>Fat mass by segment, against the analyser's reference.</summary>
    public sealed record SegmentalFat(Segments<SegmentReading> Segments) : SheetSection;

    /// <summary>Extracellular water over total body water (ECW/TBW), printed to three places.</summary>
    public sealed record ExtracellularWaterRatio(double Value) : SheetSection;

    /// <summary>The analyser's own composite score; a muscular body can score over 100.</summary>
    public sealed record FitnessScore(int Points) : SheetSection;

    /// <summary>
    /// The change in fat and lean mass the analyser suggests, signed: positive to gain, negative
    /// to lose.
    /// </summary>
    public sealed record MassControl(double BodyFat, double LeanBody) : SheetSection;

    public sealed record BasalMetabolicRate(int Kilocalories) : SheetSection;

    /// <summary>The analyser's visceral fat level, on its own 1–20 scale.</summary>
    public sealed record VisceralFatLevel(int Level) : SheetSection;

    /// <summary>The raw segmental impedance at each frequency the analyser measured.</summary>
    public sealed record Impedance(IReadOnlyList<ImpedanceRow> Rows) : SheetSection;
}
