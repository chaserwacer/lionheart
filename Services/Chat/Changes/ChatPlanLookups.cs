using System.Globalization;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Availability;
using lionheart.Services.Training.Movement;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Chat.Changes;

/// <summary>What the chat reads before it proposes a change: the days ahead, and his movements' ids.</summary>
public interface IChatPlanLookups
{
    Task<IReadOnlyList<string>> GetPlannedDaysAsync(IdentityUser user, DayRange range);

    Task<IReadOnlyList<string>> FindMovementsAsync(IdentityUser user, string query);
}

/// <summary>
/// The ids a proposal names, in the words the model reads: each day of a range with what is planned
/// or marked on it, and the athlete's own movements by name.
/// </summary>
[ToolProvider]
public sealed class ChatPlanLookups(
    ModelContext context, INonTrainingDayService nonTrainingDays, MovementPatternMap map) : IChatPlanLookups
{
    /// <summary>Days per read: a month ahead is as far as a plan made in chat reaches.</summary>
    public const int MostDays = 42;

    /// <summary>Movements per search, so a one-word query cannot list the whole library.</summary>
    public const int MostMovements = 15;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly ModelContext _context = context;
    private readonly INonTrainingDayService _nonTrainingDays = nonTrainingDays;
    private readonly MovementPatternMap _map = map;

    [Tool(Name = "GetPlannedDays",
        Description = "Each day of a range as training reads it, one line a day: open, trained, non-training (with its note, whether it repeats weekly, and the nonTrainingDayId that frees it), or planned, with each planned session's trainingSessionId, what it prescribes, its note and whether Tempo or the athlete planned it." +
            " Read it before proposing a change to a planned session or freeing a day, and to see what a week already holds before planning into it. At most 42 days per call.")]
    public Task<IReadOnlyList<string>> GetPlannedDaysAsync(IdentityUser user, DayRange range) =>
        range.Ordered()
            .Ensure(ordered => ordered.Days <= MostDays, _ => new Problem.Invalid(nameof(DayRange.To), $"At most {MostDays} days are read at once."))
            .BindAsync(ordered => _nonTrainingDays.GetAvailabilityAsync(user, ordered, CancellationToken.None))
            .MapAsync(calendar => DaysAsync(user, calendar))
            .MatchAsync(lines => lines, problem => [problem.Description]);

    [Tool(Name = "FindMovements",
        Description = "The athlete's own movements whose names hold every word of the query (\"squat\", \"hack squat\", \"dip\"), each with its movementDataId and the family it trains, at most 15." +
            " Use it to find the id a planned lift or a priority lift needs; a movement he has never logged is not in his library.")]
    public async Task<IReadOnlyList<string>> FindMovementsAsync(IdentityUser user, string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return ["Give a word of the movement's name."];

        var userId = Guid.Parse(user.Id);
        var movements = await _context.MovementDatas
            .AsNoTracking()
            .Where(movement => movement.UserID == userId)
            .Include(movement => movement.MovementBase)
            .Include(movement => movement.Equipment)
            .Include(movement => movement.MovementModifier)
            .ToListAsync();

        var found = movements
            .Select(movement => (movement.MovementDataID,
                Name: MovementNames.Of(movement.MovementBase.Name, movement.Equipment.Name, movement.MovementModifier?.Name),
                Trains: Trains(_map.StandingOf(movement.MovementBase))))
            .Where(movement => words.All(word => movement.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(movement => movement.Name.Length)
            .ThenBy(movement => movement.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MostMovements)
            .Select(movement => $"{movement.MovementDataID}: {movement.Name} ({movement.Trains})")
            .ToList();

        return found.Count == 0 ? [$"None of his movements is named with \"{query.Trim()}\"."] : found;
    }

    private async Task<IReadOnlyList<string>> DaysAsync(IdentityUser user, AvailabilityCalendar calendar)
    {
        var userId = Guid.Parse(user.Id);
        var planned = await _context.Sessions.OfType<PlannedTrainingSession>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.PlannedFor >= calendar.Range.From && s.PlannedFor <= calendar.Range.To)
            .ToListAsync();
        var names = await _context.MovementNamesAsync(
            userId, MovementNameBook.MovementsOf(planned.SelectMany(s => s.Prescriptions)), CancellationToken.None);

        return [.. calendar.Days.Select(day => Line(day, [.. planned.Where(s => s.PlannedFor == day.Day).OrderBy(s => s.CreatedAt)], names))];
    }

    /// <summary>
    /// "2026-11-11 Wed: planned. Session 0193…, planned by Tempo: Easy run, 4-5 miles; Note: with friends".
    /// A day reads as its availability says; the sessions on it are listed whatever that is, since a
    /// date marked off can still hold a plan the athlete has not removed.
    /// </summary>
    private static string Line(DayAvailability day, IReadOnlyList<PlannedTrainingSession> sessions, MovementNameBook names)
    {
        var state = day switch
        {
            DayAvailability.Open => "open",
            DayAvailability.Planned => "planned",
            DayAvailability.Trained => "trained",
            DayAvailability.NonTraining off =>
                $"non-training{(off.Note.Length > 0 ? $" ({off.Note})" : string.Empty)}{(off.Repeats ? ", every week" : string.Empty)}; nonTrainingDayId {off.NonTrainingDayId}",
            _ => throw new System.Diagnostics.UnreachableException(),
        };
        var planned = sessions.Select(session =>
            $" Session {session.TrainingSessionId}, planned by {(session.Provenance == Provenance.AiAssisted ? "Tempo" : "him")}: " +
            string.Join("; ", ChangeSummaries.Plan(session.Prescriptions, names).DefaultIfEmpty("nothing prescribed")) +
            (session.Notes.Length > 0 ? $"; Note: {session.Notes}" : string.Empty) + ".");

        return $"{day.Day.ToString("yyyy-MM-dd ddd", Invariant)}: {state}.{string.Concat(planned)}";
    }

    private static string Trains(ClassificationStanding standing) => standing switch
    {
        ClassificationStanding.Classified classified => PatternWords.Of(classified.Family),
        ClassificationStanding.Unclassified => "unclassified",
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}
