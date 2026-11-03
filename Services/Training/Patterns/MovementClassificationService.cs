using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Patterns;

public interface IMovementClassificationService
{
    /// <summary>States what a movement base trains, replacing the map file's entry for it.</summary>
    Task<Result<MovementBaseDTO, Problem>> ClassifyAsync(
        IdentityUser user, Guid movementBaseId, ClassifyMovementRequest request, CancellationToken ct);

    /// <summary>Withdraws the athlete's statement, so the map file answers for the base again.</summary>
    Task<Result<MovementBaseDTO, Problem>> WithdrawAsync(IdentityUser user, Guid movementBaseId, CancellationToken ct);
}

/// <summary>
/// The athlete's statements of what his movements train. Another athlete's base is a 404, the rule
/// every training route follows.
/// </summary>
public sealed class MovementClassificationService : IMovementClassificationService
{
    private readonly ModelContext _context;

    public MovementClassificationService(ModelContext context) => _context = context;

    public Task<Result<MovementBaseDTO, Problem>> ClassifyAsync(
        IdentityUser user, Guid movementBaseId, ClassifyMovementRequest request, CancellationToken ct) =>
        request.ToClassification()
            .BindAsync(classification => OwnedAsync(user, movementBaseId, ct)
                .MapAsync(movementBase =>
                {
                    movementBase.State(classification);
                    return movementBase;
                }))
            .MapAsync(movementBase => CommitAsync(movementBase, ct));

    public Task<Result<MovementBaseDTO, Problem>> WithdrawAsync(IdentityUser user, Guid movementBaseId, CancellationToken ct) =>
        OwnedAsync(user, movementBaseId, ct)
            .MapAsync(movementBase =>
            {
                movementBase.Unstate();
                return movementBase;
            })
            .MapAsync(movementBase => CommitAsync(movementBase, ct));

    private async Task<Result<MovementBase, Problem>> OwnedAsync(IdentityUser user, Guid movementBaseId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var movementBase = await _context.MovementBases
            .FirstOrDefaultAsync(mb => mb.MovementBaseID == movementBaseId && mb.UserID == userId, ct);

        return movementBase is null
            ? Result<MovementBase, Problem>.Failure(new Problem.NotFound("Movement base not found."))
            : Result<MovementBase, Problem>.Success(movementBase);
    }

    private async Task<MovementBaseDTO> CommitAsync(MovementBase movementBase, CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
        return movementBase.ToDTO();
    }
}
