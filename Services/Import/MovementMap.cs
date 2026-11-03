using System.Text.Json;
using lionheart.Functional;

namespace lionheart.Services.Import;

/// <summary>
/// A movement by the names that make up its identity: base, equipment and modifier.
/// </summary>
/// <remarks>
/// Names rather than ids, because that is what a mapping file can state and what an athlete's
/// existing catalogue is matched against. An empty <see cref="Modifier"/> means none.
/// </remarks>
public sealed record MovementName(string Base, string Equipment, string Modifier)
{
    public bool HasModifier => Modifier.Length > 0;

    public override string ToString() =>
        HasModifier ? $"{Base} / {Equipment} / {Modifier}" : $"{Base} / {Equipment}";
}

/// <summary>
/// What one exercise name in an imported history becomes, and why it was a judgement call when it
/// was one.
/// </summary>
public sealed record MovementMapping(string Name, MovementName Movement, string Unsure)
{
    public bool IsUnsure => Unsure.Length > 0;
}

/// <summary>
/// One shipped mapping file: what it is called, the resource the build embeds it as, and the
/// property its entries name their source exercise by.
/// </summary>
/// <remarks>
/// The key differs by source because each file names the exercise the way its own history does —
/// Strong's display name, or a training-os slug — and the file reads plainest in that source's
/// own word.
/// </remarks>
public sealed record MovementMapFile(string FileName, string ResourceName, string KeyProperty);

/// <summary>
/// An athlete-editable map from the exercise names an imported history uses to Lionheart movements.
/// </summary>
/// <remarks>
/// A data file rather than code, so the athlete can read every decision and correct one without
/// reading C#. It is compiled into the assembly, which means the file a run uses is always the one
/// in the source tree that built it.
/// <para>
/// Names match ignoring case and surrounding spaces, the same rule the athlete's catalogue is
/// matched by, because hand-typed names carry stray capitals and trailing spaces.
/// </para>
/// </remarks>
public sealed class MovementMap
{
    private static readonly JsonSerializerOptions FileJson = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly MovementMapFile _file;
    private readonly IReadOnlyDictionary<string, MovementMapping> _byName;

    private MovementMap(MovementMapFile file, IReadOnlyDictionary<string, MovementMapping> byName)
    {
        _file = file;
        _byName = byName;
    }

    public IReadOnlyCollection<MovementMapping> Mappings => [.. _byName.Values];

    /// <summary>The map this build carries in the given file.</summary>
    public static Result<MovementMap, Problem> Shipped(MovementMapFile file)
    {
        using var stream = typeof(MovementMap).Assembly.GetManifestResourceStream(file.ResourceName)
                           ?? throw new InvalidOperationException(
                               $"The build carries no '{file.ResourceName}'; it is an embedded resource of lionheart.csproj.");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd(), file);
    }

    /// <summary>The map a file's text states, or every reason it cannot be used.</summary>
    public static Result<MovementMap, Problem> Parse(string json, MovementMapFile file) =>
        Entries(json, file).Bind(entries => Accepted(file, entries.Partition(entry => Mapping(entry, file))));

    /// <summary>
    /// The mapping for every exercise named, or a refusal naming each one the map does not cover.
    /// </summary>
    /// <remarks>
    /// Checked for the whole history before anything is written, so a missing name costs a
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
                    name, $"'{name}' has no entry in {_file.FileName}, so it would become nothing."))]));
    }

    // ---- Reading the file ----

    /// <remarks>
    /// Read as plain string maps, compared ignoring case, because the key property differs by
    /// file. Deserialization answers a malformed file by throwing, so it is named here.
    /// </remarks>
    private static Result<IReadOnlyList<IReadOnlyDictionary<string, string?>>, Problem> Entries(
        string json, MovementMapFile file)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, string?>?>>(json, FileJson) is { } entries
                ? Result<IReadOnlyList<IReadOnlyDictionary<string, string?>>, Problem>.Success(
                [
                    .. entries.Select(IReadOnlyDictionary<string, string?> (entry) =>
                        new Dictionary<string, string?>(entry ?? [], StringComparer.OrdinalIgnoreCase))
                ])
                : Unusable<IReadOnlyList<IReadOnlyDictionary<string, string?>>>(file, "it is the JSON literal null");
        }
        catch (JsonException exception)
        {
            return Unusable<IReadOnlyList<IReadOnlyDictionary<string, string?>>>(
                file, $"it is not valid JSON ({exception.Message})");
        }
    }

    private static Result<MovementMapping, InvalidField> Mapping(
        IReadOnlyDictionary<string, string?> entry, MovementMapFile file)
    {
        string Field(string name) => entry.GetValueOrDefault(name)?.Trim() ?? string.Empty;

        var name = Field(file.KeyProperty);
        var label = name.Length > 0 ? name : $"(an entry with no \"{file.KeyProperty}\" name)";

        return new[] { name, Field("base"), Field("equipment") }.Any(field => field.Length == 0)
            ? Result<MovementMapping, InvalidField>.Failure(new InvalidField(
                label, $"{label}: every entry needs \"{file.KeyProperty}\", \"base\" and \"equipment\"."))
            : Result<MovementMapping, InvalidField>.Success(new MovementMapping(
                name,
                new MovementName(Field("base"), Field("equipment"), Field("modifier")),
                Field("unsure")));
    }

    private static Result<MovementMap, Problem> Accepted(
        MovementMapFile file, Partitioned<MovementMapping, InvalidField> read)
    {
        var duplicated = read.Delivered
            .GroupBy(mapping => mapping.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new InvalidField(
                group.Key, $"'{group.Key}' has {group.Count()} entries; which one applies would be a guess."));

        IReadOnlyList<InvalidField> problems = [.. read.Refused, .. duplicated];

        return problems.Count == 0
            ? Result<MovementMap, Problem>.Success(new MovementMap(
                file, read.Delivered.ToDictionary(mapping => mapping.Name, StringComparer.OrdinalIgnoreCase)))
            : Result<MovementMap, Problem>.Failure(new Problem.Invalid(problems));
    }

    private static Result<T, Problem> Unusable<T>(MovementMapFile file, string why) =>
        Result<T, Problem>.Failure(new Problem.Invalid(file.FileName, $"{file.FileName} cannot be read: {why}."));
}
