using System.Text.Json;

public static class OpenAiFunctionTools
{
    public static readonly List<FunctionToolDefinition> All = new()
    {
        new FunctionToolDefinition
        {
            Name = "createTrainingProgram",
            Description = "Create a new training program using title, date range, and tags.",
            Parameters = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "title": { "type": "string" },
                    "startDate": { "type": "string", "format": "date" },
                    "endDate": { "type": "string", "format": "date" },
                    "tags": {
                        "type": "array",
                        "items": { "type": "string" }
                    }
                },
                "required": ["title", "startDate", "endDate", "tags"]
            }
            """)
        }
,
        new FunctionToolDefinition
        {
            Name = "createTrainingSession",
            Description = "Create a new session for a program with a specific date and number.",
            Parameters = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "trainingProgramID": { "type": "string" },
                    "sessionDate": { "type": "string", "format": "date" },
                    "sessionNumber": { "type": "integer" }
                },
                "required": ["trainingProgramID", "sessionDate", "sessionNumber"]
            }
            """)
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
            Parameters = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "trainingProgramID": { "type": "string" }
                },
                "required": ["trainingProgramID"]
            }
            """)
        },
        new FunctionToolDefinition
        {
            Name = "createMovement",
            Description = "Create a movement in a session using base ID and modifier.",
            Parameters = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "trainingSessionID": { "type": "string" },
                    "movementBaseID": { "type": "string" },
                    "movementModifier": { "type": "integer" },
                    "notes": { "type": "string", "nullable": true },
                    "weightUnit": { "type": "integer", "default": 0 }
                },
                "required": ["trainingSessionID", "movementBaseID", "movementModifier"]
            }
            """)

        },
        new FunctionToolDefinition
        {
            Name = "createSetEntry",
            Description = "Create a set entry with reps, weight, and RPE.",
            Parameters = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "movementID": { "type": "string" },
                    "reps": { "type": "integer" },
                    "weight": { "type": "number" },
                    "rpe": { "type": "number" }
                },
                "required": ["movementID", "reps", "weight", "rpe"]
            }
            """)    
        },
        new FunctionToolDefinition
        {
            Name = "getIdentityUser",
            Description = "Returns the current user's GUID.",
            Parameters = new { }
        }
    };
}