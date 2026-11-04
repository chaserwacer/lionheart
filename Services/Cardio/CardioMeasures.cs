using lionheart.Model.Cardio;

namespace lionheart.Services.Cardio;

/// <summary>A window's cardio from the cardio report's sessions, or why it is unknown.</summary>
public static class CardioMeasures
{
    /// <param name="sessions">The cardio sessions that began inside the window.</param>
    /// <param name="unread">
    /// The window's workouts declined for too little heart rate: counted in nothing, but where they
    /// were runs or rides, session time with nothing behind it, so a window mostly made of them is
    /// unknown rather than empty.
    /// </param>
    /// <param name="heartRateDays">Days of the window with any stored heart rate from the ring.</param>
    /// <remarks>
    /// A week with the ring off is still measured when its sessions carry the heart rate their
    /// recording app wrote: a watch-recorded run is the better record of the two.
    /// </remarks>
    public static CardioMeasure Of(
        IReadOnlyList<CardioSession> sessions, IReadOnlyList<CardioSession> unread, MaxHeartRate? maximum, int heartRateDays)
    {
        if (heartRateDays is 0 && !sessions.Any(session => session.HeartRate is SessionHeartRate.Recorded))
            return new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate);
        if (maximum is null) return new CardioMeasure.Unknown(CardioUnknownReason.NoMaximumHeartRate);

        // Only an unread run or ride is training that may have gone uncounted; an unread walk the
        // ring guessed at is more often a walk to the shops, and must not unknow a week of real data.
        var total = sessions.Sum(session => session.Minutes)
                    + unread.Where(session => session.Modality is CardioModality.Endurance).Sum(session => session.Minutes);
        var covered = sessions.Where(session => session.HeartRate is SessionHeartRate.Recorded).Sum(session => session.Minutes);
        if (total > 0 && covered < CardioAnalysis.ZoneCoverageFloor * total)
            return new CardioMeasure.Unknown(CardioUnknownReason.SessionsUnrecorded);

        var zones = sessions
            .Select(session => session.HeartRate is SessionHeartRate.Recorded { Zones: { } recorded } ? recorded : ZoneMinutes.None)
            .Aggregate(ZoneMinutes.None, (sum, next) => sum + next);
        var quality = sessions.Where(session => session.QualityMinutes > 0).ToList();

        return new CardioMeasure.Measured(
            zones.Zone2,
            quality.Sum(session => session.QualityMinutes),
            quality.Count,
            sessions.Count,
            heartRateDays);
    }
}
