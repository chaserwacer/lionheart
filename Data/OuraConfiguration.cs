using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.User;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Data;

/// <summary>
/// EF mapping for the Oura collections (docs/oura-integration.md): one table per collection, each
/// keyed by Oura's own document id, with the measurement itself in a JSON column.
/// </summary>
/// <remarks>
/// Two mapping shapes rather than ten configurations, because the collections differ only in what
/// they measured. A <c>Day</c> collection is one document per calendar day; an <c>Event</c>
/// collection is zero to many per day, each occupying clock time, so it also carries a
/// <see cref="Model.Analysis.Period"/> and the derived StartTime column that windows query —
/// stamped by <see cref="ModelContext"/> on every save, the same way the training domain's is.
/// <para>
/// Oura's document id is the primary key, which is what makes a re-sync or a re-run of the
/// importer idempotent with no sync ledger: the row either exists or it does not. It also means a
/// day is deliberately <em>not</em> unique, since Oura has issued two SpO2 documents for one day.
/// The measurement lives in one JSON column rather than forty scalar ones, so a field Oura adds
/// costs a member on a record and no migration. Both decisions, and why nothing filters on a
/// reading in SQL, are argued in docs/oura-integration.md.
/// </para>
/// </remarks>
public static class OuraConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Day<OuraDailyActivity, DailyActivityReading>(modelBuilder, "OuraDailyActivity");
        Day<OuraDailyReadiness, DailyReadinessReading>(modelBuilder, "OuraDailyReadiness");
        Day<OuraDailySleep, DailySleepReading>(modelBuilder, "OuraDailySleep");
        Day<OuraDailySpo2, DailySpo2Reading>(modelBuilder, "OuraDailySpo2");
        Day<OuraDailyStress, DailyStressReading>(modelBuilder, "OuraDailyStress");
        Day<OuraDailyResilience, DailyResilienceReading>(modelBuilder, "OuraDailyResilience");

        Event<OuraSleepPeriod, SleepPeriodReading>(modelBuilder, "OuraSleepPeriods");
        Event<OuraWorkout, WorkoutReading>(modelBuilder, "OuraWorkouts");
        Event<OuraSession, SessionReading>(modelBuilder, "OuraSessions");
        Event<OuraTag, TagReading>(modelBuilder, "OuraTags");
    }

    private static void Day<TDocument, TReading>(ModelBuilder modelBuilder, string table)
        where TDocument : OuraDocument<TReading>
        where TReading : class
    {
        modelBuilder.Entity<TDocument>(document =>
        {
            document.ToTable(table);
            document.HasKey(d => d.OuraId);

            // Device data follows the athlete it was recorded from: nothing here is meaningful
            // once the user it belongs to is gone, and the same reasoning governs AnalyzableEntry.
            document.HasOne<LionheartUser>()
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            document.Property(d => d.Reading).HasJsonConversion();
            document.HasIndex(d => new { d.UserId, d.Day });
        });
    }

    private static void Event<TDocument, TReading>(ModelBuilder modelBuilder, string table)
        where TDocument : OuraEvent<TReading>
        where TReading : class
    {
        Day<TDocument, TReading>(modelBuilder, table);

        modelBuilder.Entity<TDocument>(document =>
        {
            // Period is read off the reading rather than stored twice; StartTime is the indexed
            // duplicate of its instant, recomputed on every save so it cannot drift.
            document.Ignore(d => d.Period);
            document.Property<DateTime>("StartTime");
            document.HasIndex("UserId", "StartTime");
        });
    }
}
