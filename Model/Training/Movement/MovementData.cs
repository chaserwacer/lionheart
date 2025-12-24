using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace lionheart.Model.Training
{
    /// <summary>
    /// Represents the modifcation of a <see cref="MovementBase"/>.
    /// This tells you how to perform the movement.
    /// </summary>
    public class MovementData
    {
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
        public MovementModifier? MovementModifier { get; set; }

        /// <summary>
        /// Checks equality based on the properties of the MovementData, excluding the private key ID.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object? obj)
        {
            if (obj is MovementData data)
            {
                return 
                       UserID == data.UserID &&
                       EquipmentID == data.EquipmentID &&
                       MovementBaseID == data.MovementBaseID &&
                       MovementModifierID == data.MovementModifierID;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MovementDataID, UserID, EquipmentID, MovementBaseID, MovementModifierID);
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