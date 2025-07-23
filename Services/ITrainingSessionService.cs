using Ardalis.Result;
using lionheart.Model.DTOs;

using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

public interface ITrainingSessionService
{
    /// <summary>
    /// Get all training sessions for a specific program.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="trainingProgramID">The program ID to get sessions for.</param>
    /// <returns>A result containing a list of training sessions.</returns>
    Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, Guid trainingProgramID);

    /// <summary>
    /// Get a specific training session by ID.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="trainingSessionID">The session ID to retrieve.</param>
    /// <returns>A result containing the training session.</returns>
    Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, GetTrainingSessionRequest request);

    /// <summary>
    /// Create a new training session within a program.
    /// </summary>
    /// <param name="user">The user to create the session for.</param>
    /// <param name="programId">The program ID to add the session to.</param>
    /// <param name="request">The session creation request.</param>
    /// <returns>A result containing the created session.</returns>
    Task<Result<TrainingSessionDTO>> CreateTrainingSessionAsync(IdentityUser user, CreateTrainingSessionRequest request);

    /// <summary>
    /// Update an existing training session.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="sessionId">The session ID to update.</param>
    /// <param name="request">The session update request.</param>
    /// <returns>A result containing the updated session.</returns>
    Task<Result<TrainingSessionDTO>> UpdateTrainingSessionAsync(IdentityUser user, UpdateTrainingSessionRequest request);

    /// <summary>
    /// Delete a training session.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="sessionId">The session ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);



    /// <summary>
    /// Create the next <see cref="Count"/> training sessions for a program, computing dates
    /// based on existing sessions (or StartDate if none exist).
    /// </summary>
    // Task<Result<List<TrainingSessionDTO>>> GenerateTrainingSessionsAsync(IdentityUser user, GenerateTrainingSessionsRequest request);

    Task<Result<TrainingSessionDTO>> CreateTrainingSessionFromJSON(IdentityUser user, TrainingSessionDTO trainingSessionDTO);

    /// <summary>
    /// Duplicate a training session, including all movements and set entries.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="trainingSessionID">The session ID to duplicate.</param>
    /// <returns>A result containing the duplicated training session.</returns>
    Task<Result<TrainingSessionDTO>> DuplicateTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);
    Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsByDateRangeAsync(IdentityUser user, DateRangeRequest dateRange);
}