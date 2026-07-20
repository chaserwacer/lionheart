using Microsoft.Extensions.Configuration;

namespace lionheart.Tests.Support;

/// <summary>
/// The LmStudio configuration, read exactly the way Program.cs reads it: DotNetEnv loads
/// <c>.env</c> into the process environment (traversing upward from the working directory),
/// and the environment-variable provider maps <c>LmStudio__X</c> to <c>LmStudio:X</c>.
/// One reader for every test that needs the local model — no parallel configuration system,
/// and no hardcoded URL or model name anywhere in test code.
/// </summary>
public sealed record LmStudioSettings(string BaseUrl, string ApiKey, string Model, string EmbeddingModel)
{
    public const string BaseUrlKey = "LmStudio__BaseUrl";
    public const string ApiKeyKey = "LmStudio__ApiKey";
    public const string ModelKey = "LmStudio__Model";
    public const string EmbeddingModelKey = "LmStudio__EmbeddingModel";

    /// <summary>
    /// The settings, or the name of the first key that is missing. Absence is an ordinary
    /// outcome — a machine without a local model is expected — so it is a value, not an
    /// exception, and the caller turns it into a skip that names the key.
    /// </summary>
    public static (LmStudioSettings? Settings, string? MissingKey) Read()
    {
        DotNetEnv.Env.TraversePath().Load();
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        var baseUrl = configuration["LmStudio:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return (null, BaseUrlKey);

        var apiKey = configuration["LmStudio:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return (null, ApiKeyKey);

        var model = configuration["LmStudio:Model"];
        if (string.IsNullOrWhiteSpace(model)) return (null, ModelKey);

        var embeddingModel = configuration["LmStudio:EmbeddingModel"];
        if (string.IsNullOrWhiteSpace(embeddingModel)) return (null, EmbeddingModelKey);

        return (new LmStudioSettings(baseUrl, apiKey, model, embeddingModel), null);
    }
}
