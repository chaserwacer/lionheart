using lionheart.Model.Analysis;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Training.Balance;

namespace lionheart.Services.Training.Load;

/// <summary>
/// The log's hard sets counted by day, all of them and by family, so the many seven-day blocks a
/// week's expectations read — its own, the four its targets grow from, the weeks a deload is
/// judged on — are each a sum of seven days rather than a pass over the whole log.
/// </summary>
public sealed class HardSetCounts
{
    private readonly IReadOnlyDictionary<DateOnly, int> _all;
    private readonly IReadOnlyDictionary<(DateOnly Day, PatternFamily Family), int> _families;

    private HardSetCounts(IReadOnlyDictionary<DateOnly, int> all, IReadOnlyDictionary<(DateOnly, PatternFamily), int> families)
    {
        _all = all;
        _families = families;
    }

    public static HardSetCounts Of(TrainingLog log) => new(
        log.Sessions.GroupBy(session => session.Day).ToDictionary(day => day.Key, day => day.Sum(session => session.HardSets.Count)),
        TrainingVolumeAnalysis.Placed(log, DateOnly.MinValue, DateOnly.MaxValue)
            .GroupBy(placed => (placed.Day, placed.Trains.Family))
            .ToDictionary(group => group.Key, group => group.Count()));

    /// <summary>Every hard set in the range, classified or not: what fatigue counts.</summary>
    public int All(DayRange range) => Days(range).Sum(day => _all.GetValueOrDefault(day));

    /// <summary>The range's hard sets of movements the family trains: what its target counts.</summary>
    public int Of(PatternFamily family, DayRange range) => Days(range).Sum(day => _families.GetValueOrDefault((day, family)));

    private static IEnumerable<DateOnly> Days(DayRange range) => Enumerable.Range(0, range.Days).Select(range.From.AddDays);
}
