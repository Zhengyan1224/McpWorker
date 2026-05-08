namespace Zhengyan.ChatUI.Desktop.ViewModels;

public class ChatImageAttachmentViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string OpenAIImageUrl { get; init; } = string.Empty;
    public bool IsLocalFile { get; init; }

    public string SourceLabel => IsLocalFile ? "Local image" : "Image URL";

    public ChatImageAttachmentViewModel Clone()
    {
        return new ChatImageAttachmentViewModel
        {
            DisplayName = DisplayName,
            Source = Source,
            OpenAIImageUrl = OpenAIImageUrl,
            IsLocalFile = IsLocalFile
        };
    }
}
