using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using lionheart.Endpoints;
using lionheart.Functional;
using lionheart.Model.Cardio;
using lionheart.Model.Interpretation;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using lionheart.Tests.Support;

namespace lionheart.Tests.Endpoints;

/// <summary>
/// A route that answers with a discriminated union is only usable if the case survives to the
/// client. The JSON formatter writes a response through its <em>declared</em> type and falls
/// back to the value's own type when the result does not state one — and serialized as the
/// concrete case, the body has no "$type" at all. These tests write through the same path the
/// executor uses, so the seam cannot quietly stop declaring the type.
/// </summary>
public class ResponseDiscriminatorTests
{
    private static readonly Probe Endpoint = new();

    [Theory]
    [InlineData("committed")]
    [InlineData("awaitingConfirmation")]
    public async Task AnInterpretationResponseNamesItsCase(string discriminator)
    {
        InterpretationOutcome outcome = discriminator is "committed"
            ? new Committed([Guid.NewGuid()], [], [])
            : new AwaitingConfirmation(
                new ParsedInterpretation([], []),
                [new UnresolvedReference("Squat", ReferenceKind.MovementBase, [])]);

        var body = await WriteAsync(Endpoint.ToActionResult(
            Result<InterpretationOutcome, Problem>.Success(outcome)));

        Assert.Equal(discriminator, JsonDocument.Parse(body).RootElement.GetProperty("$type").GetString());
    }

    [Theory]
    [InlineData("restored")]
    [InlineData("nothingStands")]
    public async Task ARetractionResponseNamesItsCase(string discriminator)
    {
        // The client has to tell "your previous record stands again" from "this subject now holds
        // no record", and the case is the only thing that says which.
        RetractionOutcome outcome = discriminator is "restored"
            ? new RetractionOutcome.Restored(new PersonalRecordView(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new HeaviestSetView(225, WeightUnit.Pounds, 5, 262.5),
                DateTime.UnixEpoch,
                null,
                RecordStandingView.Standing))
            : new RetractionOutcome.NothingStands();

        var body = await WriteAsync(Endpoint.ToActionResult(
            Result<RetractionOutcome, Problem>.Success(outcome)));

        Assert.Equal(discriminator, JsonDocument.Parse(body).RootElement.GetProperty("$type").GetString());
    }

    [Fact]
    public async Task AnAchievementNamesItsKindOnTheWire()
    {
        // Seven kinds behind one slot: without the discriminator a records screen cannot tell a
        // rep max from a longest distance.
        var body = await WriteAsync(
            AchievementView.Of(new Achievement.RepMax(5, new LiftPerformance(225, WeightUnit.Pounds, 5))),
            typeof(AchievementView));

        Assert.Equal("repMax", JsonDocument.Parse(body).RootElement.GetProperty("$type").GetString());
    }

    [Fact]
    public async Task ARepMaxFromALongerSetSendsItsRepCountAndTheSetsRepsApart()
    {
        // "reps" keeps meaning the rep count the record is held at, which a client already in the
        // field files the record under; "setReps" says the six-rep max was the front of a seven.
        var body = await WriteAsync(
            AchievementView.Of(new Achievement.RepMax(6, new LiftPerformance(180, WeightUnit.Kilograms, 7))),
            typeof(AchievementView));

        var achievement = JsonDocument.Parse(body).RootElement;
        Assert.Equal(6, achievement.GetProperty("reps").GetInt32());
        Assert.Equal(7, achievement.GetProperty("setReps").GetInt32());
        Assert.Equal(180, achievement.GetProperty("weight").GetDouble());
        // The estimate is the set's as performed, not a set of six that never stood alone.
        Assert.Equal(180 * (1 + 7 / 30.0), achievement.GetProperty("estimatedOneRepMax").GetDouble(), precision: 6);
    }

