using System.Text.Json;
using Ardalis.Result;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;
using lionheart.Services.Chat;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using OpenAI.Chat;
using Services.Chat;

namespace lionheart.Tests.LocalModel;

/// <summary>
/// The opt-in half of the LLM suite: a real local model, reached through the same
/// configuration the app uses. Excluded from a plain run by lionheart.runsettings.
/// </summary>
/// <remarks>
/// Assertions are on structure, never on wording. A local model's phrasing changes with the
/// model, the quant, and the temperature; what must hold is that the output parses into the
/// domain, that facet axes stay inside the closed enum, and that a tool call round-trips. Any
/// test that would fail because the model chose different words is a broken test, not a finding.
/// </remarks>
[Trait("Category", "LocalModel")]
[Collection(LocalModelCollection.Name)]
public sealed class LocalModelChatTests(LocalModelFixture fixture) : IDisposable
{
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(5);

    private readonly TestToolbox _toolbox = new();

    public void Dispose() => _toolbox.Dispose();

    /// <summary>Low temperature: these tests want the model's most typical output, not its range.</summary>
    private static ChatCompletionOptions Deterministic() => new() { Temperature = 0f };

    private ChatClient Client()
    {
        Skip.If(fixture.Unavailable is not null, $"LocalModel tests skipped: {fixture.Unavailable}");
        return fixture.CreateChatClient();
    }

    [SkippableFact]
    public async Task The_configured_model_answers_at_all()
    {
        using var cancellation = new CancellationTokenSource(Budget);

        var completion = await Client().CompleteChatAsync(
            [new UserChatMessage("Reply with the single word: ready")],
            Deterministic(),
            cancellation.Token);

        var result = completion.ToResult();
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.NotEmpty(string.Join("", result.Value.Content.Select(c => c.Text)));
    }

    /// <summary>
    /// The doc's canonical terse log. The model is asked for the storage shape directly, and the
    /// assertion is that it deserializes into real SetEntry values with a Texted source and an
    /// in-range confidence — not that it found any particular number of sets.
    /// </summary>
    [SkippableFact]
    public async Task A_terse_log_parses_into_schema_valid_sets_with_a_texted_source()
    {
        using var cancellation = new CancellationTokenSource(Budget);
        const string terseLog = "sled pull+push worked up to 235, 2 or 3 sets";

        var completion = await Client().CompleteChatAsync(
            [
                new SystemChatMessage(ParseInstruction),
                new UserChatMessage(terseLog),
            ],
            Deterministic(),
            cancellation.Token);

        var result = completion.ToResult();
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));

        var sets = JsonSerializer.Deserialize<IReadOnlyList<SetEntry>>(
            ExtractJsonArray(string.Join("", result.Value.Content.Select(c => c.Text))));

        Assert.NotNull(sets);
        Assert.NotEmpty(sets);
        Assert.All(sets, set =>
        {
            Assert.NotNull(set);
            var source = Assert.IsType<Texted>(set.Source);
            Assert.False(string.IsNullOrWhiteSpace(source.RawText));
            Assert.InRange(source.StructureConfidence, 0, 1);
            Assert.All(set.Facets, facet => Assert.True(Enum.IsDefined(facet.Axis)));
        });
    }

    [SkippableFact]
    public async Task Proposed_facets_stay_inside_the_closed_axis_enum()
    {
        using var cancellation = new CancellationTokenSource(Budget);

        var completion = await Client().CompleteChatAsync(
            [
                new SystemChatMessage(FacetInstruction),
                new UserChatMessage("pressure-washed the driveway for three hours, back is fried"),
            ],
            Deterministic(),
            cancellation.Token);

        var result = completion.ToResult();
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));

        var facets = JsonSerializer.Deserialize<IReadOnlyList<Facet>>(
            ExtractJsonArray(string.Join("", result.Value.Content.Select(c => c.Text))));

        Assert.NotNull(facets);
        Assert.All(facets, facet =>
        {
            Assert.True(Enum.IsDefined(facet.Axis));
            Assert.False(string.IsNullOrWhiteSpace(facet.Value));
        });
    }

    /// <summary>
    /// A real tool-call round trip through the real executor: the model must emit a call the
    /// registry recognises, and the loop must run it and come back with a final answer.
    /// </summary>
    [SkippableFact]
    public async Task A_registered_tool_is_actually_called_and_its_result_returned()
    {
        var client = Client();
        var options = Deterministic();
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "count_sets",
            "Returns how many sets the athlete performed over the given number of days.",
            BinaryData.FromString(
                "{\"type\":\"object\",\"properties\":{\"days\":{\"type\":\"integer\"}},\"required\":[\"days\"]}")));

        var service = new ChatCompletionService(client, _toolbox.Executor);
        var result = await service.GenerateChatCompletionAsync(
            new IdentityUser("tester@lionheart.local"),
            new ChatCompletionRequest
            {
                Conversation = AConversation.With("Use the count_sets tool for the last 5 days, then tell me the number."),
                Options = options,
            });

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Contains(_toolbox.Tools.Calls, call => call.StartsWith("count:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Local models wrap JSON in prose and fences no matter how firmly they are asked not to.
    /// Extracting the array is the test harness's job; strictness belongs in the assertions
    /// about what the array contains.
    /// </summary>
    private static string ExtractJsonArray(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');

        Assert.True(start >= 0 && end > start, $"Model output contained no JSON array. Raw output:\n{text}");
        return text[start..(end + 1)];
    }

    private const string ParseInstruction = """
        You convert a terse training log into JSON. Reply with ONLY a JSON array, no prose, no code fences.
        Each element is one performed set and must have this shape:
          {"$type":"freeform","Source":{"$type":"texted","RawText":"<the athlete's words>","StructureConfidence":<0..1>},"Facets":[]}
        Use "$type":"freeform" whenever you cannot confidently extract reps and weight.
        StructureConfidence is your confidence in any structure you extracted, between 0 and 1.
        """;

    private const string FacetInstruction = """
        You label an athlete's log with facets. Reply with ONLY a JSON array, no prose, no code fences.
        Each element is {"Axis":<integer>,"Value":"<short lowercase label>"}.
        Axis must be one of these integers, and nothing else:
          0 = Modality, 1 = MovementPattern, 2 = MuscleRegion, 3 = Intent, 4 = LifeDomain, 5 = BodyState
        Emit at most four facets.
        """;
}
