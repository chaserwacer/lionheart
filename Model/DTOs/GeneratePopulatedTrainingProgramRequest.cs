using lionheart.Endpoints.ProgramEndpoints;
using lionheart.Model.DTOs;
using ModelContextProtocol.Server;

public class GeneratePopulatedTrainingProgramRequest
{
    public required TrainingProgramDTO TrainingProgram { get; set; } = new TrainingProgramDTO();
    public required List<GeneratePopulatedTrainingSessionRequest> GenerateTrainingSessionsRequests { get; set; } = new List<GeneratePopulatedTrainingSessionRequest>();
    // Generate Traininf Program, using that TrainingProgrsamDTO, create the GenerratePopulatedTrainignPrograamRequest

    // FOr each generata training sessions request 
         // createtrainingsession()
        // For each createmovementrequest - create movmenent()
    // For each createsetentryrequest - CreateSetEntry()()


}

public class GeneratePopulatedTrainingSessionRequest
{
    public required CreateTrainingSessionRequest createTrainingSessionRequest { get; set; }
    public required List<CreateMovementRequest> CreateMovementRequests { get; set; } = new List<CreateMovementRequest>();
    public required List<CreateSetEntryRequest> CreateSetEntryRequests { get; set; } = new List<CreateSetEntryRequest>();
}