using System.ClientModel;
using System.Text.Json;
using Ardalis.Result;
using OpenAI.Chat;

namespace lionheart.Services.Chat
{
    /// <summary>
    /// Converts a raw chat completion into a <see cref="Result{T}"/>, guarding the case where LM Studio
    /// answers a failure (unknown route, model not loaded, context overflow) with an HTTP 200 whose body
    /// carries an "error" object and no "choices". The client SDK trusts the 200 and yields a completion
    /// with an empty choice list, so reading FinishReason/Content would throw; this surfaces the server's
    /// own error message as a failed Result instead.
    /// </summary>
    public static class ChatCompletionResultExtensions
    {
        public static Result<ChatCompletion> ToResult(this ClientResult<ChatCompletion> result)
        {
            JsonDocument body;
            try
            {
                body = JsonDocument.Parse(result.GetRawResponse().Content.ToMemory());
            }
            catch (JsonException)
            {
                return Result.Error("Chat model returned a response that was not valid JSON.");
            }

            using (body)
            {
                var root = body.RootElement;

                if (root.TryGetProperty("error", out var error))
                {
                    var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : error.ToString();
                    return Result.Error($"Chat model error: {message}");
                }

                if (!root.TryGetProperty("choices", out var choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0)
                {
                    return Result.Error("Chat model returned no choices.");
                }

                return Result.Success(result.Value);
            }
        }
    }
}
