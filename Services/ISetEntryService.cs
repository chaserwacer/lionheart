using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

public interface ISetEntryService
{
    /// <summary>
    /// Add a set entry to a movement.
    /// </summary>
    /// <param name="user">The user who owns the movement.</param>
    /// <param name="request">The set entry creation request.</param>
    /// <returns>A result containing the created set entry.</returns>
    Task<Result<SetEntryDTO>> CreateSetEntryAsync(IdentityUser user, CreateSetEntryRequest request);

    /// <summary>
    /// Update a set entry.
    /// </summary>
    /// <param name="user">The user who owns the set entry.</param>
    /// <param name="request">The set entry update request.</param>
    /// <returns>A result containing the updated set entry.</returns>
    Task<Result<SetEntryDTO>> UpdateSetEntryAsync(IdentityUser user, UpdateSetEntryRequest request);

    /// <summary>
    /// Delete a set entry.
    /// </summary>
    /// <param name="user">The user who owns the set entry.</param>
    /// <param name="setEntryId">The set entry ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteSetEntryAsync(IdentityUser user, Guid setEntryId);
}