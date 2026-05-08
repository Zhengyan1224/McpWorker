using System.Text;
using System.Text.Json;

namespace Zhengyan.OpenAIModels;

/// <summary>
/// Extracts plain text from chat message content values.
/// </summary>
public static class ChatContentTextExtractor
{
    /// <summary>
    /// Extracts text from a raw chat content object.
    /// </summary>
    public static string? GetText(object? content)
    {
        return content switch
        {
            null => null,
            string text => text,
            JsonElement jsonElement => GetText(jsonElement),
            _ => content.ToString()
        };
    }

    /// <summary>
    /// Extracts text from JSON chat content.
    /// </summary>
    public static string? GetText(JsonElement content)
    {
        var builder = new StringBuilder();
        AppendText(builder, content);
        return builder.Length == 0 ? null : builder.ToString();
    }

    private static void AppendText(StringBuilder builder, JsonElement content)
    {
        switch (content.ValueKind)
        {
            case JsonValueKind.String:
                var text = content.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    builder.Append(text);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in content.EnumerateArray())
                {
                    AppendText(builder, item);
                }
                break;
            case JsonValueKind.Object:
                if (content.TryGetProperty("text", out var textElement))
                {
                    AppendText(builder, textElement);
                    return;
                }

                if (content.TryGetProperty("content", out var contentElement))
                {
                    AppendText(builder, contentElement);
                }
                break;
        }
    }
}
