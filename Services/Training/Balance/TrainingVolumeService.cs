using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Balance;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.Availability;
using lionheart.Services.Training.Expectations;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.Training.Balance;

public interface ITrainingVolumeService
{
    /// <summary>Hard sets by family, pattern and muscle over the seven days ending on a day, and what is up next.</summary>
    Task<Result<TrainingVolume, Problem>> GetVolumeAsync(IdentityUser user, DateOnly endingOn, CancellationToken ct);
}

/// <summary>
/// Reads the log, the day's readiness and the window's availability, and hands them to
/// <see cref="TrainingVolumeAnalysis"/> with the week's family expectations, so the families are read
/// against the targets the score reads.
/// </summary>
public sealed class TrainingVolumeService : ITrainingVolumeService
{
    private readonly ModelContext _context;
    private readonly MovementPatternMap _map;

    public TrainingVolumeService(ModelContext context, MovementPatternMap map)
    {
        _context = context;
        _map = map;
    }

    public Task<Result<TrainingVolume, Problem>> GetVolumeAsync(IdentityUser user, DateOnly endingOn, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .MapAsync(async userId =>
            {
                var window = VolumeTargets.WindowEndingOn(endingOn);
                var log = await _context.ReadAsync(userId, endingOn, _map, ct);
                return TrainingVolumeAnalysis.Build(
                    log,
                    endingOn,
                    await _context.ReadinessOnAsync(userId, endingOn, ct),
                    WeekExpectationsAnalysis.Families(log, await _context.AvailabilityAsync(userId, window, ct), window));
            });
}
