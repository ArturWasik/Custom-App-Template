using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomAppTemplate.Models;

[JsonConverter(typeof(ContentValueConverter))]
public sealed class ContentValue
{
    public string? StringValue { get; }
    public List<MessageContent>? ArrayValue { get; }
    public bool IsString => StringValue is not null;

    public ContentValue(string value) { StringValue = value; }
    public ContentValue(List<MessageContent> value) { ArrayValue = value; }

    public static implicit operator ContentValue(string s) => new(s);
    public static implicit operator ContentValue(List<MessageContent> list) => new(list);
}

public class ContentValueConverter : JsonConverter<ContentValue>
{
    public override ContentValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => new ContentValue(reader.GetString()!),
            JsonTokenType.StartArray => new ContentValue(
                JsonSerializer.Deserialize<List<MessageContent>>(ref reader, options)!),
            _ => throw new JsonException($"Unexpected token type for content: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ContentValue value, JsonSerializerOptions options)
    {
        if (value.IsString)
            writer.WriteStringValue(value.StringValue);
        else
            JsonSerializer.Serialize(writer, value.ArrayValue, options);
    }
}

public class MessageContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ImageUrl { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public ContentValue? Content { get; set; }
}

public class ChatRequest
{
    [JsonPropertyName("conversation_id")]
    public string? ConversationId { get; set; }

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("tools")]
    public List<JsonElement>? Tools { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("object")]
    public string Object { get; set; } = "";

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("output")]
    public List<JsonElement> Output { get; set; } = [];

    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}

public class UsageInfo
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
