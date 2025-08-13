using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.Prompt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using Model.Tools;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Text;
using OpenAI.Responses;
using lionheart.Services;
using System.Linq;


namespace lionheart.Services.AI
{
    public class ProgramGenerationService : IProgramGenerationService
    {
        private const string ConversationCacheKeyPrefix = "ProgramGenConversation_";
        private readonly ChatClient _chatClient;
        private readonly IToolCallExecutor _toolCallExecutor;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ProgramGenerationService> _logger;

        #pragma warning disable OPENAI001
        private readonly OpenAIResponseClient _responses; // built-in tools live here

        private readonly IMovementService _movementService;
        private readonly ITrainingProgramService _trainingProgramService;


        public ProgramGenerationService(
            IConfiguration config,
            IToolCallExecutor toolCallExecutor,
            IMemoryCache cache,
            ILogger<ProgramGenerationService> logger,
            IMovementService movementService,
            ITrainingProgramService trainingProgramService)
        {
            _chatClient = new ChatClient(model: "gpt-4o", apiKey: config["OpenAI:ApiKey"]);
            _responses = new OpenAIResponseClient(model: "gpt-4o-mini", apiKey: config["OpenAI:ApiKey"]); // 4o-mini is cheap+fast
            _toolCallExecutor = toolCallExecutor;
            _cache = cache;
            _logger = logger;
            _movementService = movementService;
            _trainingProgramService = trainingProgramService;
        }
        private static string? ExtractUserAdjustments(string? goals)
        {
            if (string.IsNullOrWhiteSpace(goals)) return null;
            var marker = "User Adjustments:";
            var idx = goals.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return idx < 0 ? null : goals[(idx + marker.Length)..].Trim();
        }

        private List<ChatMessage> GetOrInitConversation(IdentityUser user)
        {
            var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return new List<ChatMessage>
                {
                    new SystemChatMessage(
                    @"You are Lionheart, a powerlifting coach-engine.

                    TOOL USE AND SAFETY
                    - FIRST, fetch info using tools (movement bases, equipment, program)—these can be parallel.
                    - THEN, create sessions with populated movements in a single CreateTrainingSessionWeekAsync(request) call per week.
                    - Use only UUIDs returned by tools for movementBaseID/equipmentID. Never guess IDs.
                    - Never generate raw JSON unless explicitly asked; call tools with proper arguments.
                    - Never create sessions without 4+ movements or without a main lift with a top set + back-offs.

                    CONSTRAINTS
                    - Obey user frequency goals exactly (per-week S/B/D counts).
                    - Weight units: 'Kilograms' or 'Pounds' only.
                    - Follow the programming guidelines exactly.
                   "),
                    new SystemChatMessage(TrainingProgrammingReference.Text),

                };
            })!;
        }

        private async Task<(List<MovementBaseSlimDTO> bases, List<EquipmentSlimDTO> eqs)>
            GetSlimMovementAndEquipmentAsync(IdentityUser user)
        {
            var basesRes = await _movementService.GetMovementBasesAsync(user);
            var eqsRes   = await _movementService.GetEquipmentsAsync(user);
            if (!basesRes.IsSuccess) throw new Exception(string.Join("; ", basesRes.Errors));
            if (!eqsRes.IsSuccess)   throw new Exception(string.Join("; ", eqsRes.Errors));

            var basesSlim = basesRes.Value.Select(mb => new MovementBaseSlimDTO
            {
                MovementBaseID = mb.MovementBaseID,
                Name = mb.Name
            }).ToList();

            var eqSlim = eqsRes.Value.Select(eq => new EquipmentSlimDTO
            {
                EquipmentID = eq.EquipmentID,
                Name = eq.Name
            }).ToList();

            return (basesSlim, eqSlim);
        }

