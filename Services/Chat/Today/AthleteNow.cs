namespace lionheart.Services.Chat.Today;

/// <summary>
/// The athlete's clock: the instant a message arrives, on the wall clock he lives by, and the days
/// it puts him in.
/// </summary>
/// <remarks>
/// Every "today" the chat states is read from this, never from UTC. The server runs on his own
/// machine, so its zone is his; a UTC day ends at seven in the evening in his winter, which is when
/// he trains and when he asks about it.
/// </remarks>
public sealed record AthleteNow(DateTimeOffset Local)
{
    public static AthleteNow Of(TimeProvider clock) => new(clock.GetLocalNow());

    public DateOnly Today => DateOnly.FromDateTime(Local.DateTime);

    public DateOnly Yesterday => Today.AddDays(-1);

    /// <summary>The Monday the week runs from, matching the score's Monday-to-Sunday weeks.</summary>
    public DateOnly WeekStart => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
}
