using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Training;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.User;
using Microsoft.EntityFrameworkCore;
using Programs = lionheart.Model.Training.Programs;
using Sessions = lionheart.Model.Training.Sessions;

namespace lionheart.Data;

/// <summary>
/// EF mapping for the training domain (docs/data-remodel.md): TPH per aggregate,
/// polymorphic shapes as JSON columns, embedding vectors as BLOBs. The derived
/// "StartTime" columns configured here are stamped by ModelContext on every save.
/// </summary>
public static class RemodelConfiguration
{
    /// <summary>
    /// Columns recomputed by <see cref="ModelContext"/> on every save from the record they
    /// describe, so a lookup column cannot drift from the achievement it was read off.
    /// </summary>
    /// <remarks>
    /// Shadow properties, and deliberately not named after the members they duplicate: EF matches
    /// a shadow property to a CLR property of the same name and maps that instead, which defeats
    /// the <c>Ignore</c> that keeps the computed member out of the schema.
    /// </remarks>
    internal const string RecordKindColumn = "KindName";
    internal const string RecordBracketColumn = "KindBracket";
    internal const string RecordIsStandingColumn = "Stands";

    public static void Apply(ModelBuilder modelBuilder)
    {
        ConfigureSessions(modelBuilder);
        ConfigurePrograms(modelBuilder);
        ConfigureContexts(modelBuilder);
        ConfigureAnalyzableEntries(modelBuilder);
        ConfigureFacetValues(modelBuilder);
        ConfigurePersonalRecords(modelBuilder);
        ConfigureFocusPins(modelBuilder);
    }

    private static void ConfigureFocusPins(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FocusPin>(pin =>
        {
            pin.ToTable("FocusPins");
            pin.HasKey(p => p.FocusPinId);
            pin.HasOne<LionheartUser>().WithMany().HasForeignKey(p => p.UserId);
            pin.HasIndex(p => p.UserId);

            // A pin always names a real movement, so unlike a record's subject there is no sentinel
            // and the key can be relational. Restrict, so a movement with pins on it cannot vanish
            // from under the history they are.
            pin.HasOne<MovementData>().WithMany().HasForeignKey(p => p.MovementDataId)
                .OnDelete(DeleteBehavior.Restrict);

            // JSON rather than a complex type with a column per member: EF cannot bind a complex
            // property to a constructor parameter, so columns would cost the terms and the pin a
            // constructor that exists only for EF. Nothing queries a horizon in SQL — the list is
            // ordered in app, because the status it leads with is JSON too.
            pin.Property(p => p.Terms).HasJsonConversion();
            pin.Property(p => p.Status).HasJsonConversion();
            pin.Ignore(p => p.IsPinned);
        });
    }

