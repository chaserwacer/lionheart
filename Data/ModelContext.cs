using Microsoft.EntityFrameworkCore;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using lionheart.Model.Oura;
using lionheart.Model.Training;
using lionheart.Model.InjuryManagement;
using lionheart.Model.User;
using lionheart.Model.Chat;
using lionheart.Model.Profile;
using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using Programs = lionheart.Model.Training.Programs;
using Sessions = lionheart.Model.Training.Sessions;

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
        public DbSet<ApiAccessToken> ApiAccessTokens { get; set; }
        public DbSet<DailyOuraData> DailyOuraDatas { get; set; }
        public DbSet<MovementBase> MovementBases { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<MuscleGroup> MuscleGroups { get; set;}
        public DbSet<MovementData> MovementDatas { get; set; }
        public DbSet<MovementModifier> MovementModifiers { get; set; }
        public DbSet<PersonalRecord> PersonalRecords { get; set; }
        public DbSet<Injury> Injuries { get; set; }
        public DbSet<InjuryEvent> InjuryEvents { get; set; }
        public DbSet<LHChatConversation> ChatConversations { get; set; }
        public DbSet<LHUserChatMessage> UserChatMessages { get; set; }
        public DbSet<LHModelChatMessage> ModelChatMessages { get; set; }
        public DbSet<LHSystemChatMessage> SystemChatMessages { get; set; }
        public DbSet<LHChatToolCallResult> ToolChatMessages { get; set; }
        public DbSet<AthleteContextCard> AthleteContextCards { get; set; }

        // Training domain — design in docs/data-remodel.md
        public DbSet<Sessions.TrainingSession> Sessions { get; set; }
        public DbSet<Programs.TrainingProgram> Programs { get; set; }
        public DbSet<Context> Contexts { get; set; }
        public DbSet<AnalyzableEntry> AnalyzableEntries { get; set; }
        public DbSet<FacetValue> FacetValues { get; set; }

        public ModelContext(DbContextOptions<ModelContext> options) : base(options)
        {
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampDerivedTimeColumns();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampDerivedTimeColumns();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Domain time-truth lives inside JSON Period columns; queries need a real indexed
        /// column. Recomputing the duplicate on every save is what keeps it unable to drift.
        /// </summary>
        private void StampDerivedTimeColumns()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
                var period = entry.Entity switch
                {
                    Sessions.CompletedTrainingSession completed => completed.Period,
                    Context context => context.Period,
                    AnalyzableEntry analyzable => analyzable.Period,
                    _ => null
                };
                if (period is not null) entry.Property("StartTime").CurrentValue = period.StartTime;
            }
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
            // Exclude the passkey entity added by the .NET 10 UserPasskeys DbSet: without a
            // base.OnModelCreating call it maps raw and fails validation on its keyless
            // IdentityPasskeyData. Passkeys are unused, matching the framework default.
            modelBuilder.Ignore<IdentityUserPasskey<string>>();

            // MovementData: reusable movement identity (base + equipment + modifier)
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
                .HasMany(mb => mb.MuscleGroups);



            modelBuilder.Entity<MuscleGroup>()
                .HasKey(m => m.MuscleGroupID);

            modelBuilder.Entity<Equipment>()
                .HasKey(e => e.EquipmentID);
            modelBuilder.Entity<Equipment>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.Equipments)
                .HasForeignKey(e => e.UserID);

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

            modelBuilder.Entity<LHChatToolCallResult>()
                .HasKey(m => m.ChatMessageItemID);

            // Athlete Context Card — one derived card per user.
            modelBuilder.Entity<AthleteContextCard>()
                .HasKey(c => c.CardID);

            modelBuilder.Entity<AthleteContextCard>()
                .HasIndex(c => c.UserID)
                .IsUnique();

            RemodelConfiguration.Apply(modelBuilder);
        }
    }
}
