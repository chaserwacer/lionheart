public static class OpenAiFunctionTools
{
    public static readonly List<FunctionToolDefinition> All = new()
    {
        new FunctionToolDefinition
        {
            Name = "createTrainingProgram",
            Description = "Create a new training program using title and start date.",
            Parameters = new
            {
                title = "string",
                startDate = "string" // YYYY-MM-DD
            }
        },
        new FunctionToolDefinition
        {
            Name = "createTrainingSession",
            Description = "Create a new session for a program with a specific date and number.",
            Parameters = new
            {
                trainingProgramID = "string",
                sessionNumber = "int",
                date = "string" // YYYY-MM-DD
            }
        },
        new FunctionToolDefinition
        {
            Name = "getMovementBases",
            Description = "Get all available movement bases.",
            Parameters = new { }
        },
        new FunctionToolDefinition
        {
            Name = "getTrainingSessions",
            Description = "Get all sessions for a given program ID.",
            Parameters = new
            {
                trainingProgramID = "string"
            }
        },
        new FunctionToolDefinition
        {
            Name = "createMovement",
            Description = "Create a movement in a session using base ID and modifier.",
            Parameters = new
            {
                trainingSessionID = "string",
                movementBaseID = "string",
                movementModifier = "int",
                notes = "string",
                weightUnit = "int"
            }
        },
        new FunctionToolDefinition
        {
            Name = "createSetEntry",
            Description = "Create a set entry with reps, weight, and RPE.",
            Parameters = new
            {
                movementID = "string",
                recommendedReps = "int",
                recommendedWeight = "float",
                recommendedRPE = "float"
            }
        },
        new FunctionToolDefinition
        {
            Name = "getIdentityUser",
            Description = "Returns the current user's GUID.",
            Parameters = new { }
        }
    };
}
