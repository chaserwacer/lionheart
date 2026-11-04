using System.Text.Json;
using lionheart.Functional;

namespace lionheart.Services.CoachedSheets;

/// <summary>A programme sheet as the coached programme it becomes.</summary>
public sealed record SheetProgram(
    string Sheet, string Title, string Summary, IReadOnlyList<string> Tags, IReadOnlyDictionary<string, string> ReadAs)
{
    /// <summary>
    /// The slug this programme's sheet means by <paramref name="exercise"/>. A sheet can say less than
    /// its era did: one coach's sheets write "Deadlift" for a pull done sumo where an earlier block's
    /// was conventional, and training-os gives both one slug.
    /// </summary>
    public string Reading(string exercise) => ReadAs.GetValueOrDefault(exercise, exercise);
}

/// <summary>Why a row the set log holds is not a set the athlete performed.</summary>
public enum NotPerformedKind
{
    /// <summary>The spreadsheet computed the weight; nobody typed it in as lifted.</summary>
    Computed,

    /// <summary>A stand-in value typed to fill the cell, not a lift.</summary>
    Placeholder,

    /// <summary>The athlete's own note on the row says it did not happen as written.</summary>
    Contradicted,

    /// <summary>A value no lift could have, so a typing slip.</summary>
    Implausible,
}

/// <summary>
/// One spreadsheet row the set log holds that is not a set the athlete performed: where it is,
/// how many sets training-os expanded it into, and why it is not counted.
/// </summary>
public sealed record NotPerformed(
    SheetSource Source, DateOnly Date, string Exercise, double Weight, int Reps, int Sets, NotPerformedKind Kind, string Reason)
{
    public bool Covers(SheetSet set) =>
        set.Source == Source
        && set.Date == Date
        && set.ExerciseAsWritten == Exercise
        && set.Weight == Weight
        && set.Reps == Reps;
}

/// <summary>
/// The judgement calls reading the programme sheets needs: which programme each sheet is, which
/// rows were never lifted, and which note text is the template's own rather than the coach's.
/// </summary>
/// <remarks>
/// A data file (<c>coached-sheets.json</c>) for the reason the movement maps are: each call is
/// reviewable, with its evidence, without reading C#. Unlike the maps it is named on the command
/// line rather than compiled in, because its evidence is the athlete's own training (dates,
/// weights, his notes) and stays with his spreadsheets rather than in this repository.
/// <c>coached-sheets.example.json</c>, beside this file, shows the format with invented values.
/// </remarks>
public sealed class SheetDecisions
{
    internal const string FileName = "coached-sheets.json";

    private static readonly JsonSerializerOptions FileJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private SheetDecisions(
        IReadOnlyDictionary<string, SheetProgram> programs,
        IReadOnlyList<NotPerformed> notPerformed,
        IReadOnlyList<string> templateText)
    {
        Programs = programs;
        NotPerformed = notPerformed;
        TemplateText = templateText;
    }

    /// <summary>Each sheet's programme, by the sheet's slug.</summary>
    public IReadOnlyDictionary<string, SheetProgram> Programs { get; }

    public IReadOnlyList<NotPerformed> NotPerformed { get; }

    /// <summary>
    /// The openings of notes a template prints on every row, which say nothing about the session.
    /// </summary>
    public IReadOnlyList<string> TemplateText { get; }

    /// <summary>The decisions the file at <paramref name="path"/> states, or why it cannot be used.</summary>
    public static Result<SheetDecisions, Problem> Read(string path) =>
        File.Exists(path)
            ? Parse(File.ReadAllText(path))
            : Result<SheetDecisions, Problem>.Failure(new Problem.Invalid("decisions", $"No file at '{path}'."));

    /// <summary>The decisions a file's text states, or every reason it cannot be used.</summary>
    public static Result<SheetDecisions, Problem> Parse(string json) =>
        Contents(json).Bind(Accepted);

