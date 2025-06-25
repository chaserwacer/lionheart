using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;               // for ChatMessage, ChatOptions
using Microsoft.Extensions.Logging;
using Ardalis.Result;
using ModelContextProtocol.Client;           // for McpClientFactory, SseClientTransport, etc.
using ModelContextProtocol.Server;           // for IMcpClient, DateRangeRequest
using lionheart.Model.DTOs;
using Model.McpServer;         
using System.Linq;    // make sure you have this at the top
              // for LionMcpPrompt, InstructionPromptSection

namespace lionheart.Services
{
    public class MCPClientService : IMCPClientService
    {
        private readonly ILogger<MCPClientService> _logger;
        private readonly IChatClient _chatClient;
        private readonly Uri MCP_SERVER_URI = new("http://localhost:7025/sse");
        private readonly IOuraService _ouraService;
        private readonly IWellnessService _wellnessService;

        public MCPClientService(
            ILogger<MCPClientService> logger,
            IChatClient chatClient,
            IOuraService ouraService,
            IWellnessService wellnessService)
        {
            _logger = logger;
            _chatClient = chatClient;
            _ouraService = ouraService;
            _wellnessService = wellnessService;
        }

        /// <summary>
        /// Original ChatAsync: builds its own LionMcpPrompt, streams back updates, and returns the concatenated text.
        /// </summary>
        public async Task<Result<string>> ChatAsync(IdentityUser user)
        {
            var client = await CreateMcpClientAsync();
            var mcpTools = await client.ListToolsAsync();

            var dateRange = new DateRangeRequest
            {
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                EndDate   = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            var lionMCPPrompt = new LionMcpPrompt { User = user };

            var taskSection = new InstructionPromptSection { Name = "Primary Task Description" };
            taskSection.AddInstruction("You are Lionheart, an intelligent training assistant that helps users manage their athletic performance, training plans, wellness, and recovery.");
            taskSection.AddInstruction("Please assist in generating insights on a users recent data that will be provided later.");
            lionMCPPrompt.Sections.Add(taskSection);

            var instructions = new InstructionPromptSection { Name = "Instructions" };
            instructions.AddInstruction("1. Analyze the users recent oura data and wellness data, generating insights.");
            instructions.AddInstruction("2. Prioritize insights on strong differences or similarities between the oura data (measured) vs the wellness data (perceived).");
            instructions.AddInstruction("3. Do not follow up with the user, simply provide the insights.");
            lionMCPPrompt.Sections.Add(instructions);

            await lionMCPPrompt.AddOuraDataSectionAsync(_ouraService, dateRange);
            await lionMCPPrompt.AddWellnessDataSectionAsync(_wellnessService, dateRange);

            var chatHistory = lionMCPPrompt.ToChatMessage();

            _logger.LogInformation(
              "Chat history: {ChatHistory}",
              JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions { WriteIndented = true }));

            var result = new StringBuilder();
            var aiTools  = mcpTools.Select(t => (AITool)t).ToList();  // ← cast here

            // ← here we use ChatOptions, not ChatRequestSettings
            var options = new ChatOptions
            {
                Tools = aiTools,                 // ✅ now IList<AITool>
                AllowMultipleToolCalls = true
            };

            await foreach (var update in _chatClient.GetStreamingResponseAsync(
                chatHistory,
                options))
            {
                result.Append(update);
            }

            return Result<string>.Success(result.ToString());
        }

        /// <summary>
        /// New overload: send an existing list of ChatMessage directly.
        /// </summary>
       public async Task<Result<string>> ChatAsync(
            IdentityUser user,
            List<ChatMessage> messages)
        {
            try
            {
                var client   = await CreateMcpClientAsync();
                var mcpTools = await client.ListToolsAsync();
                var aiTools  = mcpTools.Select(t => (AITool)t).ToList();

                var options = new ChatOptions
                {
                    Tools                  = aiTools,
                    AllowMultipleToolCalls = true
                };

                // one-shot chat
                var chatResponse = await _chatClient.GetResponseAsync(
                    messages,
                    options,
                    cancellationToken: default);

                // ✅ use the Text property (or inspect chatResponse.Messages[0].Content)
                var content = chatResponse.Text;

                return Result<string>.Success(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "One-shot ChatAsync failed");
                return Result<string>.Error(ex.Message);
            }
        }




        /// <summary>
        /// Helper to spin up the SSE-based MCP client for tool invocation.
        /// </summary>
        private async Task<IMcpClient> CreateMcpClientAsync()
        {
            return await McpClientFactory.CreateAsync(
                new SseClientTransport(new SseClientTransportOptions
                {
                    Endpoint = MCP_SERVER_URI
                }));
        }
    }
}
