using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace lionheart.Model.DTOs;
public class CreateActivityRequest
{
    [Required]
    public required DateTime DateTime { get; set; }

    [Required]
    public required int TimeInMinutes { get; set; }

    [Required]
    public required int CaloriesBurned { get; set; }

    [Required]
    public required string Name { get; set; }

    
    public string UserSummary { get; set; }= string.Empty;

    [Required]
    public required int AccumulatedFatigue { get; set; }

    [Required]
    public required int DifficultyRating { get; set; }

    [Required]
    public required int EngagementRating { get; set; }

    [Required]
    public required int ExternalVariablesRating { get; set; }
}

public class CreateRunWalkRequest : CreateActivityRequest
{
    [Required]
    public required double Distance { get; set; }

    [Required]
    public required int ElevationGain { get; set; }

    [Required]
    public required int AveragePaceInSeconds { get; set; }

    [Required]
    public required List<int> MileSplitsInSeconds { get; set; }


    public string RunType { get; set; }= string.Empty;
}

public class CreateRideRequest : CreateActivityRequest
{
    [Required]
    public required double Distance { get; set; }

    [Required]
    public required int ElevationGain { get; set; }

    [Required]
    public required int AveragePower { get; set; }

    [Required]
    public required double AverageSpeed { get; set; }

    
    public string RideType { get; set; }= string.Empty;
}

public class CreateLiftRequest : CreateActivityRequest
{
    [Required]
    public required int Tonnage { get; set; }


    public  string LiftType { get; set; }= string.Empty;

    public  string LiftFocus { get; set; }= string.Empty;

    [Required]
    public required int QuadSets { get; set; }

    [Required]
    public required int HamstringSets { get; set; }

    [Required]
    public required int BicepSets { get; set; }

    [Required]
    public required int TricepSets { get; set; }

    [Required]
    public required int ShoulderSets { get; set; }

    [Required]
    public required int BackSets { get; set; }

    [Required]
    public required int ChestSets { get; set; }
}



public record ActivityTypeRatioDto(int NumberLifts, int NumberRunWalks, int NumberRides);
public record MuscleSetsDto(int QuadSets, int HamstringSets, int BicepSets, int TricepSets, int ShoulderSets, int ChestSets, int BackSets);
