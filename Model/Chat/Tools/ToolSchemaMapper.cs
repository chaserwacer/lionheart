using System.Reflection;
using System.Text.Json;
using lionheart.Functional;
using lionheart.Services.Llm;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Tools;
using NJsonSchema;

namespace Model.Tools
{
    /// <summary>
    /// Turns a tool-annotated method into the <see cref="ToolDefinition"/> the model is offered.
    /// </summary>
    /// <remarks>
    /// The JSON Schema it produces is the application's own, not any vendor's: each chat provider
    /// translates a <see cref="ToolDefinition"/> into its own tool shape, so adding a provider does
    /// not touch this file.
    /// </remarks>
    public static class ToolSchemaMapper
    {
        /// <summary>
        /// Maps a MethodInfo object to the tool definition describing it.
        /// </summary>
        public static async Task<Result<ToolDefinition, Problem>> MapTool(MethodInfo methodInfo)
        {
            if (methodInfo is null)
                return Result<ToolDefinition, Problem>.Failure(
                    new Problem.Unexpected("MethodInfo cannot be null."));

            var parameters = methodInfo.GetParameters();
            string functionParameters;

            var paramsEmpty = parameters.Length == 0;
            var paramsOnlyUser = parameters.Length == 1 && parameters[0].ParameterType == typeof(IdentityUser);

            if (paramsEmpty || paramsOnlyUser)
            {
                functionParameters = """
                    {
                        "type": "object",
                        "properties": {},
                        "required": []
                    }
                    """;
            }
            else
            {
                if (parameters[0].ParameterType != typeof(IdentityUser))
                    return Result<ToolDefinition, Problem>.Failure(new Problem.Unexpected(
                        $"First parameter of method '{methodInfo.Name}' is expected to be 'IdentityUser'."));
                if (parameters.Length > 2)
                    return Result<ToolDefinition, Problem>.Failure(new Problem.Unexpected(
                        $"Method '{methodInfo.Name}' must have at most two parameters."));

                if (parameters[1].ParameterType == typeof(Guid) || parameters[1].ParameterType == typeof(string))
                {
                    var scalarSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            [parameters[1].Name ?? "request"] = new
                            {
                                type = "string",
                                format = parameters[1].ParameterType == typeof(Guid) ? "uuid" : null
                            }
                        },
                        required = new[] { parameters[1].Name ?? "request" }
                    };

                    // Nulls are left out rather than written: a string has no format, and
                    // "format": null is not JSON Schema, which Anthropic refuses the whole request over.
                    functionParameters = JsonSerializer.Serialize(scalarSchema, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                        WriteIndented = false
                    });
                }
                else
                {
                    var schema = JsonSchema.FromType(parameters[1].ParameterType);
                    var schemaData = schema.ToJson();
                    schema = await JsonSchema.FromJsonAsync(schemaData);
                    functionParameters = schema.ToJson();
                }
            }

            var toolAttr = methodInfo.GetCustomAttribute<ToolAttribute>()!;

            return Result<ToolDefinition, Problem>.Success(
                new ToolDefinition(toolAttr.Name, toolAttr.Description, functionParameters));
        }
    }
}
