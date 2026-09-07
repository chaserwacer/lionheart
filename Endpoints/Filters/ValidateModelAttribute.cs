using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace lionheart.Endpoints.Filters;

/// <summary>
/// Short-circuits an action whose bound model failed validation, so every endpoint can assume a
/// well-formed request instead of re-checking <c>ModelState</c> in its first line.
/// </summary>
/// <remarks>
/// Replaces Ardalis.ValidateModel, which targets netstandard2.0 and therefore pulled the entire
/// ASP.NET Core 2.0 MVC package tree in behind it — four vulnerability advisories for an attribute
/// this size. Behaviour is deliberately identical to the package's, so the wire contract did not
/// change with the dependency.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ValidateModelAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(context.ModelState);
        }
    }
}
