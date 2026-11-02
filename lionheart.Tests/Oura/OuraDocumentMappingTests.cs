using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Wire;
using lionheart.Services.Oura;
using Xunit.Sdk;

namespace lionheart.Tests.Oura;

/// <summary>
/// What the mapping makes of real recorded Oura payloads.
/// </summary>
/// <remarks>
/// The live pull and the archive import interpret a payload through this one mapping, and the pull
/// cannot be exercised without the athlete's credential — so these are the tests that say what a
/// pull would do with a genuine response. Every record asserted here came off his own account.
/// </remarks>
public class OuraDocumentMappingTests
{
    private static readonly Guid Athlete = Guid.Parse("5a4a2a0f-0000-4000-8000-000000000001");

    // ---- Daily documents ----

    [Fact]
    public void An_activity_document_keeps_every_measure_and_takes_Ouras_id_as_its_key()
    {
        var document = Mapped(RecordedPayloads.Activity(0).ToDocument(Athlete));

        Assert.Equal(Guid.Parse("a1dfbc60-29e5-4309-b53c-a9c5e2d1cd1b"), document.OuraId);
        Assert.Equal(Athlete, document.UserId);
        Assert.Equal(new DateOnly(2023, 1, 5), document.Day);

        var reading = document.Reading;
        Assert.Equal(50, reading.Score);
        Assert.Equal(15, reading.ActiveCalories);
        Assert.Equal(202, reading.EquivalentWalkingDistanceMeters);
        Assert.Equal(1.1875, reading.AverageMet);
        Assert.Equal(5700, reading.MetersToTarget);
        Assert.Equal(79620, reading.NonWearTimeSeconds);
        Assert.Equal(100, reading.Contributors.MoveEveryHour);
        Assert.Equal(2, reading.Contributors.TrainingVolume);
    }

    [Fact]
    public void A_day_Oura_could_not_score_keeps_its_measures_and_reports_no_score()
    {
        // 15 archived days carry a null score and a null for every contributor. A substituted 0
        // would make a day the ring scored badly indistinguishable from one it did not score.
        var reading = Mapped(RecordedPayloads.Activity(1).ToDocument(Athlete)).Reading;

        Assert.Null(reading.Score);
        Assert.Null(reading.Contributors.MeetDailyTargets);
        Assert.Null(reading.Contributors.TrainingVolume);
        Assert.Equal(401, reading.ActiveCalories);
        Assert.Equal(-3400, reading.MetersToTarget);
    }

    [Fact]
    public void The_minute_by_minute_MET_series_is_not_stored()
    {
        // The activity payload carries 1440 MET samples per day — 87% of that file's bytes — and
        // the wire record does not declare them, so they are skipped rather than read and dropped.
        // The daily MET-minute totals below are the same information at the resolution anything
        // here actually reads. This asserts the record maps at all in their presence.
        var line = RecordedPayloads.Lines("daily_activity")[0];
        Assert.Contains("\"met\"", line);

        var reading = Mapped(RecordedPayloads.Activity(0).ToDocument(Athlete)).Reading;

        Assert.Equal(2, reading.LowActivityMetMinutes);
        Assert.Equal(8, reading.MediumActivityMetMinutes);
        Assert.Equal(0, reading.HighActivityMetMinutes);
    }

    [Fact]
    public void A_readiness_document_keeps_all_nine_of_its_contributors()
    {
        var reading = Mapped(RecordedPayloads.Readiness(1).ToDocument(Athlete)).Reading;

        Assert.Equal(85, reading.Contributors.SleepRegularity);
        Assert.Equal(0.34, reading.TemperatureTrendDeviation);
        Assert.Equal(71, reading.Score);
        Assert.Equal(51, reading.Contributors.RestingHeartRate);
    }

    [Fact]
    public void Readiness_contributors_Oura_had_too_little_history_for_are_absent_not_zero()
    {
        var reading = Mapped(RecordedPayloads.Readiness(0).ToDocument(Athlete)).Reading;

        Assert.Null(reading.Contributors.ActivityBalance);
        Assert.Null(reading.Contributors.HrvBalance);
        Assert.Null(reading.Contributors.SleepRegularity);
        Assert.Equal(100, reading.Contributors.BodyTemperature);
        Assert.Equal(-0.09, reading.TemperatureDeviation);
    }

