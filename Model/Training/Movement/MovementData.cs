using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        public MovementDataDTO ToDTO()
        {
            return new MovementDataDTO(
                MovementDataID: MovementDataID,
                EquipmentID: EquipmentID,
                Equipment: Equipment.ToDTO(),
                MovementBaseID: MovementBaseID,
                MovementBase: MovementBase.ToDTO(),
                MovementModifierID: MovementModifierID,
                MovementModifier: MovementModifier?.ToDTO()
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
        MovementModifierDTO? MovementModifier
    );

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