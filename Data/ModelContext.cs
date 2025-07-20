using Microsoft.EntityFrameworkCore;
using lionheart.WellBeing;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using lionheart.Model.Oura;
using lionheart.Model.TrainingProgram;

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
        public DbSet<RunWalkDetails> RunWalkDetails { get; set; }
        public DbSet<RideDetails> RideDetails { get; set; }
        public DbSet<LiftDetails> LiftDetails { get; set; }
        public DbSet<ApiAccessToken> ApiAccessTokens { get; set; }
        public DbSet<DailyOuraInfo> DailyOuraInfos { get; set; }
        public DbSet<TrainingProgram> TrainingPrograms { get; set; }
        public DbSet<TrainingSession> TrainingSessions { get; set; }
        public DbSet<MovementBase> MovementBases { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<Movement> Movements { get; set; }
        public DbSet<SetEntry> SetEntries { get; set; }
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

            modelBuilder.Entity<RunWalkDetails>()
                .HasKey(d => d.ActivityID);

            modelBuilder.Entity<RideDetails>()
                .HasKey(d => d.ActivityID);

            modelBuilder.Entity<LiftDetails>()
                .HasKey(d => d.ActivityID);

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
                .HasOne<TrainingProgram>(t => t.TrainingProgram)
                .WithMany(p => p.TrainingSessions)
                .HasForeignKey(s => s.TrainingProgramID);

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
            modelBuilder.Entity<SetEntry>()
                .HasKey(s => s.SetEntryID);
            modelBuilder.Entity<SetEntry>()
                .HasOne<Movement>(m => m.Movement)
                .WithMany(m => m.Sets)
                .HasForeignKey(s => s.MovementID);

            // Access Tokens
            modelBuilder.Entity<ApiAccessToken>()
                .HasKey(a => a.ObjectID);
            modelBuilder.Entity<ApiAccessToken>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.ApiAccessTokens)
                .HasForeignKey(a => a.UserID);

            // Daily Oura Info
            modelBuilder.Entity<DailyOuraInfo>()
                .HasKey(a => a.ObjectID);

            modelBuilder.Entity<DailyOuraInfo>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.DailyOuraInfos)
                .HasForeignKey(a => a.UserID);

            modelBuilder.Entity<DailyOuraInfo>()
                .OwnsOne(o => o.ReadinessData);
            modelBuilder.Entity<DailyOuraInfo>()
                .OwnsOne(o => o.ResilienceData);
            modelBuilder.Entity<DailyOuraInfo>()
                .OwnsOne(o => o.SleepData);
            modelBuilder.Entity<DailyOuraInfo>()
                .OwnsOne(o => o.ActivityData);
        }
    }
}
