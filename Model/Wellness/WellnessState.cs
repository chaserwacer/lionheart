using System.Reflection;

namespace lionheart.WellBeing
{

    /// <summary>
    /// Represents the wellness state of a user on a specific date.
    /// This includes various scores related to motivation, stress, mood, and energy,
    /// as well as an overall wellness score calculated from these individual scores.
    /// </summary>
    /// <remarks>
    /// This metric is usable as a perceived wellness indicator for the user.
    /// </remarks>
    public class WellnessState
    {
        public Guid StateID { get; init; }
        public Guid UserID { get; init; }
        public DateOnly Date { get; init; }
        public int MotivationScore { get; set; }
        public int StressScore { get; set; }
        public int MoodScore { get; set; }
        public int EnergyScore { get; set; }
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

    }
}
