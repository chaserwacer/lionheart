using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Cardio;
using lionheart.Services.Training.PersonalRecords;

namespace lionheart.Services.Strava;

/// <summary>An activity this run made a session, and what it became.</summary>
/// <param name="Became">The session's kind of training, as the report names it: "run", "e-bike ride", "lifting".</param>
public sealed record AddedActivity(StravaActivity Activity, string Became);

/// <summary>An activity that is a session the log already held, and that session's source.</summary>
public sealed record MatchedActivity(StravaActivity Activity, SessionMatch Match, SessionSource HeldAs);

/// <summary>A held session whose recording took a part of the archive's that it held none of.</summary>
public sealed record EnrichedSession(StravaActivity Activity, Guid TrainingSessionId, RecordingFill Fill);

public sealed record LeftOutActivity(StravaActivity Activity, StravaExclusion Reason);

/// <summary>An activity whose file could not be read; it was decided on its summary row alone.</summary>
public sealed record UnreadableFile(StravaActivity Activity, string Reason);

/// <summary>
/// An activity added as a session of its own that lies partly inside a held one — less than the
/// same-session rule asks, so two sessions now stand where the athlete may know there was one.
/// </summary>
/// <param name="Share">How much of the activity lies inside the held session, 0 to 1.</param>
public sealed record NearSession(StravaActivity Activity, Guid HeldSessionId, double Share);

/// <summary>What one run of the Strava archive import did.</summary>
/// <remarks>
/// The archive is described whole, so a second run reports every activity it added the first time
/// as already imported, and adds, matches nothing new and fills nothing.
/// </remarks>
/// <param name="ClocklessRoutesKept">
/// Held sessions — Strava's own copies in Health — whose route has no clock (Strava stamps a route
/// it writes to Health with the moment it wrote it) where the archive's has one. Kept, since a held
/// part is never replaced; counted, so the choice is visible.
/// </param>
/// <param name="DeletedHealthWorkouts">
/// Health workouts whose sessions the athlete deleted. A deleted workout is remembered by its Health
/// id alone, with no time, so an archived activity that was one of them is not recognised and is
/// added as a session of its own; the count says whether there is anything to check.
/// </param>
public sealed record StravaArchiveImportReport(
    int ActivitiesInArchive,
    IReadOnlyList<AddedActivity> Added,
    IReadOnlyList<MatchedActivity> Matched,
    int AlreadyImported,
    IReadOnlyList<EnrichedSession> Enriched,
    IReadOnlyList<LeftOutActivity> LeftOut,
    IReadOnlyList<UnreadableFile> UnreadableFiles,
    IReadOnlyList<NearSession> NearSessions,
    int ClocklessRoutesKept,
    int DeletedHealthWorkouts,
    RecordsOutcome Records);
