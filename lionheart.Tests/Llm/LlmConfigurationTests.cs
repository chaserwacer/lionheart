using lionheart.Functional;
using lionheart.Services.Llm;
using lionheart.Tests.Support;
using Microsoft.Extensions.Configuration;

namespace lionheart.Tests.Llm;

/// <summary>
/// What the host accepts as a language-model configuration. These matter because the alternative
/// to failing here is failing at the first request that needs a model — by which point the message
/// is an opaque 502 rather than the name of a key in the .env file.
/// </summary>
public class LlmConfigurationTests
{
    private static Result<LlmConfiguration, Problem> Read(params (string Key, string Value)[] settings) =>
        ReadIn("Production", settings);

    private static Result<LlmConfiguration, Problem> ReadIn(string environment, params (string Key, string Value)[] settings) =>
        LlmConfiguration.Read(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s =>
                new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build(), environment);

    private static (string, string)[] LmStudioKeys =>
    [
        ("LmStudio:BaseUrl", "http://127.0.0.1:1234/v1"),
        ("LmStudio:ApiKey", "lm-key"),
        ("LmStudio:Model", "qwen-local"),
    ];

    private static (string, string)[] EmbeddingKeys =>
    [
        ("Embedding:BaseUrl", "http://127.0.0.1:1234/v1"),
        ("Embedding:ApiKey", "lm-key"),
        ("Embedding:Model", "text-embedding-local"),
    ];

    // ---- Choosing the chat provider ----

    /// <summary>
    /// Nothing named means development, which is the local server. Production says so explicitly
    /// rather than relying on a default nobody can see.
    /// </summary>
    [Fact]
    public void Naming_no_provider_runs_chat_on_the_local_server()
    {
        var configuration = ResultAssert.Succeeded(Read([.. LmStudioKeys, .. EmbeddingKeys]));

        var lmStudio = Assert.IsType<ChatProviderSettings.LmStudio>(configuration.Chat);
        Assert.Equal("qwen-local", lmStudio.Model);
    }

    [Fact]
    public void Naming_anthropic_runs_chat_on_anthropic()
    {
        var configuration = ResultAssert.Succeeded(Read([
            ("Llm:ChatProvider", "Anthropic"),
            ("Anthropic:ApiKey", "sk-ant-test"),
            .. EmbeddingKeys,
        ]));

        Assert.IsType<ChatProviderSettings.Anthropic>(configuration.Chat);
    }

    [Fact]
    public void A_provider_nobody_implements_is_refused_naming_the_key_and_the_choices()
    {
        var problem = ResultAssert.Failed(Read([("Llm:ChatProvider", "OpenAI"), .. EmbeddingKeys]));

        Assert.Contains(LlmConfiguration.ChatProviderKey, problem.Description);
        Assert.Contains("LmStudio", problem.Description);
        Assert.Contains("Anthropic", problem.Description);
    }

    // ---- The relay, a development harness ----

    [Fact]
    public void Naming_the_relay_in_development_answers_from_its_folder_under_the_production_model()
    {
        var configuration = ResultAssert.Succeeded(ReadIn("Development", [
            ("Llm:ChatProvider", "Relay"),
            ("Relay:Folder", "/tmp/relay"),
            .. EmbeddingKeys,
        ]));

        var relay = Assert.IsType<ChatProviderSettings.Relay>(configuration.Chat);
        Assert.Equal("/tmp/relay", relay.Folder);
        Assert.Equal(ChatProviderSettings.Anthropic.DefaultModel, relay.Model);
    }

