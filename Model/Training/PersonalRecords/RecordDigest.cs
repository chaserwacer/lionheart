
namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// One subject's headline records as a single line, for the chat model.
/// </summary>
/// <remarks>
/// The full standing set runs to well over a thousand records, and handed to the model whole it
/// cost about $2 a call and filled a session's context in one read. The headline answers most
/// questions; the movement's id is there so the model can ask for every rep max and its date.
/// Figures are stated in the units the athlete reads (<see cref="Imperial"/>), since the model
/// repeats them to him; a record logged in kilograms still ranks within its own unit.
/// </remarks>
public sealed record RecordDigest(Guid? MovementDataId, string Subject, DateTime LastSetAt, string Records)
{
    /// <summary>The rep maxes worth a line: strength, then the ranges hypertrophy work lives in.</summary>
    private static readonly int[] HeadlineRepMaxes = [3, 5, 8, 10];

    public static RecordDigest Of(Guid? movementDataId, string subject, IReadOnlyList<PersonalRecord> standing) => new(
        movementDataId,
        subject,
        standing.Max(record => record.AchievedAt),
        string.Join("; ", standing
            .Where(IsHeadline)
            .OrderBy(record => Rank(record.Achievement))
            .ThenBy(record => record.Achievement.HeldIn)
            .Select(record => $"{Line(record.Achievement)} ({record.AchievedAt:yyyy-MM-dd})")));

    private static bool IsHeadline(PersonalRecord record) =>
        record.Achievement is not Achievement.RepMax repMax || HeadlineRepMaxes.Contains(repMax.Reps);

    private static int Rank(Achievement achievement) => achievement switch
    {
        Achievement.HeaviestSet => 0,
        Achievement.RepMax repMax => repMax.Reps,
        Achievement.BestSetVolume => 100,
        Achievement.BestSessionVolume => 101,
        Achievement.LongestDistance => 200,
        Achievement.FastestTime fastest => 201 + (int)Math.Min(fastest.Effort.DistanceInMeters / 1000, 100),
        Achievement.BestPace => 400,
        _ => 500,
    };

    private static string Line(Achievement achievement) => achievement switch
    {
        Achievement.HeaviestSet heaviest => $"heaviest {Lift(heaviest.Lift)}",
        Achievement.RepMax repMax => $"{repMax.Reps}RM {Lift(repMax.Lift)}",
        Achievement.BestSetVolume set => $"best set volume {Lift(set.Lift)} = {Imperial.Weight(set.Lift.Volume, set.Lift.Unit)}",
        Achievement.BestSessionVolume session => $"best session volume {Imperial.Weight(session.Total.Volume, session.Total.Unit)}",
        Achievement.LongestDistance longest => $"longest {Effort(longest.Effort)}",
        Achievement.FastestTime fastest => $"fastest {Effort(fastest.Effort)}",
        Achievement.BestPace pace => $"best pace {Effort(pace.Effort)}",
        _ => achievement.Kind.Name,
    };

    private static string Lift(LiftPerformance lift) => $"{Imperial.Weight(lift.Weight, lift.Unit)} × {lift.Reps}";

    private static string Effort(DistanceEffort effort) =>
        $"{Imperial.Distance(effort.Distance, effort.Unit)} in {effort.Duration:hh\\:mm\\:ss}";
}
