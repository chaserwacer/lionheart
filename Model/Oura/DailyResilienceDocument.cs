namespace lionheart.Model.Oura.Dto;
public record OuraDailyResilienceDocument
{
    public string Id { get; init; } = string.Empty;
    public DateTime Day { get; init; }
    public required ResilienceContributorsDto Contributors { get; init; } 
    public string Level { get; init; } = string.Empty;
}

public record ResilienceContributorsDto
{
    public int SleepRecovery { get; init; }
    public int DaytimeRecovery { get; init; }
    public int Stress { get; init; }
}
