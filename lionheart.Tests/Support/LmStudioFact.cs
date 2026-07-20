namespace lionheart.Tests.Support;

/// <summary>
/// A test that talks to a live LM Studio server. Skipped unless <c>LIONHEART_LMSTUDIO_TESTS=1</c>,
/// so the default suite stays hermetic and green with nothing running. Run these after changing
/// the parse prompt, the JSON schema, or the configured model — they are the only check that the
/// model still honors the schema, which no fake can tell you.
/// </summary>
public sealed class LmStudioFactAttribute : FactAttribute
{
    public LmStudioFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("LIONHEART_LMSTUDIO_TESTS") != "1")
        {
            Skip = "Set LIONHEART_LMSTUDIO_TESTS=1 (and have LM Studio running) to run.";
        }
    }
}
