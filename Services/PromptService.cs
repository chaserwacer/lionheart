using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Ardalis.Result;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using lionheart.Model.DTOs;
using Model.McpServer;


namespace lionheart.Services
{
    public class PromptService : IPromptService
    {
        private readonly ILogger<MCPClientService> _logger;
        private readonly IOuraService _ouraService;
        private readonly IWellnessService _wellnessService;

        public PromptService(
            ILogger<MCPClientService> logger,
            IChatClient chatClient,
            IOuraService ouraService,
            IWellnessService wellnessService)
        {
            _logger = logger;

            _ouraService = ouraService;
            _wellnessService = wellnessService;
        }





        public async Task<Result<string>> GeneratePromptAsync(IdentityUser user, GeneratePromptRequest request)
        {
            if (request.PromptType is not null && request.PromptType == "cb.01")
            {
                var dateRange = new DateRangeRequest
                {
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                    EndDate = DateOnly.FromDateTime(DateTime.UtcNow)
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
                var chatPrompt = lionMCPPrompt.ToStringPrompty();
                return Result.Success(chatPrompt);
            }
            else 
            {
                return Result<string>.Error("Invalid prompt type specified.");
            }

        }
    }
}
