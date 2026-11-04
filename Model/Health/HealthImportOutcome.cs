using System.Text.Json.Serialization;
using lionheart.Model.Training.Sessions;

namespace lionheart.Model.Health;

/// <summary>What became of one Apple Health workout offered for import.</summary>
/// <remarks>
/// Every case is an answer, not a failure: a workout Lionheart already has, or one it deliberately
/// leaves out, is the import working as intended, and the phone moves on to the next either way. A
/// failure is reserved for a workout the server could not read or could not write.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Imported), "imported")]
[JsonDerivedType(typeof(AlreadyImported), "alreadyImported")]
[JsonDerivedType(typeof(NotImported), "notImported")]
public abstract record HealthImportOutcome
{
    private protected HealthImportOutcome() { }

    /// <summary>The workout is now a completed session, its recording beside it.</summary>
    public sealed record Imported(TrainingSessionView Session) : HealthImportOutcome;

    /// <summary>An earlier import already made this workout a session; nothing was written.</summary>
    public sealed record AlreadyImported(Guid TrainingSessionId) : HealthImportOutcome;

    /// <summary>The workout is not training Lionheart takes from Health, for the reason given.</summary>
    /// <param name="TrainingSessionId">The logged session it already is, where it matched one.</param>
    public sealed record NotImported(HealthWorkoutExclusion Reason, Guid? TrainingSessionId) : HealthImportOutcome;
}

/// <summary>Why an Apple Health workout did not become a session.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HealthWorkoutExclusion>))]
public enum HealthWorkoutExclusion
{
    /// <summary>
    /// Written to Health by an app whose workouts Lionheart already reads by its own route — Oura
    /// through its API, Strong through its export — so taking it from Health too would count it twice.
    /// </summary>
    [JsonStringEnumMemberName("readDirectly")] ReadDirectly,

    /// <summary>Not a run. Only runs are taken from Health so far.</summary>
    [JsonStringEnumMemberName("notARun")] NotARun,

    /// <summary>
    /// A treadmill run: no route, and a distance the app estimated, which would stand beside the
    /// trail-running records as if measured. The ring's own workout still carries it into the
    /// cardio report.
    /// </summary>
    [JsonStringEnumMemberName("indoor")] Indoor,

    /// <summary>Neither a stated distance nor a route to measure one from.</summary>
    [JsonStringEnumMemberName("noDistance")] NoDistance,

    /// <summary>Lies at least half inside a logged run, which is the same run already counted.</summary>
    [JsonStringEnumMemberName("sameAsLoggedRun")] SameAsLoggedRun,

    /// <summary>Lies at least half inside a logged lifting session: the gym as another device saw it.</summary>
    [JsonStringEnumMemberName("insideLoggedLifting")] InsideLoggedLifting,
}
