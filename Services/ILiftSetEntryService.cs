using Ardalis.Result;
using lionheart.Model.Training.SetEntry;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

public interface ILiftSetEntryService
{
    /// <summary>
    /// Add a lift set entry to a movement.
    /// </summary>
    /// <param name="user">The user who owns the movement.</param>
    /// <param name="request">The lift set entry creation request.</param>
    /// <returns>A result containing the created lift set entry.</returns>
    Task<Result<LiftSetEntryDTO>> CreateLiftSetEntryAsync(IdentityUser user, CreateLiftSetEntryRequest request);

    /// <summary>
    /// Update a lift set entry.
    /// </summary>
    /// <param name="user">The user who owns the set entry.</param>
    /// <param name="request">The lift set entry update request.</param>
    /// <returns>A result containing the updated lift set entry.</returns>
    Task<Result<LiftSetEntryDTO>> UpdateLiftSetEntryAsync(IdentityUser user, UpdateLiftSetEntryRequest request);

    /// <summary>
    /// Delete a lift set entry.
    /// </summary>
    /// <param name="user">The user who owns the set entry.</param>
    /// <param name="setEntryId">The set entry ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteLiftSetEntryAsync(IdentityUser user, Guid setEntryId);
}
