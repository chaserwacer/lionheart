using lionheart.Data;
using lionheart.Model.User;
using Microsoft.AspNetCore.Identity;
using lionheart.Functional;
using Microsoft.EntityFrameworkCore;
namespace lionheart.Services
{
    /// <summary>
    /// What setting a personal access token did. An application the athlete had not authorized
    /// yet is a new token; one they had is an overwrite of it. Nothing in the token that comes
    /// back tells the two apart, and the endpoint has to, to answer 201 or 200.
    /// </summary>
    public abstract record ApiAccessTokenWrite
    {
        private protected ApiAccessTokenWrite() { }

        public sealed record Created : ApiAccessTokenWrite;

        public sealed record Updated : ApiAccessTokenWrite;
    }

    /// <summary>
    /// User service interface for managing user profiles and personal API access tokens.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Check the status of the user's profile creation.
        /// </summary>
        Task<Result<BootUserDTO, Problem>> HasCreatedProfileAsync(IdentityUser user);
        /// <summary>
        /// Create a new profile for the user.
        /// </summary>
        Task<Result<LionheartUser, Problem>> CreateProfileAsync(IdentityUser user, CreateProfileRequest req);
        /// <summary>
        /// Set a personal access token for a given application. Creates a token for a given application if it does not yet exist,
        /// updates it if it already exists.
        /// </summary>
        Task<Result<ApiAccessTokenWrite, Problem>> SetPersonalApiAccessToken(IdentityUser user, CreatePersonalApiAccessTokenRequest req);
    }

    public class UserService : IUserService
    {
        private readonly ModelContext _context;

        public UserService(ModelContext context)
        {
            _context = context;
        }


        // Composed rather than unwrapped so that a problem from the check propagates. Reading the
        // failure branch as "no profile exists" would let a failed guard fall through to the write
        // it was guarding.
        public Task<Result<LionheartUser, Problem>> CreateProfileAsync(IdentityUser user, CreateProfileRequest req) =>
            HasCreatedProfileAsync(user)
                .EnsureAsync(
                    boot => !boot.HasCreatedProfile,
                    _ => new Problem.Conflict("Profile already exists for this user."))
                .BindAsync(_ => AddProfileAsync(user, req));

        private async Task<Result<LionheartUser, Problem>> AddProfileAsync(IdentityUser user, CreateProfileRequest req)
        {
            LionheartUser lionheartUser = new()
            {
                UserID = Guid.Parse(user.Id),
                IdentityUser = user,
                Name = req.DisplayName,
                Age = req.Age,
                Weight = req.Weight
            };

            _context.LionheartUsers.Add(lionheartUser);
            await _context.SaveChangesAsync();

            return Result<LionheartUser, Problem>.Success(lionheartUser);
        }

        public async Task<Result<BootUserDTO, Problem>> HasCreatedProfileAsync(IdentityUser user)
        {
            var privateKey = Guid.Parse(user.Id);

            var lionheartUser = await _context.LionheartUsers.FindAsync(privateKey);

            var hasCreatedProfile = lionheartUser is not null;
            var name = hasCreatedProfile ? lionheartUser!.Name : user.UserName ?? "";

            var bootUserDto = new BootUserDTO(name, hasCreatedProfile);

            return Result<BootUserDTO, Problem>.Success(bootUserDto);
        }

        public async Task<Result<ApiAccessTokenWrite, Problem>> SetPersonalApiAccessToken(IdentityUser user, CreatePersonalApiAccessTokenRequest req)
        {
            var privateKey = Guid.Parse(user.Id);
            var applicationName = req.ApplicationName;
            var accessToken = req.AccessToken;

            var existingToken = await _context.ApiAccessTokens
                .FirstOrDefaultAsync(x => x.UserID == privateKey && x.ApplicationName == applicationName);

            if (existingToken is not null)
            {
                existingToken.PersonalAccessToken = accessToken;
                await _context.SaveChangesAsync();
                return Result<ApiAccessTokenWrite, Problem>.Success(new ApiAccessTokenWrite.Updated());
            }

            _context.ApiAccessTokens.Add(new ApiAccessToken
            {
                ObjectID = Guid.NewGuid(),
                UserID = privateKey,
                ApplicationName = applicationName,
                PersonalAccessToken = accessToken,
            });
            await _context.SaveChangesAsync();

            return Result<ApiAccessTokenWrite, Problem>.Success(new ApiAccessTokenWrite.Created());
        }

        public async Task<IdentityUser?> GetIdentityUserAsync(string userID)
        {
            if (string.IsNullOrEmpty(userID))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userID));
            }

            var identityUser = await _context.Users.FindAsync(userID);
            return identityUser;
        }
    }
}
