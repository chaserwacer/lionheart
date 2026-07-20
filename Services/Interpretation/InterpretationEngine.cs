using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Interpretation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Interpretation;

public interface IInterpretationEngine
{
    Task<Result<InterpretationOutcome>> InterpretAsync(IdentityUser user, InterpretationRequest request);
}

/// <summary>
/// Parse → Resolve → Commit, the single path by which free text becomes training data.
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

    public async Task<Result<InterpretationOutcome>> InterpretAsync(
        IdentityUser user, InterpretationRequest request)
    {
        var knownMovements = await DescribeKnownMovementsAsync(user);

        var parsed = await _parser.ParseAsync(request, knownMovements);
        if (!parsed.IsSuccess) return Result<InterpretationOutcome>.Error(new ErrorList([.. parsed.Errors]));

        var resolution = await _resolver.ResolveAsync(user, parsed.Value);
        if (!resolution.IsSuccess) return Result<InterpretationOutcome>.Error(new ErrorList([.. resolution.Errors]));

        return resolution.Value switch
        {
            Ambiguous ambiguous => Result<InterpretationOutcome>.Success(
                new AwaitingConfirmation(parsed.Value, ambiguous.References)),
            Resolved resolved => await CommitAsync(resolved),
            _ => Result<InterpretationOutcome>.Error("Unrecognized resolution outcome.")
        };
    }

    private async Task<Result<InterpretationOutcome>> CommitAsync(Resolved resolved)
    {
        var committed = await _committer.CommitAsync(resolved.Interpretation);
        return committed.IsSuccess
            ? Result<InterpretationOutcome>.Success(committed.Value)
            : Result<InterpretationOutcome>.Error(new ErrorList([.. committed.Errors]));
    }

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
