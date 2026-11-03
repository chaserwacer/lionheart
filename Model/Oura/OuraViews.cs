using lionheart.Model.Analysis;
using lionheart.Model.Oura.Readings;

namespace lionheart.Model.Oura;

/// <summary>
/// Everything the ring recorded about one calendar day, as a client reads it.
/// </summary>
/// <remarks>
/// One shape rather than six routes, because every consumer of this data wants the day as a whole:
/// a readiness score means little without the night that produced it. A collection Oura filed
/// nothing for that day is absent rather than zeroed — the ring is off the finger for stretches,
/// and the collections cover visibly different sets of days.
/// <para>
/// The readings are the stored records themselves. There is nothing on them a client may not see:
/// unlike a decorated training entry, an Oura document holds no summary written to be embedded and
/// no extractor version, so there is no internals-stripping projection to do and restating the
/// shape here would only be a second place to update when Oura adds a field.
/// </para>
/// </remarks>
public sealed record OuraDayView(
    DateOnly Day,
    DailyActivityReading? Activity,
    DailyReadinessReading? Readiness,
    DailySleepReading? Sleep,
    DailySpo2Reading? Spo2,
    DailyStressReading? Stress,
    DailyResilienceReading? Resilience);

/// <summary>An Oura event as a client reads it: when it happened, the day Oura filed it under, and what it was.</summary>
/// <remarks>
/// <typeparamref name="TReading"/> keeps one envelope across sleep periods, workouts, sessions and
/// tags, so the native client learns the shape once. <see cref="Day"/> travels alongside
/// <see cref="Period"/> rather than being derived from it, because Oura's attribution and the
/// clock genuinely disagree — a night beginning before midnight is filed under the waking day.
/// </remarks>
public sealed record OuraEventView<TReading>(Guid OuraId, DateOnly Day, Period Period, TReading Reading);
