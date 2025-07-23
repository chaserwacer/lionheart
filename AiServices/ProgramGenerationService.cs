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

namespace lionheart.Services.AI
{
    public class ProgramGenerationService : IProgramGenerationService
    {
        private const string ConversationCacheKeyPrefix = "ProgramGenConversation_";
        private readonly ChatClient _chatClient;
        private readonly IToolCallExecutor _toolCallExecutor;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ProgramGenerationService> _logger;

        public ProgramGenerationService(
            IConfiguration config,
            IToolCallExecutor toolCallExecutor,
            IMemoryCache cache,
            ILogger<ProgramGenerationService> logger)
        {
            _chatClient = new ChatClient(model: "gpt-4o", apiKey: config["OpenAI:ApiKey"]);
            _toolCallExecutor = toolCallExecutor;
            _cache = cache;
            _logger = logger;
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
                    "You are a .NET assistant.  When the user asks to create a training program, " +
                    "you MUST call the functions required using tools with the proper arguments object, " +
                    "and you must NOT output any raw JSON or text unless asked to explain what you are doing."
                    )
                };
            });
        }

        public async Task<Result<string>> GeneratePreferencesAsync(IdentityUser user, ProgramPreferencesDTO dto)
        {
            var messages = GetOrInitConversation(user);
            messages.Add(new UserChatMessage(PromptBuilder.Preferences(dto).Build()));
            return await RunAiLoopAsync(messages, OpenAiToolRetriever.GetTrainingProgramPopulationTools(), user);
        }

        public async Task<Result<string>> GenerateFirstWeekAsync(IdentityUser user, FirstWeekGenerationDTO dto)
        {
            var messages = GetOrInitConversation(user);
            messages.Add(new UserChatMessage(PromptBuilder.FirstWeek(dto.TrainingProgramID).Build()));
            return await RunAiLoopAsync(messages, OpenAiToolRetriever.GetTrainingProgramPopulationTools(), user);
        }

        public async Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, RemainingWeeksGenerationDTO dto)
        {
            var messages = GetOrInitConversation(user);
            messages.Add(new UserChatMessage(PromptBuilder.RemainingWeeks().Build()));
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

            // ✅ Wrap both into a single JSON string
            var payload = new
            {
                toolResponses,
                summary = assistantNarration
            };

            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            return Result<string>.Success(json);
        }
    }
}