    /// <remarks>Deserialization answers a malformed file by throwing, so it is named here.</remarks>
    private static Result<FileContents, Problem> Contents(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<FileContents>(json, FileJson) is { } contents
                ? Result<FileContents, Problem>.Success(contents)
                : Unusable<FileContents>("it is the JSON literal null");
        }
        catch (JsonException exception)
        {
            return Unusable<FileContents>($"it is not valid JSON ({exception.Message})");
        }
    }

    private static Result<SheetDecisions, Problem> Accepted(FileContents contents)
    {
        var programs = (contents.Programs ?? []).Partition(Program);
        var notPerformed = (contents.NotPerformed ?? []).Partition(Row);

        var duplicated = programs.Delivered
            .GroupBy(program => program.Sheet)
            .Where(group => group.Count() > 1)
            .Select(group => new InvalidField(group.Key, $"Sheet '{group.Key}' has {group.Count()} programmes; which one applies would be a guess."));

        IReadOnlyList<InvalidField> problems = [.. programs.Refused, .. notPerformed.Refused, .. duplicated];

        return problems.Count == 0
            ? Result<SheetDecisions, Problem>.Success(new SheetDecisions(
                programs.Delivered.ToDictionary(program => program.Sheet),
                notPerformed.Delivered,
                [.. (contents.TemplateText ?? []).Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text.Trim())]))
            : Result<SheetDecisions, Problem>.Failure(new Problem.Invalid(problems));
    }

    private static Result<SheetProgram, InvalidField> Program(ProgramEntry? entry) =>
        entry is { Sheet: { Length: > 0 } sheet, Title: { Length: > 0 } title }
            ? Result<SheetProgram, InvalidField>.Success(new SheetProgram(
                sheet.Trim(), title.Trim(), entry.Summary?.Trim() ?? string.Empty, [.. entry.Tags ?? []],
                (entry.ReadAs ?? []).ToDictionary(
                    reading => reading.Key.Trim(), reading => reading.Value.Trim(), StringComparer.OrdinalIgnoreCase)))
            : Result<SheetProgram, InvalidField>.Failure(new InvalidField(
                "programs", $"{FileName}: every programme needs a \"sheet\" and a \"title\"."));

    private static Result<NotPerformed, InvalidField> Row(NotPerformedEntry? entry)
    {
        var where = entry is null ? "(an empty entry)" : $"{entry.Source} {entry.Date} {entry.Exercise}";
        InvalidField Wrong(string what) => new(where, $"{FileName}: not-performed row {where} {what}.");

        if (entry is not { Source: { } source, Exercise: { Length: > 0 } exercise, Weight: > 0, Reps: > 0, Sets: > 0, Reason: { Length: > 0 } reason })
            return Result<NotPerformed, InvalidField>.Failure(Wrong(
                "needs a \"source\", \"exercise\", a \"weight\", \"reps\" and \"sets\" above zero, and a \"reason\""));

        if (!DateOnly.TryParseExact(entry.Date, "yyyy-MM-dd", out var date))
            return Result<NotPerformed, InvalidField>.Failure(Wrong("needs a \"date\" as yyyy-MM-dd"));

        if (!Enum.TryParse<NotPerformedKind>(entry.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            return Result<NotPerformed, InvalidField>.Failure(Wrong(
                $"has a \"kind\" '{entry.Kind}' that is not one of {string.Join(", ", Enum.GetNames<NotPerformedKind>().Select(name => name.ToLowerInvariant()))}"));

        return SheetSource.Parse(source)
            .MapError(why => Wrong($"has a source {why}"))
            .Map(sheet => new NotPerformed(sheet, date, exercise, entry.Weight.Value, entry.Reps.Value, entry.Sets.Value, kind, reason));
    }

    private static Result<T, Problem> Unusable<T>(string why) =>
        Result<T, Problem>.Failure(new Problem.Invalid(FileName, $"{FileName} cannot be read: {why}."));

    // ---- The file as written, before anything is checked ----

    private sealed record FileContents(
        List<ProgramEntry?>? Programs = null, List<string>? TemplateText = null, List<NotPerformedEntry?>? NotPerformed = null);

    private sealed record ProgramEntry(
        string? Sheet = null, string? Title = null, string? Summary = null, List<string>? Tags = null,
        Dictionary<string, string>? ReadAs = null);

    private sealed record NotPerformedEntry(
        string? Source = null, string? Date = null, string? Exercise = null, double? Weight = null, int? Reps = null,
        int? Sets = null, string? Kind = null, string? Reason = null);
}