    [Fact]
    public void The_relay_needs_no_api_key_and_has_a_default_folder()
    {
        var configuration = ResultAssert.Succeeded(ReadIn("Development", [("Llm:ChatProvider", "Relay"), .. EmbeddingKeys]));

        Assert.Equal(ChatProviderSettings.Relay.DefaultFolder, Assert.IsType<ChatProviderSettings.Relay>(configuration.Chat).Folder);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void The_relay_refuses_to_run_outside_development(string environment)
    {
        var problem = ResultAssert.Failed(ReadIn(environment, [("Llm:ChatProvider", "Relay"), .. EmbeddingKeys]));

        Assert.Contains(LlmConfiguration.ChatProviderKey, problem.Description);
        Assert.Contains(environment, problem.Description);
    }

    // ---- Each provider's own keys ----

    [Theory]
    [InlineData("LmStudio:BaseUrl", ChatProviderSettings.LmStudio.BaseUrlKey)]
    [InlineData("LmStudio:ApiKey", ChatProviderSettings.LmStudio.ApiKeyKey)]
    [InlineData("LmStudio:Model", ChatProviderSettings.LmStudio.ModelKey)]
    public void A_missing_local_server_key_is_refused_by_name(string path, string environmentKey)
    {
        var settings = LmStudioKeys.Where(s => s.Item1 != path);

        var problem = ResultAssert.Failed(Read([.. settings, .. EmbeddingKeys]));

        Assert.Contains(environmentKey, problem.Description);
    }

    [Fact]
    public void A_missing_anthropic_key_is_refused_by_name()
    {
        var problem = ResultAssert.Failed(Read([("Llm:ChatProvider", "Anthropic"), .. EmbeddingKeys]));

        Assert.Contains(ChatProviderSettings.Anthropic.ApiKeyKey, problem.Description);
    }

    /// <summary>Model ids are complete as written; appending a date suffix names a model that does not exist.</summary>
    [Fact]
    public void Anthropic_defaults_to_the_current_opus_model()
    {
        var configuration = ResultAssert.Succeeded(Read([
            ("Llm:ChatProvider", "Anthropic"),
            ("Anthropic:ApiKey", "sk-ant-test"),
            .. EmbeddingKeys,
        ]));

        Assert.Equal("claude-opus-5-5", configuration.Chat.Model);
    }

    [Fact]
    public void A_named_anthropic_model_overrides_the_default()
    {
        var configuration = ResultAssert.Succeeded(Read([
            ("Llm:ChatProvider", "Anthropic"),
            ("Anthropic:ApiKey", "sk-ant-test"),
            ("Anthropic:Model", "claude-sonnet-5-5"),
            .. EmbeddingKeys,
        ]));

        Assert.Equal("claude-sonnet-5-5", configuration.Chat.Model);
    }

    // ---- The narrative role ----

    [Fact]
    public void The_narrative_model_falls_back_to_the_chat_model_when_nothing_names_one()
    {
        var configuration = ResultAssert.Succeeded(Read([.. LmStudioKeys, .. EmbeddingKeys]));

        Assert.Equal(configuration.Chat.Model, configuration.Chat.NarrativeModel);
    }

    [Fact]
    public void A_named_narrative_model_is_used_for_narratives_only()
    {
        var configuration = ResultAssert.Succeeded(Read([
            .. LmStudioKeys,
            ("LmStudio:NarrativeModel", "qwen-small"),
            .. EmbeddingKeys,
        ]));

        Assert.Equal("qwen-small", configuration.Chat.NarrativeModel);
        Assert.Equal("qwen-local", configuration.Chat.Model);
    }

    // ---- Embeddings, which are never Anthropic's ----

    [Theory]
    [InlineData("Embedding:BaseUrl", EmbeddingSettings.BaseUrlKey)]
    [InlineData("Embedding:ApiKey", EmbeddingSettings.ApiKeyKey)]
    [InlineData("Embedding:Model", EmbeddingSettings.ModelKey)]
    public void A_missing_embedding_key_is_refused_by_name(string path, string environmentKey)
    {
        var settings = EmbeddingKeys.Where(s => s.Item1 != path);

        var problem = ResultAssert.Failed(Read([.. LmStudioKeys, .. settings]));

        Assert.Contains(environmentKey, problem.Description);
    }

    /// <summary>
    /// The constraint that shapes the whole design: Anthropic serves no embeddings endpoint, so
    /// choosing it for chat does not excuse a host from naming an OpenAI-compatible embedding
    /// server. Getting this wrong would leave facet resolution dead at runtime.
    /// </summary>
    [Fact]
    public void Chat_on_anthropic_still_requires_an_embedding_server()
    {
        var problem = ResultAssert.Failed(Read([
            ("Llm:ChatProvider", "Anthropic"),
            ("Anthropic:ApiKey", "sk-ant-test"),
        ]));

        Assert.Contains(EmbeddingSettings.BaseUrlKey, problem.Description);
    }

    /// <summary>
    /// The expected production shape, asserted so it stays expressible: a hosted chat model and a
    /// local embedding server, neither one implying the other.
    /// </summary>
    [Fact]
    public void Chat_on_anthropic_with_embeddings_on_the_local_server_is_a_valid_configuration()
    {
        var configuration = ResultAssert.Succeeded(Read([
            ("Llm:ChatProvider", "Anthropic"),
            ("Anthropic:ApiKey", "sk-ant-test"),
            .. EmbeddingKeys,
        ]));

        Assert.IsType<ChatProviderSettings.Anthropic>(configuration.Chat);
        Assert.Equal("text-embedding-local", configuration.Embedding.Model);
        Assert.Equal("http://127.0.0.1:1234/v1", configuration.Embedding.BaseUrl);
    }

    /// <summary>
    /// The embedding server is configured independently, so it does not have to be the machine
    /// that serves chat even when LM Studio serves both roles elsewhere.
    /// </summary>
    [Fact]
    public void The_embedding_server_need_not_be_the_one_serving_chat()
    {
        var configuration = ResultAssert.Succeeded(Read([
            .. LmStudioKeys,
            ("Embedding:BaseUrl", "http://192.168.1.50:1234/v1"),
            ("Embedding:ApiKey", "other-key"),
            ("Embedding:Model", "text-embedding-local"),
        ]));

        Assert.Equal("http://192.168.1.50:1234/v1", configuration.Embedding.BaseUrl);
        Assert.Equal("http://127.0.0.1:1234/v1", Assert.IsType<ChatProviderSettings.LmStudio>(
            configuration.Chat).BaseUrl);
    }

    [Fact]
    public void Whitespace_is_not_a_configured_value()
    {
        var problem = ResultAssert.Failed(Read([
            ("LmStudio:BaseUrl", "http://127.0.0.1:1234/v1"),
            ("LmStudio:ApiKey", "   "),
            ("LmStudio:Model", "qwen-local"),
            .. EmbeddingKeys,
        ]));

        Assert.Contains(ChatProviderSettings.LmStudio.ApiKeyKey, problem.Description);
    }
}
