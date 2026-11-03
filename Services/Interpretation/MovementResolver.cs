using System.Diagnostics;
using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Interpretation;

/// <summary>Named movements mapped to ids, plus the names that need the user to disambiguate.</summary>
public sealed record MovementResolution(
    IReadOnlyDictionary<MovementReference, Guid> Resolved,
    IReadOnlyList<UnresolvedReference> Unresolved);

public interface IMovementResolver
{
    /// <summary>
    /// Maps every name in <paramref name="references"/> onto a MovementData id, consulting
    /// <paramref name="confirmed"/> for the names that do not match outright. The default is the
    /// first pass, where nothing has been confirmed because the athlete has not been asked.
    /// </summary>
    Task<Result<MovementResolution, Problem>> ResolveAsync(
        IdentityUser user,
        IReadOnlyList<MovementReference> references,
        ConfirmedReferences confirmed = default);
}

/// <summary>
/// Turns the parser's movement names into MovementData ids. Matching is lexical rather than
/// semantic: unlike facet values, movement bases and equipment carry no embedding, and an
/// exercise name the athlete already uses is an exact string far more often than a paraphrase.
/// Anything not matched outright goes back to the user with candidates instead of guessing, and
/// comes back on the next pass as a confirmed choice — an entity they picked, or one to create
/// under the name they used.
/// </summary>
public sealed class MovementResolver : IMovementResolver
{
    /// <summary>Below this, a name is too unlike an existing one to be worth offering as a candidate.</summary>
    private const double CandidateThreshold = 0.4;
    private const int MaxCandidates = 5;

    private readonly ModelContext _context;
    private readonly IMovementDataService _movementDataService;

    public MovementResolver(ModelContext context, IMovementDataService movementDataService)
    {
        _context = context;
        _movementDataService = movementDataService;
    }

    public async Task<Result<MovementResolution, Problem>> ResolveAsync(
        IdentityUser user,
        IReadOnlyList<MovementReference> references,
        ConfirmedReferences confirmed = default)
    {
        var distinct = references.Distinct().ToList();
        if (distinct.Count == 0) return Result<MovementResolution, Problem>.Success(NothingResolved);

        var catalog = await LoadCatalogAsync(Guid.Parse(user.Id));

        return await NamesIn(distinct)
            .Traverse(name => Settle(name, catalog, confirmed))
            .BindAsync(landings => LandAsync(user, distinct, landings));
    }

    /// <summary>
    /// Where one name lands: on an entity the athlete already has, on one they have confirmed is
    /// new, or back on the athlete because nothing matched and nothing was chosen.
    /// </summary>
    private static Result<NameLanding, Problem> Settle(
        NamedReference name, NameCatalog catalog, ConfirmedReferences confirmed)
    {
        if (catalog.MatchExactly(name) is { } known)
        {
            return Result<NameLanding, Problem>.Success(new NameLanding.OnEntity(name.Key, known.Id));
        }

        return confirmed.For(name.Kind, name.RawName) switch
        {
            // A chosen id the athlete does not own is answered as absent rather than forbidden:
            // whose it is would be disclosing that it exists at all.
            ReferenceDecision.ChoseExisting chosen => catalog.Owns(name.Kind, chosen.EntityId)
                ? Result<NameLanding, Problem>.Success(new NameLanding.OnEntity(name.Key, chosen.EntityId))
                : Result<NameLanding, Problem>.Failure(
                    new Problem.NotFound($"The chosen {Describe(name.Kind)} does not exist.")),
            ReferenceDecision.ChoseNewEntity =>
                Result<NameLanding, Problem>.Success(new NameLanding.OnNewEntity(name)),
            ReferenceDecision.Undecided =>
                Result<NameLanding, Problem>.Success(new NameLanding.OnTheAthlete(catalog.Unmatched(name))),
            _ => throw new UnreachableException()
        };
    }

