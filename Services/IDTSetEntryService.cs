using Ardalis.Result;
using lionheart.Model.TrainingProgram.SetEntry;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

public interface IDTSetEntryService
{
    /// <summary>
    /// Add a distance/time set entry to a movement.
    /// </summary>
    /// <param name="user">The user who owns the movement.</param>
    /// <param name="request">The DT set entry creation request.</param>
    /// <returns>A result containing the created DT set entry.</returns>
    Task<Result<DTSetEntryDTO>> CreateDTSetEntryAsync(IdentityUser user, CreateDTSetEntryRequest request);

    /// <summary>
    /// Update a distance/time set entry.
    /// </summary>
    /// <param name="user">The user who owns the set entry.</param>
    /// <param name="request">The DT set entry update request.</param>
    /// <returns>A result containing the updated DT set entry.</returns>
    Task<Result<DTSetEntryDTO>> UpdateDTSetEntryAsync(IdentityUser user, UpdateDTSetEntryRequest request);

    /// <summary>
    /// Delete a distance/time set entry.
    /// </summary>
    /// <param name="user">The user who owns the set entry.</param>
    /// <param name="setEntryId">The set entry ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteDTSetEntryAsync(IdentityUser user, Guid setEntryId);
}
