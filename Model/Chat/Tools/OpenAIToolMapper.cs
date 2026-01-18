using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Ardalis.Result;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Tools;
using NJsonSchema;
using OpenAI.Chat;

namespace Model.Tools
{
    /// <summary>
    /// Object providing functionality to map backend tools to OpenAI tool call definitions.
    /// </summary>
    public static class OpenAIToolMapper
    {
        /// <summary>
        /// Maps a MethodInfo object to its corresponding ChatTool definition.
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        public static async Task<Result<ChatTool>> MapTool(MethodInfo methodInfo)
        {
            if (methodInfo is null)
                return Result.Error("MethodInfo cannot be null.");

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
                    return Result.Error($"First parameter of method '{methodInfo.Name}' is expected to be 'IdentityUser'.");
                if (parameters.Length > 2)
                    return Result.Error($"Method '{methodInfo.Name}' must have at most two parameters.");

                if (parameters[1].ParameterType == typeof(Guid) || parameters[1].ParameterType == typeof(string))
                {
                    var openAiSchema = new
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

                    functionParameters = JsonSerializer.Serialize(openAiSchema, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
            var tool = ChatTool.CreateFunctionTool(
                functionName: toolAttr.Name,
                functionDescription: toolAttr.Description,
                functionParameters: BinaryData.FromString(functionParameters)
            );

            return Result.Success(tool);
        }

        /// <summary>
        /// Maps a list of MethodInfo objects to their corresponding ChatTool definitions.
        /// </summary>
        /// <param name="methodInfos"></param>
        /// <returns></returns>
        public static async Task<Result<List<ChatTool>>> MapTools(List<MethodInfo> methodInfos)
        {
            var tools = new List<ChatTool>();
            var errors = new List<string>();
            foreach (var methodInfo in methodInfos)
            {
                var result = await MapTool(methodInfo);
                if (!result.IsSuccess)
                    errors.AddRange(result.Errors);
                else
                    tools.Add(result.Value);
            }

            return errors.Any()
                ? Result.Error(new ErrorList(errors))
                : Result.Success(tools);
        }
    }

}