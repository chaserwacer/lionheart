using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Interpretation;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.Interpretation;

public interface IInterpretationResolver
{
    /// <summary>
    /// Resolves <paramref name="parsed"/> against what the athlete already has, settling the
    /// names in <paramref name="confirmed"/> the way they asked. The default is the first pass,
    /// where nothing has been confirmed because the athlete has not been asked.
    /// </summary>
    Task<Result<ResolutionOutcome, Problem>> ResolveAsync(
        IdentityUser user, ParsedInterpretation parsed, ConfirmedReferences confirmed = default);
}

/// <summary>
/// Stage two: drafts become real domain records. Movement names become ids and proposed facet
/// values become canonical vocabulary; anything still ambiguous stops the run and goes back to
/// the user rather than being committed on a guess.
/// </summary>
public sealed class InterpretationResolver : IInterpretationResolver
{
    private readonly IMovementResolver _movements;
    private readonly IFacetResolver _facets;

    public InterpretationResolver(IMovementResolver movements, IFacetResolver facets)
    {
        _movements = movements;
        _facets = facets;
    }

    // Movements first, and the early return matters: facet resolution stages new vocabulary on
    // the shared context, which a later unrelated save would flush. Bailing out before it keeps
    // an abandoned interpretation from leaving canonicals behind.
    public Task<Result<ResolutionOutcome, Problem>> ResolveAsync(
        IdentityUser user, ParsedInterpretation parsed, ConfirmedReferences confirmed = default) =>
        _movements.ResolveAsync(user, CollectMovementReferences(parsed), confirmed)
            .BindAsync(movements => movements.Unresolved.Count > 0
                ? Task.FromResult(Result<ResolutionOutcome, Problem>.Success(
                    new Ambiguous(movements.Unresolved)))
                : _facets.ResolveAsync(CollectProposedFacets(parsed))
                    .MapAsync(vocabulary => Build(user, parsed, movements, vocabulary)));

    private static ResolutionOutcome Build(
        IdentityUser user,
        ParsedInterpretation parsed,
        MovementResolution movements,
        ResolvedVocabulary vocabulary)
    {
        var userGuid = Guid.Parse(user.Id);
        var sessions = new List<CompletedTrainingSession>();
        var contexts = new List<Context>();

        foreach (var entry in parsed.Entries)
        {
            var decoration = Decorate(entry, vocabulary);
            if (entry is DraftSession session)
            {
                sessions.Add(BuildSession(session, userGuid, decoration, vocabulary, movements));
            }
            else
            {
                contexts.Add(BuildContext(entry, userGuid, decoration));
            }
        }

        return new Resolved(new ResolvedInterpretation(sessions, contexts, parsed.Warnings));
    }

    private static IReadOnlyList<MovementReference> CollectMovementReferences(ParsedInterpretation parsed) =>
        [.. parsed.Entries
            .OfType<DraftSession>()
            .SelectMany(session => session.Sets)
            .Select(MovementOf)
            .OfType<MovementReference>()];

    private static IReadOnlyList<ProposedFacet> CollectProposedFacets(ParsedInterpretation parsed) =>
        [.. parsed.Entries.SelectMany(entry => entry.Decoration.Facets
            .Concat(entry is DraftSession session
                ? session.Sets.SelectMany(set => set.Facets)
                : []))];

    private static MovementReference? MovementOf(DraftSet set) => set switch
    {
        DraftLiftSet lift => lift.Movement,
        DraftDistanceTimeSet distanceTime => distanceTime.Movement,
        _ => null
    };

    private static DecorationState Decorate(DraftEntry entry, ResolvedVocabulary vocabulary) =>
        new Decorated(new Decoration(
            Facets: vocabulary.Canonicalize(entry.Decoration.Facets),
            FreeTags: entry.Decoration.FreeTags,
            Valence: entry.Decoration.Valence,
            SummaryText: entry.Decoration.SummaryText,
            DecoratorVersion: DecoratorVersion.Current));

    private static CompletedTrainingSession BuildSession(
        DraftSession draft,
        Guid userId,
        DecorationState decoration,
        ResolvedVocabulary vocabulary,
        MovementResolution movements) =>
        new(
            Sets: [.. draft.Sets.Select(set => BuildSet(set, vocabulary, movements))],
            Period: draft.Period,
            Effort: draft.Effort,
            Decoration: decoration)
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = userId,
            Notes = draft.Notes,
            // The athlete supplied the words; the engine supplied the structure — which is
            // exactly what this axis records.
            Provenance = Provenance.AiAssisted,
            Membership = new Standalone(),
            CreatedAt = DateTime.UtcNow
        };

    private static SetEntry BuildSet(
        DraftSet draft, ResolvedVocabulary vocabulary, MovementResolution movements)
    {
        var facets = vocabulary.Canonicalize(draft.Facets);

        return draft switch
        {
            DraftLiftSet lift => new LiftSet(
                movements.Resolved[lift.Movement], lift.Reps, lift.Weight, lift.Unit, lift.Rpe)
            {
                Source = draft.Source,
                Facets = facets
            },
            DraftDistanceTimeSet distanceTime => new DistanceTimeSet(
                movements.Resolved[distanceTime.Movement], distanceTime.Distance,
                distanceTime.Unit, distanceTime.Duration, distanceTime.Rpe)
            {
                Source = draft.Source,
                Facets = facets
            },
            DraftTrailRunSet trailRun => new TrailRunSet(
                trailRun.Distance, trailRun.Unit, trailRun.Duration, trailRun.ElevationGainMeters, trailRun.Rpe)
            {
                Source = draft.Source,
                Facets = facets
            },
            _ => new FreeformSet { Source = draft.Source, Facets = facets }
        };
    }

    private static Context BuildContext(DraftEntry draft, Guid userId, DecorationState decoration) =>
        draft is DraftExertionContext exertion
            ? new ExertionContext(exertion.Effort)
            {
                ContextId = Guid.NewGuid(),
                UserId = userId,
                Period = draft.Period,
                Notes = draft.Notes,
                Decoration = decoration
            }
            : new StateContext
            {
                ContextId = Guid.NewGuid(),
                UserId = userId,
                Period = draft.Period,
                Notes = draft.Notes,
                Decoration = decoration
            };
}
