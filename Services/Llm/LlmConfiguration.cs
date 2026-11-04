using lionheart.Functional;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Llm;

/// <summary>
/// Which model serves chat, and which server serves embeddings. The two are chosen independently
/// because they have to be: Anthropic has no embeddings endpoint, so "chat on Anthropic, embeddings
/// on a local OpenAI-compatible server" is not a fallback, it is the expected production shape.
/// </summary>
/// <remarks>
/// Read once at startup and validated there, so a missing or contradictory key stops the host with
/// the key named rather than failing the first request that needs a model.
/// </remarks>
public sealed record LlmConfiguration(ChatProviderSettings Chat, EmbeddingSettings Embedding)
{
    public const string ChatProviderKey = "Llm__ChatProvider";

    /// <summary>
    /// The configuration, or the first thing wrong with it. Absence is an ordinary outcome of a
    /// fresh checkout, so it is a value naming the environment key rather than an exception.
    /// </summary>
    /// <param name="environmentName">
    /// The host's environment. The relay is refused outside Development, so a stray setting can
    /// never leave a deployed server waiting on a folder for a model that is not there.
    /// </param>
    public static Result<LlmConfiguration, Problem> Read(IConfiguration configuration, string environmentName) =>
        ChatProviderSettings.Read(configuration, environmentName)
            .Bind(chat => EmbeddingSettings.Read(configuration)
                .Map(embedding => new LlmConfiguration(chat, embedding)));
}

/// <summary>
/// The chat provider and what it needs. A closed set: production runs on Anthropic against the
/// owner's API key, development runs on a local LM Studio server, and the relay is a development
/// harness in which whoever is testing the chat plays the model by hand (docs/chat-relay.md).
/// </summary>
public abstract record ChatProviderSettings
{
    private protected ChatProviderSettings() { }

    /// <summary>
    /// The model that answers chat and interprets logged text — the capable one, and the one tools
    /// are offered to.
    /// </summary>
    public abstract string Model { get; }

    /// <summary>
    /// A cheaper model for Athlete Context Card narratives, off the hot path. Defaults to
    /// <see cref="Model"/> when nothing names one.
    /// </summary>
    public abstract string NarrativeModel { get; }

    /// <summary>LM Studio's OpenAI-compatible API, serving a locally hosted model.</summary>
    /// <param name="BaseUrl">Must end at the <c>/v1</c> root.</param>
    public sealed record LmStudio(string BaseUrl, string ApiKey, string ChatModel, string? NarrativeOverride)
        : ChatProviderSettings
    {
        public const string Name = "LmStudio";
        public const string BaseUrlKey = "LmStudio__BaseUrl";
        public const string ApiKeyKey = "LmStudio__ApiKey";
        public const string ModelKey = "LmStudio__Model";

        public override string Model => ChatModel;

        public override string NarrativeModel =>
            string.IsNullOrWhiteSpace(NarrativeOverride) ? ChatModel : NarrativeOverride;
    }

    /// <summary>Anthropic's Messages API, against the owner's own API key.</summary>
    public sealed record Anthropic(string ApiKey, string ChatModel, string? NarrativeOverride)
        : ChatProviderSettings
    {
        public const string Name = "Anthropic";
        public const string ApiKeyKey = "Anthropic__ApiKey";
        public const string ModelKey = "Anthropic__Model";

        /// <summary>
        /// What runs when nothing names a model. Model ids are complete as written — appending a
        /// date suffix to one produces a model that does not exist.
        /// </summary>
        public const string DefaultModel = "claude-opus-5-5";

        public override string Model => ChatModel;

        public override string NarrativeModel =>
            string.IsNullOrWhiteSpace(NarrativeOverride) ? ChatModel : NarrativeOverride;
    }

    /// <summary>
    /// Anthropic's Messages API with the network replaced by a folder: each request is written there
    /// and answered by a response file written by hand. The real Anthropic provider builds the
    /// request and reads the answer, so prompt assembly, cache breakpoints, tools and the tool loop
    /// run exactly as in production, and nothing is billed. See docs/chat-relay.md.
    /// </summary>
    /// <param name="Folder">Where requests are written and responses are read from.</param>
    /// <param name="ChatModel">
    /// The model the request names, so a request file reads as the one production would send.
    /// Nothing serves it.
    /// </param>
    public sealed record Relay(string Folder, string ChatModel) : ChatProviderSettings
    {
        public const string Name = "Relay";
        public const string FolderKey = "Relay__Folder";

        /// <summary>
        /// Outside the repository: every request carries the athlete's own data, and a folder in
        /// the project tree would be one <c>git add</c> or one publish away from leaving the machine.
        /// </summary>
        public static readonly string DefaultFolder = Path.Combine(Path.GetTempPath(), "lionheart-chat-relay");

        public override string Model => ChatModel;

