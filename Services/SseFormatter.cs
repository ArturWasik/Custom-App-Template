using System.Text;
using System.Text.Json.Nodes;

namespace CustomAppTemplate.Services;

/// <summary>
/// Transforms OpenAI Responses API SSE events into Chat Completions chunk format
/// and writes them to the HTTP response.
/// </summary>
public static class SseFormatter
{
    public static async Task FormatAndSendAsync(
        Stream openAiStream,
        HttpResponse response,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(openAiStream, Encoding.UTF8, leaveOpen: true);

        string? pendingEventType = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;

            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                pendingEventType = line[7..];
                continue;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var data = line[6..];
                var eventType = pendingEventType ?? "message";
                pendingEventType = null;

                JsonNode? responseData;
                try { responseData = JsonNode.Parse(data); }
                catch { continue; }

                if (responseData is null) continue;

                JsonObject? completionChunk = eventType switch
                {
                    "response.output_text.delta" => new JsonObject
                    {
                        ["id"] = responseData["item_id"]?.ToString() ?? "chatcmpl-unknown",
                        ["object"] = "chat.completion.chunk",
                        ["choices"] = new JsonArray(new JsonObject
                        {
                            ["delta"] = new JsonObject { ["content"] = responseData["delta"]?.ToString() ?? "" }
                        })
                    },
                    "response.created" => new JsonObject
                    {
                        ["id"] = responseData["response"]?["id"]?.ToString() ?? "chatcmpl-unknown",
                        ["object"] = "chat.completion.chunk",
                        ["choices"] = new JsonArray(new JsonObject
                        {
                            ["delta"] = new JsonObject { ["role"] = "assistant" }
                        })
                    },
                    _ => null
                };

                // response.completed and unknown events are intentionally dropped
                if (completionChunk is not null)
                {
                    await response.WriteAsync($"data: {completionChunk.ToJsonString()}\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }

                continue;
            }

            // Empty line resets the event type (end of SSE block)
            if (line.Length == 0)
                pendingEventType = null;
        }

        await response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
