using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.AspNetCore;
using System.Text;
using lionheart.Controllers;
using Ardalis.Result; // For logging and types

namespace lionheart.Services
{
    public class MCPClientService : IMCPClientService
    {
        private readonly ILogger<MCPClientService> _logger;
        private readonly IChatClient _chatClient;
        private readonly Uri MCP_SERVER_URI = new("http://localhost:7025/sse");

        public MCPClientService(ILogger<MCPClientService> logger, IChatClient chatClient)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public async Task<Result<string>> ChatAsync(IdentityUser user, string userPrompt)
        {

            var chatHistory = GenerateInitialChatHistory(user, userPrompt);
            var client = await CreateMcpClientAsync();
            var tools = await client.ListToolsAsync();

            List<ChatResponseUpdate> updates = [];
            StringBuilder result = new StringBuilder();


            var response = await _chatClient.GetResponseAsync(
            chatHistory,
            new ChatOptions
            {
                Tools = [.. tools]
            });

           return response.Text is not null
                ? Result<string>.Success(response.Text)
                : Result<string>.Error("Failed to get a valid response from the chat client.");
        }


        private async Task<IMcpClient> CreateMcpClientAsync()
        {
            return await McpClientFactory.CreateAsync(
                new SseClientTransport(
                    new SseClientTransportOptions
                    {
                        Endpoint = MCP_SERVER_URI
                    }
                )
            );
        }

        private string GetSystemPrompt(string userID)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            return $"""
                You are Lionheart, an intelligent training assistant that helps users manage their athletic performance, training plans, and recovery.

                Your job is to help user ID `{userID}`. You must personalize your responses using their stored data and the tools available to you.

                Todays date is {today}.

                You have access to the following:
                - Tools that allow you to create, modify, and analyze training programs, sessions, recovery history, and wellness data.
                - Resources that include the user's historical performance, fatigue, sleep, readiness, and injuries.

                Your responses should:
                - Be actionable, concise, and context-aware.
                - Modify or generate training plans based on recovery, soreness, goals, and preferences.
                - Offer adaptations if the user is tired, injured, or time-constrained.
                - Never invent data — always use tools or resources when needed.

                You can call tools at any time to fetch or modify data.
                """;
        }

        private List<ChatMessage> GenerateInitialChatHistory(IdentityUser user, string userPrompt)
        {
            return new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, GetSystemPrompt(user.Id.ToString())),
                new(ChatRole.User, userPrompt)
            };
        }

    }
}
