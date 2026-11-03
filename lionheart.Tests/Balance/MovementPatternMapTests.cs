using lionheart.Functional;
using lionheart.Model.Training.Patterns;
using lionheart.Services.CoachedSheets;
using lionheart.Services.Strong;
using lionheart.Services.Training.Patterns;
using lionheart.Tests.Support;

namespace lionheart.Tests.Balance;

/// <summary>
/// The shipped movement-pattern map: it reads, it covers every base the two import maps create, and
/// a malformed entry is refused with its base named rather than read as something else.
/// </summary>
public class MovementPatternMapTests
{
    [Fact]
    public void Every_base_the_import_maps_create_has_an_entry()
    {
        var bases = ResultAssert.Succeeded(StrongMovementMap.Shipped()).Mappings
            .Concat(ResultAssert.Succeeded(SheetMovementMap.Shipped()).Mappings)
            .Select(mapping => mapping.Movement.Base)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var unclassified = bases.Where(name => MovementPatternMap.Shipped.For(name) is ClassificationStanding.Unclassified).ToList();
        Assert.True(unclassified.Count is 0, $"No entry for: {string.Join(", ", unclassified)}");
    }

    [Fact]
    public void A_base_matches_ignoring_case_and_surrounding_spaces()
    {
        var entry = Assert.IsType<ClassificationStanding.Classified>(MovementPatternMap.Shipped.For("  bench press "));

        Assert.Equal(MovementPattern.HorizontalPush, entry.Classification.Pattern);
        Assert.IsType<ClassificationSource.Map>(entry.Source);
    }

    [Fact]
    public void An_unsure_entry_carries_its_reason()
    {
        var map = ResultAssert.Succeeded(MovementPatternMap.Parse(
            """[{ "base": "Dip", "pattern": "horizontalPush", "primary": ["chest"], "unsure": "between two presses" }]"""));

        var unsure = Assert.IsType<ClassificationSource.MapUnsure>(
            Assert.IsType<ClassificationStanding.Classified>(map.For("Dip")).Source);
        Assert.Equal("between two presses", unsure.Reason);
    }

    [Fact]
    public void A_base_with_no_entry_is_unclassified() =>
        Assert.IsType<ClassificationStanding.Unclassified>(MovementPatternMap.Shipped.For("Cable Mystery"));

    [Theory]
    [InlineData("""[{ "base": "Dip", "pattern": "diagonalPush", "primary": ["chest"] }]""", "movement-patterns.json")]
    [InlineData("""[{ "base": "Dip", "pattern": "horizontalPush", "primary": [] }]""", "Dip")]
    [InlineData("""[{ "pattern": "horizontalPush", "primary": ["chest"] }]""", "entry 1")]
    [InlineData("""[{ "base": "Dip", "primary": ["chest"] }]""", "Dip")]
    [InlineData("""[{ "base": "Dip", "pattern": "squat", "primary": ["quads"] }, { "base": "dip ", "pattern": "squat", "primary": ["quads"] }]""", "Dip")]
    public void A_malformed_map_is_refused_naming_where(string json, string field)
    {
        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(MovementPatternMap.Parse(json)));

        Assert.Equal(field, Assert.Single(refusal.Fields).Field);
    }
}
