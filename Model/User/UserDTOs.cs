using System.ComponentModel.DataAnnotations;
namespace lionheart.Model.User
{
    /// <summary>
    /// Dto for handling a user and their state of profile creation
    /// </summary>
    /// <param name="Name">username/display name</param>
    /// <param name="HasCreatedProfile">boolean indicating profile creation state</param>
    public record BootUserDTO(string Name, Boolean HasCreatedProfile);




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
