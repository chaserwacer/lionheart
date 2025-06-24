using Ardalis.Result;
using Microsoft.AspNetCore.Identity;


namespace lionheart.Services
{
    public interface IMCPClientService
    {
        Task<Result<string>> ChatAsync(IdentityUser user);
    }
}
