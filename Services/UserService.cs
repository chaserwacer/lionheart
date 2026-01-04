using lionheart.Data;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OpenApi.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

using lionheart.Model.DTOs;
using Ardalis.Result;
using ModelContextProtocol.Server;
using System.ComponentModel;
namespace lionheart.Services
{
    /// <summary>
    /// User service interface for managing user profiles and personal API access tokens.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Check the status of the user's profile creation.
        /// </summary>
        Task<Result<BootUserDTO>> HasCreatedProfileAsync(IdentityUser user);
        /// <summary>
        /// Create a new profile for the user.
        /// </summary>
        Task<Result<LionheartUser>> CreateProfileAsync(IdentityUser user, CreateProfileRequest req);
        /// <summary>
        /// Set a personal access token for a given application. Creates a token for a given application if it does not yet exist, 
        /// updates it if it already exists.
        /// </summary>
        Task<Result<bool>> SetPersonalApiAccessToken(IdentityUser user, CreatePersonalApiAccessTokenRequest req);
    }

    public class UserService : IUserService
    {
        private readonly ModelContext _context;

        public UserService(ModelContext context)
        {
            _context = context;
        }


        public async Task<Result<LionheartUser>> CreateProfileAsync(IdentityUser user, CreateProfileRequest req)
        {
            var bootUserDto = await HasCreatedProfileAsync(user);
            var profileExists = bootUserDto.IsSuccess && bootUserDto.Value.HasCreatedProfile;
            if (profileExists)
            {
                return Result<LionheartUser>.Conflict("Profile already exists for this user.");
            }

            var privateKey = Guid.Parse(user.Id);
            LionheartUser lionheartUser = new()
            {
                UserID = privateKey,
                IdentityUser = user,
                Name = req.DisplayName,
                Age = req.Age,
                Weight = req.Weight
            };

            _context.LionheartUsers.Add(lionheartUser);
            await _context.SaveChangesAsync();
            return Result<LionheartUser>.Created(lionheartUser);
        }

        public async Task<Result<BootUserDTO>> HasCreatedProfileAsync(IdentityUser user)
        {
            var privateKey = Guid.Parse(user.Id);

            var lionheartUser = await _context.LionheartUsers.FindAsync(privateKey);

            var hasCreatedProfile = lionheartUser is not null;
            var name = hasCreatedProfile ? lionheartUser!.Name : user.UserName ?? "";

            var bootUserDto = new BootUserDTO(name, hasCreatedProfile);

            return Result<BootUserDTO>.Success(bootUserDto);
        }

        public async Task<Result<bool>> SetPersonalApiAccessToken(IdentityUser user, CreatePersonalApiAccessTokenRequest req)
        {
            var privateKey = Guid.Parse(user.Id);
            var applicationName = req.ApplicationName;
            var accessToken = req.AccessToken;

            var existingToken = _context.ApiAccessTokens.FirstOrDefault(x => x.UserID == privateKey && x.ApplicationName == applicationName);
            if (existingToken != null)
            {
                existingToken.PersonalAccessToken = accessToken;
                await _context.SaveChangesAsync();
                return Result<bool>.Success(true);
            }
            else
            {
                ApiAccessToken apiAccessToken = new()
                {
                    ObjectID = Guid.NewGuid(),
                    UserID = privateKey,
                    ApplicationName = applicationName,
                    PersonalAccessToken = accessToken,
                };

                _context.ApiAccessTokens.Add(apiAccessToken);
                await _context.SaveChangesAsync();
                return Result<bool>.Created(true);
            }
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
