using System.Text.Json;
using lionheart.Functional;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.Training;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Serialization;

/// <summary>
/// The wire shapes the native logging screen decodes: set kinds on lifts, routines, previous
/// performances, bodyweight and movement setup. Literal expectations, because the client is
/// written against these bytes and a renamed member would decode as absent rather than fail.
/// </summary>
public class LoggingWireTests
{
    /// <summary>Web defaults are what ASP.NET reads a request body and writes a response body with.</summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private static readonly Guid Movement = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Session = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private static readonly DateTimeOffset Monday = new(2026, 11, 2, 17, 0, 0, TimeSpan.FromHours(-5));

    // ---- Set kind on a lift command ----

    [Fact]
    public void A_lift_sent_without_a_kind_is_a_working_set()
    {
        var command = JsonSerializer.Deserialize<SetCommand>(
            """{"$type":"lift","movementDataId":"11111111-2222-3333-4444-555555555555","reps":5,"weight":225,"unit":1,"rpe":null}""",
            Wire);

        Assert.Equal(SetKind.Working, Assert.IsType<LiftSet>(ResultAssert.Succeeded(command!.ToEntry())).Kind);
    }

    [Fact]
    public void A_lift_sent_with_a_kind_keeps_it()
    {
        var command = JsonSerializer.Deserialize<SetCommand>(
            """{"$type":"lift","movementDataId":"11111111-2222-3333-4444-555555555555","reps":10,"weight":135,"unit":1,"rpe":null,"kind":1}""",
            Wire);

        Assert.Equal(SetKind.WarmUp, Assert.IsType<LiftSet>(ResultAssert.Succeeded(command!.ToEntry())).Kind);
    }

    [Fact]
    public void A_kind_that_names_no_member_is_refused_on_the_kind_rather_than_thrown()
    {
        var command = new LiftSetCommand(Movement, 5, 225, WeightUnit.Pounds, null, (SetKind)9);

        var refusal = ResultAssert.Failed(command.ToEntry());

        Assert.Equal("Kind", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
    }

    [Fact]
    public void A_lift_goes_out_with_its_kind()
    {
        SetEntry lift = new LiftSet(Movement, 8, 185, WeightUnit.Pounds, Rpe.Of(9), SetKind.Drop) { Source = new Formed(), Facets = [] };

        Assert.Equal(
            """{"$type":"lift","movementDataId":"11111111-2222-3333-4444-555555555555","reps":8,"weight":185,"unit":1,"rpe":9,"kind":2,"source":{"$type":"formed"},"facets":[]}""",
            JsonSerializer.Serialize(lift, Wire));
    }

    // ---- Routines ----

    [Fact]
    public void A_routine_never_trained_goes_out_with_no_last_session()
    {
        var routine = Routine.Define(
            Session, Guid.NewGuid(),
            ResultAssert.Succeeded(RoutineContent.Of("Upper day", "pin 7", [new RoutineMovement(Movement, 2)])),
            new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(
            """{"routineId":"66666666-7777-8888-9999-000000000000","name":"Upper day","notes":"pin 7","movements":[{"movementDataId":"11111111-2222-3333-4444-555555555555","workingSets":2}],"createdAt":"2026-11-03T12:00:00Z","lastSession":null}""",
            JsonSerializer.Serialize(RoutineView.Of(routine, null), Wire));
    }

    [Fact]
    public void A_routines_last_session_is_the_session_envelope_with_its_case()
    {
        var routine = Some.UpperDay(Guid.NewGuid());
        var last = Some.CompletedSession(Guid.NewGuid()) with { Origin = new RoutineOrigin.FromRoutine(routine.RoutineId) };

        using var body = JsonDocument.Parse(JsonSerializer.Serialize(RoutineView.Of(routine, last), Wire));
        var session = body.RootElement.GetProperty("lastSession");

        Assert.Equal("completed", session.GetProperty("$type").GetString());
        Assert.Equal(routine.RoutineId, session.GetProperty("routineId").GetGuid());
    }

    [Fact]
    public void A_session_goes_out_naming_its_routine_or_null()
    {
        var routineId = Guid.NewGuid();
        TrainingSessionView fromRoutine = TrainingSessionView.Of(
            Some.CompletedSession(Guid.NewGuid()) with { Origin = new RoutineOrigin.FromRoutine(routineId) });
        TrainingSessionView adHoc = TrainingSessionView.Of(Some.CompletedSession(Guid.NewGuid()));

        Assert.Equal(routineId, JsonDocument.Parse(JsonSerializer.Serialize(fromRoutine, Wire)).RootElement.GetProperty("routineId").GetGuid());
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(JsonSerializer.Serialize(adHoc, Wire)).RootElement.GetProperty("routineId").ValueKind);
    }

    // ---- Previous performances ----

    [Fact]
    public void A_previous_performance_goes_out_with_its_session_day_start_and_sets()
    {
        var performance = new PreviousPerformanceView(
            Movement, Session, new DateOnly(2026, 11, 2), Monday,
            [new LiftSet(Movement, 10, 135, WeightUnit.Pounds, Rpe.Unrated, SetKind.WarmUp) { Source = new Formed(), Facets = [] }]);

        Assert.Equal(
            """{"movementDataId":"11111111-2222-3333-4444-555555555555","trainingSessionId":"66666666-7777-8888-9999-000000000000","date":"2026-11-02","startedAt":"2026-11-02T17:00:00-05:00","sets":[{"$type":"lift","movementDataId":"11111111-2222-3333-4444-555555555555","reps":10,"weight":135,"unit":1,"rpe":null,"kind":1,"source":{"$type":"formed"},"facets":[]}]}""",
            JsonSerializer.Serialize(performance, Wire));
    }

    // ---- Bodyweight and movement setup ----

    [Fact]
    public void A_bodyweight_goes_out_flat()
    {
        Assert.Equal(
            """{"day":"2026-11-03","weight":175.5,"unit":1}""",
            JsonSerializer.Serialize(new BodyweightView(new DateOnly(2026, 11, 3), 175.5, WeightUnit.Pounds), Wire));
    }

    [Fact]
    public void A_movement_goes_out_with_its_setup()
    {
        var dto = new MovementDataDTO(
            Movement, Guid.Empty, new EquipmentDTO(Guid.Empty, "Bodyweight", true), Guid.Empty,
            new MovementBaseDTO(Guid.Empty, "Dip", string.Empty, []), null, null,
            new MovementSetup("belt, 2 plates", AddsToBodyweight: true));

        using var body = JsonDocument.Parse(JsonSerializer.Serialize(dto, Wire));

        Assert.Equal(
            """{"notes":"belt, 2 plates","addsToBodyweight":true}""",
            body.RootElement.GetProperty("setup").GetRawText());
    }
}
