using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Functional;
using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training
{
    /// <summary>
    /// Definition of a movement performed. 
    /// This includes what the movement is ( <see cref="MovementBase"/>), the equipment used ( <see cref="Equipment"/>), and an optional modifier ( <see cref="MovementModifier"/>).
    /// </summary>
    public class MovementData
    {
        [Key]
        [Required]
        public required Guid MovementDataID { get; init; }

        [Required]
        public required Guid UserID { get; init; }

        [Required]
        public required Guid EquipmentID { get; set; }

        [ForeignKey("EquipmentID")]
        [Required]
        public required Equipment Equipment { get; set; }

        [Required]
        [ForeignKey("MovementBase")]
        public required Guid MovementBaseID { get; set; }

        [Required]
        public required MovementBase MovementBase { get; set; }

        public Guid? MovementModifierID { get; set; }

        [ForeignKey("MovementModifierID")]
        public MovementModifier? MovementModifier { get; set; }

        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        /// <summary>How the athlete sets this movement up. A movement starts with none.</summary>
        public MovementSetup Setup { get; private set; } = MovementSetup.None;

        public void ReplaceSetup(MovementSetup setup) => Setup = setup;

        /// <summary>
        /// Checks equality based on the properties of the MovementData, excluding the ID and timestamps.
        /// Two MovementDatas are equal if they have the same user, equipment, movement base, and modifier.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is MovementData data)
            {
                return UserID == data.UserID &&
                       EquipmentID == data.EquipmentID &&
                       MovementBaseID == data.MovementBaseID &&
                       MovementModifierID == data.MovementModifierID;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UserID, EquipmentID, MovementBaseID, MovementModifierID);
        }

        /// <param name="baseStanding">What the movement's base trains, as the map reads it with the athlete's statement.</param>
        public MovementDataDTO ToDTO(ClassificationStanding baseStanding)
        {
            return new MovementDataDTO(
                MovementDataID: MovementDataID,
                EquipmentID: EquipmentID,
                Equipment: Equipment.ToDTO(),
                MovementBaseID: MovementBaseID,
                MovementBase: MovementBase.ToDTO(baseStanding),
                MovementModifierID: MovementModifierID,
                MovementModifier: MovementModifier?.ToDTO(),
                Setup: Setup
            );
        }


    }
    /// <summary>
    /// Modifier that can be applied to a movement to specify a variation.
    /// </summary>
    /// <remarks>
    /// Example usage: "Incline", "Paused", "Wide Grip"
    /// </remarks>
    public class MovementModifier
    {
        [Required]
        public required Guid MovementModifierID { get; init; }
        [Required]
        public required string Name { get; set; } = string.Empty;
        [Required]
        public required Guid UserID { get; init; }

        public MovementModifierDTO ToDTO()
        {
            return new MovementModifierDTO(
                MovementModifierID: MovementModifierID,
                Name: Name
            );
        }
    }

    public record MovementDataDTO(
        Guid MovementDataID,
        Guid EquipmentID,
        EquipmentDTO Equipment,
        Guid MovementBaseID,
        MovementBaseDTO MovementBase,
        Guid? MovementModifierID,
        MovementModifierDTO? MovementModifier,
        MovementSetup Setup
    );

    /// <summary>
    /// How the athlete sets a movement up: the cues they want in front of them ("seat 4, pin 7"),
    /// and whether the weight they log on it is added to their own bodyweight.
    /// </summary>
    /// <remarks>
    /// <see cref="AddsToBodyweight"/> is what makes a weighted dip or pull-up readable: the logged
    /// weight is the plate on the belt, so the load the body moved is bodyweight plus that. It is a
    /// fact about the movement, not the set, which is why it lives here and is set once.
    /// </remarks>
    public sealed record MovementSetup(string Notes, bool AddsToBodyweight)
    {
        /// <summary>Long enough for any cue list; past it the text is not a cue.</summary>
        public const int LongestNotes = 2000;

        public static MovementSetup None => new(string.Empty, false);

        /// <summary>A setup a client states, trimmed, or why it is not one.</summary>
        public static Result<MovementSetup, Problem> Of(string notes, bool addsToBodyweight) =>
            notes.Trim() is { Length: <= LongestNotes } trimmed
                ? Result<MovementSetup, Problem>.Success(new MovementSetup(trimmed, addsToBodyweight))
                : Result<MovementSetup, Problem>.Failure(new Problem.Invalid(
                    nameof(Notes), $"Setup notes run to at most {LongestNotes} characters."));
    }

    /// <summary>Replaces a movement's setup whole.</summary>
    public sealed record MovementSetupRequest(string Notes, bool AddsToBodyweight);

    public record MovementModifierDTO(
        Guid MovementModifierID,
        string Name
    );

    public record CreateMovementDataRequest(
        [Required] Guid EquipmentID,
        [Required] Guid MovementBaseID,
        string? MovementModifierName
    );

}