using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CustomAppTemplate.Models;

namespace CustomAppTemplate.Services;

public class OpenAIService(HttpClient httpClient, ILogger<OpenAIService> logger) : IOpenAIService
{
    private const string ApiPath = "v1/responses";

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request)
    {
        var payload = BuildPayload(request, stream: false);

        using var response = await httpClient.PostAsJsonAsync(ApiPath, payload);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("OpenAI API error: {Error}", errorContent);

            string? message = null;
            try
            {
                var errorData = JsonSerializer.Deserialize<JsonElement>(errorContent);
                if (errorData.TryGetProperty("error", out var errProp) &&
                    errProp.TryGetProperty("message", out var msgProp))
                    message = msgProp.GetString();
            }
            catch { /* ignore parse errors, fall back to status text */ }

            throw new Exception($"OpenAI API error: {message ?? response.ReasonPhrase}");
        }

        return await response.Content.ReadFromJsonAsync<ChatResponse>()
               ?? throw new InvalidOperationException("Failed to deserialize OpenAI response");
    }

    public async Task<Stream> GenerateStreamedResponseAsync(ChatRequest request)
    {
        var payload = BuildPayload(request, stream: true);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiPath)
        {
            Content = JsonContent.Create(payload)
        };

        var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("OpenAI API stream error: {Error}", errorContent);
            throw new Exception($"OpenAI API error: {response.ReasonPhrase}");
        }

        return await response.Content.ReadAsStreamAsync();
    }

    private static JsonObject BuildPayload(ChatRequest request, bool stream)
    {
        var payload = new JsonObject
        {
            ["model"] = request.Model ?? "gpt-4o",
            ["input"] = TransformMessages(request.Messages),
            ["stream"] = stream
        };

        if (!string.IsNullOrEmpty(request.ConversationId))
            payload["previous_response_id"] = request.ConversationId;

        if (request.Tools?.Count > 0)
            payload["tools"] = PrepareToolsConfig(request.Tools);

        if (request.MaxTokens.HasValue)
            payload["max_output_tokens"] = request.MaxTokens.Value;

        return payload;
    }

    private static JsonArray TransformMessages(List<ChatMessage> messages)
    {
        var result = new JsonArray();

        foreach (var message in messages)
        {
            var isAssistant = message.Role == "assistant";
            var textType = isAssistant ? "output_text" : "input_text";

            var msg = new JsonObject { ["role"] = message.Role };

            if (message.Content is null)
            {
                result.Add(msg);
                continue;
            }

            if (message.Content.IsString)
            {
                msg["content"] = new JsonArray(
                    new JsonObject { ["type"] = textType, ["text"] = message.Content.StringValue });
            }
            else
            {
                var contentArray = new JsonArray();
                foreach (var item in message.Content.ArrayValue!)
                {
                    var transformed = TransformContentItem(item, isAssistant, textType);
                    if (transformed is not null)
                        contentArray.Add(transformed);
                }
                msg["content"] = contentArray;
            }

            result.Add(msg);
        }

        return result;
    }

    private static JsonObject? TransformContentItem(MessageContent item, bool isAssistant, string textType)
    {
        switch (item.Type)
        {
            case "text":
                return new JsonObject { ["type"] = textType, ["text"] = item.Text };

            case "image_url" when !isAssistant:
                var imageUrl = item.ImageUrl?.ValueKind == JsonValueKind.String
                    ? item.ImageUrl?.GetString()
                    : item.ImageUrl?.TryGetProperty("url", out var urlProp) == true
                        ? urlProp.GetString()
                        : null;
                return new JsonObject { ["type"] = "input_image", ["image_url"] = imageUrl };

            // Already correctly formatted — pass through
            case "input_text" when !isAssistant:
            case "input_image" when !isAssistant:
            case "output_text" when isAssistant:
            case "refusal" when isAssistant:
                return JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(item));

            // Role mismatch: convert input_text → output_text for assistant
            case "input_text" when isAssistant:
                return new JsonObject { ["type"] = "output_text", ["text"] = item.Text };

            // input_image on assistant or unknown types: drop
            default:
                return null;
        }
    }

    private static JsonArray PrepareToolsConfig(List<JsonElement> tools)
    {
        var toolsMap = new Dictionary<string, JsonObject>
        {
            ["web_search"] = new JsonObject { ["type"] = "web_search_preview" }
        };

        var result = new JsonArray();

        foreach (var tool in tools)
        {
            if (tool.ValueKind == JsonValueKind.String)
            {
                var name = tool.GetString();
                if (name is not null && toolsMap.TryGetValue(name, out var config))
                    result.Add(config.DeepClone());
            }
            else if (tool.ValueKind == JsonValueKind.Object)
            {
                result.Add(JsonNode.Parse(tool.GetRawText()));
            }
        }

        return result;
    }
}
