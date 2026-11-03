using lionheart.Model.Chat.Memory;
using lionheart.Services.Chat.Memory;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// What a new session starts with: every standing memory, plus the others its opening bears on —
/// by embedding where there is one, by words where there is not.
/// </summary>
public sealed class MemoryRecallTests
{
    private static readonly Guid Athlete = Guid.NewGuid();

    private static AMemory A => AMemory.For(Athlete);

    [Fact]
    public void Standing_memories_reach_every_session_whatever_it_is_about()
    {
        var goal = A.OfKind(MemoryKind.Goal).Saying("Build weighted dips to +90 lb.").Build();
        var injury = A.OfKind(MemoryKind.Injury).Saying("Left knee pain on deep squats.").Build();
        var schedule = A.OfKind(MemoryKind.Fact).Saying("He works nights on Thursdays.").Build();

        var recall = MemoryRecall.ForSession([goal, injury, schedule], "how was my sleep last night", []);

        Assert.Equal([goal, injury], recall.Core);
        Assert.Empty(recall.Relevant);
    }

    [Fact]
    public void Other_memories_are_recalled_when_the_opening_is_near_them_in_meaning()
    {
        var near = A.Saying("He switched to a hack squat for leg days.").EmbeddedAs(FakeEmbeddingGenerator.OneHot(3)).Build();
        var far = A.Saying("He owns a 24 kg kettlebell.").EmbeddedAs(FakeEmbeddingGenerator.OneHot(9)).Build();

        var recall = MemoryRecall.ForSession(
            [near, far], "what should legs look like this week", FakeEmbeddingGenerator.Between(3, 5, 0.1f));

        Assert.Equal([near], recall.Relevant);
    }

    [Fact]
    public void A_memory_with_no_vector_is_recalled_by_the_words_it_shares_with_the_opening()
    {
        var unembedded = A.Saying("He decided to run the trail loop on Sundays.").Build();
        var embeddedElsewhere = A.Saying("He prefers metric plates.").EmbeddedAs(FakeEmbeddingGenerator.OneHot(9)).Build();

        var recall = MemoryRecall.ForSession(
            [unembedded, embeddedElsewhere], "is the trail loop too much this week?", FakeEmbeddingGenerator.OneHot(1));

        Assert.Equal([unembedded], recall.Relevant);
    }

    [Fact]
    public void An_opening_that_could_not_be_embedded_still_recalls_by_words()
    {
        var memory = A.Saying("He decided to drop barbell bench for dumbbell bench.")
            .EmbeddedAs(FakeEmbeddingGenerator.OneHot(2)).Build();

        var recall = MemoryRecall.ForSession([memory], "bench plan for the block", []);

        Assert.Equal([memory], recall.Relevant);
    }

    [Fact]
    public void A_vector_from_a_different_embedding_model_falls_back_to_words_rather_than_failing()
    {
        var memory = A.Saying("He decided to run the trail loop on Sundays.").EmbeddedAs([1f, 0f, 0f]).Build();

        var recall = MemoryRecall.ForSession([memory], "trail loop", FakeEmbeddingGenerator.OneHot(1));

        Assert.Equal([memory], recall.Relevant);
    }

    [Fact]
    public void Semantic_matches_rank_above_word_matches()
    {
        var byWords = A.Saying("Squat depth: he stops at parallel.").Build();
        var byMeaning = A.Saying("Leg days are hack squat now.").EmbeddedAs(FakeEmbeddingGenerator.OneHot(4)).Build();

        var found = MemoryRecall.Search([byWords, byMeaning], "squat", FakeEmbeddingGenerator.OneHot(4));

        Assert.Equal([byMeaning, byWords], found);
    }

    [Fact]
    public void The_core_is_capped_to_its_newest_members()
    {
        var goals = Enumerable.Range(0, MemoryRecall.CoreLimit + 5)
            .Select(day => A.OfKind(MemoryKind.Goal).Saying($"Goal {day}.").RecordedOn(Some.Clock.AddDays(day)).Build())
            .ToList();

        var recall = MemoryRecall.ForSession(goals, "anything", []);

        Assert.Equal(MemoryRecall.CoreLimit, recall.Core.Count);
        Assert.DoesNotContain(goals[0], recall.Core);
        Assert.Contains(goals[^1], recall.Core);
    }

    [Fact]
    public void A_consolidation_sees_every_memory_while_there_are_few()
    {
        var memories = Enumerable.Range(0, 10).Select(i => A.Saying($"Fact {i}.").Build()).ToList();

        Assert.Equal(memories, MemoryRecall.ForConsolidation(memories, "unrelated words", []));
    }

    [Fact]
    public void A_large_memory_shows_a_consolidation_the_core_and_a_full_set_of_others()
    {
        var core = A.OfKind(MemoryKind.Goal).Saying("Bench 315.").Build();
        var others = Enumerable.Range(0, MemoryRecall.ConsolidationLimit + 20)
            .Select(i => A.Saying($"Fact number {i}.").RecordedOn(Some.Clock.AddMinutes(i)).Build())
            .ToList();

        var shown = MemoryRecall.ForConsolidation([core, .. others], "nothing shared", []);

        Assert.Equal(MemoryRecall.ConsolidationLimit, shown.Count);
        Assert.Contains(core, shown);
        Assert.Equal(shown.Count, shown.Distinct().Count());
    }
}
