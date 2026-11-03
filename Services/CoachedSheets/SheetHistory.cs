using System.Globalization;
using System.Text.RegularExpressions;
using lionheart.Functional;

namespace lionheart.Services.CoachedSheets;

/// <summary>One coached-programme session: the rows performed in it and the rows not counted.</summary>
/// <remarks>
/// <see cref="Recorded"/> is the performed rows as the log holds them, which is how the notes
/// describe them; <see cref="Sets"/> is what was lifted, the same rows with each cluster split into
/// its singles. Every row shares the session's date, day and source; <see cref="SheetHistory"/>
/// refuses a log where they do not.
/// </remarks>
public sealed record SheetWorkout(
    string Session,
    DateOnly Date,
    string Day,
    SheetSource Source,
    IReadOnlyList<SheetSet> Recorded,
    IReadOnlyList<SheetSet> Sets,
    IReadOnlyList<NotPerformed> NotCounted);

/// <summary>What the programme sheets hold once every row that was never lifted is set aside.</summary>
/// <param name="Workouts">The sessions with at least one performed set.</param>
/// <param name="SessionsWithNothingPerformed">Sessions in the log every row of which was set aside.</param>
/// <param name="ClustersSplit">Rows recorded as one set that were a cluster of singles, stored as the singles.</param>
public sealed partial record SheetHistory(
    SheetLog Log,
    IReadOnlyList<SheetWorkout> Workouts,
    IReadOnlyList<NotPerformed> NotPerformed,
    int SessionsWithNothingPerformed,
    int ClustersSplit)
{
    public IEnumerable<SheetSet> Performed => Workouts.SelectMany(workout => workout.Sets);

    /// <summary>
    /// The sets of a log the athlete performed, grouped into sessions.
    /// </summary>
    /// <remarks>
    /// Refuses, naming each, a not-performed row that no longer matches the number of sets it was
    /// written against: the set log is regenerated from the spreadsheets, and a decision that has
    /// drifted from the rows it described would otherwise set aside the wrong lifts, or none.
    /// </remarks>
    public static Result<SheetHistory, Problem> Of(SheetLog log, SheetDecisions decisions) =>
        Matched(log, decisions)
            .Bind(matched => Grouped(
                log,
                [.. log.Sets.Where(set => !matched.Contains(set)).Select(set => ReadThrough(set, decisions))],
                decisions.NotPerformed))
            .Bind(Unclustered)
            .Map(workouts => new SheetHistory(
                log,
                workouts,
                decisions.NotPerformed,
                log.Sets.Select(set => set.Session).Distinct().Count() - workouts.Count,
                workouts.Sum(workout => workout.Recorded.Count(set => ClusterNote().IsMatch(set.Note)))));

    // ---- Rows never lifted ----

    private static Result<HashSet<SheetSet>, Problem> Matched(SheetLog log, SheetDecisions decisions)
    {
        var matches = decisions.NotPerformed
            .Select(row => (Row: row, Sets: log.Sets.Where(row.Covers).ToList()))
            .ToList();

        var drifted = matches
            .Where(match => match.Sets.Count != match.Row.Sets)
            .Select(match => new InvalidField(
                $"{match.Row.Source} {match.Row.Date:yyyy-MM-dd} {match.Row.Exercise}",
                $"{SheetDecisions.FileName} sets aside {match.Row.Sets} set(s) of {match.Row.Exercise} "
                + $"{Pounds(match.Row.Weight)} × {match.Row.Reps} on {match.Row.Date:yyyy-MM-dd} ({match.Row.Source}), "
                + $"and the log holds {match.Sets.Count}."))
            .ToList();

        return drifted.Count == 0
            ? Result<HashSet<SheetSet>, Problem>.Success(matches.SelectMany(match => match.Sets).ToHashSet())
            : Result<HashSet<SheetSet>, Problem>.Failure(new Problem.Invalid(drifted));
    }

    // ---- What each sheet means by its exercise ----

    private static SheetSet ReadThrough(SheetSet set, SheetDecisions decisions) =>
        decisions.Programs.TryGetValue(set.Source.Sheet, out var program)
            ? set with { Exercise = program.Reading(set.Exercise) }
            : set;

    // ---- Sessions ----

    private static Result<IReadOnlyList<SheetWorkout>, Problem> Grouped(
        SheetLog log, IReadOnlyList<SheetSet> performed, IReadOnlyList<NotPerformed> notPerformed) =>
        Accepted(performed
            .GroupBy(set => set.Session)
            .OrderBy(sets => sets.First().Date)
            .Partition(sets => Workout(sets, NotCountedIn(sets.Key, log, notPerformed))));

    private static IReadOnlyList<NotPerformed> NotCountedIn(string session, SheetLog log, IReadOnlyList<NotPerformed> notPerformed) =>
        [.. notPerformed.Where(row => log.Sets.Any(set => set.Session == session && row.Covers(set)))];

    /// <remarks>
    /// The session id is training-os's, built from the date, the sheet and the day, so rows sharing
    /// it disagreeing on any of the three means the id is not identifying what it should.
    /// </remarks>
    private static Result<SheetWorkout, InvalidField> Workout(IGrouping<string, SheetSet> sets, IReadOnlyList<NotPerformed> notCounted)
    {
        var first = sets.First();

        return sets.FirstOrDefault(set => (set.Date, set.Day, set.Source) != (first.Date, first.Day, first.Source)) is { } disagreeing
            ? Result<SheetWorkout, InvalidField>.Failure(new InvalidField(
                $"line {disagreeing.Line}",
                $"Line {disagreeing.Line} puts session '{sets.Key}' on {disagreeing.Date:yyyy-MM-dd} {disagreeing.Day} ({disagreeing.Source}), "
                + $"where line {first.Line} has {first.Date:yyyy-MM-dd} {first.Day} ({first.Source})."))
            : Result<SheetWorkout, InvalidField>.Success(new SheetWorkout(
                sets.Key, first.Date, first.Day, first.Source, [.. sets], [.. sets], notCounted));
    }

    // ---- Clusters ----

    /// <summary>
    /// Each workout's lifts, with a cluster set the log records as one set of its total reps
    /// become the singles it was.
    /// </summary>
    /// <remarks>
    /// "CLUSTER SET (1+1)" is two singles with a short rest between, and the sheet records it as
    /// one set of two. Stored that way it would hold a two-rep max nobody lifted without racking
    /// the bar, so each part becomes a set of its own at the same weight. A cluster whose parts do
    /// not add up to the row's reps is refused, since which number is wrong would be a guess.
    /// </remarks>
    private static Result<IReadOnlyList<SheetWorkout>, Problem> Unclustered(IReadOnlyList<SheetWorkout> workouts) =>
        Accepted(workouts.Partition(workout => workout.Recorded
            .Traverse(Parts)
            .Map(parts => workout with { Sets = [.. parts.SelectMany(sets => sets)] })));

    private static Result<IReadOnlyList<SheetSet>, InvalidField> Parts(SheetSet set)
    {
        if (ClusterNote().Match(set.Note) is not { Success: true } cluster)
            return Result<IReadOnlyList<SheetSet>, InvalidField>.Success([set]);

        var parts = cluster.Groups["parts"].Value.Split('+')
            .Select(part => int.Parse(part, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToList();

        return parts.Sum() == set.Reps
            ? Result<IReadOnlyList<SheetSet>, InvalidField>.Success([.. parts.Select(reps => set with { Reps = reps })])
            : Result<IReadOnlyList<SheetSet>, InvalidField>.Failure(new InvalidField(
                $"line {set.Line}",
                $"Line {set.Line} is a cluster of {cluster.Groups["parts"].Value} recorded as {set.Reps} reps; the parts do not add up."));
    }

    [GeneratedRegex(@"CLUSTER SET \((?<parts>[0-9]+(?:\+[0-9]+)+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ClusterNote();

    private static Result<IReadOnlyList<T>, Problem> Accepted<T>(Partitioned<T, InvalidField> read) =>
        read.Refused.Count == 0
            ? Result<IReadOnlyList<T>, Problem>.Success(read.Delivered)
            : Result<IReadOnlyList<T>, Problem>.Failure(new Problem.Invalid(read.Refused));

    internal static string Pounds(double weight) =>
        $"{weight.ToString("0.##", CultureInfo.InvariantCulture)} lb";
}
