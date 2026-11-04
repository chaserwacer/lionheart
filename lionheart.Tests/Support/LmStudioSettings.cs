using lionheart.Functional;
using lionheart.Services.Llm;
using Microsoft.Extensions.Configuration;

namespace lionheart.Tests.Support;

/// <summary>
/// The configuration the live tests need, read exactly the way Program.cs reads it: DotNetEnv
/// loads <c>.env</c> into the process environment (traversing upward from the working directory),
/// and the environment-variable provider maps <c>LmStudio__X</c> to <c>LmStudio:X</c>.
/// One reader for every test that needs the local model — no parallel configuration system,
/// and no hardcoded URL or model name anywhere in test code.
/// </summary>
/// <remarks>
/// The LM Studio keys are read directly rather than through <see cref="ChatProviderSettings.Read"/>,
/// which honours <c>Llm__ChatProvider</c>: these tests exercise the LM Studio provider itself, so
/// they must target it whichever provider is configured to serve chat. The embedding half is the
/// application's own <see cref="EmbeddingSettings"/>, because embeddings are configured
/// independently of chat and there is nothing provider-specific about reaching them.
/// </remarks>
public sealed record LmStudioSettings(
    string BaseUrl, string ApiKey, string Model, EmbeddingSettings Embedding)
{
    public const string BaseUrlKey = ChatProviderSettings.LmStudio.BaseUrlKey;
    public const string ApiKeyKey = ChatProviderSettings.LmStudio.ApiKeyKey;
    public const string ModelKey = ChatProviderSettings.LmStudio.ModelKey;

    /// <summary>
    /// The settings, or the name of the first key that is missing. Absence is an ordinary
    /// outcome — a machine without a local model is expected — so it is a value, not an
    /// exception, and the caller turns it into a skip that names the key.
    /// </summary>
    public static (LmStudioSettings? Settings, string? MissingKey) Read()
    {
        lionheart.Services.Llm.EnvironmentFile.Load();
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        var baseUrl = configuration["LmStudio:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return (null, BaseUrlKey);

        var apiKey = configuration["LmStudio:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return (null, ApiKeyKey);

        var model = configuration["LmStudio:Model"];
        if (string.IsNullOrWhiteSpace(model)) return (null, ModelKey);

        return EmbeddingSettings.Read(configuration).Match(
            embedding => (new LmStudioSettings(baseUrl, apiKey, model, embedding), (string?)null),
            problem => ((LmStudioSettings?)null, MissingKeyName(problem)));
    }

    private static string MissingKeyName(Problem problem) => problem is Problem.Invalid invalid
        ? invalid.Fields[0].Field
        : problem.Description;
}
