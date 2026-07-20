using System.ClientModel;
using lionheart.Model.Interpretation;
using lionheart.Model.Training.Sets;
using lionheart.Services.Embedding;
using lionheart.Services.Interpretation;
using lionheart.Tests.Support;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace lionheart.Tests.Interpretation;

/// <summary>
/// Live checks against LM Studio. These exist because the fakes cannot answer the only question
/// that matters here: does the configured model actually return content matching our schema?
/// </summary>
public class LmStudioParserTests
{
    private const string KnownMovements = """
        Use these existing names verbatim when the text means one of them:
        Movements this athlete logs: Bench Press, Front Squat
        Equipment they own: Barbell, Sled, Bodyweight
        """;

    private static (string BaseUrl, string ApiKey, string Model, string EmbeddingModel) Configuration()
    {
        DotNetEnv.Env.TraversePath().Load();
        return (
            Environment.GetEnvironmentVariable("LmStudio__BaseUrl")!,
            Environment.GetEnvironmentVariable("LmStudio__ApiKey")!,
            Environment.GetEnvironmentVariable("LmStudio__Model")!,
            Environment.GetEnvironmentVariable("LmStudio__EmbeddingModel")!);
    }

    private static InterpretationParser BuildParser()
    {
        var (baseUrl, apiKey, model, _) = Configuration();
        var options = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
        return new InterpretationParser(new ChatClient(model, new ApiKeyCredential(apiKey), options));
    }

    /// <summary>
    /// Guards the reasoning-model trap specifically: with thinking enabled, LM Studio routes the
    /// JSON to `reasoning_content` and leaves `content` empty, which surfaces here as an empty
    /// response rather than a parse.
    /// </summary>
    [LmStudioFact]
    public async Task StructuredLiftLogParsesIntoASessionWithSets()
    {
        var parsed = await BuildParser().ParseAsync(
            new InterpretationRequest("Bench press 225x5, 235x5, 240x3 today. Felt strong.", DateTime.UtcNow),
            KnownMovements);

        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Errors));

        var session = Assert.IsType<DraftSession>(Assert.Single(parsed.Value.Entries));
        Assert.NotEmpty(session.Sets);
        Assert.NotEmpty(session.Decoration.Facets);
        Assert.NotEmpty(session.Decoration.SummaryText);
        Assert.All(session.Sets, set => Assert.NotEmpty(Assert.IsType<Texted>(set.Source).RawText));
    }

    /// <summary>Terse input must stay loggable — a guessed lift here would be worse than a freeform set.</summary>
    [LmStudioFact]
    public async Task TerseLogStillProducesAnEntry()
    {
        var parsed = await BuildParser().ParseAsync(
            new InterpretationRequest("sled pull+push worked up to 235, 2 or 3 sets. brutal", DateTime.UtcNow),
            KnownMovements);

        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Errors));
        Assert.NotEmpty(parsed.Value.Entries);
    }

    [LmStudioFact]
    public async Task NonTrainingTextBecomesAContextRatherThanASession()
    {
        var parsed = await BuildParser().ParseAsync(
            new InterpretationRequest("Slept terribly last night, maybe four hours.", DateTime.UtcNow),
            KnownMovements);

        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Errors));
        var entry = Assert.Single(parsed.Value.Entries);
        Assert.False(entry is DraftSession, "A sleep note is a context, not a training session.");
    }

    [LmStudioFact]
    public async Task EmbeddingsComeBackWithAStableDimensionCount()
    {
        var (baseUrl, apiKey, _, embeddingModel) = Configuration();
        var generator = new LmStudioEmbeddingGenerator(new EmbeddingClient(
            embeddingModel, new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) }));

        var vectors = await generator.GenerateBatchAsync(["heavy push session", "terrible sleep"]);

        Assert.True(vectors.IsSuccess, string.Join("; ", vectors.Errors));
        Assert.Equal(2, vectors.Value.Count);
        Assert.NotEmpty(vectors.Value[0]);
        // Equal dimensions are what make stored vectors comparable at all — see Vectors.CosineSimilarity.
        Assert.Equal(vectors.Value[0].Length, vectors.Value[1].Length);
    }
}
