using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Wire;
using lionheart.Services.Oura;
using Xunit.Sdk;

namespace lionheart.Tests.Oura;

/// <summary>
/// What the mapping makes of Oura's payloads.
/// </summary>
/// <remarks>
/// The live pull and the archive import interpret a payload through this one mapping, and the pull
/// cannot be exercised without the athlete's credential — so these are the tests that say what a
/// pull would do with a response. The fixture records keep the shape and quirks Oura sends, with
/// invented values.
/// </remarks>
public class OuraDocumentMappingTests
{
    private static readonly Guid Athlete = Guid.Parse("5a4a2a0f-0000-4000-8000-000000000001");

    // ---- Daily documents ----

    [Fact]
    public void An_activity_document_keeps_every_measure_and_takes_Ouras_id_as_its_key()
    {
        var document = Mapped(ArchiveFixtures.Activity(0).ToDocument(Athlete));

        Assert.Equal(Guid.Parse("4887ef8a-967c-4b07-b21c-df2dc8b697c9"), document.OuraId);
        Assert.Equal(Athlete, document.UserId);
        Assert.Equal(new DateOnly(2024, 1, 25), document.Day);

        var reading = document.Reading;
        Assert.Equal(47, reading.Score);
        Assert.Equal(22, reading.ActiveCalories);
        Assert.Equal(318, reading.EquivalentWalkingDistanceMeters);
        Assert.Equal(1.21875, reading.AverageMet);
        Assert.Equal(6100, reading.MetersToTarget);
        Assert.Equal(77400, reading.NonWearTimeSeconds);
        Assert.Equal(96, reading.Contributors.MoveEveryHour);
        Assert.Equal(5, reading.Contributors.TrainingVolume);
    }

    [Fact]
    public void A_day_Oura_could_not_score_keeps_its_measures_and_reports_no_score()
    {
        // Oura sends days with a null score and a null for every contributor. A substituted 0
        // would make a day the ring scored badly indistinguishable from one it did not score.
        var reading = Mapped(ArchiveFixtures.Activity(1).ToDocument(Athlete)).Reading;

        Assert.Null(reading.Score);
        Assert.Null(reading.Contributors.MeetDailyTargets);
        Assert.Null(reading.Contributors.TrainingVolume);
        Assert.Equal(438, reading.ActiveCalories);
        Assert.Equal(-2700, reading.MetersToTarget);
    }

    [Fact]
    public void The_minute_by_minute_MET_series_is_not_stored()
    {
        // The activity payload carries 1440 MET samples per day — 87% of that file's bytes — and
        // the wire record does not declare them, so they are skipped rather than read and dropped.
        // The daily MET-minute totals below are the same information at the resolution anything
        // here actually reads. This asserts the record maps at all in their presence.
        var line = ArchiveFixtures.Lines("daily_activity")[0];
        Assert.Contains("\"met\"", line);

        var reading = Mapped(ArchiveFixtures.Activity(0).ToDocument(Athlete)).Reading;

        Assert.Equal(5, reading.LowActivityMetMinutes);
        Assert.Equal(11, reading.MediumActivityMetMinutes);
        Assert.Equal(3, reading.HighActivityMetMinutes);
    }

    [Fact]
    public void An_activity_document_keeps_its_five_minute_classification_placed_on_the_athletes_clock()
    {
        // Oura's activity day starts at 04:00 local, and the document's timestamp carries that
        // offset, so slot n sits 5n minutes after it on the clock the athlete lived.
        var timeline = Mapped(ArchiveFixtures.Activity(1).ToDocument(Athlete)).Reading.Timeline;

        Assert.NotNull(timeline);
        Assert.Equal(new DateTimeOffset(2025, 1, 20, 4, 0, 0, TimeSpan.FromHours(-5)), timeline.StartedAt);
        Assert.Equal(288, timeline.Classes.Length);
        Assert.StartsWith("1111111111112232", timeline.Classes);
        Assert.Equal(new DateTimeOffset(2025, 1, 21, 3, 55, 0, TimeSpan.FromHours(-5)), timeline.SlotStart(287));
    }