    private static void ConfigurePersonalRecords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersonalRecord>(record =>
        {
            record.ToTable("PersonalRecords");
            record.HasKey(r => r.PersonalRecordId);
            record.HasOne<LionheartUser>().WithMany(u => u.PersonalRecords).HasForeignKey(r => r.UserId);

            // Fill-state ↔ scalar column, the same shape a session's program membership takes:
            // trail running persists as Guid.Empty so the domain never sees a nullable movement
            // id. No relational FK on purpose — the sentinel would violate it.
            record.Property(r => r.Subject)
                .HasConversion(s => SubjectToGuid(s), g => GuidToSubject(g))
                .HasColumnName("MovementDataId");

            record.Property(r => r.Achievement).HasJsonConversion();
            record.Property(r => r.Standing).HasJsonConversion();
            record.Property(r => r.Lineage).HasJsonConversion();

            // Read off the achievement and the standing rather than mapped from them: the CLR
            // members are computed, and a settable duplicate is what lets the two disagree.
            record.Ignore(r => r.Key);
            record.Ignore(r => r.Kind);
            record.Ignore(r => r.IsStanding);
            record.Property<string>(RecordKindColumn).IsRequired();
            record.Property<int>(RecordBracketColumn);
            record.Property<bool>(RecordIsStandingColumn);

            // What the records screen and detection both ask for: this athlete's current records.
            record.HasIndex(nameof(PersonalRecord.UserId), RecordIsStandingColumn);

            // What a history read asks for: one chain, newest first.
            record.HasIndex(nameof(PersonalRecord.UserId), RecordKindColumn, RecordBracketColumn);

            // No unique index over the standing records, though the invariant it would carry —
            // at most one record of a kind standing on a subject — is the one this aggregate is
            // built on. A hand-over demotes one row and inserts another inside a single
            // transaction, and SQLite enforces a unique index statement by statement with no
            // deferred form, so the insert is refused while the row it replaces is still
            // standing. The invariant lives in PersonalRecord.Succeeding instead, which writes
            // both halves and is the only way to add a record over an existing one.
        });
    }

    private static Guid SubjectToGuid(RecordSubject subject) =>
        subject is RecordSubject.Movement movement ? movement.MovementDataId : Guid.Empty;

    private static RecordSubject GuidToSubject(Guid movementDataId) =>
        movementDataId == Guid.Empty
            ? new RecordSubject.TrailRunning()
            : new RecordSubject.Movement(movementDataId);

    private static void ConfigureSessions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sessions.TrainingSession>(session =>
        {
            session.ToTable("Sessions");
            session.HasKey(s => s.TrainingSessionId);
            session.HasOne<LionheartUser>().WithMany().HasForeignKey(s => s.UserId);
            session.HasIndex(s => s.UserId);

            // Fill-state ↔ scalar column: Standalone persists as Guid.Empty so the domain
            // never sees a nullable FK. No relational FK constraint on purpose — the
            // sentinel would violate it.
            session.Property(s => s.Membership)
                .HasConversion(m => MembershipToGuid(m), g => GuidToMembership(g))
                .HasColumnName("TrainingProgramId");
            session.HasIndex(s => s.Membership);

            session.HasDiscriminator<string>("Lifecycle")
                .HasValue<Sessions.PlannedTrainingSession>("planned")
                .HasValue<Sessions.ActiveTrainingSession>("active")
                .HasValue<Sessions.CompletedTrainingSession>("completed")
                .HasValue<Sessions.SkippedTrainingSession>("skipped");
        });

        modelBuilder.Entity<Sessions.PlannedTrainingSession>(planned =>
        {
            planned.Property(s => s.PlannedFor).HasColumnName("PlannedFor");
            planned.Property(s => s.Prescriptions).HasJsonConversion();
        });

        modelBuilder.Entity<Sessions.SkippedTrainingSession>(skipped =>
            skipped.Property(s => s.PlannedFor).HasColumnName("PlannedFor"));

        modelBuilder.Entity<Sessions.ActiveTrainingSession>(active =>
            active.Property(s => s.Sets).HasJsonConversion().HasColumnName("Sets"));

        modelBuilder.Entity<Sessions.CompletedTrainingSession>(completed =>
        {
            completed.Property(s => s.Sets).HasJsonConversion().HasColumnName("Sets");
            completed.Property(s => s.Period).HasJsonConversion();
            completed.Property(s => s.Effort).HasJsonConversion();
            completed.Property(s => s.Decoration).HasJsonConversion();
            completed.Property<DateTime>("StartTime");
            completed.HasIndex("StartTime");
        });
    }

    private static void ConfigurePrograms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Programs.TrainingProgram>(program =>
        {
            program.ToTable("Programs");
            program.HasKey(p => p.TrainingProgramId);
            program.HasOne<LionheartUser>().WithMany().HasForeignKey(p => p.UserId);
            program.HasIndex(p => p.UserId);
            program.Property(p => p.Tags).HasJsonConversion();

            program.HasDiscriminator<string>("Lifecycle")
                .HasValue<Programs.PlannedTrainingProgram>("planned")
                .HasValue<Programs.ActiveTrainingProgram>("active")
                .HasValue<Programs.CompletedTrainingProgram>("completed");
        });
    }

    private static void ConfigureContexts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Context>(context =>
        {
            context.ToTable("Contexts");
            context.HasKey(c => c.ContextId);
            context.HasOne<LionheartUser>().WithMany().HasForeignKey(c => c.UserId);
            context.Property(c => c.Period).HasJsonConversion();
            context.Property(c => c.Decoration).HasJsonConversion();
            context.Property<DateTime>("StartTime");
            context.HasIndex("UserId", "StartTime");

            context.HasDiscriminator<string>("Kind")
                .HasValue<ExertionContext>("exertion")
                .HasValue<StateContext>("state");
        });

        modelBuilder.Entity<ExertionContext>(exertion =>
            exertion.Property(c => c.Effort).HasJsonConversion());
    }

    private static void ConfigureAnalyzableEntries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalyzableEntry>(entry =>
        {
            entry.ToTable("AnalyzableEntries");
            entry.HasKey(e => e.EntryId); // one projection row per analyzable, keyed by its id

            // Derived data follows its source: this row is a projection of a session or context
            // the user owns, so deleting the user must take the summary text and embedding with
            // it rather than leaving them queryable in the table retrieval reads.
            entry.HasOne<LionheartUser>().WithMany().HasForeignKey(e => e.UserId);
            entry.Property(e => e.Period).HasJsonConversion();
            entry.Property(e => e.Facets).HasJsonConversion();
            entry.Property(e => e.Embedding).HasVectorConversion();
            entry.Property<DateTime>("StartTime");
            entry.HasIndex("UserId", "StartTime");
            entry.HasIndex(e => e.Kind);
        });
    }

    private static void ConfigureFacetValues(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FacetValue>(value =>
        {
            value.ToTable("FacetValues");
            value.HasKey(v => new { v.Axis, v.Canonical }); // canonical spelling is the identity
            value.Property(v => v.Aliases).HasJsonConversion();
            value.Property(v => v.Embedding).HasVectorConversion();
        });
    }

    private static Guid MembershipToGuid(Sessions.ProgramMembership membership) =>
        membership is Sessions.InProgram inProgram ? inProgram.TrainingProgramId : Guid.Empty;

    private static Sessions.ProgramMembership GuidToMembership(Guid programId) =>
        programId == Guid.Empty ? new Sessions.Standalone() : new Sessions.InProgram(programId);
}
