using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Text.Json;

namespace Model.Tools
{
    public static class OpenAiToolHandler
    {
        /// <summary>
        /// Returns all ChatTool definitions needed for generating and fully populating a training program,
        /// including sessions, movements, and set entries.
        /// </summary>
        public static List<ChatTool> GetTrainingProgramPopulationTools()
        {
            var tools = new List<ChatTool>
            {
                // Training Program Tools
                ChatTool.CreateFunctionTool(
                functionName: "CreateTrainingProgramAsync",
                functionDescription: "Create a new training program for a user.",
                functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "request": {
                            "type": "object",
                            "properties": {
                                "title": { "type": "string" },
                                "startDate": { "type": "string", "format": "date" },
                                "endDate": { "type": "string", "format": "date" },
                                "tags": { "type": "array", "items": { "type": "string" } }
                            },
                            "required": ["title", "startDate", "endDate"]
                        }
                    },
                    "required": ["request"]
                }
                """)
            ),
                ChatTool.CreateFunctionTool(
                functionName: "UpdateTrainingProgramAsync",
                functionDescription: "Update an existing training program.",
                functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "request": {
                            "type": "object",
                            "properties": {
                                "trainingProgramID": { "type": "string", "format": "uuid" },
                                "title": { "type": "string" },
                                "startDate": { "type": "string", "format": "date" },
                                "endDate": { "type": "string", "format": "date" },
                                "tags": { "type": "array", "items": { "type": "string" } }
                            },
                            "required": ["trainingProgramID"]
                        }
                    },
                    "required": ["request"]
                }
                """)
            ),
                ChatTool.CreateFunctionTool(
                functionName: "DeleteTrainingProgramAsync",
                functionDescription: "Delete a training program by ID.",
                functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "trainingProgramId": { "type": "string", "format": "uuid" }
                    },
                    "required": ["trainingProgramId"]
                }
                """)
            ),

                // Training Session Tools
                ChatTool.CreateFunctionTool(
                functionName: "CreateTrainingSessionAsync",
                functionDescription: "Create a new training session within a program.",
                functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "request": {
                            "type": "object",
                            "properties": {
                                "trainingProgramID": { "type": "string", "format": "uuid" },
                                "date": { "type": "string", "format": "date" }
                            },
                            "required": ["trainingProgramID", "date"]
                        }
                    },
                    "required": ["request"]
                }
                """)
            ),
                ChatTool.CreateFunctionTool(
                functionName: "UpdateTrainingSessionAsync",
                functionDescription: "Update an existing training session.",
                functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "request": {
                            "type": "object",
                            "properties": {
                                "trainingSessionID": { "type": "string", "format": "uuid" },
                                "date": { "type": "string", "format": "date" },
                                "status": { "type": "string", "enum": ["Planned", "InProgress", "Completed", "Skipped"] }
                            },
                            "required": ["trainingSessionID"]
                        }
                    },
                    "required": ["request"]
                }
                """)
            ),
                ChatTool.CreateFunctionTool(
                functionName: "DeleteTrainingSessionAsync",
                functionDescription: "Delete a training session by ID.",
                functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "trainingSessionID": { "type": "string", "format": "uuid" }
                    },
                    "required": ["trainingSessionID"]
                }
                """)
            ),

                // Movement Tools
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

                // Set Entry Tools
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
            )
            };

            return tools;
        }

        public static List<ChatTool> GetModifyTrainingSessionTools()
        {
            var tools = new List<ChatTool>
            {
                // Get a specific training session
                ChatTool.CreateFunctionTool(
                    functionName: "GetTrainingSessionAsync",
                    functionDescription: "Get a specific training session by ID.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "trainingSessionID": { "type": "string", "format": "uuid" }
                        },
                        "required": ["trainingSessionID"]
                    }
                    """)
                ),

                // Create a new training session
                ChatTool.CreateFunctionTool(
                    functionName: "CreateTrainingSessionAsync",
                    functionDescription: "Create a new training session within a program.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "request": {
                                "type": "object",
                                "properties": {
                                    "trainingProgramID": { "type": "string", "format": "uuid" },
                                    "date": { "type": "string", "format": "date" }
                                },
                                "required": ["trainingProgramID", "date"]
                            }
                        },
                        "required": ["request"]
                    }
                    """)
                ),

                // Edit (update) a training session
                ChatTool.CreateFunctionTool(
                    functionName: "UpdateTrainingSessionAsync",
                    functionDescription: "Update an existing training session.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "request": {
                                "type": "object",
                                "properties": {
                                    "trainingSessionID": { "type": "string", "format": "uuid" },
                                    "date": { "type": "string", "format": "date" },
                                    "status": { "type": "string", "enum": ["Planned", "InProgress", "Completed", "Skipped"] }
                                },
                                "required": ["trainingSessionID"]
                            }
                        },
                        "required": ["request"]
                    }
                    """)
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