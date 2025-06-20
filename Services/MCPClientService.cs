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

            var chatHistory = GenerateInitialChatHistory(user, userPrompt, 1);
            var client = await CreateMcpClientAsync();
            var tools = await client.ListToolsAsync();

            List<ChatResponseUpdate> updates = [];
            StringBuilder result = new StringBuilder();


            await foreach (var update in _chatClient.GetStreamingResponseAsync(
                chatHistory,
                new() { Tools = [.. tools], AllowMultipleToolCalls = true }
            ))
            {
                result.Append(update);
                updates.Add(update);
            }
            chatHistory.AddMessages(updates);
            return result.ToString();


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

        private string GetSystemPrompt(string userID, int promptNumber)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            switch (promptNumber)
            {
                case 0:
                    return $"""
                        You are Lionheart, an intelligent training assistant that helps users manage their athletic performance, training plans, and recovery.

                        Your job is to help user ID `{userID}`.

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
                        Never give a repsonse with a tool call, always use the tools to fetch data and then respond with the data.
                        """;
                case 1:
                    return $"""
                        You are Lionheart, a helpful AI assistant trained to assist athletes with training, recovery, and wellness decisions. You are connected to tools that allow you to access and manage a user’s training programs, wellness data, and session history.

                        When a user asks a question or makes a request, determine whether a tool is available to complete the task. If a tool is available, use it by issuing a function call. Do not simulate tool execution or assume results — always use a real tool call.


                        The user you are helping is always associated with a unique user ID, which will be provided to you in the conversation. Always include this ID when making function calls.
                        Your job is to help user ID `{userID}`.
                        Use today's date (`{today}`) when needed, and do not refer to outdated knowledge cutoffs — your tools have access to live data. Avoid hallucinating or guessing; ask for clarification if needed.

                        If no tool is available to help, respond conversationally. Otherwise, always prefer tool-based solutions to ensure data accuracy and proper system integration.
                        """;
                default:
                    return "No system prompt available.";
            }
            }
        private List<ChatMessage> GenerateInitialChatHistory(IdentityUser user, string userPrompt, int promptNumber = 0)
        {
            return new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, GetSystemPrompt(user.Id.ToString(), promptNumber)),
                new(ChatRole.User, userPrompt)
            };
        }
    };
}

 