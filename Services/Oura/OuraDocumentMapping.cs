using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Oura.Wire;

namespace lionheart.Services.Oura;

/// <summary>
/// Oura's payloads turned into documents this application owns, or a reason the payload could not
/// be read.
/// </summary>
/// <remarks>
/// The one place either path — the live pull or the archive import — interprets an Oura payload.
/// The archived <c>.jsonl</c> files are recorded Oura responses, so a test that maps a real
/// archived record is also a test of what the live pull would do with it. That is what makes the
/// pull path verifiable without the athlete's token.
/// <para>
/// Each mapper is one expression. Required fields are read through <see cref="WireReader"/>, which
/// names each one and accumulates every failure; fields Oura legitimately leaves empty pass
/// straight through as the absent values they are.
/// </para>
/// </remarks>
internal static class OuraDocumentMapping
{
    extension(WireDailyActivity wire)
    {
        public Result<OuraDailyActivity, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraDailyActivity(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new DailyActivityReading(
                    wire.Score,
                    read.Object(wire.Contributors, "contributors", c => new ActivityContributors(
                        c.MeetDailyTargets,
                        c.MoveEveryHour,
                        c.RecoveryTime,
                        c.StayActive,
                        c.TrainingFrequency,
                        c.TrainingVolume),
                        NoActivityContributors),
                    read.Int(wire.Steps, "steps"),
                    read.Int(wire.ActiveCalories, "active_calories"),
                    read.Int(wire.TotalCalories, "total_calories"),
                    read.Int(wire.TargetCalories, "target_calories"),
                    read.Int(wire.EquivalentWalkingDistance, "equivalent_walking_distance"),
                    read.Int(wire.MetersToTarget, "meters_to_target"),
                    read.Int(wire.TargetMeters, "target_meters"),
                    read.Int(wire.InactivityAlerts, "inactivity_alerts"),
                    read.Double(wire.AverageMetMinutes, "average_met_minutes"),
                    read.Int(wire.LowActivityMetMinutes, "low_activity_met_minutes"),
                    read.Int(wire.MediumActivityMetMinutes, "medium_activity_met_minutes"),
                    read.Int(wire.HighActivityMetMinutes, "high_activity_met_minutes"),
                    read.Int(wire.SedentaryMetMinutes, "sedentary_met_minutes"),
                    read.Int(wire.RestingTime, "resting_time"),
                    read.Int(wire.SedentaryTime, "sedentary_time"),
                    read.Int(wire.LowActivityTime, "low_activity_time"),
                    read.Int(wire.MediumActivityTime, "medium_activity_time"),
                    read.Int(wire.HighActivityTime, "high_activity_time"),
                    read.Int(wire.NonWearTime, "non_wear_time"))));
        }
    }

    extension(WireDailyReadiness wire)
    {
        public Result<OuraDailyReadiness, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraDailyReadiness(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new DailyReadinessReading(
                    wire.Score,
                    wire.TemperatureDeviation,
                    wire.TemperatureTrendDeviation,
                    read.Object(wire.Contributors, "contributors", c => new ReadinessContributors(
                        c.ActivityBalance,
                        c.BodyTemperature,
                        c.HrvBalance,
                        c.PreviousDayActivity,
                        c.PreviousNight,
                        c.RecoveryIndex,
                        c.RestingHeartRate,
                        c.SleepBalance,
                        c.SleepRegularity),
                        NoReadinessContributors))));
        }
    }

    extension(WireDailySleep wire)
    {
        public Result<OuraDailySleep, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraDailySleep(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new DailySleepReading(
                    wire.Score,
                    read.Object(wire.Contributors, "contributors", c => new SleepContributors(
                        c.DeepSleep,
                        c.Efficiency,
                        c.Latency,
                        c.RemSleep,
                        c.Restfulness,
                        c.Timing,
                        c.TotalSleep),
                        NoSleepContributors))));
        }
    }

    extension(WireDailySpo2 wire)
    {
        public Result<OuraDailySpo2, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            // Oura nulls the whole spo2_percentage object rather than its average, so the
            // average is reached through it rather than from it. The breathing index is absent
            // independently, which is why it is read straight across.
            return read.Build(() => new OuraDailySpo2(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new DailySpo2Reading(wire.Spo2Percentage?.Average, wire.BreathingDisturbanceIndex)));
        }
    }

    extension(WireDailyStress wire)
    {
        public Result<OuraDailyStress, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraDailyStress(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new DailyStressReading(wire.StressHigh, wire.RecoveryHigh, wire.DaySummary)));
        }
    }

    extension(WireDailyResilience wire)
    {
        public Result<OuraDailyResilience, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraDailyResilience(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new DailyResilienceReading(
                    read.Text(wire.Level, "level"),
                    read.Object(wire.Contributors, "contributors", c => new ResilienceContributors(
                        c.SleepRecovery,
                        c.DaytimeRecovery,
                        c.Stress),
                        NoResilienceContributors))));
        }
    }

    extension(WireSleep wire)
    {
        public Result<OuraSleepPeriod, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraSleepPeriod(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new SleepPeriodReading(
                    read.Span(wire.BedtimeStart, wire.BedtimeEnd, "bedtime_start", "bedtime_end"),
                    read.Text(wire.Type, "type"),
                    read.Int(wire.Period, "period"),
                    read.Int(wire.TimeInBed, "time_in_bed"),
                    read.Int(wire.TotalSleepDuration, "total_sleep_duration"),
                    read.Int(wire.DeepSleepDuration, "deep_sleep_duration"),
                    read.Int(wire.LightSleepDuration, "light_sleep_duration"),
                    read.Int(wire.RemSleepDuration, "rem_sleep_duration"),
                    read.Int(wire.AwakeTime, "awake_time"),
                    read.Int(wire.Latency, "latency"),
                    read.Int(wire.Efficiency, "efficiency"),
                    read.Int(wire.RestlessPeriods, "restless_periods"),
                    wire.AverageHeartRate,
                    wire.LowestHeartRate,
                    wire.AverageHrv,
                    wire.AverageBreath,
                    wire.ReadinessScoreDelta,
                    wire.SleepScoreDelta,
                    wire.SleepAlgorithmVersion,
                    wire.SleepAnalysisReason,
                    read.Text(wire.SleepPhase5Min, "sleep_phase_5_min"),
                    read.Series(wire.HeartRate, "heart_rate"),
                    read.Series(wire.Hrv, "hrv"))));
        }
    }

    extension(WireWorkout wire)
    {
        public Result<OuraWorkout, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraWorkout(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new WorkoutReading(
                    read.Span(wire.StartDatetime, wire.EndDatetime, "start_datetime", "end_datetime"),
                    read.Text(wire.Activity, "activity"),
                    read.Text(wire.Intensity, "intensity"),
                    read.Text(wire.Source, "source"),
                    wire.Label,
                    wire.Calories,
                    wire.Distance)));
        }
    }

    extension(WireSession wire)
    {
        public Result<OuraSession, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraSession(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new SessionReading(
                    read.Span(wire.StartDatetime, wire.EndDatetime, "start_datetime", "end_datetime"),
                    read.Text(wire.Type, "type"),
                    wire.Mood,
                    read.Series(wire.HeartRate, "heart_rate"),
                    read.Series(wire.HeartRateVariability, "heart_rate_variability"),
                    read.Series(wire.MotionCount, "motion_count"))));
        }
    }

    extension(WireTag wire)
    {
        /// <remarks>
        /// A tag marks a moment rather than a span, so its period is an
        /// <see cref="Model.Analysis.Instant"/> — the one event collection here that has no end.
        /// </remarks>
        public Result<OuraTag, OuraDocumentDefects> ToDocument(Guid userId)
        {
            var read = new WireReader();

            return read.Build(() => new OuraTag(
                read.Id(wire.Id),
                userId,
                read.Day(wire.Day),
                new TagReading(
                    new Instant(read.Instant(wire.Timestamp, "timestamp")),
                    read.TextList(wire.Tags, "tags"),
                    wire.Text)));
        }
    }

    // Stand-ins handed to WireReader for a missing contributors object. The document is already
    // refused at that point; these exist only so the constructor can run to completion.
    private static readonly ActivityContributors NoActivityContributors =
        new(null, null, null, null, null, null);

    private static readonly ReadinessContributors NoReadinessContributors =
        new(null, null, null, null, null, null, null, null, null);

    private static readonly SleepContributors NoSleepContributors =
        new(null, null, null, null, null, null, null);

    private static readonly ResilienceContributors NoResilienceContributors = new(null, null, null);
}
