using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Text;
using Ardalis.Result;
using ModelContextProtocol.Server;
using lionheart.Model.DTOs;
using System.Text.Json;
using Model.McpServer;

namespace lionheart.Services
{
    public class MCPClientService : IMCPClientService
    {
        private readonly ILogger<MCPClientService> _logger;
        private readonly IChatClient _chatClient;
        private readonly Uri MCP_SERVER_URI = new("http://localhost:7025/sse");
        private readonly IOuraService _ouraService;
        private readonly IWellnessService _wellnessService;
        //private readonly ITrainingSessionService _ouraService;

        public MCPClientService(ILogger<MCPClientService> logger, IChatClient chatClient, IOuraService ouraService, IWellnessService wellnessService)
        {
            _logger = logger;
            _chatClient = chatClient;
            _ouraService = ouraService;
            _wellnessService = wellnessService;
        }

        public async Task<Result<string>> ChatAsync(IdentityUser user)
        {

            var client = await CreateMcpClientAsync();
            var mcpTools = await client.ListToolsAsync();
            var dateRange = new DateRangeRequest()
            {
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };


            LionMcpPrompt lionMCPPrompt = new LionMcpPrompt() { User = user };
           
            
            InstructionPromptSection taskSection = new() { Name = "Primary Task Description" };
            taskSection.AddInstruction("You are Lionheart, an intelligent training assistant that helps users manage their athletic performance, training plans, wellness, and recovery.");
            taskSection.AddInstruction("Please assist in generating insights on a users recent data that will be provided later.");
            lionMCPPrompt.Sections.Add(taskSection);


            InstructionPromptSection instructions = new() { Name = "Instructions" };
            instructions.AddInstruction("1. Analyze the users recent oura data and wellness data, generating insights.");
            instructions.AddInstruction("2. Prioritize insights on strong differences or similaities between the oura data (measured) vs the wellness data (percieved).");
            instructions.AddInstruction("3. Do not follow up  with the user, simply provide the insights.");
            lionMCPPrompt.Sections.Add(instructions);

            await lionMCPPrompt.AddOuraDataSectionAsync(_ouraService, dateRange);
            await lionMCPPrompt.AddWellnessDataSectionAsync(_wellnessService, dateRange);

            var chatHistory = lionMCPPrompt.ToChatMessage();

            _logger.LogInformation("Chat history: {ChatHistory}", JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions { WriteIndented = true }));
            List<ChatResponseUpdate> updates = [];
            StringBuilder result = new StringBuilder();
            await foreach (var update in _chatClient.GetStreamingResponseAsync(chatHistory, new() { Tools = [.. mcpTools], AllowMultipleToolCalls = true }))
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
    }
}

 