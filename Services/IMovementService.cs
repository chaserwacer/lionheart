using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

public interface IMovementService
{
    /// <summary>
    /// Get all movements for a specific training session.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="sessionId">The session ID to get movements for.</param>
    /// <returns>A result containing a list of movements.</returns>
    Task<Result<List<MovementDTO>>> GetMovementsAsync(IdentityUser user, Guid sessionId);

    /// <summary>
    /// Update the completion status of all movements in a training session.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="request">The request containing movement completion updates.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> UpdateMovementsCompletion(IdentityUser user, UpdateMovementsCompletionRequest request);


    /// <summary>
    /// Create a new movement within a training session.
    /// </summary>
    /// <param name="user">The user to create the movement for.</param>
    /// <param name="request">The movement creation request.</param>
    /// <returns>A result containing the created movement.</returns>
    Task<Result<MovementDTO>> CreateMovementAsync(IdentityUser user, CreateMovementRequest request);

    /// <summary>
    /// Update an existing movement.
    /// </summary>
    /// <param name="user">The user who owns the movement.</param>
    /// <param name="request">The movement update request.</param>
    /// <returns>A result containing the updated movement.</returns>
    Task<Result<MovementDTO>> UpdateMovementAsync(IdentityUser user, UpdateMovementRequest request);

    /// <summary>
    /// Delete a movement.
    /// </summary>
    /// <param name="user">The user who owns the movement.</param>
    /// <param name="movementId">The movement ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteMovementAsync(IdentityUser user, Guid movementId);

    /// <summary>
    /// Get all available movement bases.
    /// </summary>
    /// <returns>A result containing a list of movement bases.</returns>
    Task<Result<List<MovementBase>>> GetMovementBasesAsync();

    /// <summary>
    /// Create a new movement base.
    /// </summary>
    /// <param name="request">The movement base creation request.</param>
    /// <returns>A result containing the created movement base.</returns>
    Task<Result<MovementBase>> CreateMovementBaseAsync(CreateMovementBaseRequest request);
    /// <summary>
    /// Update the order of movements in a training session.
    /// This is used to change the order in which movements are displayed or performed.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    Task<Result> UpdateMovementOrder(IdentityUser user, UpdateMovementOrderRequest request);

    Task<Result> DeleteMovementBaseAsync(IdentityUser user, Guid movementBaseId);

}