using System.Text.Json;
using System.Text.Json.Serialization;
using lionheart.Functional;
using lionheart.Services.Import;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>One part's old name and the name it takes: an equipment, a base or a modifier.</summary>
public sealed record PartRename(string From, string To);

/// <summary>One movement restated whole, matched by the names it has now.</summary>
public sealed record MovementRestatement(MovementName From, MovementName To, string Unsure);

/// <summary>A movement to remove, by the names it has now, and why.</summary>
public sealed record MovementRetirement(MovementName Name, string Unsure);

/// <summary>
/// The library's naming corrections and seed list, <c>movement-library.json</c> beside this file:
/// what any movement's names become, and which common movements belong in every library.
/// </summary>
/// <remarks>
/// A data file for the reason the import maps are one: every decision is readable and correctable
/// without reading C#. The file is checked for the property that makes a tidy idempotent — every
/// name a rule produces is one no rule rewrites — so a file that would keep changing a catalogue
/// on every run is refused before it touches one. Names match ignoring case and surrounding
/// spaces, as the catalogue matches them.
/// </remarks>
public sealed class MovementLibraryRules
{
    public const string FileName = "movement-library.json";
    private const string ResourceName = "lionheart.Services.Training.Movement.Library.movement-library.json";

    private static readonly JsonSerializerOptions FileJson = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        // A misspelt key would otherwise be dropped, and the entry read as naming no modifier.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly IReadOnlyDictionary<string, string> _equipment;
    private readonly IReadOnlyDictionary<string, string> _bases;
    private readonly IReadOnlyDictionary<string, string> _modifiers;
    private readonly IReadOnlyDictionary<MovementName, MovementRestatement> _restatements;
    private readonly IReadOnlyDictionary<MovementName, MovementRetirement> _retirements;

    private MovementLibraryRules(
        IReadOnlyList<PartRename> equipment, IReadOnlyList<PartRename> bases, IReadOnlyList<PartRename> modifiers,
        IReadOnlyList<MovementRestatement> restatements, IReadOnlyList<MovementRetirement> retirements,
        IReadOnlyList<MovementName> seed)
    {
        Equipment = equipment;
        Bases = bases;
        Modifiers = modifiers;
        Restatements = restatements;
        Retirements = retirements;
        Seed = seed;
        _equipment = Renames(equipment);
        _bases = Renames(bases);
        _modifiers = Renames(modifiers);
        _restatements = restatements.ToDictionary(rule => rule.From, MovementName.Matching);
        _retirements = retirements.ToDictionary(rule => rule.Name, MovementName.Matching);
    }

    public IReadOnlyList<PartRename> Equipment { get; }
    public IReadOnlyList<PartRename> Bases { get; }
    public IReadOnlyList<PartRename> Modifiers { get; }
    public IReadOnlyList<MovementRestatement> Restatements { get; }
    public IReadOnlyList<MovementRetirement> Retirements { get; }
    public IReadOnlyList<MovementName> Seed { get; }

    /// <summary>
    /// What a movement by these names is called under the convention: its restatement where one
    /// names it, else each part renamed where a rule renames it, else the names it has.
    /// </summary>
    public MovementName Target(MovementName current) =>
        _restatements.TryGetValue(current, out var restated)
            ? restated.To
            : new MovementName(
                Renamed(_bases, current.Base), Renamed(_equipment, current.Equipment), Renamed(_modifiers, current.Modifier));

    /// <summary>The restatement that names this movement, which is what the tidy reports as unsure.</summary>
    public MovementRestatement? RestatementOf(MovementName current) => _restatements.GetValueOrDefault(current);

    /// <summary>The retirement that names this movement as it is now.</summary>
    public MovementRetirement? RetirementOf(MovementName current) => _retirements.GetValueOrDefault(current);

    /// <summary>The name a part of this kind takes, or the one it has.</summary>
    public string RenamedPart(MovementPart part, string name) => Renamed(part switch
    {
        MovementPart.Base => _bases,
        MovementPart.Equipment => _equipment,
        MovementPart.Modifier => _modifiers,
        _ => throw new System.Diagnostics.UnreachableException(),
    }, name);

