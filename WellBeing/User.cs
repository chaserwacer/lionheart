namespace lionheart.WellBeing;
public class User
{
    private string Name { get; set;}
    private int Age { get; set;}
    private float Weight { get; set;}
    private Guid UserID { get; init;}
    private List<WellnessState> WellBeingStates {get; set;}


    // SummaryGenerator (would draw conclusions about recent training and how an athlete is doing)

    public User(string name, int age, float weight){
        this.Name = name;
        this.Age = age;
        this.Weight = weight;
        this.UserID = new Guid();
        this.WellBeingStates = [];
    }

    public void AddWellBeingState(WellnessState state){
        // ToDo: Check if need to make sure there isnt dupulicate for given day??
        WellBeingStates.Add(state);
    }

    public WellnessState GetWellBeingState



}