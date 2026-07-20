using Microsoft.Extensions.Configuration;

namespace lionheart.Tests.LocalModel;

/// <summary>
/// The LmStudio configuration, read exactly the way Program.cs reads it: DotNetEnv loads
/// <c>.env</c> into the process environment (traversing upward from the working directory),
/// and the environment-variable provider maps <c>LmStudio__X</c> to <c>LmStudio:X</c>.
/// No second configuration system, no hardcoded URL or model name anywhere in the tests.
/// </summary>
public sealed record LmStudioSettings(string BaseUrl, string ApiKey, string Model)
{
    public const string BaseUrlKey = "LmStudio__BaseUrl";
    public const string ApiKeyKey = "LmStudio__ApiKey";
    public const string ModelKey = "LmStudio__Model";

    /// <summary>
    /// Returns the settings, or the name of the first key that is missing. Absence is an
    /// ordinary outcome here — a machine without a local model is expected — so it is a value,
    /// not an exception.
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

        return (new LmStudioSettings(baseUrl, apiKey, model), null);
    }
}
