using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using lionheart.Endpoints;
using lionheart.Functional;
using lionheart.Model.Interpretation;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;

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
