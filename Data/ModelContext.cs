using Microsoft.EntityFrameworkCore;
using lionheart.WellBeing;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using lionheart.Model.Oura;
using lionheart.Model.Training;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.SetEntry;
using lionheart.Model.User;
using lionheart.Model.Chat;

namespace lionheart.Data
{
    /// <summary>
    /// ModelContext class handles clarifiying the relationships among the many different objects to ef core. It specifies the different tables in the database, 
    /// as well as how those tables are related to oneanother.
    /// </summary>
    public class ModelContext : IdentityDbContext<IdentityUser>
    {
        public DbSet<LionheartUser> LionheartUsers { get; set; }
        public DbSet<WellnessState> WellnessStates { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ApiAccessToken> ApiAccessTokens { get; set; }
        public DbSet<DailyOuraData> DailyOuraDatas { get; set; }
        public DbSet<TrainingProgram> TrainingPrograms { get; set; }
        public DbSet<TrainingSession> TrainingSessions { get; set; }
        public DbSet<MovementBase> MovementBases { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<MuscleGroup> MuscleGroups { get; set;}
        public DbSet<Movement> Movements { get; set; }
        public DbSet<MovementData> MovementDatas { get; set; }
        public DbSet<MovementModifier> MovementModifiers { get; set; }
        public DbSet<LiftSetEntry> LiftSetEntries { get; set; }
        public DbSet<DTSetEntry> DTSetEntries { get; set; }
        public DbSet<PersonalRecord> PersonalRecords { get; set; }
        public DbSet<Injury> Injuries { get; set; }
        public DbSet<InjuryEvent> InjuryEvents { get; set; }
        public DbSet<LHChatConversation> ChatConversations { get; set; }
        public DbSet<LHUserChatMessage> UserChatMessages { get; set; }
        public DbSet<LHModelChatMessage> ModelChatMessages { get; set; }
        public DbSet<LHSystemChatMessage> SystemChatMessages { get; set; }
        public DbSet<LHToolChatMessage> ToolChatMessages { get; set; }
        public ModelContext(DbContextOptions<ModelContext> options) : base(options)
        {
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LionheartUser>()
                .HasKey(u => u.UserID);

            // Wellness States 
            modelBuilder.Entity<WellnessState>()
                .HasKey(w => w.StateID);

            modelBuilder.Entity<WellnessState>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.WellnessStates)
                .HasForeignKey(w => w.UserID);

            // Injury
            modelBuilder.Entity<Injury>()
                .HasKey(i => i.InjuryID);

            modelBuilder.Entity<Injury>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.Injuries)
                .HasForeignKey(i => i.UserID);

            modelBuilder.Entity<Injury>()
                .HasMany(i => i.InjuryEvents)
                .WithOne(e => e.Injury)
                .HasForeignKey(e => e.InjuryID)
                .OnDelete(DeleteBehavior.Cascade);



            // Identity Users
            modelBuilder.Entity<IdentityUserLogin<string>>()
                .HasKey(u => u.UserId);
            modelBuilder.Entity<IdentityUserRole<string>>()
                .HasKey(nameof(IdentityUserRole<string>.UserId), nameof(IdentityUserRole<string>.RoleId));
            modelBuilder.Entity<IdentityUserToken<string>>()
            .HasKey(u => u.UserId);

            // Activities
            modelBuilder.Entity<Activity>()
                .HasKey(a => a.ActivityID);

            modelBuilder.Entity<Activity>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.Activities)
                .HasForeignKey(a => a.UserID);

            modelBuilder.Entity<Activity>()
                .HasOne<PerceivedEffortRatings>(a => a.PerceivedEffortRatings)
                .WithOne()
                .IsRequired(false);
                       

            // Training Programs
            modelBuilder.Entity<TrainingProgram>()
                .HasKey(p => p.TrainingProgramID);
            modelBuilder.Entity<TrainingProgram>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.TrainingPrograms)
                .HasForeignKey(p => p.UserID);

            // Training Sessions
            modelBuilder.Entity<TrainingSession>()
                .HasKey(s => s.TrainingSessionID);
            modelBuilder.Entity<TrainingSession>()
                .HasOne<TrainingProgram>(s => s.TrainingProgram)
                .WithMany(p => p.TrainingSessions)
                .HasForeignKey(s => s.TrainingProgramID)
                .IsRequired(false);