    [Fact]
    public void An_activity_document_with_no_classification_is_stored_without_a_timeline()
    {
        // Optional in Oura's schema, so its absence is a day without a shape, not a defect.
        var reading = Mapped((ArchiveFixtures.Activity(0) with { Class5Min = null }).ToDocument(Athlete)).Reading;

        Assert.Null(reading.Timeline);
        Assert.Equal(22, reading.ActiveCalories);
    }

    [Fact]
    public void A_classification_with_no_instant_to_start_from_is_refused_naming_the_timestamp()
    {
        var defects = Refused((ArchiveFixtures.Activity(0) with { Timestamp = null }).ToDocument(Athlete));

        Assert.Equal("timestamp", Assert.Single(defects.Defects).Field);
    }

    [Fact]
    public void A_readiness_document_keeps_all_nine_of_its_contributors()
    {
        var reading = Mapped(ArchiveFixtures.Readiness(1).ToDocument(Athlete)).Reading;

        Assert.Equal(79, reading.Contributors.SleepRegularity);
        Assert.Equal(0.17, reading.TemperatureTrendDeviation);
        Assert.Equal(74, reading.Score);
        Assert.Equal(58, reading.Contributors.RestingHeartRate);
    }

    [Fact]
    public void Readiness_contributors_Oura_had_too_little_history_for_are_absent_not_zero()
    {
        var reading = Mapped(ArchiveFixtures.Readiness(0).ToDocument(Athlete)).Reading;

        Assert.Null(reading.Contributors.ActivityBalance);
        Assert.Null(reading.Contributors.HrvBalance);
        Assert.Null(reading.Contributors.SleepRegularity);
        Assert.Equal(97, reading.Contributors.BodyTemperature);
        Assert.Equal(-0.14, reading.TemperatureDeviation);
    }

    [Fact]
    public void A_night_the_ring_read_no_blood_oxygen_on_reports_neither_measure()
    {
        // Oura nulls the whole spo2_percentage object rather than its average, so a mapping that
        // reached for .average without checking would throw on every night the ring missed.
        var measured = Mapped(ArchiveFixtures.Spo2(0).ToDocument(Athlete)).Reading;
        var unmeasured = Mapped(ArchiveFixtures.Spo2(1).ToDocument(Athlete)).Reading;

        Assert.Equal(97.613, measured.AverageSpo2Percentage);
        Assert.Equal(4, measured.BreathingDisturbanceIndex);
        Assert.Null(unmeasured.AverageSpo2Percentage);
        Assert.Null(unmeasured.BreathingDisturbanceIndex);
    }

    [Fact]
    public void Two_blood_oxygen_documents_for_one_day_map_to_two_rows_with_different_keys()
    {
        // Oura has issued two documents for one day, for weeks at a stretch. Both are kept: a day
        // is not this collection's key, Oura's document id is.
        var first = Mapped(ArchiveFixtures.Spo2(2).ToDocument(Athlete));
        var second = Mapped(ArchiveFixtures.Spo2(3).ToDocument(Athlete));

        Assert.Equal(first.Day, second.Day);
        Assert.NotEqual(first.OuraId, second.OuraId);
    }

    [Fact]
    public void A_stress_document_keeps_Ouras_verdict_and_tolerates_its_absence()
    {
        var judged = Mapped(ArchiveFixtures.Stress(0).ToDocument(Athlete)).Reading;
        var unjudged = Mapped(ArchiveFixtures.Stress(1).ToDocument(Athlete)).Reading;

        Assert.Equal("restored", judged.DaySummary);
        Assert.Equal(1800, judged.StressHighSeconds);
        Assert.Null(unjudged.DaySummary);
        Assert.Equal(6300, unjudged.RecoveryHighSeconds);
    }

    // ---- Sleep periods ----

