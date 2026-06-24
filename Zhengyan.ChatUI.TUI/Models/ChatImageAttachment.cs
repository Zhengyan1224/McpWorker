namespace Zhengyan.ChatUI.TUI.Models;

public sealed class ChatImageAttachment
{
    public string DisplayName { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string OpenAIImageUrl { get; init; } = string.Empty;

    public bool IsLocalFile { get; init; }

    public string SourceLabel => IsLocalFile ? "Local image" : "Image URL";

    public ChatImageAttachment Clone()
    {
        return new ChatImageAttachment
        {
            DisplayName = DisplayName,
            Source = Source,
            OpenAIImageUrl = OpenAIImageUrl,
            IsLocalFile = IsLocalFile
        };
    }
}
