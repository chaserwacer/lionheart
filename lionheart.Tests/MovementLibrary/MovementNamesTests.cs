using lionheart.Services.Training.Movement;

namespace lionheart.Tests.MovementLibrary;

/// <summary>
/// The phrase a movement reads as: the library's convention, and the guard that keeps two parts
/// naming the same thing from reading twice.
/// </summary>
public class MovementNamesTests
{
    [Theory]
    [InlineData("Leg Curl", "Machine", "seated", "Seated Leg Curl (Machine)")]
    [InlineData("Bench Press", "Barbell", null, "Bench Press (Barbell)")]
    [InlineData("Lat Pulldown", "Cable", "kneeling single arm", "Kneeling Single Arm Lat Pulldown (Cable)")]
    [InlineData("Squat", "Barbell", "", "Squat (Barbell)")]
    public void A_movement_reads_variation_exercise_then_equipment(string movementBase, string equipment, string? modifier, string expected) =>
        Assert.Equal(expected, MovementNames.Of(movementBase, equipment, modifier));

    [Fact]
    public void Equipment_named_after_the_exercise_is_not_said_twice()
    {
        // The defect this guards: a machine stored as the equipment read "Seated Leg Curl Leg Curl".
        Assert.Equal("Seated Leg Curl", MovementNames.Of("Seated Leg Curl", "Seated Leg Curl", null));
        Assert.Equal("Kettlebell Swing", MovementNames.Of("Kettlebell Swing", "Kettlebell", null));
    }

    [Fact]
    public void A_modifier_the_base_already_says_is_not_said_twice() =>
        Assert.Equal("Incline Bench Press (Dumbbell)", MovementNames.Of("Incline Bench Press", "Dumbbell", "incline"));

    [Fact]
    public void A_part_is_matched_as_whole_words_so_a_shared_prefix_still_reads()
    {
        // "Row" is inside "Rower" as letters but not as a word.
        Assert.Equal("Rower Sprint (Row)", MovementNames.Of("Rower Sprint", "Row", null));
    }

    [Theory]
    [InlineData("behind-the-back", "Behind-the-Back")]
    [InlineData("v-bar", "V-Bar")]
    [InlineData("tempo 1.5 rep", "Tempo 1.5 Rep")]
    [InlineData("the long way", "The Long Way")]
    public void A_modifier_stored_lower_case_is_capitalised_word_by_word(string stored, string shown) =>
        Assert.Equal(shown, MovementNames.Capitalised(stored));

    [Fact]
    public void Missing_equipment_leaves_no_empty_parentheses() =>
        Assert.Equal("Paused Squat", MovementNames.Of("Squat", " ", "paused"));
}
