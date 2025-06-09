using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace lionheart.Services
{
    public interface IUserService
    {
        /// <summary>
        /// Check the status of the user's profile creation.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        Task<Result<BootUserDTO>> HasCreatedProfileAsync(IdentityUser user);
        /// <summary>
        /// Create a new profile for the user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="req"></param>
        /// <returns></returns>
        Task<Result<LionheartUser>> CreateProfileAsync(IdentityUser user, CreateProfileRequest req);
        /// <summary>
        /// Set a personal access token for a given application. Creates a token for a given application if it does not yet exist, 
        /// updates it if it already exists.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="req"></param>
        /// <returns></returns>
        Task<Result<bool>> SetPersonalApiAccessToken(IdentityUser user, CreatePersonalApiAccessTokenRequest req);
    }
}
