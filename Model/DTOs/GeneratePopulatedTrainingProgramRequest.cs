using lionheart.Endpoints.ProgramEndpoints;
using lionheart.Model.DTOs;
using ModelContextProtocol.Server;

public class GeneratePopulatedTrainingProgramRequest
{
    public required TrainingProgramDTO TrainingProgram { get; set; }
    public required List<GeneratePopulatedTrainingSessionRequest> GenerateTrainingSessionsRequests { get; set; } = new List<GeneratePopulatedTrainingSessionRequest>();


}

public class GeneratePopulatedTrainingSessionRequest
{
    public required CreateTrainingSessionRequest createTrainingSessionRequest { get; set; }
    public required List<CreateMovementRequest> CreateMovementRequests { get; set; } = new List<CreateMovementRequest>();
    public required List<CreateSetEntryRequest> CreateSetEntryRequests { get; set; } = new List<CreateSetEntryRequest>();
}