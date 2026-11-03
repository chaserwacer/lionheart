using System.Text.Json;
using lionheart.Functional;

namespace lionheart.Services.Strong;

/// <summary>
/// A movement by the names that make up its identity: base, equipment and modifier.
/// </summary>
/// <remarks>
/// Names rather than ids, because that is what the mapping file can state and what an athlete's
/// existing catalogue is matched against. An empty <see cref="Modifier"/> means none.
/// </remarks>
public sealed record MovementName(string Base, string Equipment, string Modifier)
{
    public bool HasModifier => Modifier.Length > 0;

    public override string ToString() =>
        HasModifier ? $"{Base} / {Equipment} / {Modifier}" : $"{Base} / {Equipment}";
}

/// <summary>
/// What one Strong exercise name becomes, and why it was a judgement call when it was one.
/// </summary>
public sealed record MovementMapping(string StrongName, MovementName Movement, string Unsure)
{
    public bool IsUnsure => Unsure.Length > 0;
}

/// <summary>
/// The athlete-editable map from Strong's exercise names to Lionheart movements.
/// </summary>
/// <remarks>
/// A data file rather than code (<c>strong-movements.json</c>, beside this one), so the athlete can
/// read every decision and correct one without reading C#. It is compiled into the assembly, which
/// means the file a run uses is always the one in the source tree that built it.
/// <para>
/// Names match ignoring case and surrounding spaces, the same rule the athlete's catalogue is
/// matched by, because Strong's own custom names carry stray trailing spaces.
/// </para>
/// </remarks>
public sealed class StrongMovementMap
{
    internal const string ResourceName = "lionheart.Services.Strong.strong-movements.json";

    private static readonly JsonSerializerOptions FileJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IReadOnlyDictionary<string, MovementMapping> _byName;

    private StrongMovementMap(IReadOnlyDictionary<string, MovementMapping> byName) => _byName = byName;

    public IReadOnlyCollection<MovementMapping> Mappings => [.. _byName.Values];

    /// <summary>The map this build carries.</summary>
    public static Result<StrongMovementMap, Problem> Shipped()
    {
        using var stream = typeof(StrongMovementMap).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"The build carries no '{ResourceName}'; it is an embedded resource of lionheart.csproj.");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    /// <summary>The map a file's text states, or every reason it cannot be used.</summary>
    public static Result<StrongMovementMap, Problem> Parse(string json) =>
        Entries(json).Bind(entries => Accepted(entries.Partition(Mapping)));

    /// <summary>
    /// The mapping for every exercise named, or a refusal naming each one the map does not cover.
    /// </summary>
    /// <remarks>
    /// Checked for the whole export before anything is written, so a missing name costs a
    /// one-line edit to the file rather than a partly imported history.
    /// </remarks>
    public Result<IReadOnlyDictionary<string, MovementMapping>, Problem> Cover(IEnumerable<string> exercises)
    {
        var named = exercises.Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var unmapped = named.Where(name => !_byName.ContainsKey(name)).Order(StringComparer.OrdinalIgnoreCase).ToList();

        return unmapped.Count == 0
            ? Result<IReadOnlyDictionary<string, MovementMapping>, Problem>.Success(
                named.ToDictionary(name => name, name => _byName[name], StringComparer.OrdinalIgnoreCase))
            : Result<IReadOnlyDictionary<string, MovementMapping>, Problem>.Failure(new Problem.Invalid(
                [.. unmapped.Select(name => new InvalidField(
                    name, $"'{name}' has no entry in strong-movements.json, so it would become nothing."))]));
    }

    // ---- Reading the file ----

    /// <remarks>Deserialization answers a malformed file by throwing, so it is named here.</remarks>
    private static Result<IReadOnlyList<FileEntry>, Problem> Entries(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<FileEntry?>>(json, FileJson) is { } entries
                ? Result<IReadOnlyList<FileEntry>, Problem>.Success([.. entries.Select(entry => entry ?? new FileEntry())])
                : Unusable<IReadOnlyList<FileEntry>>("it is the JSON literal null");
        }
        catch (JsonException exception)
        {
            return Unusable<IReadOnlyList<FileEntry>>($"it is not valid JSON ({exception.Message})");
        }
    }

    private static Result<MovementMapping, InvalidField> Mapping(FileEntry entry)
    {
        var strong = entry.Strong?.Trim() ?? string.Empty;
        var label = strong.Length > 0 ? strong : "(an entry with no \"strong\" name)";

        string?[] required = [entry.Strong, entry.Base, entry.Equipment];
        return required.Any(string.IsNullOrWhiteSpace)
            ? Result<MovementMapping, InvalidField>.Failure(new InvalidField(
                label, $"{label}: every entry needs \"strong\", \"base\" and \"equipment\"."))
            : Result<MovementMapping, InvalidField>.Success(new MovementMapping(
                strong,
                new MovementName(entry.Base!.Trim(), entry.Equipment!.Trim(), entry.Modifier?.Trim() ?? string.Empty),
                entry.Unsure?.Trim() ?? string.Empty));
    }

    private static Result<StrongMovementMap, Problem> Accepted(Partitioned<MovementMapping, InvalidField> read)
    {
        var duplicated = read.Delivered
            .GroupBy(mapping => mapping.StrongName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new InvalidField(
                group.Key, $"'{group.Key}' has {group.Count()} entries; which one applies would be a guess."));

        IReadOnlyList<InvalidField> problems = [.. read.Refused, .. duplicated];

        return problems.Count == 0
            ? Result<StrongMovementMap, Problem>.Success(new StrongMovementMap(
                read.Delivered.ToDictionary(mapping => mapping.StrongName, StringComparer.OrdinalIgnoreCase)))
            : Result<StrongMovementMap, Problem>.Failure(new Problem.Invalid(problems));
    }

    private static Result<T, Problem> Unusable<T>(string why) =>
        Result<T, Problem>.Failure(new Problem.Invalid("strong-movements.json", $"strong-movements.json cannot be read: {why}."));

    /// <summary>One entry as the file states it, before anything is checked.</summary>
    private sealed record FileEntry(
        string? Strong = null, string? Base = null, string? Equipment = null, string? Modifier = null, string? Unsure = null);
}
