using System.Globalization;
using System.Text;
using lionheart.Functional;
using lionheart.Model.Training.Sets;
using lionheart.Services.Import;

namespace lionheart.Services.Strava;

/// <summary>Reads the archive's <c>activities.csv</c>: one summary row per activity.</summary>
/// <remarks>
/// All or nothing, as the Strong export is read: one unreadable row refuses the archive and every
/// such row is named, since an activity left out silently would be one the athlete believes is in
/// the app.
/// <para>
/// Columns are found by their header. Strava writes some twice — first as its activity page shows
/// them, then in detail and in SI units — so the elapsed time is the first "Elapsed Time" (whole
/// seconds, present on every row, where the detailed one is blank on old activities) and the
/// distance and peak heart rate are the last "Distance" (metres, where the first is kilometres) and
/// the last "Max Heart Rate". Strava leaves a measure it has no value for blank, or writes it as
/// zero; either reads as none.
/// </para>
/// </remarks>
internal static class StravaActivitiesReader
{
    public const string FileName = "activities.csv";

    private const string IdColumn = "Activity ID";
    private const string DateColumn = "Activity Date";
    private const string NameColumn = "Activity Name";
    private const string TypeColumn = "Activity Type";
    private const string DescriptionColumn = "Activity Description";
    private const string ElapsedColumn = "Elapsed Time";
    private const string MovingColumn = "Moving Time";
    private const string DistanceColumn = "Distance";
    private const string ClimbColumn = "Elevation Gain";
    private const string AverageHeartRateColumn = "Average Heart Rate";
    private const string MaxHeartRateColumn = "Max Heart Rate";
    private const string ExertionColumn = "Perceived Exertion";
    private const string PrivateNoteColumn = "Activity Private Note";
    private const string FileColumn = "Filename";

    private static readonly string[] Columns =
    [
        IdColumn, DateColumn, NameColumn, TypeColumn, DescriptionColumn, ElapsedColumn, MovingColumn, DistanceColumn,
        ClimbColumn, AverageHeartRateColumn, MaxHeartRateColumn, ExertionColumn, PrivateNoteColumn, FileColumn,
    ];

    /// <summary>The columns Strava writes twice whose second copy is the one read.</summary>
    private static readonly string[] SecondCopyColumns = [DistanceColumn, MaxHeartRateColumn];

    /// <summary>How Strava writes an activity's start: in English, in UTC, without an offset.</summary>
    private const string DateFormat = "MMM d, yyyy, h:mm:ss tt";

    public static Result<IReadOnlyList<StravaActivity>, Problem> Read(string archive) =>
        Existing(Path.Combine(archive, FileName))
            .Bind(Lines)
            .Bind(lines => LineDefects.Accepted(lines.Partition(line => line.Bind(Activity))))
            .Bind(Distinct);

    // ---- The file ----

    private static Result<string, Problem> Existing(string path) =>
        File.Exists(path)
            ? Result<string, Problem>.Success(path)
            : Result<string, Problem>.Failure(new Problem.Invalid("archive", $"No {FileName} at '{path}': this is not a Strava archive folder."));

    private static Result<IReadOnlyList<Result<CsvLine, LineDefect>>, Problem> Lines(string path)
    {
        var records = CsvRecords.Read(File.ReadAllText(path, Encoding.UTF8)).ToList();

        return Header(records).Map(IReadOnlyList<Result<CsvLine, LineDefect>> (header) =>
            [.. records.Skip(1).Select(record => record.Bind(header.Line))]);
    }

    private static Result<CsvHeader, Problem> Header(IReadOnlyList<Result<CsvRecord, LineDefect>> records) =>
        (records.FirstOrDefault() ?? Result<CsvRecord, LineDefect>.Failure(new LineDefect(1, "is empty")))
        .MapError(Problem (defect) => new Problem.Invalid(
            "archive", $"The header of {FileName} cannot be read: line {defect.Line} {defect.Reason}."))
        .Ensure(
            record => !Columns.Except(record.Fields).Any(),
            record => new Problem.Invalid("archive",
                $"{FileName} is not Strava's: it has no {string.Join(", ", Columns.Except(record.Fields).Select(c => $"'{c}'"))} column."))
        .Map(record => new CsvHeader(
            record.Fields.Count,
            Columns.ToDictionary(
                column => column,
                column => SecondCopyColumns.Contains(column)
                    ? record.Fields.ToList().LastIndexOf(column)
                    : record.Fields.ToList().IndexOf(column))));

