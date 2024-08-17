namespace lionheart.Model.Oura.Dto;
public record OuraDailyReadinessDocument{
    public string Id { get; init; } = string.Empty;
    public required ReadinessContributorsDto Contributors { get; init; }
    public int? Score { get; init; }
    public int? TemperatureDeviation { get; init; }
    public DateTime Day { get; init; }
}
public record ReadinessContributorsDto
{
    public int? ActivityBalance { get; init; }
    public int? BodyTemperature { get; init; }
    public int? HrvBalance { get; init; }
    public int? PreviousDayActivity { get; init; }
    public int? PreviousNight { get; init; }
    public int? RecoveryIndex { get; init; }
    public int? RestingHeartRate { get; init; }
    public int? SleepBalance { get; init; }
}