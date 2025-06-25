using Ardalis.Result;
using Microsoft.AspNetCore.Identity;

public interface IPromptService
{
    /// <summary>
    /// Generates a prompt based on the provided request.
    /// </summary>
    Task<Result<string>> GeneratePromptAsync(IdentityUser user ,GeneratePromptRequest request);
}