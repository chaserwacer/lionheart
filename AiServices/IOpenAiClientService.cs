using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.AI
{
    public interface IOpenAiClientService
    {
        Task<string> ChatSimpleAsync(string prompt);
        Task<string> ChatWithToolsAsync(string prompt, IdentityUser user);
    }
}