    [Theory]
    [InlineData(true, "lift", "unpinned", "priority")]
    [InlineData(false, "none", "pinned", "emphasis")]
    public async Task AFocusPinNamesItsTargetsCaseAndItsStatussCase(bool unpinned, string target, string status, string role)
    {
        // The iOS decoder switches on every discriminator, and reads the unpinning day and the
        // horizon as plain calendar dates.
        var view = new FocusPinView(
            Guid.NewGuid(),
            Guid.NewGuid(),
            unpinned ? ResultAssert.Succeeded(FocusRole.Priority.For(PatternFamily.Push)) : new FocusRole.Emphasis(),
            new DateOnly(2026, 11, 2),
            new DateOnly(2027, 1, 31),
            2,
            unpinned
                ? ResultAssert.Succeeded(FocusTarget.Lift.Of(70, WeightUnit.Pounds, 5))
                : new FocusTarget.None(),
            unpinned ? new FocusStatus.Unpinned(new DateOnly(2026, 12, 20)) : new FocusStatus.Pinned(),
            DateTime.UnixEpoch);

        var pin = JsonDocument.Parse(await WriteAsync(Endpoint.ToActionResult(
            Result<FocusPinView, Problem>.Success(view)))).RootElement;

        Assert.Equal("2026-11-02", pin.GetProperty("starts").GetString());
        Assert.Equal(target, pin.GetProperty("target").GetProperty("$type").GetString());
        Assert.Equal(status, pin.GetProperty("status").GetProperty("$type").GetString());
        Assert.Equal(role, pin.GetProperty("role").GetProperty("$type").GetString());
        if (!unpinned) return;
        Assert.Equal("push", pin.GetProperty("role").GetProperty("family").GetString());
        Assert.Equal((int)WeightUnit.Pounds, pin.GetProperty("target").GetProperty("unit").GetInt32());
        Assert.Equal("2026-12-20", pin.GetProperty("status").GetProperty("on").GetString());
    }

    [Fact]
    public async Task TheScoreNamesItsCardioMeasuresAndPriorityLiftsCases()
    {
        // The iOS decoders switch on each of these, and on the standing and the readiness note.
        var log = Balance.ALog.Catalogue
            .WithSession(new DateOnly(2026, 10, 20), Balance.ALog.Set(Balance.ALog.Bench, 200, 5))
            .WithSession(new DateOnly(2026, 11, 2), Balance.ALog.Set(Balance.ALog.Bench, 210, 5))
            .Build();
        var score = lionheart.Services.Training.Score.TrainingScoreAnalysis.Score(
            log, [], new Model.Cardio.CardioMeasure.Unknown(Model.Cardio.CardioUnknownReason.NoHeartRate),
            new DateOnly(2026, 11, 3));
        var volume = lionheart.Services.Training.Balance.TrainingVolumeAnalysis.Build(
            log, new DateOnly(2026, 11, 3), new Model.Training.Balance.ReadinessNote.Low(58, 63, 87));

        var scoreBody = JsonDocument.Parse(await WriteAsync(Endpoint.ToActionResult(
            Result<Model.Training.Score.TrainingScore, Problem>.Success(score)))).RootElement;
        var volumeBody = JsonDocument.Parse(await WriteAsync(Endpoint.ToActionResult(
            Result<Model.Training.Balance.TrainingVolume, Problem>.Success(volume)))).RootElement;

        Assert.Equal("unknown", scoreBody.GetProperty("cardio").GetProperty("measure").GetProperty("$type").GetString());
        Assert.Equal("noHeartRate", scoreBody.GetProperty("cardio").GetProperty("measure").GetProperty("reason").GetString());
        var push = scoreBody.GetProperty("progress").GetProperty("families")[0].GetProperty("of").GetProperty("lift");
        Assert.Equal("push", scoreBody.GetProperty("lifting").GetProperty("families")[0].GetProperty("of").GetProperty("family").GetString());
        Assert.True(scoreBody.GetProperty("lifting").GetProperty("families")[0].GetProperty("outOf").GetDouble() > 0);
        Assert.Equal(1, scoreBody.GetProperty("consistency").GetProperty("blocks")[0].GetProperty("of").GetProperty("sessions").GetInt32());
        Assert.Equal(0, scoreBody.GetProperty("cardio").GetProperty("zones").GetArrayLength());
        Assert.Equal("chosen", push.GetProperty("$type").GetString());
        Assert.Equal("mostTrained", push.GetProperty("choice").GetProperty("$type").GetString());
        Assert.Equal("compared", push.GetProperty("progress").GetProperty("$type").GetString());
        Assert.Equal("lifetimeBest", push.GetProperty("progress").GetProperty("standing").GetString());
        Assert.Equal("train", volumeBody.GetProperty("upNext").GetProperty("$type").GetString());
        Assert.Equal(58, volumeBody.GetProperty("upNext").GetProperty("readiness").GetProperty("ouraScore").GetInt32());
        Assert.Equal("horizontalPush", volumeBody.GetProperty("patterns")[0].GetProperty("pattern").GetString());
    }

