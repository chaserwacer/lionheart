using System.ClientModel;
using System.Text.Json;
using lionheart.Functional;
using OpenAI.Chat;

namespace lionheart.Services.Llm.LmStudio;

/// <summary>
/// Converts a raw chat completion into a <see cref="Result{T, TProblem}"/>, guarding the case where LM Studio
/// answers a failure (unknown route, model not loaded, context overflow) with an HTTP 200 whose body
/// carries an "error" object and no "choices". The client SDK trusts the 200 and yields a completion
/// with an empty choice list, so reading FinishReason/Content would throw; this surfaces the server's
/// own error message as a failed Result instead.
/// </summary>
internal static class OpenAiCompletionResult
{
    /// <summary>
    /// Calls the model and converts every way the call can fail into a <see cref="Problem.Upstream"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ToResult"/> only covers a response that arrived. When LM Studio is not running — the
    /// single likeliest runtime condition for a local-model app — the SDK throws instead, and an
    /// uncaught throw here reaches the generic exception handler as an opaque 500. Callers get a 502
    /// naming the cause.
    /// </remarks>
    public static async Task<Result<ChatCompletion, Problem>> CompleteAsResultAsync(
        this ChatClient client,
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            return (await client.CompleteChatAsync(messages, options, ct)).ToResult();
        }
        catch (ClientResultException ex)
        {
            return Result<ChatCompletion, Problem>.Failure(
                new Problem.Upstream($"Chat model request failed: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            return Result<ChatCompletion, Problem>.Failure(new Problem.Upstream(
                $"Could not reach the chat model at its configured endpoint: {ex.Message}"));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<ChatCompletion, Problem>.Failure(
                new Problem.Upstream("The chat model did not respond in time."));
        }
    }

    public static Result<ChatCompletion, Problem> ToResult(this ClientResult<ChatCompletion> result)
    {
        JsonDocument body;
        try
        {
            body = JsonDocument.Parse(result.GetRawResponse().Content.ToMemory());
        }
        catch (JsonException)
        {
            return Result<ChatCompletion, Problem>.Failure(
                new Problem.Upstream("Chat model returned a response that was not valid JSON."));
        }

        using (body)
        {
            var root = body.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m)
                    ? m.GetString()
                    : error.ToString();
                return Result<ChatCompletion, Problem>.Failure(
                    new Problem.Upstream($"Chat model error: {message}"));
            }

            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return Result<ChatCompletion, Problem>.Failure(
                    new Problem.Upstream("Chat model returned no choices."));
            }

            return Result<ChatCompletion, Problem>.Success(result.Value);
        }
    }
}
