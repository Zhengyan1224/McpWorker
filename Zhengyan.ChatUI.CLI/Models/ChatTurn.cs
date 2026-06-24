namespace Zhengyan.ChatUI.CLI.Models;

public sealed class ChatTurn
{
    public string UserMessage { get; set; } = string.Empty;

    public string AssistantReasoning { get; set; } = string.Empty;

    public string AssistantMessage { get; set; } = string.Empty;

    public string AssistantAdditionalProperties { get; set; } = string.Empty;

    public List<ChatImageAttachment> Attachments { get; } = [];
}
