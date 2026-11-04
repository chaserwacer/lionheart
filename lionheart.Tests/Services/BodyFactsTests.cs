using lionheart.Model.BodyMeasurements;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Model.Training.Sets;
using lionheart.Services.BodyComposition;
using lionheart.Tests.Support;

namespace lionheart.Tests.Services;

/// <summary>
/// What the chat model's standing context says about the athlete's body: dated figures only, and
/// the analyser's percentages named as its own reference.
/// </summary>
public class BodyFactsTests
{
    private static readonly Guid Athlete = Guid.NewGuid();
    private static readonly DateOnly ScanDay = new(2026, 9, 14);

    private static BodyCompositionScan ScanOn(DateOnly day, BodyCompositionSheet sheet) =>
        BodyCompositionScan.Taken(Guid.NewGuid(), Athlete, day, new TimeOnly(7, 41), sheet);

    [Fact]
    public void With_nothing_measured_the_card_says_so_rather_than_inventing_a_weight() =>
        Assert.Equal(
            ["Bodyweight: none logged.", "Body composition: no scan recorded."],
            BodyFacts.Lines(null, [], 0));

    [Fact]
    public void The_bodyweight_line_carries_its_day()
    {
        var measurement = BodyweightMeasurement.Measured(Athlete, ScanDay, new Bodyweight(183.4, WeightUnit.Pounds));

        Assert.Equal(
            "Bodyweight: 183.4 lb on 2026-09-14 (latest logged measurement).",
            BodyFacts.Lines(measurement, [], 0)[0]);
    }

    [Fact]
    public void A_scan_states_its_core_figures_and_segments_against_the_analysers_reference()
    {
        var lines = BodyFacts.Lines(null, [ScanOn(ScanDay, ASheet.InBody570())], 1);

        Assert.Equal(
            "Body composition (InBody570 scan 2026-09-14, 1 scan on record; height 71.0 in): weight 183.4 lb, " +
            "skeletal muscle mass 88.2 lb, body fat mass 30.2 lb (16.5% body fat), lean body mass 153.2 lb, BMI 25.6.",
            lines[1]);
        Assert.Equal(
            "Segmental lean, lb (% of the analyser's reference): right arm 9.1 (118.0%), left arm 9.02 (116.9%), " +
            "trunk 68.4 (112.2%), right leg 22.9 (106.5%), left leg 22.71 (105.6%).",
            lines[2]);
        Assert.Equal(
            "Also on that sheet: ECW/TBW 0.375, the analyser's fitness score 84, basal metabolic rate 1890 kcal, " +
            "visceral fat level 6 (analyser's 1-20 scale).",
            lines[4]);
    }

    [Fact]
    public void A_sheet_without_panels_states_only_its_core_figures() =>
        Assert.Equal(2, BodyFacts.Lines(null, [ScanOn(ScanDay, ASheet.CoreOnly())], 1).Count);

    [Fact]
    public void The_newest_scan_is_set_against_the_one_before_it()
    {
        var earlier = ScanOn(ScanDay.AddDays(-56), ASheet.InBody570() with
        {
            Weight = 180.0, SkeletalMuscleMass = 86.9, BodyFatMass = 30.6, PercentBodyFat = 17.0,
        });

        var lines = BodyFacts.Lines(null, [ScanOn(ScanDay, ASheet.InBody570()), earlier], 2);

        Assert.Equal(
            "Since the 2026-07-20 scan: weight +3.4, skeletal muscle mass +1.3, body fat mass -0.4 lb; " +
            "body fat -0.5 percentage points.",
            lines[^1]);
    }

    [Fact]
    public void A_whole_sheet_reads_as_text_with_every_figure_in_its_unit()
    {
        var text = BodyFacts.Sheet(ScanOn(ScanDay, ASheet.InBody570()));

        Assert.StartsWith(
            "InBody570 scan 2026-09-14 07:41, height 71.0 in: weight 183.4 lb, skeletal muscle mass 88.2 lb, ",
            text);
        Assert.Contains("Body water: intracellular 70.1 lb, extracellular 42.1 lb, total 112.2 lb.", text);
        Assert.Contains("The analyser's suggested change: fat -3.1 lb, lean 0.0 lb.", text);
        Assert.EndsWith(
            "500 kHz right arm 214.4, left arm 216.0, trunk 13.5, right leg 197.3, left leg 198.1.", text);
    }

    [Fact]
    public void Scans_in_different_units_are_not_compared()
    {
        var earlier = ScanOn(ScanDay.AddDays(-56), ASheet.InBody570() with { Unit = WeightUnit.Kilograms, Weight = 82.0 });

        var lines = BodyFacts.Lines(null, [ScanOn(ScanDay, ASheet.InBody570()), earlier], 2);

        Assert.DoesNotContain(lines, line => line.StartsWith("Since"));
    }
}