    [Fact]
    public void A_night_the_ring_read_no_blood_oxygen_on_reports_neither_measure()
    {
        // Oura nulls the whole spo2_percentage object rather than its average, so a mapping that
        // reached for .average without checking would throw on one archived night in ten.
        var measured = Mapped(RecordedPayloads.Spo2(0).ToDocument(Athlete)).Reading;
        var unmeasured = Mapped(RecordedPayloads.Spo2(1).ToDocument(Athlete)).Reading;

        Assert.Equal(98.092, measured.AverageSpo2Percentage);
        Assert.Equal(6, measured.BreathingDisturbanceIndex);
        Assert.Null(unmeasured.AverageSpo2Percentage);
        Assert.Null(unmeasured.BreathingDisturbanceIndex);
    }

    [Fact]
    public void Two_blood_oxygen_documents_for_one_day_map_to_two_rows_with_different_keys()
    {
        // Oura issued two documents a day for 128 consecutive days. Both are kept: a day is not
        // this collection's key, Oura's document id is.
        var first = Mapped(RecordedPayloads.Spo2(2).ToDocument(Athlete));
        var second = Mapped(RecordedPayloads.Spo2(3).ToDocument(Athlete));

        Assert.Equal(first.Day, second.Day);
        Assert.NotEqual(first.OuraId, second.OuraId);
    }

    [Fact]
    public void A_stress_document_keeps_Ouras_verdict_and_tolerates_its_absence()
    {
        var judged = Mapped(RecordedPayloads.Stress(0).ToDocument(Athlete)).Reading;
        var unjudged = Mapped(RecordedPayloads.Stress(1).ToDocument(Athlete)).Reading;

        Assert.Equal("normal", judged.DaySummary);
        Assert.Equal(0, judged.StressHighSeconds);
        Assert.Null(unjudged.DaySummary);
        Assert.Equal(10800, unjudged.RecoveryHighSeconds);
    }

    // ---- Sleep periods ----

    [Fact]
    public void A_night_keeps_its_stages_its_hypnogram_and_both_overnight_curves()
    {
        var night = Mapped(RecordedPayloads.Sleep(0).ToDocument(Athlete));
        var reading = night.Reading;

        Assert.Equal(new DateOnly(2023, 1, 6), night.Day);
        Assert.Equal("long_sleep", reading.Kind);
        Assert.True(reading.IsMainSleep);

        Assert.Equal(28740, reading.TimeInBedSeconds);
        Assert.Equal(21780, reading.TotalSleepSeconds);
        Assert.Equal(8940, reading.DeepSleepSeconds);
        Assert.Equal(2070, reading.RemSleepSeconds);
        Assert.Equal(76, reading.Efficiency);
        Assert.Equal(100, reading.AverageHrv);
        Assert.Equal(14.375, reading.AverageBreath);

        Assert.Equal(96, reading.Hypnogram.Length);
        Assert.StartsWith("42211111142111114124", reading.Hypnogram);

        Assert.Equal(300, reading.HeartRate.IntervalSeconds);
        Assert.Equal(96, reading.HeartRate.Samples.Count);
        Assert.Equal([47, 47, 46], reading.HeartRate.Samples.Take(3));
        Assert.Equal([93, 96, 103], reading.Hrv.Samples.Take(3));
    }

    [Fact]
    public void A_nights_period_runs_from_bedtime_to_waking_at_the_offset_it_was_lived_at()
    {
        var night = Mapped(RecordedPayloads.Sleep(0).ToDocument(Athlete));

        var period = Assert.IsType<Interval>(night.Period);
        Assert.Equal(new DateTimeOffset(2023, 1, 6, 2, 18, 29, TimeSpan.FromHours(-5)), period.Start);
        Assert.Equal(new DateTimeOffset(2023, 1, 6, 10, 17, 29, TimeSpan.FromHours(-5)), period.End);
        Assert.Equal(TimeSpan.FromSeconds(28740), period.Duration);
    }

    [Fact]
    public void A_night_Oura_files_under_the_waking_day_keeps_both_that_day_and_its_real_start()
    {
        // Oura's `day` and the date its bedtime fell on disagree on 343 of 1425 archived records.
        // Both are stored: the day is what a reader asking "how did he sleep on the 8th" means,
        // and the period is where the night sits on the clock.
        var night = Mapped(RecordedPayloads.Sleep(2).ToDocument(Athlete));

        Assert.Equal(new DateOnly(2023, 1, 8), night.Day);
        Assert.Equal(new DateOnly(2023, 1, 7), night.Period.LocalDate);
    }

