using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public interface IMovementModifierService
{
    Task<Result<List<MovementModifierDTO>>> GetMovementModifiersAsync(IdentityUser user);
    Task<MovementModifierDTO?> FindOrCreateMovementModifierAsync(IdentityUser user, string? name);
}

public class MovementModifierService : IMovementModifierService
{
    private readonly ModelContext _context;

    public MovementModifierService(ModelContext context)
    {
        _context = context;
    }

    public async Task<Result<List<MovementModifierDTO>>> GetMovementModifiersAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var modifiers = await _context.MovementModifiers
            .Where(mm => mm.UserID == userGuid)
            .OrderBy(mm => mm.Name)
            .ToListAsync();
        return Result<List<MovementModifierDTO>>.Success(modifiers.Select(mm => mm.ToDTO()).ToList());
    }

    /// <summary>
    /// Finds an existing MovementModifier by normalized name, or creates a new one.
    /// Names are normalized by trimming whitespace and converting to lowercase.
    /// Returns null if name is null or empty/whitespace.
    /// </summary>
    public async Task<MovementModifierDTO?> FindOrCreateMovementModifierAsync(IdentityUser user, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var userGuid = Guid.Parse(user.Id);
        var normalizedName = name.Trim().ToLower();

        // Try to find existing modifier with matching normalized name
        var existing = await _context.MovementModifiers
            .FirstOrDefaultAsync(mm => mm.UserID == userGuid && mm.Name.ToLower() == normalizedName);

        if (existing != null)
        {
            return existing.ToDTO();
        }

        // Create new modifier
        var modifier = new MovementModifier
        {
            MovementModifierID = Guid.NewGuid(),
            Name = normalizedName,
            UserID = userGuid
        };

        _context.MovementModifiers.Add(modifier);
        await _context.SaveChangesAsync();

        return modifier.ToDTO();
    }
}
