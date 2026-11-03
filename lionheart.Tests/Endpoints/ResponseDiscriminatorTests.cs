using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using lionheart.Endpoints;
using lionheart.Functional;
using lionheart.Model.Interpretation;
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
