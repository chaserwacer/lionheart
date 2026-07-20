using lionheart.Model.Profile;
using lionheart.Services.Profile;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Llm;

/// <summary>
/// The Athlete Context Card's numbers are deterministic; the narrative model only writes prose
/// over facts that are already computed. The documented contract is that its failure must never
/// break card generation — so every way LM Studio can fail is exercised against the real card
/// service, and the card still has to come back.
/// </summary>
public sealed class NarrativeFallbackTests : IDisposable
{
    private readonly RemodelDatabase _db = new();
    private readonly FakeChatServer _server = new();

    public void Dispose()
    {
        _server.Dispose();
        _db.Dispose();
    }

    private async Task<AthleteContextCard> BuildCard()
    {
        using var context = _db.CreateContext();
        var service = new AthleteContextCardService(
            context, new NarrativeChatClient(_server.CreateChatClient()));

        var result = await service.GetOrBuildCardAsync(new IdentityUser { Id = _db.UserId.ToString() });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task A_card_is_still_produced_when_the_narrative_model_is_unreachable()
    {
        _server.RespondsWithTransportFailure();

        var card = await BuildCard();

        Assert.Equal(_db.UserId, card.UserID);
        Assert.False(string.IsNullOrWhiteSpace(card.StableProfileText));
    }

    [Fact]
    public async Task A_card_is_still_produced_when_the_narrative_model_answers_with_an_error_body()
    {
        _server.AlwaysResponds(CannedChat.Error("Model 'narrator' is not loaded."));

        var card = await BuildCard();

        Assert.False(string.IsNullOrWhiteSpace(card.StableProfileText));
    }

    [Fact]
    public async Task A_card_is_still_produced_when_the_narrative_model_returns_no_choices()
    {
        _server.AlwaysResponds(CannedChat.NoChoices());

        var card = await BuildCard();

        Assert.False(string.IsNullOrWhiteSpace(card.StableProfileText));
    }

    [Fact]
    public async Task A_card_is_still_produced_when_the_narrative_model_returns_garbage()
    {
        _server.AlwaysResponds("<html>502 Bad Gateway</html>");

        var card = await BuildCard();

        Assert.False(string.IsNullOrWhiteSpace(card.StableProfileText));
    }

    [Fact]
    public async Task A_card_is_still_produced_when_the_narrative_model_answers_normally()
    {
        _server.AlwaysResponds(CannedChat.Content("Training has been consistent through the block."));

        var card = await BuildCard();

        Assert.False(string.IsNullOrWhiteSpace(card.StableProfileText));
    }

    /// <summary>
    /// The wrapper exists so the flagship chat model and the cheap narrative model can be two
    /// singletons of the same type without DI ambiguity — it must hand back the client it was
    /// given rather than resolving one of its own.
    /// </summary>
    [Fact]
    public void The_narrative_wrapper_exposes_the_client_it_was_built_with()
    {
        var client = _server.CreateChatClient("narrator-model");

        Assert.Same(client, new NarrativeChatClient(client).Client);
    }
}
