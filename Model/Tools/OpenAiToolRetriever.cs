using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Model.Tools
{
    public static class OpenAiToolRetriever
    {
        /// <summary>
        /// Returns all ChatTool definitions needed for generating and fully populating a training program,
        /// including sessions, movements, and set entries.
        /// </summary>
        public static List<ChatTool> GetTrainingProgramPopulationTools()
        {
            // Helper local to build JSON schemas
            JsonObject BuildSchema(object schema) => (JsonObject)schema;

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

                // Create Training Session
                ChatTool.CreateFunctionTool(
                    functionName: "CreateTrainingSessionAsync",
                    functionDescription: "Create a new training session within a program.",
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
                                        ["date"] = new JsonObject { ["type"] = "string", ["format"] = "date" }
                                    },
                                    ["required"] = new JsonArray("trainingProgramID", "date")
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
                                    ["required"] = new JsonArray("trainingSessionID")
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
                                        ["movementModifier"] = new JsonObject { ["type"] = "object" },
                                        ["weightUnit"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Kilograms", "Pounds") }
                                    },
                                    ["required"] = new JsonArray("trainingSessionID", "movementBaseID")
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
                                        ["movementModifier"] = new JsonObject { ["type"] = "object" },
                                        ["isCompleted"] = new JsonObject { ["type"] = "boolean" },
                                        ["weightUnit"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Kilograms", "Pounds") }
                                    },
                                    ["required"] = new JsonArray("movementID")
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
                                    ["required"] = new JsonArray("movementID")
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
                                    ["required"] = new JsonArray("setEntryID")
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
                )
            };

            return tools;
        }

        /// <summary>
        /// Returns all ChatTool definitions used to modify a specific training session.
        /// </summary>
        public static List<ChatTool> GetModifyTrainingSessionTools()
        {
            var tools = new List<ChatTool>
            {
                // Get Training Session
                ChatTool.CreateFunctionTool(
                    functionName: "GetTrainingSessionAsync",
                    functionDescription: "Get a specific training session by ID.",
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
                                        ["trainingSessionID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                                    },
                                    ["required"] = new JsonArray("trainingProgramID", "trainingSessionID")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Create Training Session
                ChatTool.CreateFunctionTool(
                    functionName: "CreateTrainingSessionAsync",
                    functionDescription: "Create a new training session within a program.",
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
                                        ["date"] = new JsonObject { ["type"] = "string", ["format"] = "date" }
                                    },
                                    ["required"] = new JsonArray("trainingProgramID", "date")
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
                                    ["required"] = new JsonArray("trainingSessionID")
                                }
                            },
                            ["required"] = new JsonArray("request")
                        }
                    )
                ),

                // Create a new movement
                ChatTool.CreateFunctionTool(
                    functionName: "CreateMovementAsync",
                    functionDescription: "Create a new movement within a training session.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "request": {
                                "type": "object",
                                "properties": {
                                    "trainingSessionID": { "type": "string", "format": "uuid" },
                                    "movementBaseID": { "type": "string", "format": "uuid" },
                                    "notes": { "type": "string" },
                                    "movementModifier": { "type": "object" },
                                    "weightUnit": { "type": "string", "enum": ["Kilograms", "Pounds"] }
                                },
                                "required": ["trainingSessionID", "movementBaseID"]
                            }
                        },
                        "required": ["request"]
                    }
                    """)
                ),

                // Edit (update) a movement
                ChatTool.CreateFunctionTool(
                    functionName: "UpdateMovementAsync",
                    functionDescription: "Update an existing movement.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "request": {
                                "type": "object",
                                "properties": {
                                    "movementID": { "type": "string", "format": "uuid" },
                                    "movementBaseID": { "type": "string", "format": "uuid" },
                                    "notes": { "type": "string" },
                                    "movementModifier": { "type": "object" },
                                    "isCompleted": { "type": "boolean" },
                                    "weightUnit": { "type": "string", "enum": ["Kilograms", "Pounds"] }
                                },
                                "required": ["movementID"]
                            }
                        },
                        "required": ["request"]
                    }
                    """)
                ),

                // Delete a movement
                ChatTool.CreateFunctionTool(
                    functionName: "DeleteMovementAsync",
                    functionDescription: "Delete a movement by ID.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "movementId": { "type": "string", "format": "uuid" }
                        },
                        "required": ["movementId"]
                    }
                    """)
                ),

                // Create a set entry
                ChatTool.CreateFunctionTool(
                    functionName: "CreateSetEntryAsync",
                    functionDescription: "Add a set entry to a movement.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "request": {
                                "type": "object",
                                "properties": {
                                    "movementID": { "type": "string", "format": "uuid" },
                                    "recommendedReps": { "type": "integer" },
                                    "recommendedWeight": { "type": "number" },
                                    "recommendedRPE": { "type": "number" },
                                    "actualReps": { "type": "integer" },
                                    "actualWeight": { "type": "number" },
                                    "actualRPE": { "type": "number" }
                                },
                                "required": ["movementID"]
                            }
                        },
                        "required": ["request"]
                    }
                    """)
                ),

                // Edit (update) a set entry
                ChatTool.CreateFunctionTool(
                    functionName: "UpdateSetEntryAsync",
                    functionDescription: "Update an existing set entry.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "request": {
                                "type": "object",
                                "properties": {
                                    "setEntryID": { "type": "string", "format": "uuid" },
                                    "recommendedReps": { "type": "integer" },
                                    "recommendedWeight": { "type": "number" },
                                    "recommendedRPE": { "type": "number" },
                                    "actualReps": { "type": "integer" },
                                    "actualWeight": { "type": "number" },
                                    "actualRPE": { "type": "number" }
                                },
                                "required": ["setEntryID"]
                            }
                        },
                        "required": ["request"]
                    }
                    """)
                ),

                // Delete a set entry
                ChatTool.CreateFunctionTool(
                    functionName: "DeleteSetEntryAsync",
                    functionDescription: "Delete a set entry by ID.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "setEntryId": { "type": "string", "format": "uuid" }
                        },
                        "required": ["setEntryId"]
                    }
                    """)
                ),

                // Get all movement bases
                ChatTool.CreateFunctionTool(
                    functionName: "GetMovementBasesAsync",
                    functionDescription: "Gets all movement bases available for creating movements.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {},
                        "required": []
                    }
                    """)
                )
            };

            return tools;
        }
    }
}