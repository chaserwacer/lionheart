using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Focus;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.Score;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.Training.Focus;

public interface IPriorityReportService
{
    /// <summary>Each family's priority lift and the week's cardio, over the seven days ending on a day.</summary>
    Task<Result<PriorityReport, Problem>> GetReportAsync(IdentityUser user, DateOnly endingOn, CancellationToken ct);
}

/// <summary>Reads the log, the pins and the window's cardio, and hands them to <see cref="PriorityAnalysis"/>.</summary>
public sealed class PriorityReportService : IPriorityReportService
{
    private readonly ModelContext _context;
    private readonly ICardioReportService _cardio;
    private readonly MovementPatternMap _map;

    public PriorityReportService(ModelContext context, ICardioReportService cardio, MovementPatternMap map)
    {
        _context = context;
        _cardio = cardio;
        _map = map;
    }

    public Task<Result<PriorityReport, Problem>> GetReportAsync(IdentityUser user, DateOnly endingOn, CancellationToken ct)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        return _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => CardioWindows.ReadAsync(_context, _cardio, user, window, ct)
                .MapAsync(async cardio => new PriorityReport(
                    window,
                    PriorityAnalysis.Read(
                        await _context.ReadAsync(userId, endingOn, _map, ct),
                        await _context.PinsOfAsync(userId, ct),
                        endingOn),
                    cardio.For(window))));
    }
}
