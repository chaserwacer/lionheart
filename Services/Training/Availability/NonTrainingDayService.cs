using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Availability;

public interface INonTrainingDayService
{
    /// <summary>Every non-training day the athlete has marked, the latest-starting first.</summary>
    Task<Result<IReadOnlyList<NonTrainingDayView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct);

    /// <summary>Marks a day, or a weekday every week, non-training.</summary>
    Task<Result<NonTrainingDayView, Problem>> MarkAsync(IdentityUser user, MarkNonTrainingDayRequest request, CancellationToken ct);

    /// <summary>Removes a statement whole: every day a weekly one covered is free again.</summary>
    Task<Result<Unit, Problem>> RemoveAsync(IdentityUser user, Guid nonTrainingDayId, CancellationToken ct);

    /// <summary>
    /// Each day of a range — open, planned or non-training — or a refusal for a range that runs
    /// backwards or holds more than <see cref="NonTrainingDayService.MostDays"/> days.
    /// </summary>
    Task<Result<AvailabilityCalendar, Problem>> GetAvailabilityAsync(IdentityUser user, DayRange range, CancellationToken ct);
}

/// <summary>
/// The athlete's non-training days, for the app and for any other caller that marks them on his
/// behalf. Guard → Stage → Commit; another athlete's statement is a 404.
/// </summary>
public sealed class NonTrainingDayService(ModelContext context, TimeProvider clock) : INonTrainingDayService
{
    /// <summary>The most days one availability read covers: a year and a week.</summary>
    public const int MostDays = 371;

    private readonly ModelContext _context = context;
    private readonly TimeProvider _clock = clock;

    /// <remarks>Read on the identity's own id, so an account with no profile yet reads an empty list.</remarks>
    public async Task<Result<IReadOnlyList<NonTrainingDayView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var marked = await _context.NonTrainingDays.AsNoTracking().Where(day => day.UserId == userId).ToListAsync(ct);

        return Result<IReadOnlyList<NonTrainingDayView>, Problem>.Success(
            [.. marked
                .OrderByDescending(day => day.Schedule.First)
                .ThenByDescending(day => day.MarkedAt)
                .Select(NonTrainingDayView.Of)]);
    }

    public Task<Result<NonTrainingDayView, Problem>> MarkAsync(
        IdentityUser user, MarkNonTrainingDayRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => request.ToSchedule()
                .Bind(schedule => request.ToNote().Map(note => NewDay(userId, schedule, note)))
                .BindAsync(day => NotAlreadyMarkedAsync(day, ct)))
            .BindAsync(day => AddAsync(day, ct));

    public Task<Result<Unit, Problem>> RemoveAsync(IdentityUser user, Guid nonTrainingDayId, CancellationToken ct) =>
        OwnedAsync(user, nonTrainingDayId, ct).BindAsync(day => DeleteAsync(day, ct));

    public Task<Result<AvailabilityCalendar, Problem>> GetAvailabilityAsync(IdentityUser user, DayRange range, CancellationToken ct) =>
        range.Ordered()
            .Ensure(
                ordered => ordered.Days <= MostDays,
                _ => new Problem.Invalid(nameof(DayRange.To), $"At most {MostDays} days are read at once."))
            .MapAsync(ordered => _context.AvailabilityAsync(Guid.Parse(user.Id), ordered, ct));

    // ---- Guard ----

    private async Task<Result<NonTrainingDay, Problem>> OwnedAsync(IdentityUser user, Guid nonTrainingDayId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var day = await _context.NonTrainingDays
            .FirstOrDefaultAsync(d => d.NonTrainingDayId == nonTrainingDayId && d.UserId == userId, ct);

        return day is null
            ? Result<NonTrainingDay, Problem>.Failure(new Problem.NotFound("Non-training day not found."))
            : Result<NonTrainingDay, Problem>.Success(day);
    }

    /// <summary>
    /// The same schedule marked twice says nothing new, and two statements would make removing it
    /// a two-step chore; a different schedule over some of the same days is a different statement.
    /// </summary>
    private async Task<Result<NonTrainingDay, Problem>> NotAlreadyMarkedAsync(NonTrainingDay day, CancellationToken ct)
    {
        var marked = await _context.NonTrainingDays.AsNoTracking().Where(other => other.UserId == day.UserId).ToListAsync(ct);
        return marked.Any(other => other.Schedule == day.Schedule)
            ? Result<NonTrainingDay, Problem>.Failure(new Problem.Conflict("That day is already marked non-training."))
            : Result<NonTrainingDay, Problem>.Success(day);
    }

    // ---- Stage ----

    private NonTrainingDay NewDay(Guid userId, NonTrainingSchedule schedule, string note)
    {
        var now = _clock.GetUtcNow();
        return NonTrainingDay.Mark(Guid.CreateVersion7(now), userId, schedule, note, now.UtcDateTime);
    }

    // ---- Commit ----

    private async Task<Result<NonTrainingDayView, Problem>> AddAsync(NonTrainingDay day, CancellationToken ct)
    {
        _context.NonTrainingDays.Add(day);
        await _context.SaveChangesAsync(ct);
        return Result<NonTrainingDayView, Problem>.Success(NonTrainingDayView.Of(day));
    }

    private async Task<Result<Unit, Problem>> DeleteAsync(NonTrainingDay day, CancellationToken ct)
    {
        _context.NonTrainingDays.Remove(day);
        await _context.SaveChangesAsync(ct);
        return Result<Unit, Problem>.Success(Unit.Value);
    }
}
