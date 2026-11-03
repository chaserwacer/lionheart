using System.Diagnostics;
using System.Text.RegularExpressions;
using lionheart.Functional;
using lionheart.Model.Chat.Memory;
using lionheart.Services.Embedding;

namespace lionheart.Services.Chat.Memory;

/// <summary>What a new session starts with: the standing memories, and the others its opening bears on.</summary>
public sealed record RecalledMemories(IReadOnlyList<ChatMemory> Core, IReadOnlyList<ChatMemory> Relevant)
{
    public static RecalledMemories None { get; } = new([], []);

    public bool IsEmpty => Core.Count == 0 && Relevant.Count == 0;
}

/// <summary>
/// Which current memories reach a session, and in what order a search returns them. Pure: the
/// vectors arrive already computed, so the ranking is testable without an embedding server.
/// </summary>
/// <remarks>
/// Ranking is by embedding similarity where both sides have one, and by shared words where either
/// does not — the embedding server is a local process that may not be running, and a memory written
/// while it was down still has to be findable.
/// </remarks>
public static partial class MemoryRecall
{
    /// <summary>
    /// The most standing memories a session opens with. Goals, current injuries and preferences
    /// are a handful in practice; the cap only stops a runaway set from crowding the prompt, and
    /// keeps the newest.
    /// </summary>
    public const int CoreLimit = 30;

    /// <summary>How many other memories a session's opening message recalls.</summary>
    public const int RelevantLimit = 6;

    /// <summary>How many memories one search returns.</summary>
    public const int SearchLimit = 8;

    /// <summary>
    /// The least cosine similarity that counts as bearing on the text. Low, because recall at the
    /// start of a session is cheap to get slightly wrong — a marginal memory costs a line of prompt —
    /// and expensive to miss.
    /// </summary>
    public const double MinimumSimilarity = 0.35;

    public static RecalledMemories ForSession(IReadOnlyList<ChatMemory> current, string opening, float[] openingVector)
    {
        var core = current
            .Where(memory => memory.Kind.IsCore)
            .OrderByDescending(memory => memory.RecordedAt)
            .Take(CoreLimit)
            .OrderBy(memory => memory.Kind)
            .ThenBy(memory => memory.RecordedAt)
            .ToList();

        var relevant = Ranked(current.Where(memory => !memory.Kind.IsCore), opening, openingVector)
            .Take(RelevantLimit)
            .ToList();

        return new RecalledMemories(core, relevant);
    }

    /// <summary>
    /// The most current memories a consolidation is shown. Below it, every memory is shown, so the
    /// model can revise any of them; above it, the standing ones and those nearest the session.
    /// </summary>
    public const int ConsolidationLimit = 60;

    /// <summary>
    /// The memories a session's consolidation may revise or retire. A memory left out cannot be
    /// superseded by this session, so the limit is generous and only bites on a large memory.
    /// </summary>
    public static IReadOnlyList<ChatMemory> ForConsolidation(
        IReadOnlyList<ChatMemory> current, string sessionText, float[] sessionVector)
    {
        if (current.Count <= ConsolidationLimit) return current;

        var core = current.Where(memory => memory.Kind.IsCore).OrderByDescending(memory => memory.RecordedAt).Take(CoreLimit).ToList();
        var others = current.Where(memory => !memory.Kind.IsCore).ToList();
        var nearest = Ranked(others, sessionText, sessionVector).ToList();

        // Topped up with the newest of the rest, so a session that matches little still sees a full set.
        return
        [
            .. core,
            .. nearest
                .Concat(others.OrderByDescending(memory => memory.RecordedAt))
                .Distinct()
                .Take(ConsolidationLimit - core.Count),
        ];
    }

    /// <summary>Every current memory, core included, that bears on the query, best first.</summary>
    public static IReadOnlyList<ChatMemory> Search(IReadOnlyList<ChatMemory> current, string query, float[] queryVector) =>
        [.. Ranked(current, query, queryVector).Take(SearchLimit)];

    private static IEnumerable<ChatMemory> Ranked(IEnumerable<ChatMemory> candidates, string text, float[] vector)
    {
        var words = Words(text);
        return candidates
            .Select(memory => (Memory: memory, Score: Score(memory, words, vector)))
            .Where(scored => scored.Score is Similarity.Semantic { Cosine: >= MinimumSimilarity } or Similarity.Lexical { Shared: > 0 })
            // Semantic matches rank above lexical ones: a cosine and a word count are not on one scale,
            // and a memory the embedding found is the stronger evidence.
            .OrderByDescending(scored => scored.Score is Similarity.Semantic)
            .ThenByDescending(scored => scored.Score switch
            {
                Similarity.Semantic semantic => semantic.Cosine,
                Similarity.Lexical lexical => lexical.Shared,
                _ => throw new UnreachableException(),
            })
            .ThenByDescending(scored => scored.Memory.RecordedAt)
            .Select(scored => scored.Memory);
    }

    private static Similarity Score(ChatMemory memory, IReadOnlySet<string> words, float[] vector) =>
        memory.IsEmbedded && vector.Length > 0
            ? Vectors.CosineSimilarity(memory.Vector, vector).Match(
                Similarity (cosine) => new Similarity.Semantic(cosine),
                // A vector from a different embedding model cannot be compared; words still can.
                _ => Lexical(memory, words))
            : Lexical(memory, words);

    private static Similarity Lexical(ChatMemory memory, IReadOnlySet<string> words) =>
        new Similarity.Lexical(Words(memory.Text).Count(words.Contains));

    /// <summary>The text's content words: lower-cased, three letters or more, without the commonest English ones.</summary>
    private static HashSet<string> Words(string text) =>
        [.. WordPattern().Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length >= 3 && !StopWords.Contains(word))];

    private static readonly HashSet<string> StopWords =
    [
        "the", "and", "for", "are", "but", "not", "you", "all", "any", "can", "had", "has", "have", "her", "his",
        "him", "how", "its", "was", "what", "when", "where", "which", "who", "why", "will", "with", "that",
        "this", "from", "they", "them", "then", "than", "there", "their", "been", "were", "would", "could",
        "should", "about", "into", "onto", "over", "just", "also", "does", "did", "doing", "being", "because",
    ];

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex WordPattern();

    private abstract record Similarity
    {
        public sealed record Semantic(double Cosine) : Similarity;

        public sealed record Lexical(int Shared) : Similarity;
    }
}