    [Fact]
    public void A_night_keeps_its_stages_its_hypnogram_and_both_overnight_curves()
    {
        var night = Mapped(ArchiveFixtures.Sleep(0).ToDocument(Athlete));
        var reading = night.Reading;

        Assert.Equal(new DateOnly(2024, 1, 26), night.Day);
        Assert.Equal("long_sleep", reading.Kind);
        Assert.True(reading.IsMainSleep);

        Assert.Equal(28710, reading.TimeInBedSeconds);
        Assert.Equal(23460, reading.TotalSleepSeconds);
        Assert.Equal(6630, reading.DeepSleepSeconds);
        Assert.Equal(3930, reading.RemSleepSeconds);
        Assert.Equal(82, reading.Efficiency);
        Assert.Equal(64, reading.AverageHrv);
        Assert.Equal(15.125, reading.AverageBreath);

        Assert.Equal(96, reading.Hypnogram.Length);
        Assert.StartsWith("44222333211122111222", reading.Hypnogram);

        Assert.Equal(300, reading.HeartRate.IntervalSeconds);
        Assert.Equal(96, reading.HeartRate.Samples.Count);
        Assert.Equal([54, 53, 55], reading.HeartRate.Samples.Take(3));
        Assert.Equal([61, 66, 58], reading.Hrv.Samples.Take(3));
    }

    [Fact]
    public void A_nights_period_runs_from_bedtime_to_waking_at_the_offset_it_was_lived_at()
    {
        var night = Mapped(ArchiveFixtures.Sleep(0).ToDocument(Athlete));

        var period = Assert.IsType<Interval>(night.Period);
        Assert.Equal(new DateTimeOffset(2024, 1, 26, 1, 43, 12, TimeSpan.FromHours(-5)), period.Start);
        Assert.Equal(new DateTimeOffset(2024, 1, 26, 9, 41, 42, TimeSpan.FromHours(-5)), period.End);
        Assert.Equal(TimeSpan.FromSeconds(28710), period.Duration);
    }

    [Fact]
    public void A_night_Oura_files_under_the_waking_day_keeps_both_that_day_and_its_real_start()
    {
        // Oura's `day` and the date its bedtime fell on often disagree. Both are stored: the day
        // is what a reader asking "how did he sleep on the 28th" means, and the period is where
        // the night sits on the clock.
        var night = Mapped(ArchiveFixtures.Sleep(2).ToDocument(Athlete));

        Assert.Equal(new DateOnly(2024, 1, 28), night.Day);
        Assert.Equal(new DateOnly(2024, 1, 27), night.Period.LocalDate);
    }

    [Fact]
    public void A_nap_the_ring_read_nothing_on_keeps_Ouras_zero_and_its_run_of_empty_samples()
    {
        // Oura answers 0 rather than null for average_heart_rate on naps it read nothing on. It is
        // stored as it arrived: rewriting it to absence on the way in would destroy the only
        // evidence of which of the two Oura actually said.
        var nap = Mapped(ArchiveFixtures.Sleep(1).ToDocument(Athlete)).Reading;

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
        var series = Mapped(ArchiveFixtures.Sleep(0).ToDocument(Athlete)).Reading.HeartRate;

        Assert.Equal(series.StartedAt, series.At(0));
        Assert.Equal(series.StartedAt.AddMinutes(5), series.At(1));
        Assert.Equal(series.StartedAt.AddMinutes(60), series.At(12));
    }

    // ---- Other events ----

