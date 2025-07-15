using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.Prompt;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.AI
{
    public class ProgramGenerationService : IProgramGenerationService
    {
        private readonly IOpenAiClientService _openAi;

        public ProgramGenerationService(IOpenAiClientService openAi)
        {
            _openAi = openAi;
        }

        public async Task<Result<string>> GenerateInitializationAsync(IdentityUser user)
        {
            var prompt = PromptBuilder.Initialization().Build();
            var response = await _openAi.ChatSimpleAsync(prompt);
            return Result.Success(response);
        }

        public async Task<Result<string>> GenerateProgramShellAsync(IdentityUser user, ProgramShellDTO dto)
        {
            var prompt = PromptBuilder.ProgramShell(dto.Title, dto.LengthWeeks.ToString()).Build();
            var response = await _openAi.ChatWithToolsAsync(prompt, user);
            return Result.Success(response);
        }

        public async Task<Result<string>> GeneratePreferencesAsync(IdentityUser user, ProgramPreferencesDTO dto)
        {
            var prompt = PromptBuilder.Preferences(dto).Build();
            var response = await _openAi.ChatWithToolsAsync(prompt, user);
            return Result.Success(response);
        }

        public async Task<Result<string>> GenerateFirstWeekAsync(IdentityUser user, FirstWeekGenerationDTO dto)
        {
            var prompt = PromptBuilder.FirstWeek(dto.TrainingProgramID).Build();
            var response = await _openAi.ChatWithToolsAsync(prompt, user);
            return Result.Success(response);
        }

        public async Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, RemainingWeeksGenerationDTO _)
        {
            var prompt = PromptBuilder.RemainingWeeks().Build();
            var response = await _openAi.ChatWithToolsAsync(prompt, user);
            return Result.Success(response);
        }
    }
}
