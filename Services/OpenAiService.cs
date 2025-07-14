using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class OpenAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenAiService(IConfiguration config)
    {
        _apiKey = config["OpenAI:ApiKey"] ?? throw new ArgumentNullException("Missing OpenAI:ApiKey config");

        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> ChatAsync(string userPrompt)
    {
        var payload = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { role = "system", content = "You are Lionheart, an intelligent training assistant." },
                new { role = "user", content = userPrompt }
            }
        };

        var resp = await _http.PostAsJsonAsync("v1/chat/completions", payload);
        resp.EnsureSuccessStatusCode();

        using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var msg = doc!
            .RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return msg!;
    }
    
    public async Task<string> ChatAndRespondAsync(
    string prompt,
    List<FunctionToolDefinition> tools,
    Func<string, string, Task<object?>> toolExecutor)
{
    var payload = new
    {
        model = "gpt-4o",
        messages = new[]
        {
            new { role = "system", content = "You are Lionheart, an intelligent training assistant." },
            new { role = "user", content = prompt }
        },
        tools = tools.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }
        }),
        tool_choice = "auto"
    };
    Console.WriteLine("Sending payload:");
    Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

    var response = await _http.PostAsJsonAsync("v1/chat/completions", payload);
    var body = await response.Content.ReadAsStringAsync(); // 👈 get error detail
    Console.WriteLine($"Status: {(int)response.StatusCode}");
    Console.WriteLine("Response body:");
    Console.WriteLine(body);



    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
    var root = json!.RootElement;

    var choice = root.GetProperty("choices")[0];
    var finishReason = choice.GetProperty("finish_reason").GetString();

    if (finishReason == "tool_calls")
    {
        var toolCalls = choice.GetProperty("message").GetProperty("tool_calls");
        var toolCall = toolCalls[0];

        var functionName = toolCall.GetProperty("function").GetProperty("name").GetString()!;
        var argumentsJson = toolCall.GetProperty("function").GetProperty("arguments").GetRawText();
        var toolResult = await toolExecutor(functionName, argumentsJson);

        // Send follow-up request with tool result
       var toolMessage = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                ["role"] = "system",
                ["content"] = "You are Lionheart, an intelligent training assistant."
            },
            new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = prompt
            },
            new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["tool_calls"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["id"] = toolCall.GetProperty("id").GetString()!,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, object>
                        {
                            ["name"] = functionName,
                            ["arguments"] = argumentsJson
                        }
                    }
                }
            },
            new Dictionary<string, object>
            {
                ["role"] = "tool",
                ["tool_call_id"] = toolCall.GetProperty("id").GetString()!,
                ["content"] = JsonSerializer.Serialize(toolResult)
            }
        };


        var secondPayload = new
        {
            model = "gpt-4o",
            messages = toolMessage
        };


        var secondResponse = await _http.PostAsJsonAsync("v1/chat/completions", secondPayload);
        secondResponse.EnsureSuccessStatusCode();

        var secondJson = await secondResponse.Content.ReadFromJsonAsync<JsonDocument>();
        return secondJson!
            .RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;
    }

    // Regular assistant message (no tool call)
    return choice.GetProperty("message").GetProperty("content").GetString()!;
}








}