    [Fact]
    public void A_workout_keeps_its_vocabulary_and_the_measures_Oura_could_not_take()
    {
        var measured = Mapped(ArchiveFixtures.Workout(0).ToDocument(Athlete));
        var unmeasured = Mapped(ArchiveFixtures.Workout(1).ToDocument(Athlete)).Reading;

        Assert.Equal("tennis", measured.Reading.Activity);
        Assert.Equal("hard", measured.Reading.Intensity);
        Assert.Equal("autodetected", measured.Reading.Source);
        Assert.Equal(512.847, measured.Reading.Calories);
        Assert.Equal(1342.618204551283, measured.Reading.DistanceMeters);
        Assert.Equal(
            new DateTimeOffset(2024, 8, 27, 18, 5, 0, TimeSpan.FromHours(-4)),
            measured.Period.StartTime);

        Assert.Equal("yoga", unmeasured.Activity);
        Assert.Null(unmeasured.Calories);
        Assert.Null(unmeasured.DistanceMeters);
    }

    [Fact]
    public void A_session_keeps_all_three_of_its_sample_runs()
    {
        var session = Mapped(ArchiveFixtures.Session(0).ToDocument(Athlete)).Reading;

        Assert.Equal("breathing", session.Kind);
        Assert.Equal("good", session.Mood);
        Assert.Equal(5, session.HeartRate.IntervalSeconds);
        Assert.Equal(104, session.HeartRate.Samples.Count);
        Assert.Equal(104, session.Hrv.Samples.Count);
        Assert.Equal(104, session.MotionCount.Samples.Count);
    }

    [Fact]
    public void A_session_without_a_mood_recorded_maps_without_one()
    {
        Assert.Null(Mapped(ArchiveFixtures.Session(2).ToDocument(Athlete)).Reading.Mood);
    }

    [Fact]
    public void A_tag_marks_an_instant_and_keeps_its_codes_and_whatever_was_typed()
    {
        var written = Mapped(ArchiveFixtures.Tag(0).ToDocument(Athlete));
        var bare = Mapped(ArchiveFixtures.Tag(1).ToDocument(Athlete)).Reading;
        var blank = Mapped(ArchiveFixtures.Tag(2).ToDocument(Athlete)).Reading;

        Assert.Equal(["tag_generic_late_meal"], written.Reading.Codes);
        Assert.Equal("Late dinner, screens until midnight. ", written.Reading.Text);
        Assert.IsType<Instant>(written.Period);
        Assert.Equal(
            new DateTimeOffset(2025, 9, 7, 20, 47, 0, TimeSpan.FromHours(-4)),
            written.Period.StartTime);

        Assert.Equal(["tag_generic_sauna"], bare.Codes);
        Assert.Null(bare.Text);
        Assert.Equal(string.Empty, blank.Text);
    }

    // ---- Collections shaped from Oura's published schema ----
    //
    // The archive fixtures hold none of these three; the payloads below are built to Oura's OpenAPI
    // 1.43 schema. What these tests pin is that a document of that shape maps, and that one missing a
    // required field is refused by name — which is what the first live sync's tally will report.