    [Fact]
    public void A_nap_the_ring_read_nothing_on_keeps_Ouras_zero_and_its_run_of_empty_samples()
    {
        // Oura answers 0 rather than null for average_heart_rate on 148 archived records. It is
        // stored as it arrived: rewriting it to absence on the way in would destroy the only
        // evidence of which of the two Oura actually said.
        var nap = Mapped(RecordedPayloads.Sleep(1).ToDocument(Athlete)).Reading;

        Assert.Equal(0, nap.AverageHeartRate);
        Assert.Null(nap.LowestHeartRate);
        Assert.Null(nap.AverageHrv);
        Assert.False(nap.IsMainSleep);
        Assert.Equal(4, nap.HeartRate.Samples.Count);
        Assert.All(nap.HeartRate.Samples, sample => Assert.Null(sample));
    }

    [Fact]
    public void A_sample_runs_clock_position_comes_from_its_interval_and_start()
    {
        var series = Mapped(RecordedPayloads.Sleep(0).ToDocument(Athlete)).Reading.HeartRate;

        Assert.Equal(series.StartedAt, series.At(0));
        Assert.Equal(series.StartedAt.AddMinutes(5), series.At(1));
        Assert.Equal(series.StartedAt.AddMinutes(60), series.At(12));
    }

    // ---- Other events ----

    [Fact]
    public void A_workout_keeps_its_vocabulary_and_the_measures_Oura_could_not_take()
    {
        var measured = Mapped(RecordedPayloads.Workout(0).ToDocument(Athlete));
        var unmeasured = Mapped(RecordedPayloads.Workout(1).ToDocument(Athlete)).Reading;

        Assert.Equal("basketball", measured.Reading.Activity);
        Assert.Equal("moderate", measured.Reading.Intensity);
        Assert.Equal("confirmed", measured.Reading.Source);
        Assert.Equal(465.516, measured.Reading.Calories);
        Assert.Equal(478.0736939766074, measured.Reading.DistanceMeters);
        Assert.Equal(
            new DateTimeOffset(2023, 8, 8, 19, 19, 0, TimeSpan.FromHours(-4)),
            measured.Period.StartTime);

        Assert.Equal("walking", unmeasured.Activity);
        Assert.Null(unmeasured.Calories);
        Assert.Null(unmeasured.DistanceMeters);
    }

    [Fact]
    public void A_session_keeps_all_three_of_its_sample_runs()
    {
        var session = Mapped(RecordedPayloads.Session(0).ToDocument(Athlete)).Reading;

        Assert.Equal("meditation", session.Kind);
        Assert.Equal("same", session.Mood);
        Assert.Equal(5, session.HeartRate.IntervalSeconds);
        Assert.Equal(99, session.HeartRate.Samples.Count);
        Assert.Equal(99, session.Hrv.Samples.Count);
        Assert.Equal(99, session.MotionCount.Samples.Count);
    }

    [Fact]
    public void A_session_without_a_mood_recorded_maps_without_one()
    {
        Assert.Null(Mapped(RecordedPayloads.Session(2).ToDocument(Athlete)).Reading.Mood);
    }

    [Fact]
    public void A_tag_marks_an_instant_and_keeps_its_codes_and_whatever_was_typed()
    {
        var written = Mapped(RecordedPayloads.Tag(0).ToDocument(Athlete));
        var bare = Mapped(RecordedPayloads.Tag(1).ToDocument(Athlete)).Reading;
        var blank = Mapped(RecordedPayloads.Tag(2).ToDocument(Athlete)).Reading;

        Assert.Equal(["tag_sleep_stress"], written.Reading.Codes);
        Assert.Equal("Moving back to college. ", written.Reading.Text);
        Assert.IsType<Instant>(written.Period);
        Assert.Equal(
            new DateTimeOffset(2024, 8, 18, 18, 28, 0, TimeSpan.FromHours(-4)),
            written.Period.StartTime);

        Assert.Equal(["tag_generic_nocaffeine"], bare.Codes);
        Assert.Null(bare.Text);
        Assert.Equal(string.Empty, blank.Text);
    }

    // ---- Refusals ----

