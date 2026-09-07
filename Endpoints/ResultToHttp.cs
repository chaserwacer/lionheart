using System.Diagnostics;
using lionheart.Functional;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints;

/// <summary>
/// The single seam where a domain outcome becomes an HTTP response. Services state refusals as
/// <see cref="Problem"/> cases and know nothing about status codes; the success code is the
/// endpoint's to choose, because only the endpoint knows whether its route creates, replaces or
/// reads.
/// </summary>
public static class ResultToHttp
{
    extension(ControllerBase controller)
    {
        public ActionResult<T> ToActionResult<T>(
            Result<T, Problem> result, int successStatusCode = StatusCodes.Status200OK) =>
            result.Match(
                value => (ActionResult<T>)Delivered(value, successStatusCode),
                problem => (ActionResult<T>)Refused(problem));

        /// <summary>
        /// For a route whose success code depends on what the write did: creating answers 201
        /// and updating answers 200, and only the outcome knows which.
        /// <paramref name="body"/> is what the client receives, so the outcome stays a
        /// server-side distinction rather than a shape the client has to understand.
        /// </summary>
        public ActionResult<TBody> ToActionResult<T, TBody>(
            Result<T, Problem> result, Func<T, TBody> body, Func<T, int> successStatusCode) =>
            result.Match(
                outcome => (ActionResult<TBody>)Delivered(body(outcome), successStatusCode(outcome)),
                problem => (ActionResult<TBody>)Refused(problem));

        /// <summary>
        /// For operations whose success carries nothing. 204 is the default because a route with
        /// no response body has nothing to say beyond "done".
        /// </summary>
        public ActionResult ToActionResult(
            Result<Unit, Problem> result, int successStatusCode = StatusCodes.Status204NoContent) =>
            result.Match(
                _ => new StatusCodeResult(successStatusCode),
                problem => Refused(problem));
    }

    /// <summary>
    /// The declared type is set deliberately: the JSON formatter serializes a response through
    /// it, and falls back to the value's own type when it is absent. For a discriminated result
    /// that difference is the whole contract — serialized as <c>Committed</c> the body loses the
    /// <c>$type</c> that tells the client which case it is, while serialized as the route's
    /// declared union it keeps it.
    /// </summary>
    private static ActionResult Delivered<T>(T value, int successStatusCode) => successStatusCode switch
    {
        StatusCodes.Status201Created => new CreatedResult((string?)null, value) { DeclaredType = typeof(T) },
        StatusCodes.Status204NoContent => new NoContentResult(),
        _ => new ObjectResult(value) { StatusCode = successStatusCode, DeclaredType = typeof(T) }
    };

    private static ActionResult Refused(Problem problem) => problem switch
    {
        // 401 and 403 answer with the status alone: telling an unauthenticated or unauthorized
        // caller why would describe resources they are not allowed to know exist.
        Problem.Unauthorized => new UnauthorizedResult(),
        Problem.Forbidden => new StatusCodeResult(StatusCodes.Status403Forbidden),
        Problem.Invalid invalid => Invalid(invalid),
        Problem.NotFound => Detailed(StatusCodes.Status404NotFound, "Resource not found.", problem),
        Problem.Conflict => Detailed(StatusCodes.Status409Conflict, "There was a conflict.", problem),
        // A dependency that failed is a gateway problem, not this application's: the caller may
        // usefully retry, which is the distinction 502 carries and 500 does not.
        Problem.Upstream => Detailed(StatusCodes.Status502BadGateway, "An upstream service failed.", problem),
        Problem.Unexpected => Detailed(
            StatusCodes.Status500InternalServerError, "Something went wrong.", problem),
        _ => throw new UnreachableException()
    };

    private static ActionResult Invalid(Problem.Invalid invalid)
    {
        var byField = invalid.Fields
            .GroupBy(field => field.Field)
            .ToDictionary(group => group.Key, group => group.Select(field => field.Message).ToArray());

        return new ObjectResult(
            new ValidationProblemDetails(byField) { Status = StatusCodes.Status400BadRequest })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private static ActionResult Detailed(int statusCode, string title, Problem problem) =>
        new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = problem.Description
        })
        {
            StatusCode = statusCode
        };
}
