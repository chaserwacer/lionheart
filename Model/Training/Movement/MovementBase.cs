using System.ComponentModel.DataAnnotations;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Training.Patterns;

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
    /// What the athlete has said this movement trains, if he has. The map file answers for a base
    /// he has not classified; <see cref="Standing"/> reads the two together.
    /// </summary>
    public StatedClassification Classification { get; private set; } = new StatedClassification.Unstated();

    /// <summary>The athlete's statement, else the map's entry for this base's name, else unclassified.</summary>
    public ClassificationStanding Standing(MovementPatternMap map) => Classification switch
    {
        StatedClassification.Stated stated => new ClassificationStanding.Classified(
            stated.Classification, new ClassificationSource.Athlete()),
        StatedClassification.Unstated => map.For(Name),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    public void State(MovementClassification classification) =>
        Classification = new StatedClassification.Stated(classification);

    /// <summary>Withdraws the athlete's statement, so the map's entry answers again.</summary>
    public void Unstate() => Classification = new StatedClassification.Unstated();

    public MovementBaseDTO ToDTO() => new(MovementBaseID, Name, Description, Standing(MovementPatternMap.Shipped));
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
