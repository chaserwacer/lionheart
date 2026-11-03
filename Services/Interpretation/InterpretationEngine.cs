using System.Diagnostics;
using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Interpretation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Interpretation;

public interface IInterpretationEngine
{
    Task<Result<InterpretationOutcome, Problem>> InterpretAsync(IdentityUser user, InterpretationRequest request);

    /// <summary>
    /// Resumes a run that came back <see cref="AwaitingConfirmation"/>, entering at Resolve with
    /// the athlete's answers.
    /// </summary>
    Task<Result<InterpretationOutcome, Problem>> ConfirmAsync(IdentityUser user, ConfirmationRequest request);
}

/// <summary>
/// Parse → Resolve → Commit, the single path by which free text becomes training data.
/// A run that has to ask the athlete about a name resumes at Resolve, so the two entry points
/// differ only in where they join the same chain.
/// Every decoration it writes is stamped with <see cref="DecoratorVersion"/>, so the same
/// engine can later re-run over stale rows to upgrade freeform entries or re-annotate them.
/// </summary>
public sealed class InterpretationEngine : IInterpretationEngine
{
    private readonly ModelContext _context;
    private readonly IInterpretationParser _parser;
    private readonly IInterpretationResolver _resolver;
    private readonly IInterpretationCommitter _committer;

    public InterpretationEngine(
        ModelContext context,
        IInterpretationParser parser,
        IInterpretationResolver resolver,
        IInterpretationCommitter committer)
    {
        _context = context;
        _parser = parser;
        _resolver = resolver;
        _committer = committer;
    }

    public async Task<Result<InterpretationOutcome, Problem>> InterpretAsync(
        IdentityUser user, InterpretationRequest request)
    {
        var knownMovements = await DescribeKnownMovementsAsync(user);

        return await _parser.ParseAsync(request, knownMovements)
            .BindAsync(draft => ResolveAsync(user, draft, ConfirmedReferences.None));
    }

    /// <summary>
    /// The resume path enters here, at Resolve. The draft the client echoed back is the one the
    /// parser already produced, so the model call the parse costs is not paid a second time.
    /// </summary>
    public Task<Result<InterpretationOutcome, Problem>> ConfirmAsync(
        IdentityUser user, ConfirmationRequest request) =>
        Confirmable(request)
            .BindAsync(confirmable => ResolveAsync(
                user, confirmable.Draft, ConfirmedReferences.From(confirmable.Choices)));

    private static Result<ConfirmationRequest, Problem> Confirmable(ConfirmationRequest request) =>
        Result<ConfirmationRequest, Problem>
            .Success(request)
            .Ensure(
                confirmable => confirmable.Draft.Entries.Count > 0,
                _ => new Problem.Invalid(
                    nameof(request.Draft), "The draft being confirmed has nothing in it."));

    /// <summary>
    /// The draft is carried alongside the resolution because an ambiguous run hands it back to
    /// the athlete unchanged — that is what makes the confirmation round free of a re-parse.
    /// </summary>
    private Task<Result<InterpretationOutcome, Problem>> ResolveAsync(
        IdentityUser user, ParsedInterpretation draft, ConfirmedReferences confirmed) =>
        _resolver.ResolveAsync(user, draft, confirmed)
            .BindAsync(resolution => ConcludeAsync(draft, resolution));

    private Task<Result<InterpretationOutcome, Problem>> ConcludeAsync(
        ParsedInterpretation draft, ResolutionOutcome resolution) =>
        resolution switch
        {
            Ambiguous ambiguous => Task.FromResult(Result<InterpretationOutcome, Problem>.Success(
                new AwaitingConfirmation(draft, ambiguous.References))),
            Resolved resolved => _committer.CommitAsync(resolved.Interpretation)
                .MapAsync(committed => (InterpretationOutcome)committed),
            _ => throw new UnreachableException()
        };

    /// <summary>
    /// Names the athlete already uses, given to the parser so it reaches for those spellings
    /// instead of coining a synonym the resolver would then have to bounce back to the user.
    /// </summary>
    private async Task<string> DescribeKnownMovementsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var bases = await _context.MovementBases.Where(mb => mb.UserID == userGuid).Select(mb => mb.Name).ToListAsync();
        var equipment = await _context.Equipments.Where(e => e.UserID == userGuid).Select(e => e.Name).ToListAsync();
        var modifiers = await _context.MovementModifiers.Where(mm => mm.UserID == userGuid).Select(mm => mm.Name).ToListAsync();

        var sections = new List<string>();
        if (bases.Count > 0) sections.Add($"Movements this athlete logs: {string.Join(", ", bases)}");
        if (equipment.Count > 0) sections.Add($"Equipment they own: {string.Join(", ", equipment)}");
        if (modifiers.Count > 0) sections.Add($"Modifiers they use: {string.Join(", ", modifiers)}");

        return sections.Count == 0
            ? string.Empty
            : "Use these existing names verbatim when the text means one of them:\n" + string.Join("\n", sections);
    }
}