    [Fact]
    public void A_document_with_no_id_names_the_id_as_the_defect()
    {
        var defects = Refused(new WireDailySleep { Day = "2023-01-06", Score = 58 }.ToDocument(Athlete));

        Assert.Contains("id", defects.Defects.Select(defect => defect.Field));
    }

    [Fact]
    public void A_document_whose_day_is_not_a_date_names_the_day_rather_than_failing_to_parse()
    {
        var defects = Refused(new WireDailySleep
        {
            Id = "c94fd016-3383-4152-8c5c-068ff9415053",
            Day = "the eighth of January",
        }.ToDocument(Athlete));

        var day = Assert.Single(defects.Defects, defect => defect.Field == "day");
        Assert.Contains("the eighth of January", day.Detail);
    }

    [Fact]
    public void Every_unreadable_field_is_reported_at_once_rather_than_only_the_first()
    {
        // Accumulating is the point of the wire reader. Reporting one defect per attempt would
        // send a reader back to the same record four times to learn four things about it.
        var defects = Refused(new WireDailySleep().ToDocument(Athlete));

        Assert.Equal(
            ["contributors", "day", "id"],
            defects.Defects.Select(defect => defect.Field).Order());
    }

    [Fact]
    public void A_span_that_ends_before_it_starts_is_refused_rather_than_throwing()
    {
        // Interval refuses a reversed pair by throwing, which is right for a domain caller and
        // fatal for an import: one bad archived record would end the run.
        var defects = Refused(new WireWorkout
        {
            Id = "39fa1a79-0496-40e4-98c3-f8d5702f6714",
            Day = "2023-08-08",
            Activity = "walking",
            Intensity = "moderate",
            Source = "confirmed",
            StartDatetime = "2023-08-08T20:00:00.000-04:00",
            EndDatetime = "2023-08-08T19:00:00.000-04:00",
        }.ToDocument(Athlete));

        var span = Assert.Single(defects.Defects, defect => defect.Field == "end_datetime");
        Assert.Contains("before its start", span.Detail);
    }

    [Fact]
    public void A_sample_run_Oura_omitted_entirely_names_that_run()
    {
        var defects = Refused((RecordedPayloads.Sleep(0) with { Hrv = null }).ToDocument(Athlete));

        Assert.Equal("hrv", Assert.Single(defects.Defects).Field);
    }

    [Fact]
    public void A_sample_run_with_no_items_names_the_items_rather_than_the_run()
    {
        var defects = Refused((RecordedPayloads.Sleep(0) with
        {
            Hrv = new WireSampleSeries { Interval = 300, Timestamp = "2023-01-06T02:18:29.000-05:00" },
        }).ToDocument(Athlete));

        Assert.Equal("hrv.items", Assert.Single(defects.Defects).Field);
    }

    [Fact]
    public void A_payload_whose_constructor_refuses_it_is_reported_rather_than_thrown_out_of()
    {
        // The reader builds the document from stand-in values when a field could not be read, and
        // a reading record's own guard is entitled to reject one. A guard that threw here would
        // end an import instead of rejecting a record, which is the one thing it must never do.
        var defects = Refused(new WireTag
        {
            Id = "408469c1-3182-4782-aebb-ac7b0a7bbe95",
            Day = "2024-08-18",
            Tags = ["tag_sleep_stress"],
            Timestamp = null,
        }.ToDocument(Athlete));

        // The absent instant is named, and nothing escaped as an exception.
        Assert.Contains("timestamp", defects.Defects.Select(defect => defect.Field));
    }

    [Fact]
    public void A_tag_with_an_empty_code_is_refused_so_no_hole_reaches_the_stored_list()
    {
        var defects = Refused((RecordedPayloads.Tag(0) with { Tags = [null] }).ToDocument(Athlete));

        Assert.Equal("tags", Assert.Single(defects.Defects).Field);
    }

    // ---- Unwrapping ----

    private static TDocument Mapped<TDocument>(Result<TDocument, OuraDocumentDefects> result) =>
        result.Match(
            document => document,
            defects => throw new XunitException($"Expected a document, but the payload was refused: {defects.Description}"));

    private static OuraDocumentDefects Refused<TDocument>(Result<TDocument, OuraDocumentDefects> result) =>
        result.Match(
            document => throw new XunitException($"Expected a refusal, but the payload mapped to {document}."),
            defects => defects);
}
