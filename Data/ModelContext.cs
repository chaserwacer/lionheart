using Microsoft.EntityFrameworkCore;
using lionheart.WellBeing;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using lionheart.Model.Oura;

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
