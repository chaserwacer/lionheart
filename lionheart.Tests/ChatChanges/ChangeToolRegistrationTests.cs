using System.Text.Json;
using lionheart.Model.Chat.Tools;
using lionheart.Services.Chat.Changes;
using Microsoft.Extensions.DependencyInjection;
using Model.Chat.Tools;

namespace lionheart.Tests.ChatChanges;

/// <summary>
/// That the write tools and their lookups reach the model as the app registers them: every proposing
/// tool known as one, offered to the thread and kept from a conversation that only reads.
/// </summary>
/// <remarks>
/// The registry drops a tool whose signature it cannot describe without a word, so a broken one would
/// show only as a chat that never offers to plan anything.
/// </remarks>
public sealed class ChangeToolRegistrationTests
{
    private static readonly string[] Proposing =
    [
        "ProposePlanSession", "ProposePlanChange", "ProposePlanRemoval", "ProposeNonTrainingDay",
        "ProposeFreeingDay", "ProposePriorityLift", "ProposeInjury", "ProposeInjuryCheckIn",
    ];

    private static async Task<ToolRegistry> RegistryAsync()
    {
        var services = new ServiceCollection();
        services.AddTransient<IChatChangeTools, ChatChangeTools>();
        services.AddTransient<IChatPlanLookups, ChatPlanLookups>();
        return await ToolRegistryBuilder.Build(services);
    }

    [Fact]
    public async Task Every_write_tool_is_registered_as_proposing_and_scoped_to_the_athlete()
    {
        var registry = await RegistryAsync();

        Assert.All(Proposing, name =>
        {
            Assert.True(registry.TryGet(name, out var descriptor));
            Assert.True(descriptor!.ProposesChange);
            Assert.True(descriptor.RequireAuthorization);
        });
    }

    [Fact]
    public async Task The_thread_is_offered_the_write_tools_and_a_reading_conversation_only_the_lookups()
    {
        var registry = await RegistryAsync();

        Assert.Equal(
            Proposing.Append("FindMovements").Append("GetPlannedDays").Order(StringComparer.Ordinal),
            registry.ToolDefinitions(ToolAccess.ReadAndPropose).Select(tool => tool.Name));
        Assert.Equal(["FindMovements", "GetPlannedDays"], registry.ToolDefinitions(ToolAccess.Read).Select(tool => tool.Name));
    }

    /// <summary>A choice among words is described as a string, not as the integers an enum schema would ask a model for.</summary>
    [Fact]
    public async Task A_word_choice_is_described_to_the_model_as_a_string()
    {
        var registry = await RegistryAsync();
        registry.TryGet("ProposeNonTrainingDay", out var descriptor);

        var repeat = JsonDocument.Parse(descriptor!.Definition.ParametersJson).RootElement.GetProperty("properties").GetProperty("Repeat");

        Assert.Contains("string", repeat.GetProperty("type").EnumerateArray().Select(type => type.GetString()));
    }
}
