using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.Prompt;
using Microsoft.AspNetCore.Identity;
using OpenAI;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;
using Model.Tools;

namespace lionheart.Services.AI
{
    public class ProgramGenerationService : IProgramGenerationService
    {
        private readonly ChatClient _chatClient;
        private readonly IToolCallExecutor _toolCallExecutor;

        public ProgramGenerationService(IConfiguration config, IToolCallExecutor toolCallExecutor)
        {
            _chatClient = new ChatClient(model: "gpt-4o", apiKey: config["OpenAI:ApiKey"]);
            _toolCallExecutor = toolCallExecutor;
        }

        public async Task<Result<string>> GenerateInitializationAsync(IdentityUser user)
        {
            var prompt = PromptBuilder.Initialization().Build();
            var messages = new List<ChatMessage> { new UserChatMessage(prompt) };

            var result = await RunAiLoopAsync(messages, OpenAiToolHandler.GetTrainingProgramPopulationTools(), user);
            return result;
        }

        public async Task<Result<string>> GenerateProgramShellAsync(IdentityUser user, ProgramShellDTO dto)
        {
            var prompt = PromptBuilder.ProgramShell(dto.Title, dto.StartDate, dto.EndDate, dto.Tag).Build();
            var messages = new List<ChatMessage> { new UserChatMessage(prompt) };

            var result = await RunAiLoopAsync(messages, OpenAiToolHandler.GetTrainingProgramPopulationTools(), user);
            return result;
        }

        public async Task<Result<string>> GeneratePreferencesAsync(IdentityUser user, ProgramPreferencesDTO dto)
        {
            var prompt = PromptBuilder.Preferences(dto).Build();
            var messages = new List<ChatMessage> { new UserChatMessage(prompt) };

            var result = await RunAiLoopAsync(messages, OpenAiToolHandler.GetTrainingProgramPopulationTools(), user);
            return result;
        }

        public async Task<Result<string>> GenerateFirstWeekAsync(IdentityUser user, FirstWeekGenerationDTO dto)
        {
            var prompt = PromptBuilder.FirstWeek(dto.TrainingProgramID).Build();
            var messages = new List<ChatMessage> { new UserChatMessage(prompt) };

            var result = await RunAiLoopAsync(messages, OpenAiToolHandler.GetTrainingProgramPopulationTools(), user);
            return result;
        }

        public async Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, RemainingWeeksGenerationDTO _)
        {
            var prompt = PromptBuilder.RemainingWeeks().Build();
            var messages = new List<ChatMessage> { new UserChatMessage(prompt) };

            var result = await RunAiLoopAsync(messages, OpenAiToolHandler.GetTrainingProgramPopulationTools(), user);
            return result;
        }

        private async Task<Result<string>> RunAiLoopAsync(List<ChatMessage> messages, List<ChatTool> tools, IdentityUser user)
        {
            var options = new ChatCompletionOptions();
            tools.ForEach(tool => options.Tools.Add(tool));

            bool requiresAction;
            do
            {
                requiresAction = false;
                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        messages.Add(new AssistantChatMessage(completion));
                        var finalResponse = completion.Content[0].Text;
                        return Result<string>.Success(finalResponse);

                    case ChatFinishReason.ToolCalls:
                        messages.Add(new AssistantChatMessage(completion));
                        var toolCallResults = await _toolCallExecutor.ExecuteToolCallsAsync(completion.ToolCalls, user);

                        foreach (var result in toolCallResults)
                        {
                            if (!result.IsSuccess)
                                return Result<string>.Error(result.Errors.ToString());
                            messages.Add(result.Value);
                        }

                        requiresAction = true;
                        break;

                    default:
                        return Result<string>.Error($"AI response failed with reason: {completion.FinishReason}");
                }

            } while (requiresAction);

            return Result<string>.Error("Unexpected end to tool call loop.");
        }
    }
}
