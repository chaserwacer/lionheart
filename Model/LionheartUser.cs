using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
namespace lionheart.WellBeing;

public class LionheartUser
{
    public Guid UserID { get; init;}
    public IdentityUser IdentityUser{ get; init;} = null!;
    public string Name { get; set;} = string.Empty;
    public int Age { get; set;}
    public float Weight { get; set;}
    
    public List<WellnessState> WellnessStates {get; set;} = [];
    public List<Activity> Activities { get; set;} = [];
    public List<ApiAccessToken> ApiAccessToken { get; set;} = [];

}