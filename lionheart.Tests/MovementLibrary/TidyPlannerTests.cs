using lionheart.Functional;
using lionheart.Model.Training;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Import;
using lionheart.Services.Training.Movement.Library;
using lionheart.Tests.Support;

namespace lionheart.Tests.MovementLibrary;

/// <summary>
/// Planning a tidy, with no database: what becomes of each movement and part row, and every reason
/// a library cannot be tidied cleanly.
/// </summary>
public class TidyPlannerTests
{
    private static readonly DateTime Made = new(2026, 1, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_machine_stored_as_equipment_becomes_the_machine_and_the_variation()
    {
        var library = new ALibrary().With("Leg Curl", "Seated Leg Curl");

        var plan = Planned(library, """
            { "movements": [ { "from": { "base": "Leg Curl", "equipment": "Seated Leg Curl" },
                               "to": { "base": "Leg Curl", "equipment": "Machine", "modifier": "seated" } } ] }
            """);

        Assert.Equal(new MovementFate.Restated(new MovementName("Leg Curl", "Machine", "seated"), ""), Assert.Single(plan.Movements).Fate);
        Assert.Equal(
            [
                (MovementPart.Equipment, "Seated Leg Curl", typeof(PartFate.Removed)),
                (MovementPart.Equipment, "Machine", typeof(PartFate.Created)),
                (MovementPart.Modifier, "seated", typeof(PartFate.Created)),
            ],
            plan.Parts.Select(part => (part.Part, part.Name, part.Fate.GetType())));
    }

    [Fact]
    public void Renaming_onto_a_name_the_athlete_has_folds_the_rows_and_merges_their_movements()
    {
        var library = new ALibrary()
            .With("Pull Up", "Pull-Up Bar", uses: MovementUses.None with { Sets = 30, Pins = 1 })
            .With("Pull Up", "Bodyweight", uses: MovementUses.None with { Sets = 2 });

        var plan = Planned(library, """{ "equipment": [ { "from": "Pull-Up Bar", "to": "Bodyweight" } ] }""");

        // The one already named that way is kept, though the other has more history.
        var kept = library.Id("Pull Up", "Bodyweight");
        Assert.Equal(
            new MovementFate.Merged(kept, new MovementName("Pull Up", "Bodyweight", ""), MovementUses.None with { Sets = 30, Pins = 1 }, ""),
            plan.Movements.Single(movement => movement.MovementDataId == library.Id("Pull Up", "Pull-Up Bar")).Fate);
        Assert.Equal(new PartFate.Folded(library.Row("Pull-Up Bar"), "Bodyweight"), Assert.Single(plan.Parts).Fate);
    }

    [Fact]
    public void Two_rows_spelling_one_name_fold_into_the_one_more_movements_use()
    {
        var library = new ALibrary()
            .With("Squat", "Machine").With("Leg Press", "Machine")
            .With("Hack Squat", "machine ", uses: MovementUses.None with { Sets = 3 });

        var plan = Planned(library, "{}");

        Assert.Equal(new PartFate.Folded(library.Row("machine "), "Machine"), Assert.Single(plan.Parts).Fate);
        Assert.All(plan.Movements, movement => Assert.IsType<MovementFate.Kept>(movement.Fate));
    }

    [Fact]
    public void A_retired_movement_that_anything_names_is_refused()
    {
        var library = new ALibrary().With("Squat", "Pull-Up Bar", uses: MovementUses.None with { Sets = 3, Records = 1 });

        var problem = Refused(library, """{ "retire": [ { "base": "Squat", "equipment": "Pull-Up Bar" } ] }""");

        Assert.Contains("'Squat (Pull-Up Bar)' is to be retired, but 3 sets, 1 record name it.", problem.What);
    }

    [Fact]
    public void Merging_movements_set_up_differently_is_refused()
    {
        var library = new ALibrary()
            .With("Dip", "Dip Bars", setup: new MovementSetup("belt", true))
            .With("Dip", "Bodyweight", setup: new MovementSetup("no belt", false));

        var problem = Refused(library, """{ "equipment": [ { "from": "Dip Bars", "to": "Bodyweight" } ] }""");

        Assert.Contains("'Dip (Bodyweight)' and 'Dip Bars' merge, and their setups differ", problem.What);
    }

    [Fact]
    public void A_merged_movements_setup_goes_to_the_one_kept_when_it_has_none()
    {
        var setup = new MovementSetup("belt", true);
        var library = new ALibrary().With("Dip", "Dip Bars", setup: setup).With("Dip", "Bodyweight");

        var plan = Planned(library, """{ "equipment": [ { "from": "Dip Bars", "to": "Bodyweight" } ] }""");

        Assert.Equal(new SetupCarry(library.Id("Dip", "Bodyweight"), setup), Assert.Single(plan.SetupCarries));
    }

    [Fact]
    public void A_folded_base_hands_its_classification_to_a_base_that_states_none()
    {
        var stated = new StatedClassification.Stated(Classification(MovementPattern.HorizontalPush));
        var library = new ALibrary().WithBase("Pushup", stated).With("Pushup", "Bodyweight").With("Push Up", "Barbell");

        var plan = Planned(library, """{ "bases": [ { "from": "Pushup", "to": "Push Up" } ] }""");

        Assert.Equal(new BaseCarry("Pushup", "Push Up", stated, ""), Assert.Single(plan.BaseCarries));
    }

    [Fact]
    public void A_base_emptied_onto_one_that_states_something_else_is_refused()
    {
        // Carrying the statement over would change what the receiving base's own movements train.
        var library = new ALibrary()
            .WithBase("Bench Press", new StatedClassification.Stated(Classification(MovementPattern.HorizontalPush)))
            .WithBase("Chest Press", new StatedClassification.Stated(Classification(MovementPattern.Other)))
            .With("Bench Press", "Incline Machine Press")
            .With("Chest Press", "Machine");

        var problem = Refused(library, """
            { "movements": [ { "from": { "base": "Bench Press", "equipment": "Incline Machine Press" },
                               "to": { "base": "Chest Press", "equipment": "Machine", "modifier": "incline" } } ] }
            """);

        Assert.Contains("base 'Bench Press' goes, and what the athlete said about it — a stated classification — would be lost", problem.What);
    }

    [Fact]
    public void Two_movements_the_library_would_show_under_one_name_are_refused()
    {
        var library = new ALibrary().With("Incline Bench Press", "Dumbbell").With("Bench Press", "Dumbbell", "incline");

        var problem = Refused(library, """{ "bases": [ { "from": "Bench Press", "to": "Incline Bench Press" } ] }""");

        Assert.Contains("would both read 'Incline Bench Press (Dumbbell)'", problem.What);
    }

    [Fact]
    public void A_seed_the_library_already_holds_is_not_added_again()
    {
        var library = new ALibrary().With("deadlift", "BARBELL");

        var plan = Planned(library, """{ "seed": [ { "base": "Deadlift", "equipment": "Barbell" }, { "base": "Push Up", "equipment": "Bodyweight" } ] }""");

        Assert.Equal([new MovementName("Push Up", "Bodyweight", "")], plan.Added);
    }

    [Fact]
    public void A_library_already_in_the_convention_has_nothing_to_do()
    {
        var library = new ALibrary().With("Leg Curl", "Machine", "seated").With("Pull Up", "Bodyweight");

        var plan = Planned(library, """
            { "equipment": [ { "from": "Pull-Up Bar", "to": "Bodyweight" } ],
              "movements": [ { "from": { "base": "Leg Curl", "equipment": "Seated Leg Curl" },
                               "to": { "base": "Leg Curl", "equipment": "Machine", "modifier": "seated" } } ],
              "seed": [ { "base": "Pull Up", "equipment": "Bodyweight" } ] }
            """);

        Assert.True(plan.IsEmpty);
    }

    // ---- Building a library ----

    private static TidyPlan Planned(ALibrary library, string rules) =>
        ResultAssert.Succeeded(TidyPlanner.Plan(library.State, ResultAssert.Succeeded(MovementLibraryRules.Parse(rules))));

    private static Problem.Conflict Refused(ALibrary library, string rules) =>
        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(
            TidyPlanner.Plan(library.State, ResultAssert.Succeeded(MovementLibraryRules.Parse(rules)))));