    [Fact]
    public void A_VO2max_estimate_keeps_its_value_and_when_Oura_made_it()
    {
        var document = Mapped(new WireVo2Max
        {
            Id = "6f1b0c7e-2f6e-4f0b-9a5e-0d6f3c0b1a11",
            Day = "2026-10-12",
            Timestamp = "2026-10-12T07:14:00-04:00",
            Vo2Max = 47,
        }.ToDocument(Athlete));

        Assert.Equal(new DateOnly(2026, 10, 12), document.Day);
        Assert.Equal(47, document.Reading.Vo2Max);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 7, 14, 0, TimeSpan.FromHours(-4)), document.Reading.EstimatedAt);
    }

    [Fact]
    public void A_VO2max_estimate_without_a_value_is_refused_naming_it()
    {
        var defects = Refused(new WireVo2Max
        {
            Id = "6f1b0c7e-2f6e-4f0b-9a5e-0d6f3c0b1a11",
            Day = "2026-10-12",
            Timestamp = "2026-10-12T07:14:00-04:00",
        }.ToDocument(Athlete));

        Assert.Equal("vo2_max", Assert.Single(defects.Defects).Field);
    }

    [Fact]
    public void A_cardiovascular_age_keeps_each_figure_independently_absent()
    {
        var document = Mapped(new WireDailyCardiovascularAge
        {
            Id = "0b7d6f4e-8c1a-4d55-b3a0-6a2f9e1c7d22",
            Day = "2026-10-12",
            VascularAge = 23,
        }.ToDocument(Athlete));

        Assert.Equal(23, document.Reading.VascularAge);
        Assert.Null(document.Reading.PulseWaveVelocity);
    }

    [Fact]
    public void A_heart_rate_sample_is_kept_in_UTC_whatever_offset_Oura_wrote()
    {
        var sample = Mapped(new WireHeartRate
        {
            Timestamp = "2026-10-12T07:14:00-04:00",
            Bpm = 61,
            Source = "awake",
        }.ToSample());

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 11, 14, 0, TimeSpan.Zero), sample.At);
        Assert.Equal(TimeSpan.Zero, sample.At.Offset);
        Assert.Equal(61, sample.Bpm);
        Assert.Equal("awake", sample.Source);
    }

    [Fact]
    public void A_heart_rate_of_zero_is_refused_rather_than_stored_as_a_heartbeat()
    {
        var defects = Refused(new WireHeartRate { Timestamp = "2026-10-12T11:14:00Z", Bpm = 0, Source = "awake" }.ToSample());

        Assert.Equal("bpm", Assert.Single(defects.Defects).Field);
    }

    // ---- Refusals ----

    [Fact]
    public void A_document_with_no_id_names_the_id_as_the_defect()
    {
        var defects = Refused(new WireDailySleep { Day = "2024-01-26", Score = 62 }.ToDocument(Athlete));

        Assert.Contains("id", defects.Defects.Select(defect => defect.Field));
    }

    [Fact]
    public void A_document_whose_day_is_not_a_date_names_the_day_rather_than_failing_to_parse()
    {
        var defects = Refused(new WireDailySleep
        {
            Id = "6748c745-2688-4430-948f-7edbe0e27531",
            Day = "the twenty-eighth of January",
        }.ToDocument(Athlete));

        var day = Assert.Single(defects.Defects, defect => defect.Field == "day");
        Assert.Contains("the twenty-eighth of January", day.Detail);
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
            Id = "efb07c8e-da77-4e54-8712-4a5601feb1aa",
            Day = "2024-08-27",
            Activity = "tennis",
            Intensity = "hard",
            Source = "autodetected",
            StartDatetime = "2024-08-27T20:00:00.000-04:00",
            EndDatetime = "2024-08-27T19:00:00.000-04:00",
        }.ToDocument(Athlete));

        var span = Assert.Single(defects.Defects, defect => defect.Field == "end_datetime");
        Assert.Contains("before its start", span.Detail);
    }

    [Fact]
    public void A_sample_run_Oura_omitted_entirely_names_that_run()
    {
        var defects = Refused((ArchiveFixtures.Sleep(0) with { Hrv = null }).ToDocument(Athlete));

        Assert.Equal("hrv", Assert.Single(defects.Defects).Field);
    }

    [Fact]
    public void A_sample_run_with_no_items_names_the_items_rather_than_the_run()
    {
        var defects = Refused((ArchiveFixtures.Sleep(0) with
        {
            Hrv = new WireSampleSeries { Interval = 300, Timestamp = "2024-01-26T01:43:12.000-05:00" },
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
            Id = "3590d464-8870-40d8-8aee-5a9a6ff4b751",
            Day = "2025-09-07",
            Tags = ["tag_generic_late_meal"],
            Timestamp = null,
        }.ToDocument(Athlete));

        // The absent instant is named, and nothing escaped as an exception.
        Assert.Contains("timestamp", defects.Defects.Select(defect => defect.Field));
    }

    [Fact]
    public void A_tag_with_an_empty_code_is_refused_so_no_hole_reaches_the_stored_list()
    {
        var defects = Refused((ArchiveFixtures.Tag(0) with { Tags = [null] }).ToDocument(Athlete));

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
