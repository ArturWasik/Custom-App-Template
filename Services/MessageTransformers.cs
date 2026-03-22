using CustomAppTemplate.Models;

namespace CustomAppTemplate.Services;

public static class MessageTransformers
{
    /// <summary>
    /// Adds a system message at the beginning if none exists.
    /// </summary>
    public static Func<List<ChatMessage>, List<ChatMessage>> AddSystemMessage(string text)
        => messages =>
        {
            if (messages.Any(m => m.Role == "system"))
                return messages;

            return [new ChatMessage { Role = "system", Content = text }, .. messages];
        };

    /// <summary>
    /// Replaces all system messages with a single one at the top.
    /// </summary>
    public static Func<List<ChatMessage>, List<ChatMessage>> ReplaceSystemMessages(string text)
        => messages =>
        {
            var nonSystem = messages.Where(m => m.Role != "system").ToList();
            return [new ChatMessage { Role = "system", Content = text }, .. nonSystem];
        };

    /// <summary>
    /// Prepends a prefix string to all user messages (string and text content items).
    /// </summary>
    public static Func<List<ChatMessage>, List<ChatMessage>> PrefixUserMessages(string prefix)
        => messages => messages.Select(msg =>
        {
            if (msg.Role != "user" || msg.Content is null)
                return msg;

            if (msg.Content.IsString)
                return new ChatMessage { Role = msg.Role, Content = $"{prefix} {msg.Content.StringValue}" };

            var items = msg.Content.ArrayValue!.Select(item =>
            {
                if (item.Type is "text" or "input_text")
                    return new MessageContent { Type = item.Type, Text = $"{prefix} {item.Text}", ImageUrl = item.ImageUrl };
                return item;
            }).ToList();

            return new ChatMessage { Role = msg.Role, Content = items };
        }).ToList();

    /// <summary>
    /// Keeps all system messages and only the last <paramref name="maxMessages"/> non-system messages.
    /// </summary>
    public static Func<List<ChatMessage>, List<ChatMessage>> LimitMessageHistory(int maxMessages)
        => messages =>
        {
            var system = messages.Where(m => m.Role == "system").ToList();
            var nonSystem = messages.Where(m => m.Role != "system").ToList();
            var recent = nonSystem.TakeLast(maxMessages).ToList();
            return [.. system, .. recent];
        };
}
