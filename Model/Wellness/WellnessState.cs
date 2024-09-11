using System.Reflection;

namespace lionheart.WellBeing;


/// <summary>
/// Class to represent a users state of well-being on a given day. 
///  Well-being is represented via a list of integer scores for things like motivation, mood, etc. 
/// </summary>
public class WellnessState{
    public Guid StateID { get; init; }
    public Guid UserID {get; init;}
    public DateOnly Date {get; init;}
    public int MotivationScore { get; set; }
    public int StressScore { get; set; }
    public int MoodScore { get; set; }
    public int EnergyScore {get; set; }
    public double OverallScore { get; set; }


    public WellnessState(Guid userID, int motivationScore, int stressScore, int moodScore, int energyScore, DateOnly date)
    {
        this.UserID = userID;
        this.MotivationScore = motivationScore;
        this.StressScore = stressScore;  
        this.MoodScore = moodScore; 
        this.EnergyScore = energyScore;
        double avg = (double)(moodScore + energyScore + motivationScore + (6 - stressScore)) / 4;
        int decimalPlaces = 2;
        this.OverallScore = Math.Round(avg, decimalPlaces);
        this.StateID = Guid.NewGuid();
        this.Date = date;
    }

}// end WellBeingState


