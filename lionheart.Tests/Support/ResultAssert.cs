using lionheart.Functional;
using Xunit.Sdk;

namespace lionheart.Tests.Support;

/// <summary>
/// Unwraps a <see cref="Result{T, TProblem}"/> inside an assertion. A plain
/// <c>Assert.True(result.IsSuccess)</c> reports only "expected true"; these put the refusal's own
/// words in the failure message, which is usually the whole diagnosis.
/// </summary>
public static class ResultAssert
{
    public static T Succeeded<T>(Result<T, Problem> result) => Succeeded<T, Problem>(result);

    /// <summary>For a layer below the problem vocabulary, whose refusals are its own words.</summary>
    public static T Succeeded<T, TProblem>(Result<T, TProblem> result) =>
        result.Match(
            value => value,
            problem => throw new XunitException($"Expected success, but the result refused: {problem}"));

    public static Problem Failed<T>(Result<T, Problem> result) =>
        result.Match(
            value => throw new XunitException($"Expected a problem, but the result succeeded with: {value}"),
            problem => problem);
}
