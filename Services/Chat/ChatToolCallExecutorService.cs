using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using lionheart.Functional;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using lionheart.Services.Llm;
using Model.Chat.Tools;
using lionheart.Model.Chat.Changes;
using lionheart.Model.Chat.Tools;

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
        /// Executes one tool the model asked for and returns what to send back to it.
        /// </summary>
        /// <remarks>
        /// Every refusal here is something the model can act on — a tool that does not exist,
        /// arguments that will not bind — so it comes back as a <see cref="Problem"/> for the caller
        /// to put in front of the model rather than as a failed exchange.
        /// </remarks>
        public async Task<Result<ToolAnswer, Problem>> ExecuteToolCallAsync(
            ToolInvocation toolCall,
            IdentityUser? user,
            ISet<string>? allowedToolNames = null,
            CancellationToken cancellationToken = default)
        {
            if (toolCall is null)
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Unexpected("Provided tool invocation is null."));

            if (string.IsNullOrWhiteSpace(toolCall.CallId))
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Unexpected("Provided tool invocation is missing a call id."));

            var toolName = toolCall.ToolName;
            if (string.IsNullOrWhiteSpace(toolName))
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Invalid("ToolName", "Tool call is missing a tool name."));

            if (!_registry.TryGet(toolName, out var descriptor) || descriptor is null)
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.NotFound("No such tool is available."));

            if (allowedToolNames is not null && !allowedToolNames.Contains(toolName))
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Forbidden("This tool is not available in this conversation."));

            if (descriptor.RequireAuthorization && user is null)
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Unauthorized("You are not authorized to call this tool."));


            using var scope = _scopeFactory.CreateScope();

            object service;
            try
            {
                service = scope.ServiceProvider.GetRequiredService(descriptor.ServiceType.GetInterfaces().First());
            }
            catch (Exception ex)
            {
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Unexpected($"Failed to resolve tool service from DI.  ({ex.GetType().Name})"));
            }

            object?[] args;
            try
            {
                args = BindArguments(descriptor.MethodInfo, toolCall.ArgumentsJson, user, _jsonOptions);
            }
            catch (ToolBindingException bex)
            {
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Invalid("arguments", bex.Message));
            }

            var validationErrors = ValidateArguments(args);
            if (validationErrors.Count > 0)
                return Result<ToolAnswer, Problem>.Failure(new Problem.Invalid(validationErrors));

            object? invokeResult;
            try
            {
                invokeResult = descriptor.MethodInfo.Invoke(service, args);
            }
            catch (Exception ex)
            {
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Unexpected($"Tool execution failed. ({ex.GetType().Name})"));
            }

            // An async tool fails when its task is awaited, after Invoke has returned, so the
            // failure is caught here too or it would end the exchange instead of reaching the model.
            object? unwrapped;
            try
            {
                unwrapped = await UnwrapAsync(invokeResult, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<ToolAnswer, Problem>.Failure(
                    new Problem.Unexpected($"Tool execution failed. ({ex.GetType().Name})"));
            }

            return Answer(toolCall.CallId, unwrapped);
        }

        /// <summary>
        /// A proposing tool's draft goes to the exchange to become a card, and the model is told what it
        /// proposed; a refused draft is a failed call, so the model hears why and can put it right.
        /// Anything else is data, serialized for the model to read.
        /// </summary>
        private Result<ToolAnswer, Problem> Answer(string callId, object? unwrapped) => unwrapped switch
        {
            Result<ProposalDraft, Problem> drafted => drafted.Map(draft =>
                (ToolAnswer)new ToolAnswer.Proposed(ToolOutcome.Produced(callId, draft.ToldToModel), draft)),
            _ => Result<ToolAnswer, Problem>.Success(
                new ToolAnswer.Read(ToolOutcome.Produced(callId, JsonSerializer.Serialize(unwrapped, _jsonOptions)))),
        };

        private static object?[] BindArguments(MethodInfo method, string? functionArgsJson, IdentityUser? user, JsonSerializerOptions jsonOptions)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                return Array.Empty<object?>();

            var json = string.IsNullOrWhiteSpace(functionArgsJson) ? "{}" : functionArgsJson;

            // Filter out injectable parameters (like IdentityUser) for JSON binding
            var jsonBindableParams = parameters.Where(p => !IsInjectableParameter(p.ParameterType)).ToArray();

            // If only one JSON-bindable parameter, deserialize the entire JSON directly to that parameter
            // If only one JSON-bindable parameter, deserialize either:
            // 1) the matching property (common tool-call shape: { paramName: value })
            // 2) or the full JSON (raw value shape: "..." or 123 etc.)
            if (jsonBindableParams.Length == 1)
            {
                var bound = new object?[parameters.Length];
                var single = jsonBindableParams[0];
                var singleName = single.Name ?? "arg0";

                JsonDocument? singleDoc = null;
                JsonElement singleRoot = default;
                bool parsed = false;

                try
                {
                    singleDoc = JsonDocument.Parse(json);
                    singleRoot = singleDoc.RootElement;
                    parsed = true;
                }
                catch
                {
                    // Not an object/value JSON we can parse (or it's a raw primitive string not wrapped as JSON).
                    // We'll let JsonSerializer handle it below.
                }

                try
                {
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var p = parameters[i];

                        if (IsInjectableParameter(p.ParameterType))
                        {
                            bound[i] = GetInjectedValue(p.ParameterType, user);
                            continue;
                        }

                        object? value;

                        // A model is free to send arguments that are not valid JSON, or JSON of
                        // the wrong shape. That is a bad tool call to report back to it, not a
                        // fault in the conversation, so it converts to the same
                        // ToolBindingException the multi-parameter path below raises.
                        try
                        {
                            // If JSON is an object and it has a property matching the param name,
                            // bind from that property instead of the whole object.
                            value = parsed && singleRoot.ValueKind == JsonValueKind.Object &&
                                    singleRoot.TryGetProperty(singleName, out var prop)
                                ? prop.Deserialize(p.ParameterType, jsonOptions)
                                : JsonSerializer.Deserialize(json, p.ParameterType, jsonOptions);
                        }
                        catch (JsonException je)
                        {
                            throw new ToolBindingException($"Invalid JSON for argument '{p.Name}'.",
                                new { parameter = p.Name, type = p.ParameterType.FullName, je.Message });
                        }

                        if (value is null && IsNonNullableValueType(p.ParameterType))
                            throw new ToolBindingException($"Argument '{p.Name}' is required.",
                                new { parameter = p.Name, type = p.ParameterType.FullName });

                        bound[i] = value;
                    }
                }
                finally
                {
                    singleDoc?.Dispose();
                }

                return bound;
            }


            // Multiple JSON-bindable parameters: parse JSON as object and bind by property name
            JsonDocument? doc = null;
            JsonElement root = default;

            if (jsonBindableParams.Length > 1)
            {
                try
                {
                    doc = JsonDocument.Parse(json);
                    root = doc.RootElement;
                }
                catch (JsonException je)
                {
                    throw new ToolBindingException("Invalid JSON for tool arguments.", new { je.Message });
                }

                if (root.ValueKind != JsonValueKind.Object)
                    throw new ToolBindingException("Tool arguments must be a JSON object when multiple parameters are used.");
            }

            var boundMulti = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var name = p.Name ?? $"arg{i}";

                // Inject IdentityUser from context instead of JSON
                if (IsInjectableParameter(p.ParameterType))
                {
                    boundMulti[i] = GetInjectedValue(p.ParameterType, user);
                    continue;
                }

                if (doc is not null && root.TryGetProperty(name, out var prop))
                {
                    boundMulti[i] = prop.Deserialize(p.ParameterType, jsonOptions);
                }
                else if (p.HasDefaultValue)
                {
                    boundMulti[i] = p.DefaultValue;
                }
                else if (IsNonNullableValueType(p.ParameterType))
                {
                    throw new ToolBindingException($"Missing required argument '{name}'.", new { parameter = name, type = p.ParameterType.FullName });
                }
                else
                {
                    boundMulti[i] = null;
                }
            }

            return boundMulti;
        }

        /// <summary>
        /// Determines if a parameter type should be injected from context rather than deserialized from JSON.
        /// </summary>
        private static bool IsInjectableParameter(Type parameterType)
        {
            return parameterType == typeof(IdentityUser)
                || parameterType.IsAssignableTo(typeof(IdentityUser));
        }

        /// <summary>
        /// Gets the injected value for a parameter type from the available context.
        /// </summary>
        private static object? GetInjectedValue(Type parameterType, IdentityUser? user)
        {
            if (parameterType == typeof(IdentityUser) || parameterType.IsAssignableTo(typeof(IdentityUser)))
                return user;

            return null;
        }

        private static List<InvalidField> ValidateArguments(object?[] args)
        {
            var errors = new List<InvalidField>();

            foreach (var arg in args)
            {
                if (arg is null)
                    continue;

                // A range the model left out binds as two default dates, which no reader can use.
                if (arg is lionheart.Model.Analysis.DayRange { From.Year: 1 } or lionheart.Model.Analysis.DayRange { To.Year: 1 })
                {
                    errors.Add(new InvalidField("range", "Give the range as { \"from\": \"yyyy-MM-dd\", \"to\": \"yyyy-MM-dd\" }."));
                    continue;
                }

                var context = new ValidationContext(arg);
                var results = new List<ValidationResult>();
                if (!Validator.TryValidateObject(arg, context, results, validateAllProperties: true))
                {
                    foreach (var vr in results)
                    {
                        var members = (vr.MemberNames?.Any() == true) ? vr.MemberNames : new[] { string.Empty };
                        foreach (var member in members)
                        {
                            errors.Add(new InvalidField(
                                member ?? string.Empty, vr.ErrorMessage ?? "Validation error."));
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