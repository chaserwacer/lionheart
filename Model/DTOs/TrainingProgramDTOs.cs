using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Eventing.Reader;
using lionheart.Model.TrainingProgram;

namespace lionheart.Model.DTOs;








public class ModifyTrainingSessionWithAIRequest
{
    [Required]
    public required Guid TrainingSessionID { get; init; }
    [Required]
    public required Guid TrainingProgramID { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required string UserPrompt { get; init; } = string.Empty;
}