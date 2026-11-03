using System.Text.Json;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Tools;
using Model.Tools;

namespace lionheart.Tests.Llm;

/// <summary>The JSON Schema a tool's parameter is described with, which a provider validates before it runs anything.</summary>
public sealed class ToolSchemaMapperTests
{
    private sealed class Tools
    {
        [Tool(Name = "search", Description = "Searches.")]
        public Task<string> SearchAsync(IdentityUser user, string query) => Task.FromResult(query);

        [Tool(Name = "look_up", Description = "Looks up.")]
        public Task<string> LookUpAsync(IdentityUser user, Guid movementDataId) => Task.FromResult(movementDataId.ToString());
    }

    private static async Task<JsonElement> ParameterOf(string method)
    {
        var definition = ResultAssert.Succeeded(await ToolSchemaMapper.MapTool(typeof(Tools).GetMethod(method)!));
        return JsonDocument.Parse(definition.ParametersJson).RootElement.GetProperty("properties").EnumerateObject().Single().Value;
    }

    /// <summary>
    /// A string has no format, and writing one as null is not JSON Schema: Anthropic rejects the
    /// whole request, every tool with it, over one such property.
    /// </summary>
    [Fact]
    public async Task A_string_parameter_is_a_plain_string_with_no_format()
    {
        var parameter = await ParameterOf(nameof(Tools.SearchAsync));

        Assert.Equal("string", parameter.GetProperty("type").GetString());
        Assert.False(parameter.TryGetProperty("format", out _));
    }

    [Fact]
    public async Task A_guid_parameter_is_a_string_in_uuid_format()
    {
        var parameter = await ParameterOf(nameof(Tools.LookUpAsync));

        Assert.Equal("uuid", parameter.GetProperty("format").GetString());
    }
}
