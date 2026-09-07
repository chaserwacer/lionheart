using lionheart.Endpoints;
using lionheart.Functional;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Tests.Endpoints;

/// <summary>
/// The one seam where a refusal becomes a status code. Every <see cref="Problem"/> case is
/// pinned here because the mapping is a published API contract — a case that quietly changes
/// status breaks clients that branch on it, and nothing else in the suite would notice.
/// Kept in step with the table in <c>docs/api-surface.md</c>.
/// </summary>
public class ResultToHttpTests
{
    private static readonly Probe Endpoint = new();

    public static TheoryData<Problem, int> EveryRefusal() => new()
    {
        { new Problem.NotFound("No such session."), StatusCodes.Status404NotFound },
        { new Problem.Unauthorized("No identity."), StatusCodes.Status401Unauthorized },
        { new Problem.Forbidden("Not yours."), StatusCodes.Status403Forbidden },
        { new Problem.Invalid("Reps", "Must be positive."), StatusCodes.Status400BadRequest },
        { new Problem.Conflict("Already logged."), StatusCodes.Status409Conflict },
        { new Problem.Upstream("The model did not answer."), StatusCodes.Status502BadGateway },
        { new Problem.Unexpected("A defect."), StatusCodes.Status500InternalServerError },
    };

    [Theory]
    [MemberData(nameof(EveryRefusal))]
    public void EveryRefusalMapsToItsStatusCode(Problem problem, int expectedStatusCode) =>
        Assert.Equal(expectedStatusCode, StatusCodeOf(Refused<string>(problem)));

    [Theory]
    [MemberData(nameof(EveryRefusal))]
    public void ARefusalCarryingNoValueMapsTheSameWay(Problem problem, int expectedStatusCode) =>
        Assert.Equal(
            expectedStatusCode,
            StatusCodeOf(Endpoint.ToActionResult(Result<Unit, Problem>.Failure(problem))));

    /// <summary>
    /// The two statuses that answer with nothing but themselves: saying why would describe
    /// resources the caller is not allowed to know exist.
    /// </summary>
    [Fact]
    public void AuthorizationRefusalsDiscloseNothing()
    {
        Assert.IsType<UnauthorizedResult>(Refused<string>(new Problem.Unauthorized("Session expired.")).Result);

        var forbidden = Assert.IsType<StatusCodeResult>(
            Refused<string>(new Problem.Forbidden("Belongs to another athlete.")).Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public void ARefusalCarriesItsOwnWordsAsTheDetail()
    {
        var details = Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(Refused<string>(new Problem.NotFound("No such session.")).Result).Value);

        Assert.Equal("No such session.", details.Detail);
        Assert.Equal(StatusCodes.Status404NotFound, details.Status);
    }

    /// <summary>Filling in a form is told about every bad field at once, grouped by field.</summary>
    [Fact]
    public void AnInvalidRequestReportsEveryOffendingField()
    {
        var problem = new Problem.Invalid(
        [
            new InvalidField("Reps", "Must be positive."),
            new InvalidField("Reps", "Must be a whole number."),
            new InvalidField("Weight", "Must be finite.")
        ]);

        var details = Assert.IsType<ValidationProblemDetails>(
            Assert.IsType<ObjectResult>(Refused<string>(problem).Result).Value);

        Assert.Equal(2, details.Errors["Reps"].Length);
        Assert.Equal("Must be finite.", Assert.Single(details.Errors["Weight"]));
    }

    [Fact]
    public void ASuccessAnswersWithTheCodeTheRouteChose()
    {
        var delivered = Assert.IsType<ObjectResult>(Delivered("a value").Result);

        Assert.Equal(StatusCodes.Status200OK, delivered.StatusCode);
        Assert.Equal("a value", delivered.Value);
        Assert.IsType<CreatedResult>(Delivered("a value", StatusCodes.Status201Created).Result);
        Assert.IsType<NoContentResult>(Delivered("a value", StatusCodes.Status204NoContent).Result);
    }

    /// <summary>An operation whose success says nothing beyond "done" answers 204 by default.</summary>
    [Fact]
    public void AValuelessSuccessAnswersNoContent()
    {
        var done = Result<Unit, Problem>.Success(Unit.Value);

        Assert.Equal(StatusCodes.Status204NoContent, StatusCodeOf(Endpoint.ToActionResult(done)));
        Assert.Equal(
            StatusCodes.Status200OK,
            StatusCodeOf(Endpoint.ToActionResult(done, StatusCodes.Status200OK)));
    }

    /// <summary>
    /// The create-or-update shape: the same route answers 201 or 200 depending on what the
    /// write did, while the client still receives the same body either way.
    /// </summary>
    [Fact]
    public void AnOutcomeCanChooseTheSuccessCode()
    {
        var created = Endpoint.ToActionResult(
            Result<(bool IsNew, string Name), Problem>.Success((true, "a value")),
            written => written.Name,
            written => written.IsNew ? StatusCodes.Status201Created : StatusCodes.Status200OK);

        Assert.Equal("a value", Assert.IsType<CreatedResult>(created.Result).Value);

        var updated = Endpoint.ToActionResult(
            Result<(bool IsNew, string Name), Problem>.Success((false, "a value")),
            written => written.Name,
            written => written.IsNew ? StatusCodes.Status201Created : StatusCodes.Status200OK);

        Assert.Equal(StatusCodes.Status200OK, StatusCodeOf(updated));
    }

    private static ActionResult<T> Refused<T>(Problem problem) =>
        Endpoint.ToActionResult(Result<T, Problem>.Failure(problem));

    private static ActionResult<string> Delivered(
        string value, int successStatusCode = StatusCodes.Status200OK) =>
        Endpoint.ToActionResult(Result<string, Problem>.Success(value), successStatusCode);

    private static int? StatusCodeOf<T>(ActionResult<T> result) => StatusCodeOf(result.Result!);

    private static int? StatusCodeOf(ActionResult result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => null
    };

    /// <summary>
    /// The mapping hangs off <see cref="ControllerBase"/> because only an endpoint may choose a
    /// status code. This is the smallest thing that is one.
    /// </summary>
    private sealed class Probe : ControllerBase;
}
