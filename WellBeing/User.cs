namespace lionheart.WellBeing;
public class User
{
    public Guid UserID { get; init;}
    public string UserName { get; set;}
    public string Name { get; set;}
    public string Password { get; set;}
    public int Age { get; set;}
    public float Weight { get; set;}
    
    public List<WellnessState> WellnessStates {get; set;}

    public User(string userName, string name, string password, int age, float weight){
        this.UserName = userName;
        this.Name = name;
        this.Password = password;
        this.Age = age;
        this.Weight = weight;
        this.UserID = Guid.NewGuid();
        this.WellnessStates = [];
    }

}