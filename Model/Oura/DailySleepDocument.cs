namespace lionheart.Model.Oura.Dto;
public record OuraDailySleepDocument
{
    public string Id { get; init; } = string.Empty;
    public required SleepContributorsDto Contributors { get; init; }
    public DateTime Day { get; init; }
    public int? Score { get; init; }

}

public record SleepContributorsDto
{
    public int? DeepSleep { get; init; }   
    public int? Efficiency { get; init; }  
    public int? Latency { get; init; }     
    public int? RemSleep { get; init; }    
    public int? Restfulness { get; init; } 
    public int? Timing { get; init; }      
    public int? TotalSleep { get; init; }  
}