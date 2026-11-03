using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
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
    public static void Apply(ModelBuilder modelBuilder)
    {
        ConfigureSessions(modelBuilder);
        ConfigurePrograms(modelBuilder);
        ConfigureContexts(modelBuilder);
        ConfigureAnalyzableEntries(modelBuilder);
        ConfigureFacetValues(modelBuilder);
    }

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