    [Fact]
    public async Task TheCardioReportNamesHowARingWorkoutCameAboutAndWhyOneWasDeclined()
    {
        // The iOS decoders switch on the entry, the exclusion reasons and the category.
        var start = new DateTimeOffset(2026, 10, 14, 17, 0, 0, TimeSpan.FromHours(-4));
        CardioSession Handball(RingEntry entry) => new(
            new CardioSessionSource.RingWorkout(Guid.NewGuid(), "handball", WorkoutCategory.Sport, entry),
            start, start.AddMinutes(25), null,
            new SessionHeartRate.Recorded(105, 120, 1, new ZoneMinutes(0, 25, 0, 0, 0, 0), HeartRateSource.Ring));
        var none = new Comparison(null, null);
        var range = new Model.Analysis.DayRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var report = new CardioReport(
            range, null, [Handball(RingEntry.Entered)], [], [], [], [],
            new AerobicBase(range, range, none, none, none, none, none),
            [new ExcludedWorkouts(WorkoutCategory.Unspecified, WorkoutExclusion.HeartRateBelowTraining, 1)],
            [new DeclinedWorkout(Handball(RingEntry.Detected), WorkoutExclusion.HeartRateUnread)]);

        var body = JsonDocument.Parse(await WriteAsync(Endpoint.ToActionResult(
            Result<CardioReport, Problem>.Success(report)))).RootElement;

        Assert.Equal("entered", body.GetProperty("sessions")[0].GetProperty("source").GetProperty("entry").GetString());
        var declined = body.GetProperty("declined")[0];
        Assert.Equal("heartRateUnread", declined.GetProperty("reason").GetString());
        Assert.Equal("detected", declined.GetProperty("session").GetProperty("source").GetProperty("entry").GetString());
        Assert.Equal("ringWorkout", declined.GetProperty("session").GetProperty("source").GetProperty("$type").GetString());
        Assert.Equal("heartRateBelowTraining", body.GetProperty("excluded")[0].GetProperty("reason").GetString());
        Assert.Equal("unspecified", body.GetProperty("excluded")[0].GetProperty("category").GetString());
    }

    [Fact]
    public void APinRequestReadsItsPriorityFamilyByName()
    {
        var request = JsonSerializer.Deserialize<PinFocusRequest>(
            """{"movementDataId":"11111111-1111-1111-1111-111111111111","starts":"2026-11-02","ends":"2027-01-31","sessionsPerWeek":2,"target":null,"priority":"pull"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(PatternFamily.Pull, Assert.IsType<FocusRole.Priority>(ResultAssert.Succeeded(request!.ToRole())).Family);
    }

    /// <summary>
    /// Why the declared type has to be stated: written as the concrete case, the discriminator
    /// is simply absent, and a client has nothing to branch on.
    /// </summary>
    [Fact]
    public async Task WrittenAsItsOwnTypeTheCaseIsLost()
    {
        var body = await WriteAsync(new Committed([], [], []), typeof(Committed));

        Assert.False(JsonDocument.Parse(body).RootElement.TryGetProperty("$type", out _));
    }

    /// <summary>
    /// Mirrors <c>ObjectResultExecutor</c>: the declared type is what the formatter is given,
    /// and the value's own type is only the fallback when none was declared.
    /// </summary>
    private static Task<string> WriteAsync<T>(ActionResult<T> result)
    {
        var delivered = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        return WriteAsync(delivered.Value!, delivered.DeclaredType ?? delivered.Value!.GetType());
    }

    private static async Task<string> WriteAsync(object value, Type declaredType)
    {
        var formatter = new SystemTextJsonOutputFormatter(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        });

        var httpContext = new DefaultHttpContext();
        var body = new MemoryStream();
        httpContext.Response.Body = body;

        await formatter.WriteResponseBodyAsync(
            new OutputFormatterWriteContext(
                httpContext, (stream, encoding) => new StreamWriter(stream, encoding), declaredType, value),
            Encoding.UTF8);

        return Encoding.UTF8.GetString(body.ToArray());
    }

    private sealed class Probe : ControllerBase;
}
