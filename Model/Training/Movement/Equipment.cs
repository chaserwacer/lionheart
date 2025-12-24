using System.ComponentModel.DataAnnotations;
namespace lionheart.Model.Training
{
public class Equipment
{
    [Key]
    [Required]
    public required Guid EquipmentID { get; init; }
    [Required]
    public required string Name { get; set; } = string.Empty;
    [Required]
    public required Guid UserID { get; init; }
    [Required]
    public bool Enabled { get; set; } = true;

}
public record CreateEquipmentRequest(
    string Name
);
public record EquipmentDTO(
    Guid EquipmentID,
    string Name,
    bool Enabled
);
public record UpdateEquipmentRequest(
    Guid EquipmentID,
    string Name,
    bool Enabled
);

}