    /// <summary>
    /// Nothing is created until every name has landed. Creating movement data — or a movement
    /// base under a name the athlete never confirmed — must not outlive an interpretation they
    /// end up abandoning at the confirmation prompt.
    /// </summary>
    private async Task<Result<MovementResolution, Problem>> LandAsync(
        IdentityUser user, IReadOnlyList<MovementReference> references, IReadOnlyList<NameLanding> landings)
    {
        IReadOnlyList<UnresolvedReference> needed =
            [.. landings.OfType<NameLanding.OnTheAthlete>().Select(landing => landing.Reference)];

        if (needed.Count > 0)
        {
            return Result<MovementResolution, Problem>.Success(new MovementResolution(NoMovements, needed));
        }

        var entityIds = await CreateNewlyNamedEntitiesAsync(Guid.Parse(user.Id), landings);
        return await IdentifyMovementsAsync(user, references, entityIds);
    }

    /// <summary>
    /// Gives every settled name an entity id, creating rows for the names the athlete confirmed
    /// are new. The row carries the name and nothing else — description, muscle groups and the
    /// rest are the athlete's to fill in later, and guessing them here would be inventing data.
    /// </summary>
    private async Task<IReadOnlyDictionary<ReferenceKey, Guid>> CreateNewlyNamedEntitiesAsync(
        Guid userId, IReadOnlyList<NameLanding> landings)
    {
        var entityIds = landings
            .OfType<NameLanding.OnEntity>()
            .ToDictionary(landing => landing.Key, landing => landing.EntityId);

        var newlyNamed = landings.OfType<NameLanding.OnNewEntity>().ToList();
        foreach (var landing in newlyNamed)
        {
            entityIds[landing.Name.Key] = Stage(userId, landing.Name);
        }

        if (newlyNamed.Count > 0) await _context.SaveChangesAsync();
        return entityIds;
    }

    private Guid Stage(Guid userId, NamedReference name)
    {
        var entityId = Guid.NewGuid();
        var trimmed = name.RawName.Trim();

        switch (name.Kind)
        {
            case ReferenceKind.MovementBase:
                _context.MovementBases.Add(new MovementBase
                {
                    MovementBaseID = entityId,
                    Name = trimmed,
                    UserID = userId,
                    Description = string.Empty,
                    MuscleGroups = []
                });
                break;
            case ReferenceKind.Equipment:
                _context.Equipments.Add(new Equipment
                {
                    EquipmentID = entityId,
                    Name = trimmed,
                    UserID = userId
                });
                break;
            default:
                throw new UnreachableException();
        }

        return entityId;
    }

    private Task<Result<MovementResolution, Problem>> IdentifyMovementsAsync(
        IdentityUser user,
        IReadOnlyList<MovementReference> references,
        IReadOnlyDictionary<ReferenceKey, Guid> entityIds) =>
        references
            .TraverseAsync(reference => IdentifyMovementAsync(user, reference, entityIds))
            .MapAsync(identified => new MovementResolution(identified.ToDictionary(), []));

    private Task<Result<KeyValuePair<MovementReference, Guid>, Problem>> IdentifyMovementAsync(
        IdentityUser user,
        MovementReference reference,
        IReadOnlyDictionary<ReferenceKey, Guid> entityIds) =>
        _movementDataService
            .FindOrCreateMovementDataAsync(user, new CreateMovementDataRequest(
                EquipmentID: entityIds[ReferenceKey.For(ReferenceKind.Equipment, reference.EquipmentName)],
                MovementBaseID: entityIds[ReferenceKey.For(ReferenceKind.MovementBase, reference.BaseName)],
                MovementModifierName: string.IsNullOrWhiteSpace(reference.ModifierName)
                    ? null
                    : reference.ModifierName))
            .MapAsync(movementData => KeyValuePair.Create(reference, movementData.MovementDataID));

