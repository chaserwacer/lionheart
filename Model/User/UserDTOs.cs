using System.ComponentModel.DataAnnotations;
namespace lionheart.Model.User
{
    /// <summary>
    /// Dto for handling a user and their state of profile creation
    /// </summary>
    /// <param name="Name">username/display name</param>
    /// <param name="HasCreatedProfile">boolean indicating profile creation state</param>
    public record BootUserDTO(string Name, Boolean HasCreatedProfile);

    /// <summary>
    /// The athlete's profile as a client reads it.
    /// </summary>
    /// <remarks>
    /// Purpose-built rather than the <see cref="LionheartUser"/> entity, and not merely as a matter
    /// of style. That entity holds an <see cref="Microsoft.AspNetCore.Identity.IdentityUser"/>
    /// navigation property, so serializing it put the account's <c>PasswordHash</c> and
    /// <c>SecurityStamp</c> on the wire; it also holds <c>ApiAccessTokens</c>, which is where the
    /// athlete's Oura credential lives. Every member here is one the client already supplied or
    /// may see.
    /// <para>
    /// Weight carries no unit anywhere in the domain — it is a bare number the athlete is asked to
    /// state in whatever unit they log their lifts in. Naming that here rather than inventing a
    /// unit keeps the gap visible.
    /// </para>
    /// </remarks>
    public record UserProfileView(Guid UserId, string Name, int Age, float Weight)
    {
        public static UserProfileView Of(LionheartUser user) =>
            new(user.UserID, user.Name, user.Age, user.Weight);
    }




    public record CreateProfileRequest
    {
        [Required]
        public required string DisplayName { get; set; } = String.Empty;

        [Required]
        public required int Age { get; set; }

        [Required]
        public required float Weight { get; set; }
    }

    public record CreatePersonalApiAccessTokenRequest
    {
        [Required]
        public required string ApplicationName { get; set; } = String.Empty;

        [Required]
        public required string AccessToken { get; set; } = String.Empty;
    }

}
