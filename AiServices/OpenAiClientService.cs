using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using lionheart.Model.DTOs;
using System.Text;
using Model.McpServer;
using Model.Tools;

namespace lionheart.Services.AI
{
    public class OpenAiClientService : IOpenAiClientService
    {
        private readonly IChatClient _chatClient;
        private readonly IToolCallExecutor _toolCallExecutor;

        public OpenAiClientService(
            IConfiguration config,
            IToolCallExecutor toolCallExecutor)
        {
            _chatClient = new OpenAIClient(config["OpenAI:ApiKey"])
                .GetChatClient("gpt-4o")
                .AsIChatClient();

            _toolCallExecutor = toolCallExecutor;
        }

        public async Task<string> ChatSimpleAsync(string prompt)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are Lionheart, an intelligent training assistant."),
                new(ChatRole.User, prompt)
            };

            await foreach (var response in _chatClient.GetStreamingResponseAsync(messages))
            {
                if (response.FinishReason == ChatFinishReason.Stop)
                    return response.Message?.Content ?? "";
            }

            return "";
        }

        public async Task<string> ChatWithToolsAsync(string prompt, IdentityUser user)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are Lionheart, an intelligent training assistant."),
                new(ChatRole.User, prompt)
            };

            await foreach (var response in _chatClient.GetStreamingResponseAsync(
                messages,
                OpenAiToolHandler.GetTrainingProgramPopulationTools()))
            {
                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    var toolCalls = response.ToolCalls;
                    if (toolCalls == null || toolCalls.Count == 0)
                        break;

                    var toolCall = toolCalls[0];
                    var functionName = toolCall.Name;
                    var argsJson = toolCall.Arguments;

                    var toolResult = await _toolCallExecutor.ExecuteAsync(functionName, argsJson, user);

                    var followUpMessages = new List<ChatMessage>
                    {
                        new(ChatRole.System, "You are Lionheart, an intelligent training assistant."),
                        new(ChatRole.User, prompt),
                        new ChatMessage(ChatRole.Assistant, toolCalls: new List<ToolCall> { toolCall }),
                        new(ChatRole.Tool, toolCall.Id, JsonSerializer.Serialize(toolResult))
                    };

                    await foreach (var followUp in _chatClient.GetStreamingResponseAsync(followUpMessages))
                    {
                        if (followUp.FinishReason == ChatFinishReason.Stop)
                            return followUp.Message?.Content ?? "";
                    }

                    break;
                }

                if (response.FinishReason == ChatFinishReason.Stop)
                    return response.Message?.Content ?? "";
            }

            return "";
        }
    }
}
