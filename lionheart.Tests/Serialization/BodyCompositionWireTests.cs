using System.Text.Json;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Tests.Support;

namespace lionheart.Tests.Serialization;

/// <summary>
/// The scan shapes the native client writes and decodes. Literal expectations, because the client
/// is written against these bytes: a renamed member or a panel without its discriminator would
/// decode as absent rather than fail.
/// </summary>
public class BodyCompositionWireTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Each_panel_goes_out_named_by_its_discriminator()
    {
        var view = new ScanView(Guid.Empty, new DateOnly(2026, 9, 14), new TimeOnly(7, 41), ASheet.InBody570());

        var sections = JsonDocument.Parse(JsonSerializer.Serialize(view, Wire)).RootElement
            .GetProperty("sheet").GetProperty("sections").EnumerateArray()
            .Select(section => section.GetProperty("$type").GetString());

        Assert.Equal(
            ["bodyWater", "dryLeanMass", "segmentalLean", "segmentalFat", "extracellularWaterRatio",
             "fitnessScore", "massControl", "basalMetabolicRate", "visceralFatLevel", "impedance"],
            sections);
    }

    [Fact]
    public void A_scan_goes_out_with_its_day_and_the_sheets_own_clock()
    {
        var view = new ScanView(Guid.Empty, new DateOnly(2026, 9, 14), new TimeOnly(7, 41), ASheet.CoreOnly());

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, Wire)).RootElement;

        Assert.Equal(("2026-09-14", "07:41:00"), (root.GetProperty("day").GetString(), root.GetProperty("time").GetString()));
    }

    [Theory]
    [InlineData("day")]
    [InlineData("time")]
    [InlineData("recordsBodyweight")]
    public void A_request_missing_a_member_is_refused_rather_than_read_as_its_default(string missing)
    {
        var members = new Dictionary<string, string>
        {
            ["day"] = "\"day\":\"2026-09-14\"",
            ["time"] = "\"time\":\"07:41:00\"",
            ["recordsBodyweight"] = "\"recordsBodyweight\":true",
        };
        var sheet = JsonSerializer.Serialize(ASheet.CoreOnly(), Wire);
        var body = "{" + string.Join(",", members.Where(m => m.Key != missing).Select(m => m.Value)) + ",\"sheet\":" + sheet + "}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ScanRequest>(body, Wire));
    }

    [Fact]
    public void A_request_as_the_client_writes_it_reads_into_the_sheet()
    {
        var request = JsonSerializer.Deserialize<ScanRequest>(
            """
            {"day":"2026-09-14","time":"07:41:00","recordsBodyweight":true,
             "sheet":{"device":"InBody570","unit":1,"height":{"value":71.0,"unit":0},
               "weight":183.4,"skeletalMuscleMass":88.2,"bodyFatMass":30.2,"leanBodyMass":153.2,
               "percentBodyFat":16.5,"bodyMassIndex":25.6,
               "sections":[
                 {"$type":"segmentalLean","segments":{
                   "rightArm":{"mass":9.10,"percentOfReference":118.0},"leftArm":{"mass":9.02,"percentOfReference":116.9},
                   "trunk":{"mass":68.4,"percentOfReference":112.2},"rightLeg":{"mass":22.90,"percentOfReference":106.5},
                   "leftLeg":{"mass":22.71,"percentOfReference":105.6}}},
                 {"$type":"impedance","rows":[{"kilohertz":5,"ohms":{"rightArm":290.1,"leftArm":292.4,"trunk":21.9,"rightLeg":262.0,"leftLeg":263.3}}]}
               ]}}
            """,
            Wire)!;

        Assert.True(request.RecordsBodyweight);
        Assert.Equal(new SegmentReading(22.71, 105.6), request.Sheet.Section<SheetSection.SegmentalLean>()!.Segments.LeftLeg);
        Assert.Equal(21.9, Assert.Single(request.Sheet.Section<SheetSection.Impedance>()!.Rows).Ohms.Trunk);
        Assert.Empty(SheetRules.Problems(request.Sheet));
    }
}
