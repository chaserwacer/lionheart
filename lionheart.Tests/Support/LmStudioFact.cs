namespace lionheart.Tests.Support;

/// <summary>
/// A test that talks to a live LM Studio server. Run these after changing a prompt, a JSON
/// schema, or the configured model — they are the only check that the model still honors the
/// schema, which no fake can tell you.
/// </summary>
/// <remarks>
/// Two separate things keep these out of a normal run, and they do different jobs.
/// **Selection** is the <c>Category=LocalModel</c> trait on the test class, which
/// lionheart.runsettings filters out — that is what keeps a plain <c>dotnet test</c> hermetic
/// and fast, and it is why nothing here probes the network at discovery time.
/// **Skipping** is this attribute plus <see cref="LmStudio.Unavailable"/>: once selected, a test
/// that cannot reach a server reports as skipped with the failing key or step named, never as a
/// failure. Deriving from SkippableFact is what allows that decision to be made at run time,
/// after the readiness probe has actually run.
/// </remarks>
public sealed class LmStudioFactAttribute : SkippableFactAttribute
{
    /// <summary>
    /// Applied by every live test before touching a client; skips with the reason.
    /// Qualified as Xunit.Skip because the inherited FactAttribute.Skip property shadows it here.
    /// </summary>
    public static void SkipUnlessAvailable() =>
        Xunit.Skip.If(LmStudio.Unavailable is not null, $"LM Studio unavailable: {LmStudio.Unavailable}");
}
