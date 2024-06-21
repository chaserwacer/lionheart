using System.Reflection;

namespace lionheart.WellBeing;

public class WellBeingCatalog{
    // State and Const
    private Dictionary<DateOnly, WellBeingState> Catalog;

    public WellBeingCatalog(){
        this.Catalog = new Dictionary<DateOnly,WellBeingState>();
    }

    // Methods
    public void AddWellBeingState(WellBeingState state){
        // TODO: WOuld just passsing the new date work to basically replace the previous values?
        this.Catalog[DateOnly.FromDateTime(DateTime.Now)] = state;
    }

    public WellBeingState? GetWellBeingState(DateOnly date){
        this.Catalog.TryGetValue(date, out WellBeingState? state);
        //if (state is null){return new WellBeingState(-1);}
        if (state is null){return null;}
        return state;
    }

    public WellBeingState GetTodaysState(){
        this.Catalog.TryGetValue(DateOnly.FromDateTime(DateTime.Now), out WellBeingState? state);
        if (state is null){return new WellBeingState(0);}
        return state;
    }

    /// <summary>
    /// Method to calculate avg WellBeing #'s from past X days
    /// </summary>
    /// <returns>WellBeingState holding averages for #'s from past X days/returns>
    public WellBeingState returnXDayAverage(int X){
        // Return avg number for each score
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        int motivationScore = 0;
        int fatigueScore = 0;
        int moodScore = 0;
        int energyScore = 0;

        for(int i = X; i > 0; i--){
            WellBeingState? holder = GetWellBeingState(today.AddDays(-i));

            if(holder is not null){
                motivationScore += holder.MotivationScore;
                fatigueScore += holder.FatigueScore;
                moodScore += holder.MoodScore;
                energyScore += holder.EnergyScore;
            }        
        }

        return new WellBeingState(motivationScore, fatigueScore, moodScore, energyScore, "");
    }
}// end WellBeingCatalog



/// <summary>
/// Class to represent a users state of well-being on a give day. 
///  Well-being is represented via a list of integer scores for things like motivation, mood, etc. 
///  There is a final field for a written description for how the person is feeling. 
///  ###WrittenDesciprtion will likely be updated to be limited to 5-20 words.
/// </summary>
public class WellBeingState{
    public int MotivationScore { get; set; }
    public int FatigueScore { get; set; }
    public int MoodScore { get; set; }
    public int EnergyScore {get; set; }
    public string WrittenDescription {get; set; }

    public WellBeingState(int motivationScore, int fatigueScore, int moodScore, int energyScore, string writtenDescription){
        this.MotivationScore = motivationScore;
        this.FatigueScore = fatigueScore;  
        this.MoodScore = moodScore; 
        this.EnergyScore = energyScore;
        this.WrittenDescription = writtenDescription;
    }

    public WellBeingState(int number){
        this.MotivationScore = number;
        this.FatigueScore = number;  
        this.MoodScore = number; 
        this.EnergyScore = number;
        this.WrittenDescription = "";
    }



}// end WellBeingState
