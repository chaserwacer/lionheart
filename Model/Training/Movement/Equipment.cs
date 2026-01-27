using System.ComponentModel.DataAnnotations;
namespace lionheart.Model.Training
{
    /// <summary>
    /// Representation of equipment owned by a user that can be used during training.
    /// </summary>
    /// <remarks>
    /// Equipment can be <see cref="Enabled"/> to indicate use availability. 
    /// </remarks>
    public class Equipment
    {
        [Key]
        [Required]
        public required Guid EquipmentID { get; init; }
        [Required]
        public required string Name { get; set; } = string.Empty;
        [Required]
        public required Guid UserID { get; init; }
        /// <summary>
        /// Indicates whether the equipment is currently enabled for use.
        /// </summary>
        [Required]
        public bool Enabled { get; set; } = true;
        public EquipmentDTO ToDTO()
        {
            return new EquipmentDTO(
                EquipmentID: EquipmentID,
                Name: Name,
                Enabled: Enabled
            );
        }


    }
    public record CreateEquipmentRequest(
        [Required]string Name
    );
    public record EquipmentDTO(
        Guid EquipmentID,
        string Name,
        bool Enabled
    );
    public record UpdateEquipmentRequest(
        [Required]Guid EquipmentID,
        [Required]string Name,
        [Required]bool Enabled
    );

}