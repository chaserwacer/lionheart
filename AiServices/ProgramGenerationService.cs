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
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.AspNetCore.Http;
using OpenAI.Responses;
using ModelContextProtocol.Protocol;

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
#pragma warning restore OPENAI001

        private readonly IMovementService _movementService;
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly IHttpContextAccessor _http;

        public ProgramGenerationService(
            IConfiguration config,
            IToolCallExecutor toolCallExecutor,
            IMemoryCache cache,
            ILogger<ProgramGenerationService> logger,
            IMovementService movementService,
            ITrainingProgramService trainingProgramService,
            IHttpContextAccessor http)
        {
            _chatClient = new ChatClient(model: "gpt-4o", apiKey: config["OpenAI:ApiKey"]);
            _toolCallExecutor = toolCallExecutor;
            _cache = cache;
            _logger = logger;
            _movementService = movementService;
            _trainingProgramService = trainingProgramService;
            _http = http;

#pragma warning disable OPENAI001
            _responses = new OpenAIResponseClient(model: "gpt-4o-mini", apiKey: config["OpenAI:ApiKey"]);
#pragma warning restore OPENAI001
        }

        private static string? ExtractUserAdjustments(string? goals)
        {
            if (string.IsNullOrWhiteSpace(goals)) return null;
            var marker = "User Adjustments:";
            var idx = goals.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return idx < 0 ? null : goals[(idx + marker.Length)..].Trim();
        }

        static class SportTags
        {
            public const string Powerlifting = "Powerlifting";
            public const string Bodybuilding = "Bodybuilding";
            // Future: Running, Swimming, Tennis, Basketball, MartialArts, etc.

            public static bool Is(string? tag, string target) =>
                string.Equals(tag?.Trim(), target, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ResolveTagFromHeadersOrCacheAsync(IdentityUser user)
        {
            var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}_SelectedTag";

            // 1) Prefer explicit header from the UI
            var tagFromHeader = _http.HttpContext?.Request?.Headers?["X-Program-Tag"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tagFromHeader))
            {
                var cleaned = tagFromHeader.Trim();
                _cache.Set(cacheKey, cleaned, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });
                return cleaned;
            }

            // 2) Use cached tag if present
            var cached = _cache.Get<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached)) return cached;

            // 3) Optional: derive from program if header provided
            var pidHeader = _http.HttpContext?.Request?.Headers?["X-Program-Id"].FirstOrDefault();
            if (Guid.TryParse(pidHeader, out var programId))
            {
                var userProgram = await _trainingProgramService.GetTrainingProgramAsync(user, programId);
                if (userProgram.IsSuccess && userProgram.Value?.Tags is { Count: > 0 })
                {
                    var tag = userProgram.Value.Tags[0];
                    _cache.Set(cacheKey, tag, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
                    return tag;
                }
            }

            // 4) Fallback
            const string fallback = SportTags.Powerlifting;
            _cache.Set(cacheKey, fallback, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
            return fallback;
        }

        private string GetCachedTag(IdentityUser user)
        {
            var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}_SelectedTag";
            return _cache.Get<string>(cacheKey) ?? SportTags.Powerlifting;
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
                    @"You are Lionheart, a training coach-engine.

                    TOOL USE & SAFETY
                    - FIRST, fetch info using tools (movement bases, equipment, program)—these can be parallel.
                    - THEN, create sessions with populated movements in a single CreateTrainingSessionWeekAsync(request) call per week.
                    - Use only UUIDs returned by tools for movementBaseID/equipmentID. Never guess IDs.
                    - Never generate raw JSON unless explicitly asked; call tools with proper arguments.
                    - Never create sessions without 4+ movements or without a main lift with a top set + back-offs (or equivalent per sport).

                    CONSTRAINTS
                    - Obey user frequency goals exactly (per-week S/B/D counts or sport analogs).
                    - Weight units: 'Kilograms' or 'Pounds' only where applicable.
                    - Follow the programming guidelines exactly."),
                    new SystemChatMessage(TrainingProgrammingReference.Text),
                };
            })!;
        }

        private async Task<(List<MovementBaseSlimDTO> bases, List<EquipmentSlimDTO> eqs)>
            GetSlimMovementAndEquipmentAsync(IdentityUser user)
        {
            var basesRes = await _movementService.GetMovementBasesAsync(user);
            var eqsRes = await _movementService.GetEquipmentsAsync(user);
            if (!basesRes.IsSuccess) throw new Exception(string.Join("; ", basesRes.Errors));
            if (!eqsRes.IsSuccess) throw new Exception(string.Join("; ", eqsRes.Errors));

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
                var messages = GetOrInitConversation(user);

                // Resolve sport tag (header → cache → program → fallback)
                var tag = await ResolveTagFromHeadersOrCacheAsync(user);

                // Manual fetch (no AI tools in this step)
                var (basesSlim, eqSlim) = await GetSlimMovementAndEquipmentAsync(user);

                // Step-specific override: NO tools, JSON only, outline only.
                messages.Add(new SystemChatMessage(
                    "STEP=PreferencesOutline. For THIS step only: do NOT call tools. " +
                    "If the prompt contains a section starting with 'User Adjustments:', treat those items as HARD CONSTRAINTS and apply them LITERALLY. " +
                    "Return ONLY JSON for the outline. No sets/reps. No RPE."));
                messages.Add(new SystemChatMessage($"SPORT_CONTEXT={tag}"));

                var feedback = ExtractUserAdjustments(dto.UserGoals);

                // Choose sport-specific outline prompt with sport-specific DTO
                PromptBuilder outlinePrompt;
                if (SportTags.Is(tag, SportTags.Bodybuilding))
                {
                    var bb = BodybuildingPreferencesDTO.FromBase(dto);
                    outlinePrompt = PromptBuilder.BodybuildingPreferencesOutline(bb, basesSlim, eqSlim, feedback);
                }
                else
                {
                    var pl = PowerliftingPreferencesDTO.FromBase(dto);
                    outlinePrompt = PromptBuilder.PowerliftingPreferencesOutline(pl, basesSlim, eqSlim, feedback);
                }

                messages.Add(new UserChatMessage(outlinePrompt.Build()));

                // No tools exposed here
                var options = new ChatCompletionOptions();
                var result = await _chatClient.CompleteChatAsync(messages, options);
                var completion = result.Value;

                if (completion.FinishReason != ChatFinishReason.Stop)
                    return Result<string>.Error($"AI did not return a normal message: {completion.FinishReason}");

                var raw = completion.Content?[0].Text?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                    return Result<string>.Error("Empty outline response.");

                // Strictly slice JSON in case the model adds fluff
                var first = raw.IndexOf('{');
                var last = raw.LastIndexOf('}');
                var json = (first >= 0 && last > first) ? raw.Substring(first, last - first + 1) : raw;

                // Append assistant to conversation so the outline is remembered
                messages.Add(new AssistantChatMessage(completion));

                var baseCacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
                _cache.Set(baseCacheKey, messages, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });
                _cache.Set($"{baseCacheKey}_ApprovedOutlineJson", json,
                    new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
                _cache.Set($"{baseCacheKey}_SelectedTag", tag,
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
            var baseCacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
            var messages = GetOrInitConversation(user);

            var outlineJson = _cache.Get<string>($"{baseCacheKey}_ApprovedOutlineJson");
            if (!string.IsNullOrWhiteSpace(outlineJson))
            {
                messages.Add(new SystemChatMessage("Approved Outline JSON (DO NOT DEVIATE): " + outlineJson));
            }

            // Resolve sport from cache (headers may not be present now)
            var tag = GetCachedTag(user);
            messages.Add(new SystemChatMessage($"SPORT_CONTEXT={tag}"));

            // Choose sport-specific Week 1 prompt
            var firstWeekPrompt =
                SportTags.Is(tag, SportTags.Bodybuilding)
                    ? PromptBuilder.BodybuildingFirstWeek(dto.TrainingProgramID)
                    : PromptBuilder.PowerliftingFirstWeek(dto.TrainingProgramID);

            messages.Add(new UserChatMessage(firstWeekPrompt.Build()));

            return await RunAiLoopAsync(messages, OpenAiToolRetriever.GetTrainingProgramPopulationTools(), user);
        }

        public async Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, RemainingWeeksGenerationDTO dto)
        {
            var messages = GetOrInitConversation(user);

            var tag = GetCachedTag(user);
            messages.Add(new SystemChatMessage($"SPORT_CONTEXT={tag}"));

            // Choose sport-specific remaining weeks prompt
            var remPrompt =
                SportTags.Is(tag, SportTags.Bodybuilding)
                    ? PromptBuilder.BodybuildingRemainingWeeks(dto.TrainingProgramID)
                    : PromptBuilder.PowerliftingRemainingWeeks(dto.TrainingProgramID);

            messages.Add(new UserChatMessage(remPrompt.Build()));

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

                var result = await _chatClient.CompleteChatAsync(messages, options);
                var completion = result.Value;

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        messages.Add(new AssistantChatMessage(completion));
                        assistantNarration = completion.Content != null && completion.Content.Count > 0
                            ? completion.Content[0].Text
                            : null;
                        break;

                    case ChatFinishReason.ToolCalls:
                        messages.Add(new AssistantChatMessage(completion));

                        var toolResults = await _toolCallExecutor.ExecuteToolCallsAsync(completion.ToolCalls, user);
                        foreach (var tr in toolResults)
                        {
                            if (!tr.IsSuccess)
                                return Result<string>.Error(string.Join("; ", tr.Errors));

                            messages.Add(tr.Value);
                            if (tr.Value.Content != null && tr.Value.Content.Count > 0)
                            {
                                toolResponses.Add(tr.Value.Content[0].Text);
                            }
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

            // Refresh cache TTL on conversation
            var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
            _cache.Set(cacheKey, messages, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });

            return Result<string>.Success(json);
        }
    }
}
