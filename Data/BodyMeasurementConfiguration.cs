using lionheart.Model.BodyMeasurements;
using lionheart.Model.User;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Data;

/// <summary>EF mapping for the athlete's dated body measurements.</summary>
public static class BodyMeasurementConfiguration
{
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
    }
}
