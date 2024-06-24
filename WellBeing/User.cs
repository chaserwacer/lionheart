namespace lionheart.WellBeing;
public class User
{
    public Guid UserID { get; init;}
    public string Name { get; set;}
    public int Age { get; set;}
    public float Weight { get; set;}
    
    public List<WellnessState> WellnessStates {get; set;}


    // SummaryGenerator (would draw conclusions about recent training and how an athlete is doing)



    public User(string name, int age, float weight){
        this.Name = name;
        this.Age = age;
        this.Weight = weight;
        this.UserID = Guid.NewGuid();
        this.WellnessStates = [];
    }

    // public void AddWellnessState(WellnessState state){
    //     // ToDo: Check if need to make sure there isnt dupulicate for given day??
    //     WellnessStates.Add(state);
    // }

}