using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Zhengyan.ChatUI.Desktop.ViewModels;

public partial class ChatMessagePairViewModel : ObservableObject
{
    public ChatMessagePairViewModel()
    {
        UserAttachments.CollectionChanged += OnUserAttachmentsChanged;
    }

    [ObservableProperty]
    private string _userMessage = string.Empty;

    [ObservableProperty]
    private string _assistantMessage = string.Empty;

    [ObservableProperty]
    private string _assistantReasoning = string.Empty;

    [ObservableProperty]
    private string _assistantAdditionalProperties = string.Empty;

    [ObservableProperty]
    private bool _isAssistantAdditionalPropertiesExpanded;

    public ObservableCollection<ChatImageAttachmentViewModel> UserAttachments { get; } = new();

    public bool HasUserMessage => !string.IsNullOrWhiteSpace(UserMessage);
    public bool HasUserAttachments => UserAttachments.Count > 0;
    public bool HasAssistantMessage => !string.IsNullOrWhiteSpace(AssistantMessage);
    public bool HasAssistantReasoning => !string.IsNullOrWhiteSpace(AssistantReasoning);
    public bool HasAssistantAdditionalProperties => !string.IsNullOrWhiteSpace(AssistantAdditionalProperties);

    partial void OnUserMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasUserMessage));
    }

    partial void OnAssistantAdditionalPropertiesChanged(string value)
    {
        OnPropertyChanged(nameof(HasAssistantAdditionalProperties));

        if (string.IsNullOrWhiteSpace(value))
        {
            IsAssistantAdditionalPropertiesExpanded = false;
        }
    }

    partial void OnAssistantReasoningChanged(string value)
    {
        OnPropertyChanged(nameof(HasAssistantReasoning));
    }

    partial void OnAssistantMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasAssistantMessage));
    }

    private void OnUserAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasUserAttachments));
    }
}