    private static MovementClassification Classification(MovementPattern pattern) =>
        ResultAssert.Succeeded(MovementClassification.Of(pattern, [MuscleGroup.Chest], []));

    /// <summary>A library in memory, its part rows reused by exact spelling so a test can plant two spellings of one name.</summary>
    private sealed class ALibrary
    {
        private readonly List<BaseRow> _bases = [];
        private readonly List<PartRow> _equipment = [];
        private readonly List<PartRow> _modifiers = [];
        private readonly List<HeldMovement> _movements = [];

        public LibraryState State => new(_bases, _equipment, _modifiers, _movements);

        public ALibrary WithBase(string name, StatedClassification statement)
        {
            _bases.Add(new BaseRow(Guid.NewGuid(), name, statement, string.Empty));
            return this;
        }

        public ALibrary With(
            string movementBase, string equipment, string modifier = "", MovementUses? uses = null, MovementSetup? setup = null)
        {
            var baseRow = _bases.FirstOrDefault(row => row.Name == movementBase)
                          ?? Planted(_bases, new BaseRow(Guid.NewGuid(), movementBase, new StatedClassification.Unstated(), string.Empty));
            var equipmentRow = _equipment.FirstOrDefault(row => row.Name == equipment) ?? Planted(_equipment, new PartRow(Guid.NewGuid(), equipment));
            var modifierRow = modifier.Length == 0
                ? null
                : _modifiers.FirstOrDefault(row => row.Name == modifier) ?? Planted(_modifiers, new PartRow(Guid.NewGuid(), modifier));

            _movements.Add(new HeldMovement(
                Guid.NewGuid(), new MovementName(movementBase, equipment, modifier), baseRow.Id, equipmentRow.Id, modifierRow?.Id,
                setup ?? MovementSetup.None, Made.AddMinutes(_movements.Count), uses ?? MovementUses.None));
            return this;
        }

        public Guid Id(string movementBase, string equipment) =>
            _movements.Single(movement => movement.Name == new MovementName(movementBase, equipment, "")).Id;

        public Guid Row(string equipment) => _equipment.Single(row => row.Name == equipment).Id;

        private static T Planted<T>(List<T> rows, T row)
        {
            rows.Add(row);
            return row;
        }
    }
}
