using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;   // for ChatMessage and ChatRole


namespace lionheart.Services
{
    public interface IMCPClientService
    {

        Task<Result<string>> ChatAsync(IdentityUser user);
        Task<Result<string>> ChatAsync(IdentityUser user, List<ChatMessage> messages);


    }
}
