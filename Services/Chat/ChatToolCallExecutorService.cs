using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Ardalis.Result;
using lionheart.Endpoints.Training.Activity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Model.Chat.Tools;
using OpenAI.Chat;

namespace Model.Tools
{
    /// <summary>
    /// Executor for handling chat tool calls.
    /// </summary>
    public sealed class ChatToolCallExecutor(
        ToolRegistry registry,
        IServiceScopeFactory scopeFactory,
        JsonSerializerOptions jsonOptions)
    {
        private readonly ToolRegistry _registry = registry;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly JsonSerializerOptions _jsonOptions = jsonOptions;

        /// <summary>
        /// Executes a chat tool call and returns the corresponding tool chat message.
        /// Always returns a ToolChatMessage (success or standardized error envelope) when possible.
        /// </summary>
        public async Task<Result<ToolChatMessage>> ExecuteToolCallAsync(
            ChatToolCall toolCall,
            IdentityUser? user,
            ISet<string>? allowedToolNames = null,
            CancellationToken cancellationToken = default)
        {
            if (toolCall is null)
                return Result.Error("Provided ChatToolCall is null.");

            if (string.IsNullOrWhiteSpace(toolCall.Id))
                return Result.Error("Provided ChatToolCall is missing an Id.");

            var toolName = toolCall.FunctionName;
            if (string.IsNullOrWhiteSpace(toolName))
                return Result.Invalid(new[]
                {
                    new ValidationError { Identifier = "FunctionName", ErrorMessage = "Tool call is missing FunctionName." }
                });

            if (allowedToolNames is not null && !allowedToolNames.Contains(toolName))
                return Result.Forbidden();

            if (!_registry.TryGet(toolName, out var descriptor) || descriptor is null)
                return Result.NotFound();

            if (descriptor.RequireAuthorization && user is null)
                return Result.Unauthorized();


            using var scope = _scopeFactory.CreateScope();

            object service;
            try
            {
                service = scope.ServiceProvider.GetRequiredService(descriptor.ServiceType);
            }
            catch (Exception ex)
            {
                return Result.Error($"Failed to resolve tool service from DI.  ({ex.GetType().Name})");
            }

            object?[] args;
            try
            {
                var functionArgsJson = toolCall.FunctionArguments?.ToString();
                args = BindArguments(descriptor.MethodInfo, functionArgsJson, _jsonOptions);
            }
            catch (ToolBindingException bex)
            {
                return Result.Invalid(
                [
                    new ValidationError { Identifier = "arguments", ErrorMessage = bex.Message }
                ]);
            }

            var validationErrors = ValidateArguments(args);
            if (validationErrors.Count > 0)
                return Result.Invalid(validationErrors);

            object? invokeResult;
            try
            {
                invokeResult = descriptor.MethodInfo.Invoke(service, args);
            }
            catch (Exception ex)
            {
                return Result.Error($"Tool execution failed. ({ex.GetType().Name})");
            }

            var unwrapped = await UnwrapAsync(invokeResult, cancellationToken).ConfigureAwait(false);
            return Result.Success(ToToolMessage(toolCall.Id, unwrapped));
        }

        private ToolChatMessage ToToolMessage(string toolCallId, object? payload)
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            return new ToolChatMessage(toolCallId, json);
        }

        private static object?[] BindArguments(MethodInfo method, string? functionArgsJson, JsonSerializerOptions jsonOptions)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                return Array.Empty<object?>();

            var json = string.IsNullOrWhiteSpace(functionArgsJson) ? "{}" : functionArgsJson;

            if (parameters.Length == 1)
            {
                var p = parameters[0];
                var value = JsonSerializer.Deserialize(json, p.ParameterType, jsonOptions);
                if (value is null && IsNonNullableValueType(p.ParameterType))
                    throw new ToolBindingException($"Argument '{p.Name}' is required.", new { parameter = p.Name, type = p.ParameterType.FullName });
                return [value];
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException je)
            {
                throw new ToolBindingException("Invalid JSON for tool arguments.", new { je.Message });
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new ToolBindingException("Tool arguments must be a JSON object when multiple parameters are used.");

            var root = doc.RootElement;
            var bound = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var name = p.Name ?? $"arg{i}";

                if (root.TryGetProperty(name, out var prop))
                {
                    bound[i] = prop.Deserialize(p.ParameterType, jsonOptions);
                }
                else if (p.HasDefaultValue)
                {
                    bound[i] = p.DefaultValue;
                }
                else if (IsNonNullableValueType(p.ParameterType))
                {
                    throw new ToolBindingException($"Missing required argument '{name}'.", new { parameter = name, type = p.ParameterType.FullName });
                }
                else
                {
                    bound[i] = null;
                }
            }

            return bound;
        }

        private static List<ValidationError> ValidateArguments(object?[] args)
        {
            var errors = new List<ValidationError>();

            foreach (var arg in args)
            {
                if (arg is null)
                    continue;

                var context = new ValidationContext(arg);
                var results = new List<ValidationResult>();
                if (!Validator.TryValidateObject(arg, context, results, validateAllProperties: true))
                {
                    foreach (var vr in results)
                    {
                        var members = (vr.MemberNames?.Any() == true) ? vr.MemberNames : new[] { string.Empty };
                        foreach (var member in members)
                        {
                            errors.Add(new ValidationError
                            {
                                Identifier = member ?? string.Empty,
                                ErrorMessage = vr.ErrorMessage ?? "Validation error."
                            });
                        }
                    }
                }
            }

            return errors;
        }

        private static async Task<object?> UnwrapAsync(object? invokeResult, CancellationToken cancellationToken)
        {
            if (invokeResult is null)
                return null;

            if (invokeResult is Task task)
            {
                await task.ConfigureAwait(false);

                var taskType = task.GetType();
                if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // read Task<T>.Result via reflection
                    return taskType.GetProperty("Result")?.GetValue(task);
                }

                return null; // Task (non-generic)
            }

            return invokeResult;
        }

        private static bool IsNonNullableValueType(Type t) => t.IsValueType && Nullable.GetUnderlyingType(t) is null;

        private sealed class ToolBindingException : Exception
        {
            public object? Details { get; }

            public ToolBindingException(string message, object? details = null) : base(message)
            {
                Details = details;
            }
        }

    }

}