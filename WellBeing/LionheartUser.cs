using Microsoft.AspNetCore.Identity;
namespace lionheart.WellBeing;

public class LionheartUser
{
    public Guid UserID { get; init;}
    public IdentityUser identityUser{ get; init;}
    public string Name { get; set;}
    public int Age { get; set;}
    public float Weight { get; set;}
    
    public List<WellnessState> WellnessStates {get; set;}

    public LionheartUser(Guid userID, IdentityUser identityUser, string name, int age, float weight){
        this.UserID = userID;
        this.identityUser = identityUser;
        this.Name = name;
        this.Age = age;
        this.Weight = weight;
        this.WellnessStates = new List<WellnessState>();
    }

}