using System.Text.Json;
using lionheart.Services.Llm;
using Microsoft.Extensions.DependencyInjection;
using Model.Chat.Tools;
using Model.Tools;

namespace lionheart.Tests.Support;

public interface ITestTools
{
    Task<string> EchoAsync(string phrase);
    Task<string> BoomAsync(string phrase);
    Task<int> CountSetsAsync(int days);
    Task<string> FaultAsync(string phrase);
    Task<int> DaysInAsync(lionheart.Model.Analysis.DayRange range);
}

/// <summary>
/// A minimal tool provider standing in for the real chat tools, so the executor's binding,
/// invocation and failure paths can be driven without any of the app's services.
/// </summary>
public sealed class TestTools : ITestTools
{
    public List<string> Calls { get; } = [];

    public Task<string> EchoAsync(string phrase)
    {
        Calls.Add($"echo:{phrase}");
        return Task.FromResult($"you said {phrase}");
    }

    public Task<string> BoomAsync(string phrase)
    {
        Calls.Add($"boom:{phrase}");
        throw new InvalidOperationException("tool exploded");
    }

    public Task<int> CountSetsAsync(int days)
    {
        Calls.Add($"count:{days}");
        return Task.FromResult(days * 3);
    }

    /// <summary>Fails once awaited, the way a real async tool's read does.</summary>
    public async Task<string> FaultAsync(string phrase)
    {
        Calls.Add($"fault:{phrase}");
        await Task.Yield();
        throw new ArgumentOutOfRangeException(nameof(phrase));
    }

    public Task<int> DaysInAsync(lionheart.Model.Analysis.DayRange range)
    {
        Calls.Add($"days:{range.From:yyyy-MM-dd}..{range.To:yyyy-MM-dd}");
        return Task.FromResult(range.Days);
    }
}

/// <summary>
/// Wires a <see cref="ChatToolCallExecutor"/> over <see cref="TestTools"/>. The registry is
/// built by hand rather than through ToolRegistryBuilder's assembly scan so a test names
/// exactly the tools it wants the model to be able to call.
/// </summary>
public sealed class TestToolbox : IDisposable
{
    private readonly ServiceProvider _provider;

    public TestToolbox()
    {
        Tools = new TestTools();
        var services = new ServiceCollection();
        services.AddSingleton<ITestTools>(Tools);
        _provider = services.BuildServiceProvider();

        Registry = BuildRegistry();
        Executor = new ChatToolCallExecutor(
            Registry,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public TestTools Tools { get; }

    /// <summary>The tools the model is offered — the same registry the executor resolves against.</summary>
    public ToolRegistry Registry { get; }

    public ChatToolCallExecutor Executor { get; }

    public void Dispose() => _provider.Dispose();

    private static ToolRegistry BuildRegistry()
    {
        var descriptors = new[]
        {
            Describe("echo", nameof(ITestTools.EchoAsync), "phrase", "string"),
            Describe("boom", nameof(ITestTools.BoomAsync), "phrase", "string"),
            Describe("count_sets", nameof(ITestTools.CountSetsAsync), "days", "integer"),
            Describe("fault", nameof(ITestTools.FaultAsync), "phrase", "string"),
            Describe("days_in", nameof(ITestTools.DaysInAsync), "range", "object"),
        };

        return new ToolRegistry(descriptors.ToDictionary(d => d.ToolName, StringComparer.Ordinal));
    }

    private static ToolDescriptor Describe(string toolName, string methodName, string parameterName, string jsonType)
    {
        var parameters =
            "{\"type\":\"object\",\"properties\":{\"" + parameterName + "\":{\"type\":\"" + jsonType + "\"}},"
            + "\"required\":[\"" + parameterName + "\"]}";

        return new ToolDescriptor(
            toolName,
            typeof(TestTools),
            typeof(TestTools).GetMethod(methodName)!,
            new ToolDefinition(toolName, $"test tool {toolName}", parameters),
            RequireAuthorization: false);
    }
}