    private async Task<NameCatalog> LoadCatalogAsync(Guid userId) => new(
        Bases: await _context.MovementBases
            .Where(mb => mb.UserID == userId)
            .Select(mb => new NamedEntity(mb.MovementBaseID, mb.Name))
            .ToListAsync(),
        Equipment: await _context.Equipments
            .Where(e => e.UserID == userId)
            .Select(e => new NamedEntity(e.EquipmentID, e.Name))
            .ToListAsync());

    /// <summary>
    /// Every name a run has to settle, each one once: the same movement across a dozen sets must
    /// not produce a dozen identical prompts.
    /// </summary>
    private static IEnumerable<NamedReference> NamesIn(IEnumerable<MovementReference> references) =>
        references
            .SelectMany(reference => new NamedReference[]
            {
                new(ReferenceKind.MovementBase, reference.BaseName),
                new(ReferenceKind.Equipment, reference.EquipmentName)
            })
            .DistinctBy(name => name.Key);

    private static string Describe(ReferenceKind kind) => kind switch
    {
        ReferenceKind.MovementBase => "movement",
        ReferenceKind.Equipment => "equipment",
        _ => throw new UnreachableException()
    };

    private static readonly IReadOnlyDictionary<MovementReference, Guid> NoMovements =
        new Dictionary<MovementReference, Guid>();

    private static MovementResolution NothingResolved => new(NoMovements, []);

    private static double Similarity(string left, string right)
    {
        var a = left.Trim().ToLowerInvariant();
        var b = right.Trim().ToLowerInvariant();
        if (a.Length == 0 || b.Length == 0) return 0;

        // Containment scores by length ratio so "squat" against "front squat" reads as a strong
        // near-miss; unrelated names fall through to edit distance.
        if (a.Contains(b) || b.Contains(a))
        {
            return (double)Math.Min(a.Length, b.Length) / Math.Max(a.Length, b.Length);
        }

        return 1.0 - (double)LevenshteinDistance(a, b) / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var distances = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) distances[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) distances[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + substitution);
            }
        }

        return distances[a.Length, b.Length];
    }

    /// <summary>One name the parser used, and the kind of thing it names.</summary>
    private sealed record NamedReference(ReferenceKind Kind, string RawName)
    {
        public ReferenceKey Key => ReferenceKey.For(Kind, RawName);
    }

    private sealed record NamedEntity(Guid Id, string Name);

    /// <summary>The entities this athlete has, which is the whole universe a name can match.</summary>
    private sealed record NameCatalog(IReadOnlyList<NamedEntity> Bases, IReadOnlyList<NamedEntity> Equipment)
    {
        public NamedEntity? MatchExactly(NamedReference name) =>
            Of(name.Kind).FirstOrDefault(entity =>
                entity.Name.Trim().Equals(name.RawName.Trim(), StringComparison.OrdinalIgnoreCase));

        public bool Owns(ReferenceKind kind, Guid entityId) => Of(kind).Any(entity => entity.Id == entityId);

        public UnresolvedReference Unmatched(NamedReference name) => new(
            name.RawName,
            name.Kind,
            [
                .. Of(name.Kind)
                    .Select(entity => new EntityMatch(
                        entity.Id, entity.Name, Math.Round(Similarity(name.RawName, entity.Name), 2)))
                    .Where(match => match.Confidence >= CandidateThreshold)
                    .OrderByDescending(match => match.Confidence)
                    .Take(MaxCandidates)
            ]);

        private IReadOnlyList<NamedEntity> Of(ReferenceKind kind) => kind switch
        {
            ReferenceKind.MovementBase => Bases,
            ReferenceKind.Equipment => Equipment,
            _ => throw new UnreachableException()
        };
    }

    /// <summary>Where one name landed on this pass.</summary>
    private abstract record NameLanding
    {
        private protected NameLanding() { }

        public sealed record OnEntity(ReferenceKey Key, Guid EntityId) : NameLanding;

        public sealed record OnNewEntity(NamedReference Name) : NameLanding;

        public sealed record OnTheAthlete(UnresolvedReference Reference) : NameLanding;
    }
}
