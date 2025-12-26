using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace lionheart.Model.Training
{
    /// <summary>
    /// Represents a unique combination of MovementBase + Equipment + optional Modifier.
    /// This defnes the specific movement being performed in a training session, as well as how it is performed
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
    }

    public class MovementModifier
    {
        [Required]
        public required Guid MovementModifierID { get; init; }
        [Required]
        public required string Name { get; set; } = string.Empty;
        [Required]
        public required Guid UserID { get; init; }
    }

    public record MovementDataDTO(
        Guid MovementDataID,
        Guid EquipmentID,
        EquipmentDTO Equipment,
        Guid MovementBaseID,
        MovementBaseDTO MovementBase,
        Guid? MovementModifierID,
        MovementModifierDTO? MovementModifier
    );

    public record MovementModifierDTO(
        Guid MovementModifierID,
        string Name
    );

    public record CreateMovementDataRequest(
        Guid EquipmentID,
        Guid MovementBaseID,
        Guid? MovementModifierID
    );

}