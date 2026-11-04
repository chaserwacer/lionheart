using lionheart.Functional;
using lionheart.Model.Training.Patterns;
using lionheart.Services.CoachedSheets;
using lionheart.Services.Import;
using lionheart.Services.Strong;
using lionheart.Services.Training.Movement.Library;
using lionheart.Services.Training.Patterns;
using lionheart.Tests.Support;

namespace lionheart.Tests.MovementLibrary;

/// <summary>
/// The shipped library rules: they read, the imports already land on the names they produce, every
/// base they name is classified, and a file that would never finish tidying is refused.
/// </summary>
public class MovementLibraryRulesTests
{
    private static MovementLibraryRules Shipped => ResultAssert.Succeeded(MovementLibraryRules.Shipped());

    private static IEnumerable<MovementName> ImportMapped =>
        ResultAssert.Succeeded(StrongMovementMap.Shipped()).Mappings
            .Concat(ResultAssert.Succeeded(SheetMovementMap.Shipped()).Mappings)
            .Select(mapping => mapping.Movement);

    [Fact]
    public void Every_movement_the_import_maps_create_is_already_in_the_convention()
    {
        // Otherwise a re-import would bring back what the tidy removed.
        var untidy = ImportMapped
            .Where(name => !Shipped.Target(name).SameAs(name) || Shipped.RetirementOf(name) is not null)
            .Select(name => $"{name} -> {Shipped.Target(name)}")
            .ToList();

        Assert.True(untidy.Count is 0, $"Not in the convention: {string.Join("; ", untidy)}");
    }

    [Fact]
    public void Every_base_the_rules_and_the_seed_end_with_is_classified()
    {
        var bases = Shipped.Seed
            .Concat(Shipped.Restatements.Select(rule => rule.To))
            .Select(name => name.Base)
            .Concat(Shipped.Bases.Select(rule => rule.To))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var unclassified = bases.Where(name => MovementPatternMap.Shipped.For(name) is ClassificationStanding.Unclassified).ToList();
        Assert.True(unclassified.Count is 0, $"No movement-pattern entry for: {string.Join(", ", unclassified)}");
    }

    [Fact]
    public void A_restatement_names_the_movement_whole_and_wins_over_part_renames()
    {
        var rules = Parse("""
            {
              "equipment": [ { "from": "Dumbell", "to": "Dumbbell" } ],
              "movements": [ { "from": { "base": "Curl", "equipment": "Dumbell", "modifier": "on top" },
                               "to": { "base": "Preacher Curl", "equipment": "Dumbbell" } } ]
            }
            """);

        Assert.Equal(new MovementName("Preacher Curl", "Dumbbell", ""), rules.Target(new MovementName(" curl", "DUMBELL", "On Top")));
        Assert.Equal(new MovementName("Curl", "Dumbbell", ""), rules.Target(new MovementName("Curl", "dumbell", "")));
    }

    [Fact]
    public void A_rename_onto_a_name_another_rule_renames_is_refused()
    {
        // A second run would rename again, so the tidy would never be finished.
        var problem = Refused("""{ "equipment": [ { "from": "DB", "to": "Dumbell" }, { "from": "Dumbell", "to": "Dumbbell" } ] }""");

        var field = Assert.Single(problem.Fields);
        Assert.Contains("'DB' becomes 'Dumbell', which is itself renamed", field.Message);
    }

    [Fact]
    public void A_seed_outside_the_convention_is_refused()
    {
        var problem = Refused("""
            { "equipment": [ { "from": "Dumbell", "to": "Dumbbell" } ],
              "seed": [ { "base": "Row", "equipment": "Dumbell" } ] }
            """);

        Assert.Contains(problem.Fields, field => field.Message.Contains("seed: Row / Dumbell is not in the convention"));
    }

    [Fact]
    public void A_movement_both_retired_and_restated_is_refused()
    {
        var problem = Refused("""
            { "movements": [ { "from": { "base": "Squat", "equipment": "Bar" }, "to": { "base": "Squat", "equipment": "Barbell" } } ],
              "retire": [ { "base": "Squat", "equipment": "Bar" } ] }
            """);

        Assert.Contains(problem.Fields, field => field.Message == "retire: Squat / Bar is also restated; it cannot be both.");
    }

    [Fact]
    public void A_misspelt_key_is_refused_rather_than_read_as_no_modifier()
    {
        var problem = Refused("""{ "seed": [ { "base": "Squat", "equipment": "Barbell", "modifer": "paused" } ] }""");

        Assert.Contains("movement-library.json cannot be read", Assert.Single(problem.Fields).Message);
    }

    [Fact]
    public void Every_problem_in_a_file_is_named_at_once()
    {
        var problem = Refused("""
            { "bases": [ { "from": "Row" }, { "from": "Lunge", "to": "Lunge" } ],
              "seed": [ { "base": "Squat" }, { "base": "Dip", "equipment": "Bodyweight" }, { "base": "dip", "equipment": "bodyweight" } ] }
            """);

        Assert.Equivalent(
            new[]
            {
                "bases: every entry needs \"from\" and \"to\".",
                "bases: 'Lunge' is renamed to itself.",
                "seed: every movement needs \"base\" and \"equipment\".",
                "seed: Dip / Bodyweight is listed 2 times.",
            },
            problem.Fields.Select(field => field.Message),
            strict: true);
    }

    private static MovementLibraryRules Parse(string json) => ResultAssert.Succeeded(MovementLibraryRules.Parse(json));

    private static Problem.Invalid Refused(string json) =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(MovementLibraryRules.Parse(json)));
}