        public async Task<Result<string>> GeneratePreferencesAsync(IdentityUser user, ProgramPreferencesDTO dto)
        {
            try
            {
                // Keep memory: reuse the cached conversation
                var messages = GetOrInitConversation(user);

                // Manual fetch (no AI tools)
                var (basesSlim, eqSlim) = await GetSlimMovementAndEquipmentAsync(user);

                // Step-specific override: NO tools, JSON only, outline only.
                messages.Add(new SystemChatMessage(
                    "STEP=PreferencesOutline. For THIS step only: do NOT call tools. " +
                    "If the prompt contains a section starting with 'User Adjustments:', treat those items as HARD CONSTRAINTS and apply them LITERALLY (e.g., 'move Secondary Deadlift to Monday' means it must appear on Monday). " +
                    "Return ONLY JSON for the outline. No sets/reps. No RPE."));


                var feedback = ExtractUserAdjustments(dto.UserGoals);
                messages.Add(new UserChatMessage(
                    PromptBuilder.PreferencesOutline(dto, basesSlim, eqSlim, feedback).Build()));

                // No tools exposed here
                var options = new ChatCompletionOptions();


                var result = await _chatClient.CompleteChatAsync(messages, options);
                var completion = result.Value;

                if (completion.FinishReason != ChatFinishReason.Stop)
                    return Result<string>.Error($"AI did not return a normal message: {completion.FinishReason}");

                var raw = completion.Content[0].Text?.Trim();

                if (string.IsNullOrWhiteSpace(raw))
                    return Result<string>.Error("Empty outline response.");

                // Defensive: slice the first/last JSON braces in case the model adds fluff
                var first = raw.IndexOf('{');
                var last  = raw.LastIndexOf('}');
                var json  = (first >= 0 && last > first) ? raw.Substring(first, last - first + 1) : raw;

                // Append assistant to conversation so the outline is *remembered*
                messages.Add(new AssistantChatMessage(completion));

                // Cache updated conversation (keeps memory across steps)
                var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
                _cache.Set(cacheKey, messages, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });

                // Optional: also cache the approved outline separately for Week 1
                _cache.Set($"{cacheKey}_ApprovedOutlineJson", json,
                    new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });

                return Result<string>.Success(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GeneratePreferencesAsync failed.");
                return Result<string>.Error(ex.Message);
            }
        }


        public async Task<Result<string>> GenerateFirstWeekAsync(IdentityUser user, FirstWeekGenerationDTO dto)
        {
            var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
            var messages = GetOrInitConversation(user);
                var outlineJson = _cache.Get<string>($"{cacheKey}_ApprovedOutlineJson");

                if (!string.IsNullOrWhiteSpace(outlineJson))
                {
                    // Add a step-specific anchor so the AI adheres to the plan it presented
                    messages.Add(new SystemChatMessage(
                        "Approved Outline JSON (DO NOT DEVIATE): " + outlineJson));
                }
            messages.Add(new UserChatMessage(PromptBuilder.FirstWeek(dto.TrainingProgramID).Build()));
            return await RunAiLoopAsync(messages, OpenAiToolRetriever.GetTrainingProgramPopulationTools(), user);
        }

        public async Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, RemainingWeeksGenerationDTO dto)
        {  
          var messages = GetOrInitConversation(user);
            messages.Add(new UserChatMessage(PromptBuilder.RemainingWeeks(dto.TrainingProgramID).Build()));
            return await RunAiLoopAsync(messages, OpenAiToolRetriever.GetTrainingProgramPopulationTools(), user);
        }

        private async Task<Result<string>> RunAiLoopAsync(
            List<ChatMessage> messages,
            List<ChatTool> tools,
            IdentityUser user)
        {
            var options = new ChatCompletionOptions();
            tools.ForEach(tool => options.Tools.Add(tool));

            var toolResponses = new List<string>();
            string? assistantNarration = null;

            bool requiresAction;
            do
            {
                requiresAction = false;
                ChatCompletion response = await _chatClient.CompleteChatAsync(messages, options);
                ChatCompletion completion = response;

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        messages.Add(new AssistantChatMessage(completion));
                        assistantNarration = completion.Content[0].Text;
                        break;

                    case ChatFinishReason.ToolCalls:
                        messages.Add(new AssistantChatMessage(completion));
                        var toolResults = await _toolCallExecutor.ExecuteToolCallsAsync(completion.ToolCalls, user);

                        foreach (var tr in toolResults)
                        {
                            if (!tr.IsSuccess)
                                return Result<string>.Error(string.Join("; ", tr.Errors));

                            messages.Add(tr.Value);
                            toolResponses.Add(tr.Value.Content[0].Text);
                        }

                        requiresAction = true;
                        break;

                    default:
                        return Result<string>.Error($"AI response failed: {completion.FinishReason}");
                }
            } while (requiresAction);

            var payload = new
            {
                toolResponses,
                summary = assistantNarration
            };

            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
            _cache.Set(cacheKey, messages, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });

            return Result<string>.Success(json);
        }
    }
}
