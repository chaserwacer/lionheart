using lionheart.ActivityTracking;
using lionheart.Model.Oura;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
namespace lionheart.WellBeing;

/// <summary>
/// Class to represent a user. This user has some basic properties, such as age and name. The class contains an Identity User, which is an object
/// it is associated with. Identity Users are users created via the outsourced ASP.NET authentication process. A user then holds a list of their 
/// wellness states, activites, etc. 
/// </summary>
public class LionheartUser
{
    public Guid UserID { get; init; }
    public IdentityUser IdentityUser { get; init; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public float Weight { get; set; }

    public List<WellnessState> WellnessStates { get; set; } = [];
    public List<Activity> Activities { get; set; } = [];
    public List<ApiAccessToken> ApiAccessTokens { get; set; } = [];
    public List<DailyOuraInfo> DailyOuraInfos { get; set; } = [];
    public List<TrainingProgram> TrainingPrograms { get; set; } = [];
    public List<MovementBase> MovementBases { get; set; } = [];
    public List<Equipment> Equipments { get; set; } = [];
}