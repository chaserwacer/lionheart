using lionheart.Model.Cardio;
using lionheart.Model.Training.Score;

namespace lionheart.Services.Cardio;

/// <summary>A window's cardio from the cardio report's sessions, or why it is unknown.</summary>
public static class CardioMeasures
{
    /// <param name="sessions">The cardio sessions that began inside the window.</param>
    /// <param name="unread">
    /// The window's workouts declined for too little heart rate: counted in nothing, but session time
    /// with nothing behind it, so a window mostly made of them is unknown rather than empty.
    /// </param>
    /// <param name="heartRateDays">Days of the window with any stored heart rate.</param>
    public static CardioMeasure Of(
        IReadOnlyList<CardioSession> sessions, IReadOnlyList<CardioSession> unread, MaxHeartRate? maximum, int heartRateDays)
    {
        if (heartRateDays is 0) return new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate);
        if (maximum is null) return new CardioMeasure.Unknown(CardioUnknownReason.NoMaximumHeartRate);

        var total = sessions.Sum(session => session.Minutes) + unread.Sum(session => session.Minutes);
        var covered = sessions.Where(session => session.HeartRate is SessionHeartRate.Recorded).Sum(session => session.Minutes);
        if (total > 0 && covered < CardioAnalysis.ZoneCoverageFloor * total)
            return new CardioMeasure.Unknown(CardioUnknownReason.SessionsUnrecorded);

        var zones = sessions
            .Select(session => session.HeartRate is SessionHeartRate.Recorded { Zones: { } recorded } ? recorded : ZoneMinutes.None)
            .Aggregate(ZoneMinutes.None, (sum, next) => sum + next);

        return new CardioMeasure.Measured(
            zones.Zone2, ScoreRules.Zone2Minutes,
            zones.Zone3 + zones.Zone4 + zones.Zone5, ScoreRules.HighIntensityMinutes,
            sessions.Count, heartRateDays);
    }
}
