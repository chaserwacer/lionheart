using System.Reflection;

namespace lionheart.WellBeing;


/// <summary>
/// Class to represent a users state of well-being on a give day. 
///  Well-being is represented via a list of integer scores for things like motivation, mood, etc. 
///  There is a final field for a written description for how the person is feeling. 
///  ###WrittenDesciprtion will likely be updated to be limited to 5-20 words.
/// </summary>
public class WellnessState{
    public Guid StateID { get; init; }
    public Guid UserID {get; init;}
    public DateOnly Date {get; init;}
    public int MotivationScore { get; set; }
    public int FatigueScore { get; set; }
    public int MoodScore { get; set; }
    public int EnergyScore {get; set; }
    public string WrittenDescription {get; set; }

    public WellnessState(Guid userId, int motivationScore, int fatigueScore, int moodScore, int energyScore, string writtenDescription){
        this.StateID = new Guid();
        this.UserID = userId;
        this.Date = DateOnly.FromDateTime(DateTime.Now);
        this.MotivationScore = motivationScore;
        this.FatigueScore = fatigueScore;  
        this.MoodScore = moodScore; 
        this.EnergyScore = energyScore;
        this.WrittenDescription = writtenDescription;
    }

    public WellnessState(int number){
        this.MotivationScore = number;
        this.FatigueScore = number;  
        this.MoodScore = number; 
        this.EnergyScore = number;
        this.WrittenDescription = "";
    }
}// end WellBeingState



public class WellBeingCatalog{
    // State and Const
    private Dictionary<DateOnly, WellnessState> Catalog;

    public WellBeingCatalog(){
        this.Catalog = new Dictionary<DateOnly,WellnessState>();
    }

    // Methods
    public void AddWellBeingState(WellnessState state){
        // TODO: WOuld just passsing the new date work to basically replace the previous values?
        this.Catalog[DateOnly.FromDateTime(DateTime.Now)] = state;
    }

    public WellnessState? GetWellBeingState(DateOnly date){
        this.Catalog.TryGetValue(date, out WellnessState? state);
        //if (state is null){return new WellBeingState(-1);}
        if (state is null){return null;}
        return state;
    }

    public WellnessState GetTodaysState(){
        this.Catalog.TryGetValue(DateOnly.FromDateTime(DateTime.Now), out WellnessState? state);
        if (state is null){return new WellnessState(0);}
        return state;
    }

    /// <summary>
    /// Method to calculate avg WellBeing #'s from past X days
    /// </summary>
    /// <returns>WellBeingState holding averages for #'s from past X days/returns>
    // public WellnessState returnXDayAverage(int X){
    //     // Return avg number for each score
    //     DateOnly today = DateOnly.FromDateTime(DateTime.Now);
    //     int motivationScore = 0;
    //     int fatigueScore = 0;
    //     int moodScore = 0;
    //     int energyScore = 0;

    //     for(int i = X; i > 0; i--){
    //         WellnessState? holder = GetWellBeingState(today.AddDays(-i));

    //         if(holder is not null){
    //             motivationScore += holder.MotivationScore;
    //             fatigueScore += holder.FatigueScore;
    //             moodScore += holder.MoodScore;
    //             energyScore += holder.EnergyScore;
    //         }        
    //     }

    //     return new WellnessState(motivationScore, fatigueScore, moodScore, energyScore, "");
    // }
}// end WellBeingCatalog



