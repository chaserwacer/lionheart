using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using lionheart.Model.DTOs;
using lionheart.Services;

namespace Model.Tools
{
    /// <summary>
    /// Provides methods to retrieve OpenAI chat tool definitions for various operations/purposes.
    /// </summary>
    public static class OpenAiToolRetriever
    {

private static JsonObject BuildMovementModifierSchema()
{
    return new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject {
                ["type"] = "string",
                ["description"] = "Modifier label, e.g., 'Competition', 'Paused', 'Tempo'. Use 'Competition' when no special modifier."
            },
            ["equipmentID"] = new JsonObject {
                ["type"] = "string", ["format"] = "uuid"
            },
            // FULL equipment object (matches your entity's required fields)
            ["equipment"] = new JsonObject {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["equipmentID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                    ["name"]        = new JsonObject { ["type"] = "string" },
                    ["userID"]      = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                },
                ["required"] = new JsonArray("equipmentID", "name", "userID")
            },
            ["duration"] = new JsonObject {
                ["type"] = "integer",
                ["description"] = "Modifier duration in seconds (e.g., pause or tempo count). Use 0 when not applicable."
            }
        },
        ["required"] = new JsonArray("name", "equipmentID", "equipment", "duration")
    };
}



        /// <summary>
        /// Returns all ChatTool definitions needed for generating and fully populating a training program,
        /// including sessions, movements, and set entries.
        /// </summary>
        public static List<ChatTool> GetTrainingProgramPopulationTools()
        {
            var tools = new List<ChatTool>
            {
                // Create Training Program
                ChatTool.CreateFunctionTool(
                    functionName: "CreateTrainingProgramAsync",
                    functionDescription: "Create a new training program for a user.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["title"]     = new JsonObject { ["type"] = "string" },
                                ["startDate"] = new JsonObject { ["type"] = "string", ["format"] = "date" },
                                ["endDate"]   = new JsonObject { ["type"] = "string", ["format"] = "date" },
                                ["tags"] = new JsonObject
                                {
                                    ["type"]  = "array",
                                    ["items"] = new JsonObject { ["type"] = "string" }
                                }
                            },
                            ["required"] = new JsonArray("title", "startDate", "endDate", "tags")
                        }
                    )
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "GetTrainingProgramAsync",
                    functionDescription: "Get a specific training program by ID (for the current user).",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["TrainingProgramID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                                    },
                                    ["required"] = new JsonArray("TrainingProgramID")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),
                // Update Training Program
                ChatTool.CreateFunctionTool(
                    functionName: "UpdateTrainingProgramAsync",
                    functionDescription: "Update an existing training program.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["trainingProgramID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["title"] = new JsonObject { ["type"] = "string" },
                                        ["startDate"] = new JsonObject { ["type"] = "string", ["format"] = "date" },
                                        ["endDate"]   = new JsonObject { ["type"] = "string", ["format"] = "date" },
                                        ["tags"] = new JsonObject
                                        {
                                            ["type"]  = "array",
                                            ["items"] = new JsonObject { ["type"] = "string" }
                                        }
                                    },
                                    ["required"] = new JsonArray("trainingProgramID")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Delete Training Program
                ChatTool.CreateFunctionTool(
                    functionName: "DeleteTrainingProgramAsync",
                    functionDescription: "Delete a training program by ID.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["trainingProgramId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                            },
                            ["required"] = new JsonArray("trainingProgramId")
                        }
                    )
                ),

                ChatTool.CreateFunctionTool(
                    functionName: "CreateTrainingSessionAsync",
                    functionDescription: "Create a new training session within a program, including structured movement data.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["trainingSessionID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["trainingProgramID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["sessionNumber"] = new JsonObject { ["type"] = "integer" },
                                        ["date"] = new JsonObject { ["type"] = "string", ["format"] = "date" },
                                        ["status"] = new JsonObject { ["type"] = "integer" },
                                        ["movements"] = new JsonObject
                                        {
                                            ["type"] = "array",
                                            ["items"] = new JsonObject
                                            {
                                                ["type"] = "object",
                                                ["properties"] = new JsonObject
                                                {
                                                    ["movementID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                                    ["movementBaseID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                                    ["movementModifier"] = new JsonObject { ["type"] = "object" }, // optionally expand if strict validation is needed
                                                    ["notes"] = new JsonObject { ["type"] = "string" },
                                                    ["isCompleted"] = new JsonObject { ["type"] = "boolean" },
                                                    ["sets"] = new JsonObject
                                                    {
                                                        ["type"] = "array",
                                                        ["items"] = new JsonObject
                                                        {
                                                            ["type"] = "object",
                                                            ["properties"] = new JsonObject
                                                            {
                                                                ["setEntryID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                                                ["recommendedReps"] = new JsonObject { ["type"] = "integer" },
                                                                ["recommendedWeight"] = new JsonObject { ["type"] = "number" },
                                                                ["recommendedRPE"] = new JsonObject { ["type"] = "number" }
                                                            },
                                                            ["required"] = new JsonArray("setEntryID", "recommendedReps", "recommendedWeight", "recommendedRPE")
                                                        }
                                                    }
                                                },
                                                ["required"] = new JsonArray("movementID", "movementBaseID", "sets")
                                            }
                                        }
                                    },
                                    ["required"] = new JsonArray("trainingSessionID", "trainingProgramID", "sessionNumber", "date", "status", "movements")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),


                // Update Training Session
                ChatTool.CreateFunctionTool(
                    functionName: "UpdateTrainingSessionAsync",
                    functionDescription: "Update an existing training session.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["trainingSessionID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["date"] = new JsonObject { ["type"] = "string", ["format"] = "date" },
                                        ["status"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Planned", "InProgress", "Completed", "Skipped") }
                                    },
                                    ["required"] = new JsonArray("trainingSessionID", "date", "status")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Delete Training Session
                ChatTool.CreateFunctionTool(
                    functionName: "DeleteTrainingSessionAsync",
                    functionDescription: "Delete a training session by ID.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["trainingSessionID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                            },
                            ["required"] = new JsonArray("trainingSessionID")
                        }
                    )
                ),

                // Create Movement
                ChatTool.CreateFunctionTool(
                    functionName: "CreateMovementAsync",
                    functionDescription: "Create a new movement within a training session.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["trainingSessionID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["movementBaseID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["notes"] = new JsonObject { ["type"] = "string" },
                                        ["movementModifier"] = new JsonObject
                                        {
                                            ["type"] = "object",
                                            ["properties"] = new JsonObject
                                            {
                                                ["name"] = new JsonObject { ["type"] = "string" },
                                                ["equipmentID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                                ["duration"] = new JsonObject { ["type"] = "integer", ["nullable"] = true }
                                            },
                                            ["required"] = new JsonArray("name", "equipmentID")
                                        },
                                        ["weightUnit"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Kilograms", "Pounds") }
                                    },
                                    ["required"] = new JsonArray("trainingSessionID", "movementBaseID", "notes", "movementModifier", "weightUnit")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Update Movement
                ChatTool.CreateFunctionTool(
                    functionName: "UpdateMovementAsync",
                    functionDescription: "Update an existing movement.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["movementID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["movementBaseID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["notes"] = new JsonObject { ["type"] = "string" },
                                        ["movementModifier"] = new JsonObject
                                        {
                                            ["type"] = "object",
                                            ["properties"] = new JsonObject
                                            {
                                                ["name"] = new JsonObject { ["type"] = "string" },
                                                ["equipmentID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                                ["duration"] = new JsonObject { ["type"] = "integer", ["nullable"] = true }
                                            },
                                            ["required"] = new JsonArray("name", "equipmentID")
                                        },
                                        ["isCompleted"] = new JsonObject { ["type"] = "boolean" },
                                        ["weightUnit"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Kilograms", "Pounds") }
                                    },
                                    ["required"] = new JsonArray("movementID", "movementBaseID", "notes", "movementModifier", "isCompleted", "weightUnit")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Delete Movement
                ChatTool.CreateFunctionTool(
                    functionName: "DeleteMovementAsync",
                    functionDescription: "Delete a movement by ID.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["movementId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                            },
                            ["required"] = new JsonArray("movementId")
                        }
                    )
                ),

                // Create Set Entry
                ChatTool.CreateFunctionTool(
                    functionName: "CreateSetEntryAsync",
                    functionDescription: "Add a set entry to a movement.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["movementID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["recommendedReps"] = new JsonObject { ["type"] = "integer" },
                                        ["recommendedWeight"] = new JsonObject { ["type"] = "number" },
                                        ["recommendedRPE"] = new JsonObject { ["type"] = "number" },
                                        ["actualReps"] = new JsonObject { ["type"] = "integer" },
                                        ["actualWeight"] = new JsonObject { ["type"] = "number" },
                                        ["actualRPE"] = new JsonObject { ["type"] = "number" }
                                    },
                                    ["required"] = new JsonArray("movementID", "recommendedReps", "recommendedWeight", "recommendedRPE", "actualReps", "actualWeight", "actualRPE")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Update Set Entry
                ChatTool.CreateFunctionTool(
                    functionName: "UpdateSetEntryAsync",
                    functionDescription: "Update an existing set entry.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["setEntryID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["recommendedReps"] = new JsonObject { ["type"] = "integer" },
                                        ["recommendedWeight"] = new JsonObject { ["type"] = "number" },
                                        ["recommendedRPE"] = new JsonObject { ["type"] = "number" },
                                        ["actualReps"] = new JsonObject { ["type"] = "integer" },
                                        ["actualWeight"] = new JsonObject { ["type"] = "number" },
                                        ["actualRPE"] = new JsonObject { ["type"] = "number" }
                                    },
                                    ["required"] = new JsonArray("setEntryID", "recommendedReps", "recommendedWeight", "recommendedRPE", "actualReps", "actualWeight", "actualRPE")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Delete Set Entry
                ChatTool.CreateFunctionTool(
                    functionName: "DeleteSetEntryAsync",
                    functionDescription: "Delete a set entry by ID.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["setEntryId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                            },
                            ["required"] = new JsonArray("setEntryId")
                        }
                    )
                ),

                ChatTool.CreateFunctionTool(
                    functionName: "CreateTrainingSessionWeekAsync",
                    functionDescription: "Create a full week of structured training sessions with movements and sets.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["request"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["trainingProgramID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                        ["sessions"] = new JsonObject
                                        {
                                            ["type"] = "array",
                                            ["items"] = new JsonObject
                                            {
                                                ["type"] = "object",
                                                ["properties"] = new JsonObject
                                                {
                                                    ["date"] = new JsonObject { ["type"] = "string", ["format"] = "date" },
                                                    ["sessionNumber"] = new JsonObject { ["type"] = "integer" },
                                                    ["movements"] = new JsonObject
                                                    {
                                                        ["type"] = "array",
                                                        ["items"] = new JsonObject
                                                        {
                                                            ["type"] = "object",
                                                            ["properties"] = new JsonObject
                                                            {
                                                                ["movementBaseID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                                                                ["movementModifier"] = BuildMovementModifierSchema(),

                                                                ["notes"] = new JsonObject { ["type"] = "string" },
                                                                ["weightUnit"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Kilograms", "Pounds") },
                                                                ["sets"] = new JsonObject
                                                                {
                                                                    ["type"] = "array",
                                                                    ["items"] = new JsonObject
                                                                    {
                                                                        ["type"] = "object",
                                                                        ["properties"] = new JsonObject
                                                                        {
                                                                            ["recommendedReps"] = new JsonObject { ["type"] = "integer" },
                                                                            ["recommendedWeight"] = new JsonObject { ["type"] = "number" },
                                                                            ["recommendedRPE"] = new JsonObject { ["type"] = "number" }
                                                                        },
                                                                        ["required"] = new JsonArray("recommendedReps", "recommendedWeight", "recommendedRPE")
                                                                    }
                                                                }
                                                            },
                                                            ["required"] = new JsonArray("movementBaseID", "movementModifier", "notes", "weightUnit", "sets")
                                                        }
                                                    }
                                                },
                                                ["required"] = new JsonArray("date", "sessionNumber", "movements")
                                            }
                                        }
                                    },
                                    ["required"] = new JsonArray("trainingProgramID", "sessions")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),



                // Get Movement Bases
                ChatTool.CreateFunctionTool(
                    functionName: "GetMovementBasesAsync",
                    functionDescription: "Gets all movement bases available for creating movements.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject(),
                            ["required"] = new JsonArray()
                        }
                    )
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "GetEquipmentsAsync",
                    functionDescription: "Gets all equipment available for creating movements.",
                    functionParameters: BinaryData.FromObjectAsJson(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject(),
                            ["required"] = new JsonArray()
                        }
                    )
                )
            };

            return tools;
        }

        /// <summary>
        /// Returns all ChatTool definitions used to modify a specific training session using ChatToolHelper.
        /// </summary>
        public static async Task<List<ChatTool>> GetModifyTrainingSessionTools()
        {
            var tools = new List<ChatTool>();
            var sessionTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(TrainingSessionService),
                new[]
                {
                    "GetTrainingSessionAsync",
                    "UpdateTrainingSessionAsync",
                }
            );

            if (sessionTools.IsSuccess)
            {
                tools.AddRange(sessionTools.Value);
            }


            var movementTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(MovementService),
                new[]
                {
                    "CreateMovementAsync",
                    "UpdateMovementAsync",
                    "DeleteMovementAsync",
                    // "GetMovementBasesAsync",
                    // "GetEquipmentsAsync",
                }
            );

            if (movementTools.IsSuccess)
            {
                tools.AddRange(movementTools.Value);
            }

            var setEntryTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(SetEntryService),
                new[]
                {
                    "CreateSetEntryAsync",
                    "UpdateSetEntryAsync",
                    "DeleteSetEntryAsync"
                }
            );

            if (setEntryTools.IsSuccess)
            {
                tools.AddRange(setEntryTools.Value);
            }


            return tools;
        }

        /// <summary>
        /// Returns all ChatTool definitions used for the chat interface to access user data.
        /// </summary>
        public static async Task<List<ChatTool>> GetChatTools()
        {
            var tools = new List<ChatTool>();
            
            // Training Program tools
            var programTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(TrainingProgramService),
                new[]
                {
                    "GetTrainingProgramAsync",
                    "GetTrainingProgramsAsync"
                }
            );
            
            if (programTools.IsSuccess)
            {
                tools.AddRange(programTools.Value);
            }
            
            // Training Session tools
            var sessionTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(TrainingSessionService),
                new[]
                {
                    "GetTrainingSessionAsync",
                    "GetTrainingSessionsByDateRangeAsync",
                    "GetTrainingSessionsAsync"
                }
            );
            
            if (sessionTools.IsSuccess)
            {
                tools.AddRange(sessionTools.Value);
            }
            
            // Movement tools
            var movementTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(MovementService),
                new[]
                {
                    "GetMovementsAsync",
                    "GetMovementBasesAsync",
                    "GetEquipmentsAsync"
                }
            );
            
            if (movementTools.IsSuccess)
            {
                tools.AddRange(movementTools.Value);
            }
            
            // Wellness tools
            var wellnessTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(WellnessService),
                new[]
                {

                    "GetWellnessStatesAsync"
                }
            );
            
            if (wellnessTools.IsSuccess)
            {
                tools.AddRange(wellnessTools.Value);
            }
            
            // Oura tools
            var ouraTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(OuraService),
                new[]
                {
                    "GetDailyOuraInfosAsync"
                }
            );
            
            if (ouraTools.IsSuccess)
            {
                tools.AddRange(ouraTools.Value);
            }
            
            // Activity tools
            var activityTools = await ChatToolHelper.CreateToolsFromMethodsAsync(
                typeof(ActivityService),
                new[]
                {
                    "GetActivitiesAsync"
                }
            );
            
            if (activityTools.IsSuccess)
            {
                tools.AddRange(activityTools.Value);
            }
            
            return tools;
        }
    }
}