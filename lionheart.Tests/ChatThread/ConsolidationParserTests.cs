using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Memory;
using lionheart.Services.Chat.Thread;
using lionheart.Services.Llm;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// The consolidating model answers in short handles; the parser maps them back onto real messages
/// and memories, and sets aside the changes it cannot place without throwing away the rest.
/// </summary>
public sealed class ConsolidationParserTests
{
    private static readonly Guid Athlete = Guid.NewGuid();

    private readonly LHUserChatMessage _said = Said("My knee is fine now.");
    private readonly ChatMemory _knee = AMemory.For(Athlete).OfKind(MemoryKind.Injury).Saying("Left knee pain on deep squats.").Build();

    private ConsolidationPrompt Prompt() =>
        ConsolidationPrompt.For(Some.Clock, [_said], [_knee], TimeZoneInfo.Utc);

    private Result<ConsolidationPlan, Problem> Read(string json) =>
        ConsolidationParser.Read(new ModelTurn.Answered(json, 100), Prompt());

    [Fact]
    public void The_prompt_names_messages_and_memories_by_handle()
    {
        var prompt = Prompt();

        Assert.Equal(_said.ChatMessageItemID, prompt.Messages["m1"]);
        Assert.Same(_knee, prompt.Memories["k1"]);
        Assert.Contains("k1 [injury, recorded 2026-07-18] Left knee pain on deep squats.", prompt.UserText);
        Assert.Contains("m1 athlete (09:30): My knee is fine now.", prompt.UserText);
    }

    [Fact]
    public void A_revision_names_the_kept_memory_and_the_messages_it_rests_on()
    {
        var plan = ResultAssert.Succeeded(Read("""
            {"changes":[{"action":"revise","memoryId":"k1","kind":"fact","text":"Left knee pain resolved as of 2026-07-18.","sourceMessageIds":["m1"]}]}
            """));

        var revise = Assert.IsType<MemoryChange.Revise>(Assert.Single(plan.Changes));
        Assert.Equal(_knee.MemoryId, revise.MemoryId);
        Assert.Equal(MemoryKind.Fact, revise.Kind);
        Assert.Equal([_said.ChatMessageItemID], revise.SourceMessageIds);
        Assert.Empty(plan.Unusable);
    }

    [Fact]
    public void A_change_naming_no_kept_memory_is_set_aside_and_the_rest_are_kept()
    {
        var plan = ResultAssert.Succeeded(Read("""
            {"changes":[
              {"action":"revise","memoryId":"k7","kind":"fact","text":"Nope.","sourceMessageIds":[]},
              {"action":"remember","memoryId":"","kind":"goal","text":"Squat 405 by spring.","sourceMessageIds":["m1"]}
            ]}
            """));

        var remember = Assert.IsType<MemoryChange.Remember>(Assert.Single(plan.Changes));
        Assert.Equal("Squat 405 by spring.", remember.Text);
        Assert.Contains("'k7' names no kept memory.", Assert.Single(plan.Unusable));
    }

    [Fact]
    public void A_source_handle_that_names_no_message_is_dropped()
    {
        var plan = ResultAssert.Succeeded(Read("""
            {"changes":[{"action":"remember","memoryId":"","kind":"fact","text":"He trains at 6am.","sourceMessageIds":["m1","m9"]}]}
            """));

        Assert.Equal([_said.ChatMessageItemID], Assert.Single(plan.Changes).SourceMessageIds);
    }

    [Theory]
    [InlineData("""{"changes":[{"action":"remember","memoryId":"","kind":"mood","text":"x","sourceMessageIds":[]}]}""")]
    [InlineData("""{"changes":[{"action":"forget","memoryId":"k1","kind":"fact","text":"x","sourceMessageIds":[]}]}""")]
    [InlineData("""{"changes":[{"action":"remember","memoryId":"","kind":"fact","text":"  ","sourceMessageIds":[]}]}""")]
    public void A_malformed_change_is_set_aside_rather_than_failing_the_plan(string json)
    {
        var plan = ResultAssert.Succeeded(Read(json));

        Assert.Empty(plan.Changes);
        Assert.Single(plan.Unusable);
    }

    [Fact]
    public void A_retirement_without_a_reason_is_given_one()
    {
        var plan = ResultAssert.Succeeded(Read("""
            {"changes":[{"action":"retire","memoryId":"k1","kind":"injury","text":"","sourceMessageIds":["m1"]}]}
            """));

        Assert.Equal("No longer holds.", Assert.IsType<MemoryChange.Retire>(Assert.Single(plan.Changes)).Reason);
    }

    [Fact]
    public void An_answer_that_is_not_json_fails_the_consolidation() =>
        Assert.IsType<Problem.Upstream>(ResultAssert.Failed(Read("I think he said his knee is fine.")));

    [Fact]
    public void An_empty_answer_fails_the_consolidation() =>
        Assert.IsType<Problem.Upstream>(ResultAssert.Failed(Read("")));

    private static LHUserChatMessage Said(string text) => new()
    {
        ChatMessageItemID = Guid.NewGuid(),
        ChatConversationID = Guid.NewGuid(),
        CreationTime = Some.Clock,
        TokenCount = 5,
        Content = text,
    };
}
