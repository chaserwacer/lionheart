using lionheart.ActivityTracking;
using lionheart.Model.Chat;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Oura;
using lionheart.Model.Training;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
namespace lionheart.Model.User
{
    /// <summary>
    /// Represents a user in the Lionheart application.
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
        public List<DailyOuraData> DailyOuraInfos { get; set; } = [];
        public List<TrainingProgram> TrainingPrograms { get; set; } = [];
        public List<TrainingSession> TrainingSessions { get; set; } = [];
        public List<MovementBase> MovementBases { get; set; } = [];
        public List<MovementData> MovementDatas { get; set; } = [];
        public List<MovementModifier> MovementModifiers { get; set; } = [];
        public List<Equipment> Equipments { get; set; } = [];
        public List<PersonalRecord> PersonalRecords { get; set; } = [];
        public List<Injury> Injuries { get; set; } = [];
        public List<ChatConversation> ChatConversations { get; set; } = [];
    }
}