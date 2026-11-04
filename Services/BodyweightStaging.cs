using lionheart.Data;
using lionheart.Model.BodyMeasurements;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

/// <summary>Staging a day's bodyweight, for every write path that measures one.</summary>
/// <remarks>
/// Two paths measure a bodyweight — logging one, and recording a body-composition scan, whose
/// analyser weighs the athlete — and both obey the rule that a day has one measurement. Staged
/// rather than saved, so a scan and the bodyweight it measured land in one save or not at all.
/// </remarks>
internal static class BodyweightStaging
{
    extension(ModelContext context)
    {
        /// <summary>Restates the day's measurement when it has one, and records one when it does not.</summary>
        public async Task<BodyweightMeasurement> StageBodyweightAsync(
            Guid userId, DateOnly day, Bodyweight bodyweight, CancellationToken ct)
        {
            var measurement = await context.BodyweightMeasurements
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Day == day, ct);

            if (measurement is null)
            {
                measurement = BodyweightMeasurement.Measured(userId, day, bodyweight);
                context.BodyweightMeasurements.Add(measurement);
            }
            else
            {
                measurement.Restate(bodyweight);
            }

            return measurement;
        }
    }
}
