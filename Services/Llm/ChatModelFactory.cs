using System.ClientModel;
using lionheart.Services.Llm.Anthropic;
using lionheart.Services.Llm.LmStudio;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using AnthropicSettings = lionheart.Services.Llm.ChatProviderSettings.Anthropic;
using LmStudioSettings = lionheart.Services.Llm.ChatProviderSettings.LmStudio;

namespace lionheart.Services.Llm;

/// <summary>
/// Builds the configured provider's client. The composition root's only knowledge of either SDK's
/// constructor, so adding a provider touches this file and the provider's own, and nothing else.
/// </summary>
/// <remarks>
/// The model name is a parameter rather than read from the settings, because one provider serves
/// two roles at different models: the flagship model that answers chat and interprets logs, and a
/// cheaper one for off-the-hot-path narratives.
/// </remarks>
public static class ChatModelFactory
{
    public static IChatModel Create(ChatProviderSettings settings, string model) => settings switch
    {
        LmStudioSettings lmStudio => new LmStudioChatModel(new ChatClient(
            model: model,
            credential: new ApiKeyCredential(lmStudio.ApiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri(lmStudio.BaseUrl) })),

        AnthropicSettings anthropic => new AnthropicChatModel(
            new global::Anthropic.AnthropicClient { ApiKey = anthropic.ApiKey }, model),

        _ => throw new ArgumentOutOfRangeException(
            nameof(settings), settings, "No chat provider is configured for these settings.")
    };

    /// <summary>
    /// The embedding client. Takes <see cref="EmbeddingSettings"/> rather than a provider choice
    /// because there is none to make: Anthropic serves no embeddings endpoint.
    /// </summary>
    public static EmbeddingClient CreateEmbeddingClient(EmbeddingSettings settings) =>
        new(model: settings.Model,
            credential: new ApiKeyCredential(settings.ApiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri(settings.BaseUrl) });
}
