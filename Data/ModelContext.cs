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
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Availability;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;
using lionheart.Model.Chat.Changes;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Model.Health;
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
        public DbSet<MovementBase> MovementBases { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
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
        public DbSet<ChatThreadSession> ChatThreadSessions { get; set; }
        public DbSet<ChatMemory> ChatMemories { get; set; }
        public DbSet<ChangeProposal> ChangeProposals { get; set; }

        // Training domain — design in docs/data-remodel.md
        public DbSet<Sessions.TrainingSession> Sessions { get; set; }
        public DbSet<Programs.TrainingProgram> Programs { get; set; }
        public DbSet<Context> Contexts { get; set; }
        public DbSet<AnalyzableEntry> AnalyzableEntries { get; set; }
        public DbSet<FacetValue> FacetValues { get; set; }
        public DbSet<FocusPin> FocusPins { get; set; }
        public DbSet<Routine> Routines { get; set; }
        public DbSet<NonTrainingDay> NonTrainingDays { get; set; }
        public DbSet<BodyweightMeasurement> BodyweightMeasurements { get; set; }
        public DbSet<BodyCompositionScan> BodyCompositionScans { get; set; }
        public DbSet<WorkoutRecording> WorkoutRecordings { get; set; }
        public DbSet<Model.Health.Measurements.HealthDailyMeasure> HealthDailyMeasures { get; set; }
        public DbSet<DeletedHealthWorkout> DeletedHealthWorkouts { get; set; }
        public DbSet<Sessions.DeletedStravaActivity> DeletedStravaActivities { get; set; }

        // Oura — design in docs/oura-integration.md. One table per collection: eight documents that
        // describe a calendar day, four that describe events within one, and the all-day heart-rate
        // series stored as one run per UTC day.
        public DbSet<OuraDailyActivity> OuraDailyActivity { get; set; }
        public DbSet<OuraDailyReadiness> OuraDailyReadiness { get; set; }
        public DbSet<OuraDailySleep> OuraDailySleep { get; set; }
        public DbSet<OuraDailySpo2> OuraDailySpo2 { get; set; }
        public DbSet<OuraDailyStress> OuraDailyStress { get; set; }
        public DbSet<OuraDailyResilience> OuraDailyResilience { get; set; }
        public DbSet<OuraSleepPeriod> OuraSleepPeriods { get; set; }
        public DbSet<OuraWorkout> OuraWorkouts { get; set; }
        public DbSet<OuraSession> OuraSessions { get; set; }
        public DbSet<OuraTag> OuraTags { get; set; }
        public DbSet<OuraVo2Max> OuraVo2Max { get; set; }
        public DbSet<OuraDailyCardiovascularAge> OuraDailyCardiovascularAge { get; set; }
        public DbSet<OuraHeartRateDay> OuraHeartRateDays { get; set; }

        public ModelContext(DbContextOptions<ModelContext> options) : base(options)
        {
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampDerivedColumns();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampDerivedColumns();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Recomputes every column that duplicates something the domain derives, so none of them
        /// can disagree with what it was read off.
        /// </summary>
        private void StampDerivedColumns()
        {
            StampDerivedTimeColumns();
            StampSessionSourceColumns();
            StampRecordLookupColumns();
            StampChatThreadLookupColumns();
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
                    IOuraEvent ouraEvent => ouraEvent.Period,
                    _ => null
                };
                if (period is not null) entry.Property("StartTime").CurrentValue = period.StartTimeUtc;
            }
        }

        /// <summary>
        /// A session's Health workout id lives inside its JSON source; stamped out so the unique
        /// index can hold one session per imported workout.
        /// </summary>
        private void StampSessionSourceColumns()
        {
            foreach (var entry in ChangeTracker.Entries<Sessions.TrainingSession>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
                entry.Property(RemodelConfiguration.HealthWorkoutIdColumn).CurrentValue =
                    entry.Entity.Source is Sessions.SessionSource.AppleHealth health ? health.WorkoutId : null;
            }
        }

        /// <summary>
        /// A record's kind, its bracket and whether it stands live inside JSON columns the
        /// database can neither index nor read. Stamping them out again on every save is what
        /// lets a standing read and a history read be indexed queries instead of deserializing
        /// every record the athlete holds.
        /// </summary>
        private void StampRecordLookupColumns()
        {
            foreach (var entry in ChangeTracker.Entries<PersonalRecord>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

                var kind = entry.Entity.Kind;
                entry.Property(RemodelConfiguration.RecordKindColumn).CurrentValue = kind.Name;
                entry.Property(RemodelConfiguration.RecordBracketColumn).CurrentValue = kind.Bracket;
                entry.Property(RemodelConfiguration.RecordIsStandingColumn).CurrentValue =
                    entry.Entity.IsStanding;
            }
        }


        /// <summary>
        /// Whether a thread session is consolidated and whether a memory is current both live in
        /// JSON columns; stamped out so the reads that ask for them are indexed.
        /// </summary>
        private void StampChatThreadLookupColumns()
        {
            foreach (var entry in ChangeTracker.Entries<ChatThreadSession>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
                entry.Property(ChatThreadConfiguration.SessionIsConsolidatedColumn).CurrentValue =
                    entry.Entity.IsConsolidated;
            }

            foreach (var entry in ChangeTracker.Entries<ChatMemory>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
                entry.Property(ChatThreadConfiguration.MemoryIsCurrentColumn).CurrentValue = entry.Entity.IsCurrent;
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

            // The athlete's setup for the movement, as two columns on the movement's row.
            modelBuilder.Entity<MovementData>()
                .ComplexProperty(md => md.Setup, setup =>
                {
                    setup.Property(s => s.Notes).HasColumnName("SetupNotes");
                    setup.Property(s => s.AddsToBodyweight).HasColumnName("AddsToBodyweight");
                });

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

            // JSON for the reason a focus pin's terms are: EF cannot bind a complex property to the
            // classification's constructor, and nothing filters on it in SQL. Only the athlete's own
            // statement is stored; the map file answers for every base without one.
            modelBuilder.Entity<MovementBase>()
                .Property(mb => mb.Classification)
                .HasJsonConversion();

            modelBuilder.Entity<Equipment>()
                .HasKey(e => e.EquipmentID);
            modelBuilder.Entity<Equipment>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.Equipments)
                .HasForeignKey(e => e.UserID);

            // Personal records are mapped with the rest of the training domain, in
            // RemodelConfiguration.

            // Access Tokens
            modelBuilder.Entity<ApiAccessToken>()
                .HasKey(a => a.ObjectID);
            modelBuilder.Entity<ApiAccessToken>()
                .HasOne<LionheartUser>()
                .WithMany(u => u.ApiAccessTokens)
                .HasForeignKey(a => a.UserID);

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
            OuraConfiguration.Apply(modelBuilder);
            BodyMeasurementConfiguration.Apply(modelBuilder);
            ChatThreadConfiguration.Apply(modelBuilder);
            HealthConfiguration.Apply(modelBuilder);
        }
    }
}