    // ---- One row ----

    private static Result<StravaActivity, LineDefect> Activity(CsvLine line) =>
        Id(line).Bind(id =>
        Start(line).Bind(start =>
        Measures(line).Map(measures => new StravaActivity(
            id,
            start,
            line[NameColumn].Trim(),
            line[TypeColumn].Trim(),
            measures.Elapsed,
            measures.Moving,
            measures.DistanceMeters,
            measures.ClimbMeters,
            line[DescriptionColumn].Trim(),
            line[PrivateNoteColumn].Trim(),
            measures.Exertion,
            measures.AverageHeartRate,
            measures.MaxHeartRate,
            line[FileColumn].Trim()))));

    private static Result<long, LineDefect> Id(CsvLine line) =>
        long.TryParse(line[IdColumn], NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? Result<long, LineDefect>.Success(id)
            : LineDefects.Defect<long>(line.Number, $"has an activity id '{line[IdColumn]}' that is not one");

    /// <remarks>
    /// Newer platforms write a narrow no-break space before "PM"; it reads as a space, so an archive
    /// exported from either reads the same.
    /// </remarks>
    private static Result<DateTimeOffset, LineDefect> Start(CsvLine line)
    {
        var text = line[DateColumn].Replace(' ', ' ').Replace(' ', ' ').Trim();
        return DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)
            ? Result<DateTimeOffset, LineDefect>.Success(new DateTimeOffset(utc, TimeSpan.Zero))
            : LineDefects.Defect<DateTimeOffset>(line.Number, $"has an activity date '{line[DateColumn]}' that is not one");
    }

    private static Result<ActivityMeasures, LineDefect> Measures(CsvLine line) =>
        Measure(line, ElapsedColumn).Bind(elapsed =>
        Measure(line, MovingColumn).Bind(moving =>
        Measure(line, DistanceColumn).Bind(distance =>
        Measure(line, ClimbColumn).Bind(climb =>
        Exertion(line).Bind(exertion =>
        Measure(line, AverageHeartRateColumn).Bind(average =>
        Measure(line, MaxHeartRateColumn).Map(peak => new ActivityMeasures(
            TimeSpan.FromSeconds(elapsed ?? 0),
            TimeSpan.FromSeconds(moving ?? 0),
            distance ?? 0,
            climb,
            exertion,
            average is { } mean ? (int)Math.Round(mean) : null,
            peak is { } max ? (int)Math.Round(max) : null))))))));

    /// <summary>A measure, or none where Strava left it blank or wrote zero; a negative or non-number is a defect.</summary>
    private static Result<double?, LineDefect> Measure(CsvLine line, string column)
    {
        var text = line[column].Trim();
        if (text.Length is 0) return Result<double?, LineDefect>.Success(null);

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
               && double.IsFinite(value) && value >= 0
            ? Result<double?, LineDefect>.Success(value > 0 ? value : null)
            : LineDefects.Defect<double?>(line.Number, $"has a {column.ToLowerInvariant()} '{text}' that is not a measure");
    }

    private static Result<Rpe, LineDefect> Exertion(CsvLine line) =>
        Measure(line, ExertionColumn).Bind(rating => rating switch
        {
            null => Result<Rpe, LineDefect>.Success(Rpe.Unrated),
            <= 10 => Result<Rpe, LineDefect>.Success(Rpe.Of(rating.Value)),
            _ => LineDefects.Defect<Rpe>(line.Number, $"has a perceived exertion of {rating} where Strava's scale ends at 10"),
        });

    // ---- The archive as a whole ----

    /// <summary>Refuses an archive listing one activity twice, since the id is what the import is keyed on.</summary>
    private static Result<IReadOnlyList<StravaActivity>, Problem> Distinct(IReadOnlyList<StravaActivity> activities) =>
        activities.GroupBy(activity => activity.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList() switch
        {
            [] => Result<IReadOnlyList<StravaActivity>, Problem>.Success(activities),
            var repeated => Result<IReadOnlyList<StravaActivity>, Problem>.Failure(new Problem.Invalid(
                "archive", $"{FileName} lists activit{(repeated.Count == 1 ? "y" : "ies")} {string.Join(", ", repeated)} more than once.")),
        };

    // ---- Shapes local to reading ----

    private sealed record ActivityMeasures(
        TimeSpan Elapsed,
        TimeSpan Moving,
        double DistanceMeters,
        double? ClimbMeters,
        Rpe Exertion,
        int? AverageHeartRate,
        int? MaxHeartRate);
}
