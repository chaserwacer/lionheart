using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

public interface ITrainingProgramService
{
    /// <summary>
    /// Get all programs for a user.
    /// </summary>
    /// <param name="user">The user whose programs to retrieve.</param>
    /// <returns>A result containing a list of programs.</returns>
    Task<Result<List<TrainingProgramDTO>>> GetTrainingProgramsAsync(IdentityUser user);

    /// <summary>
    /// Get a specific program by ID for a user.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="programId">The program ID to retrieve.</param>
    /// <returns>A result containing the program.</returns>
    Task<Result<TrainingProgramDTO>> GetTrainingProgramAsync(IdentityUser user, Guid programId);

    /// <summary>
    /// Create a new training program for a user.
    /// </summary>
    /// <param name="user">The user to create the program for.</param>
    /// <param name="request">The program creation request.</param>
    /// <returns>A result containing the created program.</returns>
    Task<Result<TrainingProgramDTO>> CreateTrainingProgramAsync(IdentityUser user, CreateTrainingProgramRequest request);

    /// <summary>
    /// Update an existing training program.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="request">The program update request.</param>
    /// <returns>A result containing the updated program.</returns>
    Task<Result<TrainingProgramDTO>> UpdateTrainingProgramAsync(IdentityUser user, UpdateTrainingProgramRequest request);

    /// <summary>
    /// Delete a training program.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="programId">The program ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteTrainingProgramAsync(IdentityUser user, Guid programId);

    Task<Result<TrainingProgramDTO>> GeneratePopulatedTrainingProgramAsync(IdentityUser user, GeneratePopulatedTrainingProgramRequest request);
    Task<Result<TrainingProgramDTO>> CreateTrainingProgramFromJSON(IdentityUser user, TrainingProgramDTO trainingProgramDTO);
    
}