using lionheart.Model.BodyMeasurements;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Model.User;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Data;

/// <summary>EF mapping for the athlete's dated body measurements.</summary>
public static class BodyMeasurementConfiguration
{
    /// <summary>
    /// The columns of the index that makes a scan's minute unique, as SQLite names them when it
    /// refuses a second scan at that minute — shared with the service that turns the refusal into
    /// a conflict, so the two cannot come to disagree.
    /// </summary>
    public const string ScanMinuteColumns =
        "BodyCompositionScans.UserId, BodyCompositionScans.Day, BodyCompositionScans.Time";

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BodyweightMeasurement>(measurement =>
        {
            measurement.ToTable("BodyweightMeasurements");

            // The day is the identity: one measurement per athlete per day is the rule, and the
            // key is what makes a second one unrepresentable rather than merely unlikely.
            measurement.HasKey(m => new { m.UserId, m.Day });
            measurement.HasOne<LionheartUser>().WithMany().HasForeignKey(m => m.UserId);

            measurement.ComplexProperty(m => m.Bodyweight, bodyweight =>
            {
                bodyweight.Property(b => b.Value).HasColumnName("Weight");
                bodyweight.Property(b => b.Unit).HasColumnName("Unit");
            });
        });

        modelBuilder.Entity<BodyCompositionScan>(scan =>
        {
            scan.ToTable("BodyCompositionScans");
            scan.HasKey(s => s.ScanId);
            scan.HasOne<LionheartUser>().WithMany().HasForeignKey(s => s.UserId);

            // The sheet's own clock names a scan: two scans at the same minute are one scan sent
            // twice, and the index makes the second unrepresentable.
            scan.HasIndex(s => new { s.UserId, s.Day, s.Time }).IsUnique();

            // A sheet is one printed document whose panels vary by analyser, read and written
            // whole; nothing queries inside it.
            scan.Property(s => s.Sheet).HasJsonConversion();
        });
    }
}
