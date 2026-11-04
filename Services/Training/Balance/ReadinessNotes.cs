using lionheart.Model.Training.Balance;

namespace lionheart.Services.Training.Balance;

/// <summary>Reads a day's readiness against the athlete's own usual range.</summary>
public static class ReadinessNotes
{
    /// <summary>The days of history the usual range is drawn from.</summary>
    public const int UsualRangeDays = 90;

    /// <summary>The fewest scored days a usual range is read from; fewer and its tails are noise.</summary>
    public const int FewestDays = 30;

    /// <param name="scores">Oura's readiness scores by day, any order; days with none are left out.</param>
    public static ReadinessNote Of(IReadOnlyList<(DateOnly Day, int Score)> scores, DateOnly day)
    {
        var today = scores.Where(score => score.Day == day).Select(score => (int?)score.Score).FirstOrDefault();
        var before = scores
            .Where(score => score.Day < day && score.Day >= day.AddDays(-UsualRangeDays))
            .Select(score => score.Score)
            .Order()
            .ToList();

        if (today is not { } score || before.Count < FewestDays) return new ReadinessNote.NotRead();

        var low = Percentile(before, 0.1);
        var high = Percentile(before, 0.9);
        return score < low ? new ReadinessNote.Low(score, low, high) : new ReadinessNote.Usual(score);
    }

    /// <summary>Nearest-rank percentile of a sorted list.</summary>
    private static int Percentile(IReadOnlyList<int> sorted, double fraction) =>
        sorted[(int)Math.Floor(fraction * (sorted.Count - 1))];
}
