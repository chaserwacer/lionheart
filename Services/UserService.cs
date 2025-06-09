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
namespace lionheart.Services
{
    /// <summary>
    /// The UserService class acts on the service layer to handle business logic and interactions with the database, after being called upon via the UserController.
    /// This class is the most hectic in terms of the different things it 'does'. It handles profile creation, which is a process that meshes with 
    /// account creation via ASP.Net Identity User services. A user first registers through ASP.Net to create an Identity User (those code methods are outsourced), and then must create their actual 
    /// user profile/LionheartUser profile (with display name, etc.). A LionheartUser contains that identity user. See model context for more details. 
    /// This class also handles creating/editing user WellnessStates, as well as adding the personal access token for Oura.  
    /// 
    /// TODO: This class is to be taken and manipulated such that it only contains user centered methods, with those other methods moving to their own service class and then their own controller class. 
    /// </summary>
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






    }
}
