using System.Text.Json;
using lionheart.Functional;
using lionheart.Model.Training.Patterns;

namespace lionheart.Services.Training.Patterns;

/// <summary>
/// The shipped map from movement-base names to what each trains, <c>movement-patterns.json</c>
/// beside this file.
/// </summary>
/// <remarks>
/// A data file rather than code, for the reason the import maps are one: the athlete can read
/// every judgement and correct it without reading C#. It is read live rather than copied onto the
/// bases, so a correction reaches every base the athlete has not classified himself. Names match
/// ignoring case and surrounding spaces, the rule the catalogue is matched by.
/// </remarks>
public sealed class MovementPatternMap
{
    public const string FileName = "movement-patterns.json";
    private const string ResourceName = "lionheart.Services.Training.Patterns.movement-patterns.json";

    private static readonly JsonSerializerOptions FileJson = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<MovementPatternMap> ShippedMap = new(() => Parse(ShippedText()).Match(
        map => map,
        problem => throw new InvalidOperationException($"{FileName} cannot be used: {problem.Description}")));

    private readonly IReadOnlyDictionary<string, ClassificationStanding.Classified> _byBase;

    private MovementPatternMap(IReadOnlyDictionary<string, ClassificationStanding.Classified> byBase) => _byBase = byBase;

    /// <summary>
    /// The map this build carries. A malformed shipped file is a build defect, which the test of
    /// the shipped file catches before it ships, so it throws rather than answering a refusal.
    /// </summary>
    public static MovementPatternMap Shipped => ShippedMap.Value;

    public IReadOnlyCollection<string> Bases => [.. _byBase.Keys];

    /// <summary>The entry for a base, or unclassified when the file has none.</summary>
    public ClassificationStanding For(string baseName) =>
        _byBase.TryGetValue(baseName.Trim(), out var entry) ? entry : new ClassificationStanding.Unclassified();

    /// <summary>The map a file's text states, or every reason it cannot be used.</summary>
    public static Result<MovementPatternMap, Problem> Parse(string json) =>
        Entries(json).Bind(entries => Accepted(entries.Select((entry, index) => (entry, index)).Partition(Entry)));

    private static Result<MovementPatternMap, Problem> Accepted(
        Partitioned<(string Base, ClassificationStanding.Classified Entry), InvalidField> entries) =>
        entries.Refused.Count is 0
            ? Unique(entries.Delivered)
            : Result<MovementPatternMap, Problem>.Failure(new Problem.Invalid(entries.Refused));

    private static Result<MovementPatternMap, Problem> Unique(IReadOnlyList<(string Base, ClassificationStanding.Classified Entry)> entries)
    {
        var repeated = entries
            .GroupBy(entry => entry.Base, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new InvalidField(group.Key, $"'{group.Key}' has more than one entry in {FileName}."))
            .ToList();

        return repeated.Count is 0
            ? Result<MovementPatternMap, Problem>.Success(new MovementPatternMap(
                entries.ToDictionary(entry => entry.Base, entry => entry.Entry, StringComparer.OrdinalIgnoreCase)))
            : Result<MovementPatternMap, Problem>.Failure(new Problem.Invalid(repeated));
    }

    private sealed record FileEntry(
        string? Base, MovementPattern? Pattern, List<MuscleGroup>? Primary, List<MuscleGroup>? Secondary, string? Unsure);

    /// <remarks>Deserialization answers a malformed file, or an unknown pattern or muscle, by throwing, so it is named here.</remarks>
    private static Result<IReadOnlyList<FileEntry>, Problem> Entries(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<FileEntry>>(json, FileJson) is { } entries
                ? Result<IReadOnlyList<FileEntry>, Problem>.Success(entries)
                : Result<IReadOnlyList<FileEntry>, Problem>.Failure(new Problem.Invalid(FileName, $"{FileName} is the JSON literal null."));
        }
        catch (JsonException exception)
        {
            return Result<IReadOnlyList<FileEntry>, Problem>.Failure(
                new Problem.Invalid(FileName, $"{FileName} cannot be read: {exception.Message}"));
        }
    }

    private static Result<(string Base, ClassificationStanding.Classified Entry), InvalidField> Entry(
        (FileEntry Entry, int Index) numbered)
    {
        var (entry, index) = numbered;
        var name = entry.Base?.Trim() ?? string.Empty;
        var label = name.Length > 0 ? name : $"entry {index + 1}";

        if (name.Length is 0)
            return Result<(string, ClassificationStanding.Classified), InvalidField>.Failure(
                new InvalidField(label, $"{label} in {FileName} names no base."));
        if (entry.Pattern is not { } pattern)
            return Result<(string, ClassificationStanding.Classified), InvalidField>.Failure(
                new InvalidField(label, $"'{label}' in {FileName} names no pattern."));

        var unsure = entry.Unsure?.Trim() ?? string.Empty;
        ClassificationSource source = unsure.Length > 0
            ? new ClassificationSource.MapUnsure(unsure)
            : new ClassificationSource.Map();

        return MovementClassification.Of(pattern, entry.Primary ?? [], entry.Secondary ?? [])
            .Map(classification => (name, new ClassificationStanding.Classified(classification, source)))
            .MapError(problem => new InvalidField(label, $"'{label}' in {FileName}: {problem.Description}"));
    }

    private static string ShippedText()
    {
        using var stream = typeof(MovementPatternMap).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"The build carries no '{ResourceName}'; it is an embedded resource of lionheart.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
