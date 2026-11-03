using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Balance;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.Training.Balance;

public interface ITrainingVolumeService
{
    /// <summary>Hard sets by family, pattern and muscle over the seven days ending on a day, and what is up next.</summary>
    Task<Result<TrainingVolume, Problem>> GetVolumeAsync(IdentityUser user, DateOnly endingOn, CancellationToken ct);
}

/// <summary>Reads the log and the day's readiness and hands them to <see cref="TrainingVolumeAnalysis"/>.</summary>
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
            .MapAsync(async userId => TrainingVolumeAnalysis.Build(
                await _context.ReadAsync(userId, endingOn, _map, ct),
                endingOn,
                await _context.ReadinessOnAsync(userId, endingOn, ct)));
}
