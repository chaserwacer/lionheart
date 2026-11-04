using System.ComponentModel.DataAnnotations;
using lionheart.Model.Training.Patterns;

/// <summary>
/// Base definition of a physical movement: its name, a description, and what it trains.
/// </summary>
public class MovementBase
{
    [Key]
    public required Guid MovementBaseID { get; init; }
    public required string Name { get; set; } = string.Empty;
    public required Guid UserID { get; init; }
    public required string Description { get; set; } = string.Empty;

    /// <summary>
    /// What the athlete has said this movement trains, if he has. The shipped map answers for a base
    /// he has not classified; <c>MovementPatternMap.StandingOf</c> reads the two together.
    /// </summary>
    public StatedClassification Classification { get; private set; } = new StatedClassification.Unstated();

    public void State(MovementClassification classification) =>
        Classification = new StatedClassification.Stated(classification);

    /// <summary>Withdraws the athlete's statement, so the map's entry answers again.</summary>
    public void Unstate() => Classification = new StatedClassification.Unstated();

    /// <param name="standing">What the base trains as the map reads it with the athlete's statement.</param>
    public MovementBaseDTO ToDTO(ClassificationStanding standing) => new(MovementBaseID, Name, Description, standing);
}

public record CreateMovementBaseRequest(
    [Required]string Name,
    [Required(AllowEmptyStrings = true)]string Description
);
public record MovementBaseDTO(
    Guid MovementBaseID,
    string Name,
    string Description,
    ClassificationStanding Classification
);
public record UpdateMovementBaseRequest(
    [Required]Guid MovementBaseID,
    [Required]string Name,
    [Required(AllowEmptyStrings = true)]string Description
);
