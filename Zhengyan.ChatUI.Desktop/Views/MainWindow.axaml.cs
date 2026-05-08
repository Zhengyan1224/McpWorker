using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Zhengyan.ChatUI.Desktop.ViewModels;

namespace Zhengyan.ChatUI.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly ScrollViewer? _chatScrollViewer;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _chatScrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer");
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        DetachFromViewModel();
        DataContextChanged -= OnDataContextChanged;
        base.OnClosed(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachFromViewModel();

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ChatHistory.CollectionChanged += OnChatHistoryChanged;
        foreach (var item in _viewModel.ChatHistory)
        {
            item.PropertyChanged += OnChatItemPropertyChanged;
        }

        ScrollToBottom();
    }

    private void DetachFromViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ChatHistory.CollectionChanged -= OnChatHistoryChanged;
        foreach (var item in _viewModel.ChatHistory)
        {
            item.PropertyChanged -= OnChatItemPropertyChanged;
        }

        _viewModel = null;
    }

    private void OnChatHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ChatMessagePairViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnChatItemPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (ChatMessagePairViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnChatItemPropertyChanged;
            }
        }

        ScrollToBottom();
    }

    private void OnChatItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatMessagePairViewModel.AssistantMessage)
            || e.PropertyName == nameof(ChatMessagePairViewModel.AssistantReasoning)
            || e.PropertyName == nameof(ChatMessagePairViewModel.AssistantAdditionalProperties))
        {
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _chatScrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private async void CopyBubbleText_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string textToCopy } || string.IsNullOrWhiteSpace(textToCopy))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(sender as Visual);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(textToCopy);
    }

    private async void AddLocalImage_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Select image files"
        });

        foreach (var file in files)
        {
            var path = file.Path?.LocalPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _viewModel.AddPendingLocalImage(path);
            }
        }
    }

    private void RemovePendingAttachment_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control { Tag: ChatImageAttachmentViewModel attachment })
        {
            return;
        }

        _viewModel.RemovePendingAttachmentCommand.Execute(attachment);
    }
}