    /// <summary>The rules this build carries.</summary>
    public static Result<MovementLibraryRules, Problem> Shipped()
    {
        using var stream = typeof(MovementLibraryRules).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"The build carries no '{ResourceName}'; it is an embedded resource of lionheart.csproj.");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    /// <summary>The rules a file's text states, or every reason they cannot be used.</summary>
    public static Result<MovementLibraryRules, Problem> Parse(string json) =>
        Read(json).Bind(Accepted).Bind(Settled);

    // ---- Reading the file ----

    private static Result<RulesFile, Problem> Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<RulesFile>(json, FileJson) is { } file
                ? Result<RulesFile, Problem>.Success(file)
                : Unusable<RulesFile>("it is the JSON literal null");
        }
        catch (JsonException exception)
        {
            return Unusable<RulesFile>($"it is not valid JSON for this file ({exception.Message})");
        }
    }

    /// <summary>Every entry whole and every "from" stated once, or each one that is not.</summary>
    private static Result<MovementLibraryRules, Problem> Accepted(RulesFile file)
    {
        var equipment = (file.Equipment ?? []).Partition(entry => Rename("equipment", entry));
        var bases = (file.Bases ?? []).Partition(entry => Rename("bases", entry));
        // Stored lower case, as every modifier is.
        var modifiers = (file.Modifiers ?? []).Partition(entry => Rename("modifiers", entry)
            .Map(rule => new PartRename(rule.From.ToLowerInvariant(), rule.To.ToLowerInvariant())));
        var movements = (file.Movements ?? []).Partition(Restatement);
        var retire = (file.Retire ?? []).Partition(entry => Name("retire", entry).Map(name => new MovementRetirement(name, Unsure(entry?.Unsure))));
        var seed = (file.Seed ?? []).Partition(entry => Name("seed", entry));

        IReadOnlyList<InvalidField> problems =
        [
            .. equipment.Refused, .. bases.Refused, .. modifiers.Refused, .. movements.Refused, .. retire.Refused, .. seed.Refused,
            .. Repeated("equipment", equipment.Delivered.Select(rule => rule.From), StringComparer.OrdinalIgnoreCase),
            .. Repeated("bases", bases.Delivered.Select(rule => rule.From), StringComparer.OrdinalIgnoreCase),
            .. Repeated("modifiers", modifiers.Delivered.Select(rule => rule.From), StringComparer.OrdinalIgnoreCase),
            .. Repeated("movements", movements.Delivered.Select(rule => rule.From), MovementName.Matching),
            .. Repeated("retire", retire.Delivered.Select(rule => rule.Name), MovementName.Matching),
            .. Repeated("seed", seed.Delivered, MovementName.Matching),
        ];

        return problems.Count == 0
            ? Result<MovementLibraryRules, Problem>.Success(new MovementLibraryRules(
                equipment.Delivered, bases.Delivered, modifiers.Delivered, movements.Delivered, retire.Delivered, seed.Delivered))
            : Result<MovementLibraryRules, Problem>.Failure(new Problem.Invalid(problems));
    }

    /// <summary>
    /// The rules, if every name they produce is one they leave alone, no movement is both kept by
    /// one rule and removed by another, and none restates a movement as itself.
    /// </summary>
    private static Result<MovementLibraryRules, Problem> Settled(MovementLibraryRules rules)
    {
        IReadOnlyList<InvalidField> problems =
        [
            .. Rewritten("equipment", rules.Equipment, rules._equipment),
            .. Rewritten("bases", rules.Bases, rules._bases),
            .. Rewritten("modifiers", rules.Modifiers, rules._modifiers),
            .. rules.Restatements
                .Where(rule => rule.From.SameAs(rule.To))
                .Select(rule => new InvalidField(rule.From.ToString(), $"movements: {rule.From} is restated as itself.")),
            .. rules.Restatements
                .Where(rule => !rules.Target(rule.To).SameAs(rule.To))
                .Select(rule => new InvalidField(rule.To.ToString(),
                    $"movements: {rule.From} becomes {rule.To}, which the rules would rewrite again as {rules.Target(rule.To)}.")),
            .. rules.Seed
                .Where(name => !rules.Target(name).SameAs(name))
                .Select(name => new InvalidField(name.ToString(),
                    $"seed: {name} is not in the convention; the rules would rewrite it as {rules.Target(name)}.")),
            .. rules.Retirements
                .Where(rule => rules._restatements.ContainsKey(rule.Name))
                .Select(rule => new InvalidField(rule.Name.ToString(), $"retire: {rule.Name} is also restated; it cannot be both.")),
            .. rules.Seed
                .Where(rules._retirements.ContainsKey)
                .Select(name => new InvalidField(name.ToString(),
                    $"seed: {name} is also retired, so every run would remove it and add it again.")),
        ];

        return problems.Count == 0
            ? Result<MovementLibraryRules, Problem>.Success(rules)
            : Result<MovementLibraryRules, Problem>.Failure(new Problem.Invalid(problems));
    }

    private static IEnumerable<InvalidField> Rewritten(
        string section, IReadOnlyList<PartRename> renames, IReadOnlyDictionary<string, string> byFrom) =>
        renames
            .Where(rule => byFrom.ContainsKey(rule.To) && !rule.To.Equals(rule.From, StringComparison.OrdinalIgnoreCase))
            .Select(rule => new InvalidField(rule.To,
                $"{section}: '{rule.From}' becomes '{rule.To}', which is itself renamed; rename '{rule.From}' to the last name directly."));

    private static IEnumerable<InvalidField> Repeated<T>(string section, IEnumerable<T> names, IEqualityComparer<T> comparer) =>
        names
            .GroupBy(name => name, comparer)
            .Where(group => group.Count() > 1)
            .Select(group => new InvalidField($"{group.Key}", $"{section}: {group.Key} is listed {group.Count()} times."));

    /// <remarks>A rename that changes only capitals is a rename: it is how a part's spelling is corrected.</remarks>
    private static Result<PartRename, InvalidField> Rename(string section, RenameEntry? entry) =>
        (entry?.From?.Trim(), entry?.To?.Trim()) switch
        {
            ({ Length: > 0 } from, { Length: > 0 } to) when from == to =>
                Result<PartRename, InvalidField>.Failure(new InvalidField(from, $"{section}: '{from}' is renamed to itself.")),
            ({ Length: > 0 } from, { Length: > 0 } to) =>
                Result<PartRename, InvalidField>.Success(new PartRename(from, to)),
            _ => Result<PartRename, InvalidField>.Failure(new InvalidField(
                section, $"{section}: every entry needs \"from\" and \"to\".")),
        };

    private static Result<MovementRestatement, InvalidField> Restatement(RestatementEntry? entry) =>
        Name("movements \"from\"", entry?.From)
            .Bind(from => Name("movements \"to\"", entry?.To)
                .Map(to => new MovementRestatement(from, to, Unsure(entry?.Unsure))));

    private static Result<MovementName, InvalidField> Name(string section, NameEntry? entry) =>
        (entry?.Base?.Trim(), entry?.Equipment?.Trim()) is ({ Length: > 0 } movementBase, { Length: > 0 } equipment)
            ? Result<MovementName, InvalidField>.Success(new MovementName(
                movementBase, equipment, entry.Modifier?.Trim().ToLowerInvariant() ?? string.Empty))
            : Result<MovementName, InvalidField>.Failure(new InvalidField(
                section, $"{section}: every movement needs \"base\" and \"equipment\"."));

    private static string Unsure(string? unsure) => unsure?.Trim() ?? string.Empty;

    private static IReadOnlyDictionary<string, string> Renames(IEnumerable<PartRename> renames) =>
        renames.ToDictionary(rule => rule.From, rule => rule.To, StringComparer.OrdinalIgnoreCase);

    private static string Renamed(IReadOnlyDictionary<string, string> renames, string name) =>
        renames.GetValueOrDefault(name.Trim(), name);

    private static Result<T, Problem> Unusable<T>(string why) =>
        Result<T, Problem>.Failure(new Problem.Invalid(FileName, $"{FileName} cannot be read: {why}."));

    // ---- The file's shape ----

    private sealed record RulesFile(
        List<RenameEntry?>? Equipment,
        List<RenameEntry?>? Bases,
        List<RenameEntry?>? Modifiers,
        List<RestatementEntry?>? Movements,
        List<NameEntry?>? Retire,
        List<NameEntry?>? Seed);

    private sealed record RenameEntry(string? From, string? To);

    private sealed record RestatementEntry(NameEntry? From, NameEntry? To, string? Unsure);

    private sealed record NameEntry(string? Base, string? Equipment, string? Modifier, string? Unsure);
}

/// <summary>The three parts a movement is named by.</summary>
public enum MovementPart { Base, Equipment, Modifier }