        /// <summary>One model for both roles: a request file is labelled by its shape, not its model.</summary>
        public override string NarrativeModel => ChatModel;
    }

    public static Result<ChatProviderSettings, Problem> Read(IConfiguration configuration, string environmentName) =>
        (configuration["Llm:ChatProvider"] ?? LmStudio.Name).Trim() switch
        {
            var name when name.Equals(LmStudio.Name, StringComparison.OrdinalIgnoreCase) =>
                ReadLmStudio(configuration),
            var name when name.Equals(Anthropic.Name, StringComparison.OrdinalIgnoreCase) =>
                ReadAnthropic(configuration),
            var name when name.Equals(Relay.Name, StringComparison.OrdinalIgnoreCase) =>
                ReadRelay(configuration, environmentName),
            var unknown => Result<ChatProviderSettings, Problem>.Failure(new Problem.Invalid(
                LlmConfiguration.ChatProviderKey,
                $"'{unknown}' is not a chat provider. Set {LlmConfiguration.ChatProviderKey} to "
                + $"'{LmStudio.Name}', '{Anthropic.Name}' or '{Relay.Name}'."))
        };

    private static Result<ChatProviderSettings, Problem> ReadRelay(IConfiguration configuration, string environmentName) =>
        environmentName.Equals(Environments.Development, StringComparison.OrdinalIgnoreCase)
            ? Result<ChatProviderSettings, Problem>.Success(new Relay(
                Optional(configuration, "Relay:Folder") ?? Relay.DefaultFolder,
                Optional(configuration, "Anthropic:Model") ?? Anthropic.DefaultModel))
            : Result<ChatProviderSettings, Problem>.Failure(new Problem.Invalid(
                LlmConfiguration.ChatProviderKey,
                $"The '{Relay.Name}' chat provider is a development harness and does not run in the "
                + $"'{environmentName}' environment. Set {LlmConfiguration.ChatProviderKey} to "
                + $"'{LmStudio.Name}' or '{Anthropic.Name}'."));

    private static Result<ChatProviderSettings, Problem> ReadLmStudio(IConfiguration configuration) =>
        Required(configuration, "LmStudio:BaseUrl", LmStudio.BaseUrlKey)
            .Bind(baseUrl => Required(configuration, "LmStudio:ApiKey", LmStudio.ApiKeyKey)
                .Bind(apiKey => Required(configuration, "LmStudio:Model", LmStudio.ModelKey)
                    .Map(model => (ChatProviderSettings)new LmStudio(
                        baseUrl, apiKey, model, configuration["LmStudio:NarrativeModel"]))));

    private static Result<ChatProviderSettings, Problem> ReadAnthropic(IConfiguration configuration) =>
        Required(configuration, "Anthropic:ApiKey", Anthropic.ApiKeyKey)
            .Map(apiKey => (ChatProviderSettings)new Anthropic(
                apiKey,
                Optional(configuration, "Anthropic:Model") ?? Anthropic.DefaultModel,
                configuration["Anthropic:NarrativeModel"]));

    internal static Result<string, Problem> Required(
        IConfiguration configuration, string path, string environmentKey) =>
        Optional(configuration, path) is { } value
            ? Result<string, Problem>.Success(value)
            : Result<string, Problem>.Failure(new Problem.Invalid(
                environmentKey, $"{environmentKey} is not set. Add it to your .env file — see .env.example."));

    internal static string? Optional(IConfiguration configuration, string path) =>
        configuration[path] is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

/// <summary>
/// The server that produces embedding vectors. Always an OpenAI-compatible one, and deliberately
/// not a choice: Anthropic serves no embeddings, so a provider knob here could only ever be set to
/// something that cannot work.
/// </summary>
/// <remarks>
/// <paramref name="Model"/> is pinned in configuration because vectors from different embedding
/// models are mutually incomparable — <c>Vectors.CosineSimilarity</c> refuses a dimension mismatch
/// rather than scoring it — so changing it invalidates every stored vector and owes a full
/// recompute, never a partial one.
/// </remarks>
public sealed record EmbeddingSettings(string BaseUrl, string ApiKey, string Model)
{
    public const string BaseUrlKey = "Embedding__BaseUrl";
    public const string ApiKeyKey = "Embedding__ApiKey";
    public const string ModelKey = "Embedding__Model";

    public static Result<EmbeddingSettings, Problem> Read(IConfiguration configuration) =>
        ChatProviderSettings.Required(configuration, "Embedding:BaseUrl", BaseUrlKey)
            .Bind(baseUrl => ChatProviderSettings.Required(configuration, "Embedding:ApiKey", ApiKeyKey)
                .Bind(apiKey => ChatProviderSettings.Required(configuration, "Embedding:Model", ModelKey)
                    .Map(model => new EmbeddingSettings(baseUrl, apiKey, model))));
}
