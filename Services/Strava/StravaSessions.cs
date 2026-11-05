using System.Globalization;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Import;

namespace lionheart.Services.Strava;

/// <summary>The identity and time a session imported from the archive takes.</summary>
/// <param name="Zone">Where the athlete's clock was: Strava writes UTC, and a session's day is its local one.</param>
public sealed record ArchivePlacement(Guid UserId, TimeZoneInfo Zone, DateTime CreatedAt);

/// <summary>A Strava activity as the completed training session it imports as.</summary>
/// <remarks>
/// One set, as a Health workout is: a trail run or an endurance set measured by Strava's distance,
/// its moving time and its climb, or — for a gym session, whose sets Strava never knew — a freeform
/// set saying what it was, which the athlete can restate as lifts. The period runs from the start
/// for the elapsed time, pauses included, as Strava's own workout in Health does, so the same
/// activity read either way occupies the same clock time. The athlete's own log, standalone, ad
/// hoc and undecorated, with his perceived exertion as the set's RPE where he gave one.
/// </remarks>
public static class StravaSessions
{
    /// <summary>
    /// The namespace every archived activity's session id is derived under. Fixed forever: changing
    /// it would give every imported activity a new id and let the next run import the archive twice.
    /// </summary>
    private static readonly Guid Namespace = new("b3e1d7a2-5c49-5f0e-8a63-2d71c4f90e18");

    public static Guid SessionIdFor(long activityId, Guid userId) =>
        NameBasedId.Of(Namespace, string.Create(CultureInfo.InvariantCulture, $"{userId:D}|{activityId}"));

    /// <param name="distanceMeters">What the activity is measured by: Strava's distance, else its route's.</param>
    /// <param name="climbMeters">Strava's climb, else its route's.</param>
    public static CompletedTrainingSession Measured(
        StravaActivity activity, StravaActivityKind kind, double distanceMeters, double climbMeters, ArchivePlacement placement) =>
        Session(activity, placement, MeasuredSet(activity, kind, distanceMeters, climbMeters), NoteLines(activity));

    public static CompletedTrainingSession Lifting(StravaActivity activity, ArchivePlacement placement) =>
        Session(
            activity,
            placement,
            new FreeformSet { Source = new Texted($"Weight training, {Clock(activity.Elapsed)}, recorded in Strava", 0), Facets = [] },
            [.. NoteLines(activity), .. HeartRateLine(activity)]);

    private static CompletedTrainingSession Session(
        StravaActivity activity, ArchivePlacement placement, SetEntry set, IEnumerable<string> notes) =>
        new([set], Placed(activity, placement.Zone), new EffortNotReported(), new NotDecorated())
        {
            TrainingSessionId = SessionIdFor(activity.Id, placement.UserId),
            UserId = placement.UserId,
            Notes = string.Join("\n", notes),
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.StravaArchive(activity.Id),
            CreatedAt = placement.CreatedAt,
        };

    /// <summary>The activity's clock time, at the offset the zone kept on its date.</summary>
    private static Interval Placed(StravaActivity activity, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTime(activity.Span.Start, zone), TimeZoneInfo.ConvertTime(activity.Span.End, zone));

    private static SetEntry MeasuredSet(StravaActivity activity, StravaActivityKind kind, double distanceMeters, double climbMeters)
    {
        var kilometres = distanceMeters / DistanceUnit.Kilometers.MetersEach();
        var duration = activity.Moving > TimeSpan.Zero ? activity.Moving : activity.Elapsed;

        return kind switch
        {
            StravaActivityKind.TrailRun => new TrailRunSet(kilometres, DistanceUnit.Kilometers, duration, climbMeters, activity.PerceivedExertion)
            {
                Source = new Formed(),
                Facets = [],
            },
            StravaActivityKind.Endurance endurance => new EnduranceSet(
                endurance.Activity, kilometres, DistanceUnit.Kilometers, duration, climbMeters, activity.PerceivedExertion)
            {
                Source = new Formed(),
                Facets = [],
            },
            _ => throw new System.Diagnostics.UnreachableException("Only an activity measured by distance becomes a measured set."),
        };
    }

    /// <summary>What the athlete wrote on Strava that has no field here: a name he gave it, its description, his private note.</summary>
    internal static IEnumerable<string> NoteLines(StravaActivity activity) =>
        new[]
        {
            StravaActivityKinds.IsDefaultName(activity.Name, activity.Type) ? string.Empty : activity.Name,
            activity.Description,
            activity.PrivateNote,
        }.Where(line => line.Length > 0);

    private static IEnumerable<string> HeartRateLine(StravaActivity activity) =>
        (activity.AverageHeartRate, activity.MaxHeartRate) switch
        {
            ({ } average, { } peak) => [$"Heart rate (Strava): average {average} bpm, peak {peak} bpm."],
            ({ } average, null) => [$"Heart rate (Strava): average {average} bpm."],
            _ => [],
        };

    private static string Clock(TimeSpan span) => span.ToString(span.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
}
