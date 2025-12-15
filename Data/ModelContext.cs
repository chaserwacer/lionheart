using Microsoft.EntityFrameworkCore;
using lionheart.WellBeing;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using lionheart.Model.Oura;
using lionheart.Model.Training;
using lionheart.Model.Injury;
using lionheart.Model.Training.SetEntry;

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
        public DbSet<DailyOuraData> DailyOuraInfos { get; set; }
        public DbSet<TrainingProgram> TrainingPrograms { get; set; }
        public DbSet<TrainingSession> TrainingSessions { get; set; }
        public DbSet<MovementBase> MovementBases { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<Movement> Movements { get; set; }
        public DbSet<LiftSetEntry> LiftSetEntries { get; set; }
        public DbSet<DTSetEntry> DTSetEntries { get; set; }
        public DbSet<Injury> Injuries { get; set; }
        public DbSet<InjuryEvent> InjuryEvents { get; set; }
        public DbSet<ChatConversation> ChatConversations { get; set; }
        public DbSet<ChatMessageItem> ChatMessageItems { get; set; }

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

            // Movements + Movement Bases + Movement Modifiers         
            modelBuilder.Entity<Movement>()
            .HasKey(m => m.MovementID);
            modelBuilder.Entity<Movement>()
                .HasOne<TrainingSession>(t => t.TrainingSession)
                .WithMany(s => s.Movements)
                .HasForeignKey(m => m.TrainingSessionID);

            modelBuilder.Entity<MovementBase>()
                .HasKey(m => m.MovementBaseID);
            modelBuilder.Entity<Movement>()
                .OwnsOne(m => m.MovementModifier)
                .HasOne(m => m.Equipment)
                .WithMany()
                .HasForeignKey(m => m.EquipmentID)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Movement>()
                .HasOne<MovementBase>(m => m.MovementBase)
                .WithMany()
                .HasForeignKey(m => m.MovementBaseID)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<MovementBase>()
                    .HasOne<LionheartUser>()
                    .WithMany(u => u.MovementBases)
                    .HasForeignKey(m => m.UserID);
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
            modelBuilder.Entity<ChatConversation>()
                .HasKey(c => c.ChatConversationId);
            modelBuilder.Entity<ChatConversation>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.ChatConversations)
                .HasForeignKey(c => c.UserID);
            modelBuilder.Entity<ChatMessageItem>()
                .HasKey(m => m.ChatMessageItemID);
            modelBuilder.Entity<ChatMessageItem>()
                .HasOne<ChatConversation>(c => c.ChatConversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatConversationID)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for ordering and fast queries by conversation
            modelBuilder.Entity<ChatMessageItem>()
                .HasIndex(m => new { m.ChatConversationID, m.CreationTime });

        
        }
    }
}
