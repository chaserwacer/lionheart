using Ardalis.Result;
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
    Task<Result<MovementResolution>> ResolveAsync(
        IdentityUser user, IReadOnlyList<MovementReference> references);
}

/// <summary>
/// Turns the parser's movement names into MovementData ids. Matching is lexical rather than
/// semantic: unlike facet values, movement bases and equipment carry no embedding, and an
/// exercise name the athlete already uses is an exact string far more often than a paraphrase.
/// Anything not matched outright goes back to the user with candidates instead of guessing.
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

    public async Task<Result<MovementResolution>> ResolveAsync(
        IdentityUser user, IReadOnlyList<MovementReference> references)
    {
        var distinct = references.Distinct().ToList();
        if (distinct.Count == 0) return Result<MovementResolution>.Success(
            new MovementResolution(new Dictionary<MovementReference, Guid>(), []));

        var userGuid = Guid.Parse(user.Id);
        var bases = await _context.MovementBases
            .Where(mb => mb.UserID == userGuid)
            .Select(mb => new NamedEntity(mb.MovementBaseID, mb.Name))
            .ToListAsync();
        var equipment = await _context.Equipments
            .Where(e => e.UserID == userGuid)
            .Select(e => new NamedEntity(e.EquipmentID, e.Name))
            .ToListAsync();

        var unresolved = new List<UnresolvedReference>();
        var matched = new Dictionary<MovementReference, (Guid Base, Guid Equipment)>();

        foreach (var reference in distinct)
        {
            var baseMatch = MatchExactly(reference.BaseName, bases);
            var equipmentMatch = MatchExactly(reference.EquipmentName, equipment);

            if (baseMatch is null) unresolved.Add(Unmatched(reference.BaseName, ReferenceKind.MovementBase, bases));
            if (equipmentMatch is null) unresolved.Add(Unmatched(reference.EquipmentName, ReferenceKind.Equipment, equipment));
            if (baseMatch is not null && equipmentMatch is not null)
            {
                matched[reference] = (baseMatch.Id, equipmentMatch.Id);
            }
        }

        // Every name must land before anything is created: FindOrCreate persists, and an
        // interpretation the user ends up abandoning must not leave MovementData behind.
        if (unresolved.Count > 0)
        {
            return Result<MovementResolution>.Success(
                new MovementResolution(new Dictionary<MovementReference, Guid>(), Deduplicate(unresolved)));
        }

        var resolved = new Dictionary<MovementReference, Guid>();
        foreach (var (reference, ids) in matched)
        {
            var movementData = await _movementDataService.FindOrCreateMovementDataAsync(
                user,
                new CreateMovementDataRequest(
                    EquipmentID: ids.Equipment,
                    MovementBaseID: ids.Base,
                    MovementModifierName: string.IsNullOrWhiteSpace(reference.ModifierName)
                        ? null
                        : reference.ModifierName));

            if (!movementData.IsSuccess) return Result<MovementResolution>.Error(
                new ErrorList([.. movementData.Errors]));

            resolved[reference] = movementData.Value.MovementDataID;
        }

        return Result<MovementResolution>.Success(new MovementResolution(resolved, []));
    }

    private static NamedEntity? MatchExactly(string rawName, List<NamedEntity> candidates) =>
        candidates.FirstOrDefault(c => c.Name.Trim().Equals(rawName.Trim(), StringComparison.OrdinalIgnoreCase));

    private static UnresolvedReference Unmatched(
        string rawName, ReferenceKind kind, List<NamedEntity> candidates)
    {
        IReadOnlyList<EntityMatch> matches =
        [
            .. candidates
                .Select(c => new EntityMatch(c.Id, c.Name, Math.Round(Similarity(rawName, c.Name), 2)))
                .Where(m => m.Confidence >= CandidateThreshold)
                .OrderByDescending(m => m.Confidence)
                .Take(MaxCandidates)
        ];

        return new UnresolvedReference(rawName, kind, matches);
    }

    private static IReadOnlyList<UnresolvedReference> Deduplicate(List<UnresolvedReference> unresolved) =>
        [.. unresolved
            .GroupBy(u => (u.Kind, u.RawName.Trim().ToLowerInvariant()))
            .Select(group => group.First())];

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

    private sealed record NamedEntity(Guid Id, string Name);
}
