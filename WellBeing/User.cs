namespace lionheart.WellBeing;
public class User
{
    private string Name { get; set;}
    private int age { get; set;}
    private float weight { get; set;}
    //private ActivityCatalog activityCatalog;
    private WellBeingCatalog wellBeingCatalog;
    // SummaryGenerator (would draw conclusions about recent training and how an athlete is doing)

    public User(string name, int age, float weight){
        this.Name = name;
        this.age = age;
        this.weight = weight;
        //this.activityCatalog = new ActivityCatalog();
        this.wellBeingCatalog = new WellBeingCatalog();
    }

}