            modelBuilder.Entity<TrainingSession>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.TrainingSessions)
                .HasForeignKey(s => s.UserID);
            
            modelBuilder.Entity<TrainingSession>()
                .HasOne<PerceivedEffortRatings>(s => s.PerceivedEffortRatings)
                .WithOne()
                .IsRequired(false);

            // Movements + Movement Bases + Movement Modifiers + MovementData
            modelBuilder.Entity<Movement>()
                .HasKey(m => m.MovementID);
            modelBuilder.Entity<Movement>()
                .HasOne<TrainingSession>(t => t.TrainingSession)
                .WithMany(s => s.Movements)
                .HasForeignKey(m => m.TrainingSessionID);

            // MovementData is now a separate table referenced by Movement via FK
            modelBuilder.Entity<MovementData>()
                .HasKey(md => md.MovementDataID);

            modelBuilder.Entity<MovementData>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.MovementDatas)
                .HasForeignKey(md => md.UserID);

            modelBuilder.Entity<MovementData>()
                .HasOne(md => md.Equipment)
                .WithMany()
                .HasForeignKey(md => md.EquipmentID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovementData>()
                .HasOne(md => md.MovementBase)
                .WithMany()
                .HasForeignKey(md => md.MovementBaseID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovementData>()
                .HasOne(md => md.MovementModifier)
                .WithMany()
                .HasForeignKey(md => md.MovementModifierID)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique index to prevent duplicate MovementData combinations per user
            modelBuilder.Entity<MovementData>()
                .HasIndex(md => new { md.UserID, md.EquipmentID, md.MovementBaseID, md.MovementModifierID })
                .IsUnique();

            // Movement references MovementData via FK
            modelBuilder.Entity<Movement>()
                .HasOne(m => m.MovementData)
                .WithMany()
                .HasForeignKey(m => m.MovementDataID)
                .OnDelete(DeleteBehavior.Restrict);

            // MovementModifier configuration
            modelBuilder.Entity<MovementModifier>()
                .HasKey(mm => mm.MovementModifierID);

            modelBuilder.Entity<MovementModifier>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.MovementModifiers)
                .HasForeignKey(mm => mm.UserID);

            modelBuilder.Entity<MovementBase>()
                .HasKey(m => m.MovementBaseID);

            modelBuilder.Entity<MovementBase>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.MovementBases)
                .HasForeignKey(m => m.UserID);

            modelBuilder.Entity<MovementBase>()
                .OwnsMany(m => m.TrainedMuscles);

            modelBuilder.Entity<MuscleGroup>()
                .HasKey(m => m.MuscleGroupID);

            modelBuilder.Entity<Equipment>()
                .HasKey(e => e.EquipmentID);
            modelBuilder.Entity<Equipment>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.Equipments)
                .HasForeignKey(e => e.UserID);

            // Set Entries
            modelBuilder.Entity<LiftSetEntry>()
                .HasKey(s => s.SetEntryID);
            modelBuilder.Entity<LiftSetEntry>()
                .HasOne<Movement>(s => s.Movement)
                .WithMany(m => m.LiftSets)
                .HasForeignKey(s => s.MovementID);

            modelBuilder.Entity<DTSetEntry>()
                .HasKey(s => s.SetEntryID);
            modelBuilder.Entity<DTSetEntry>()
                .HasOne<Movement>(s => s.Movement)
                .WithMany(m => m.DistanceTimeSets)
                .HasForeignKey(s => s.MovementID);

            // Personal Records
            modelBuilder.Entity<PersonalRecord>()
                .HasKey(pr => pr.PersonalRecordID);

            modelBuilder.Entity<PersonalRecord>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.PersonalRecords)
                .HasForeignKey(pr => pr.UserID);

            modelBuilder.Entity<PersonalRecord>()
                .HasOne(pr => pr.MovementData)
                .WithMany()
                .HasForeignKey(pr => pr.MovementDataID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PersonalRecord>()
                .HasOne(pr => pr.PreviousPersonalRecord)
                .WithMany()
                .HasForeignKey(pr => pr.PreviousPersonalRecordID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PersonalRecord>()
                .HasOne(pr => pr.SourceLiftSetEntry)
                .WithMany()
                .HasForeignKey(pr => pr.SourceLiftSetEntryID)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for fast lookup of active PRs by user and movement data
            modelBuilder.Entity<PersonalRecord>()
                .HasIndex(pr => new { pr.UserID, pr.MovementDataID, pr.PRType, pr.IsActive });

            // Access Tokens
            modelBuilder.Entity<ApiAccessToken>()
                .HasKey(a => a.ObjectID);
            modelBuilder.Entity<ApiAccessToken>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.ApiAccessTokens)
                .HasForeignKey(a => a.UserID);

            // Daily Oura Info
            modelBuilder.Entity<DailyOuraData>()
                .HasKey(a => a.ObjectID);

            modelBuilder.Entity<DailyOuraData>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.DailyOuraInfos)
                .HasForeignKey(a => a.UserID);

            modelBuilder.Entity<DailyOuraData>()
                .OwnsOne(o => o.ReadinessData);
            modelBuilder.Entity<DailyOuraData>()
                .OwnsOne(o => o.ResilienceData);
            modelBuilder.Entity<DailyOuraData>()
                .OwnsOne(o => o.SleepData);
            modelBuilder.Entity<DailyOuraData>()
                .OwnsOne(o => o.ActivityData);

            // Chat Conversations
            modelBuilder.Entity<LHChatConversation>()
                .HasKey(c => c.ChatConversationID);

            modelBuilder.Entity<LHChatConversation>()
                .HasOne(c => c.ChatSystemMessage)
                .WithOne(m => m.ChatConversation)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LHChatConversation>()
                .HasMany(c => c.UserMessages)
                .WithOne(m => m.ChatConversation)
                .HasForeignKey(m => m.ChatConversationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LHChatConversation>()
                .HasMany(c => c.ModelMessages)
                .WithOne(m => m.ChatConversation)
                .HasForeignKey(m => m.ChatConversationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LHChatConversation>()
                .HasMany(c => c.ToolMessages)
                .WithOne(m => m.ChatConversation)
                .HasForeignKey(m => m.ChatConversationID)
                .OnDelete(DeleteBehavior.Cascade);

            // Chat Messages
            modelBuilder.Entity<LHUserChatMessage>()
                .HasKey(m => m.ChatMessageItemID);

            modelBuilder.Entity<LHModelChatMessage>()
                .HasKey(m => m.ChatMessageItemID);

            modelBuilder.Entity<LHSystemChatMessage>()
                .HasKey(m => m.ChatMessageItemID);

            modelBuilder.Entity<LHToolChatMessage>()
                .HasKey(m => m.ChatMessageItemID);

            

        
        }
    }
}
