
using System.ComponentModel.DataAnnotations;
namespace lionheart.Model.DTOs;
public class CreateWellnessStateRequest
{
    [Required]
    public required string Date { get; init; }

    [Required]
    [Range(1, 5)]
    public required int Energy { get; init; }

    [Required]
    [Range(1, 5)]
    public required int Motivation { get; init; }

    [Required]
    [Range(1, 5)]
    public required int Mood { get; init; }

    [Required]
    [Range(1, 5)]
    public required int Stress { get; init; }
}




