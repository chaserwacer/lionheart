using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Ardalis.Result;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Parlot.Fluent;
using ModelContextProtocol.Server;
using System.Text.Json;
using NJsonSchema;
using Microsoft.AspNetCore.Identity;

public static class ChatToolHelper
{
    /// <summary>
    /// Create a single ChatTool from a given method. Returns Failure instead of throwing.
    /// </summary>
    public static async Task<Result<ChatTool>> CreateFromMethodAsync(Type declaringType, string methodName)
    {
        var method = declaringType
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (method is null)
            return Result.Error($"Method '{methodName}' not found on type '{declaringType.FullName}'.");

        var parameters = method.GetParameters();

        string functionParameters;

        if (parameters.Length == 0 || (parameters.Length == 1 && parameters[0].ParameterType == typeof(IdentityUser)))
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
                return Result.Error($"Method '{methodName}' must have a first parameter of type 'IdentityUser' to match method design.");

            if (parameters.Length > 2)
                return Result.Error($"Method '{methodName}' must have at most two parameters: 'IdentityUser' and a request object.");

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

        var tool = ChatTool.CreateFunctionTool(
            functionName: method.Name,
            functionDescription: method.Name,           // TODO: Implement system to get description
            functionParameters: BinaryData.FromString(functionParameters)
        );

        return Result.Success(tool);

    }

    /// <summary>
    /// Create multiple ChatTools by explicit method names.
    /// </summary>
    public static async Task<Result<List<ChatTool>>> CreateToolsFromMethodsAsync(Type declaringType, IEnumerable<string> methodNames)
    {
        var tools = new List<ChatTool>();
        var errors = new List<string>();

        foreach (var name in methodNames)
        {
            var result = await CreateFromMethodAsync(declaringType, name);
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
