using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zhengyan.ChatUI.Desktop.Models;
using Zhengyan.ChatUI.Desktop.Services;
using Zhengyan.OpenAIModels;

namespace Zhengyan.ChatUI.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp"
    };

    private readonly HttpClient _httpClient = new();
    private ConfigModels? _configModels;
    private bool _isUpdatingModelSelection;

    public MainWindowViewModel()
    {
        PendingUserAttachments.CollectionChanged += OnPendingUserAttachmentsChanged;
        LoadSettings();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetModelsCommand))]
    private string _serverEndpoint = "http://localhost:9083/mcphost/api/v1";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryMessageCommand))]
    private string _selectedModel = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _models = new();

    [ObservableProperty]
    private ObservableCollection<ChatMessagePairViewModel> _chatHistory = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _inputMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddImageUrlCommand))]
    private string _imageUrlInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ChatImageAttachmentViewModel> _pendingUserAttachments = new();

    [ObservableProperty]
    private string _maxCompletionTokens = "4096";

    [ObservableProperty]
    private string _temperature = "0.9";

    [ObservableProperty]
    private string _topP = "0.9";

    [ObservableProperty]
    private bool _useResponsesApi;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetModelsCommand))]
    private bool _isLoadingModels;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryMessageCommand))]
    private bool _isSending;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasPendingAttachments => PendingUserAttachments.Count > 0;

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnSelectedModelChanged(string value)
    {
        if (_isUpdatingModelSelection || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = SwitchModelAsync(value);
    }

    [RelayCommand(CanExecute = nameof(CanGetModels))]
    private async Task GetModelsAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerEndpoint))
        {
            StatusMessage = "Server URL cannot be empty.";
            return;
        }

        IsLoadingModels = true;
        StatusMessage = string.Empty;

        try
        {
            ApplyAuthorizationHeader();

            var server = GetServerEndpoint();
            var response = await _httpClient.GetFromJsonAsync<ConfigModels>($"{server}/models/config");
            if (response?.Models == null || response.Models.Count == 0)
            {
                throw new Exception("Failed to fetch models from the server.");
            }

            _configModels = response;
            Models.Clear();
            foreach (var model in response.Models)
            {
                Models.Add(model.Name);
            }

            var currentModelIndex = Math.Clamp(response.Current, 0, Models.Count - 1);
            _isUpdatingModelSelection = true;
            SelectedModel = Models[currentModelIndex];
            _isUpdatingModelSelection = false;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    private bool CanGetModels()
    {
        return !IsLoadingModels && !string.IsNullOrWhiteSpace(ServerEndpoint);
    }

    private async Task SwitchModelAsync(string model)
    {
        try
        {
            var models = _configModels?.Models?.Select(item => item.Name).ToList();
            if (models == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ServerEndpoint))
            {
                throw new Exception("Server URL cannot be empty.");
            }

            var index = models.IndexOf(model);
            if (index == -1)
            {
                throw new Exception("Model not found in the list of available models.");
            }

            if (_configModels?.Current == index)
            {
                return;
            }

            var server = GetServerEndpoint();
            var response = await _httpClient.PutAsync($"{server}/models/switch?id={index}", null);
            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = "Failed to switch model.";
                await Task.Delay(2000);

                if (_configModels is not null && _configModels.Current >= 0 && _configModels.Current < models.Count)
                {
                    _isUpdatingModelSelection = true;
                    SelectedModel = models[_configModels.Current];
                    _isUpdatingModelSelection = false;
                }

                return;
            }

            _configModels!.Current = index;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddImageUrl))]
    private void AddImageUrl()
    {
        if (!TryCreateUrlAttachment(ImageUrlInput, out var attachment, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Failed to add the image URL.";
            return;
        }

        AddPendingAttachment(attachment!);
        ImageUrlInput = string.Empty;
        StatusMessage = string.Empty;
    }

    private bool CanAddImageUrl()
    {
        return !IsSending && !string.IsNullOrWhiteSpace(ImageUrlInput);
    }

    public void AddPendingLocalImage(string filePath)
    {
        if (!TryCreateLocalAttachment(filePath, out var attachment, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Failed to add the local image.";
            return;
        }

        AddPendingAttachment(attachment!);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void RemovePendingAttachment(ChatImageAttachmentViewModel? attachment)
    {
        if (attachment is null)
        {
            return;
        }

        PendingUserAttachments.Remove(attachment);
    }

    [RelayCommand]
    private void ClearPendingAttachments()
    {
        PendingUserAttachments.Clear();
        ImageUrlInput = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            StatusMessage = "No model selected.";
            return;
        }

        if (string.IsNullOrWhiteSpace(InputMessage) && PendingUserAttachments.Count == 0)
        {
            return;
        }

        var userMessage = string.IsNullOrWhiteSpace(InputMessage) ? string.Empty : InputMessage;
        var attachments = CloneAttachments(PendingUserAttachments);

        InputMessage = string.Empty;
        PendingUserAttachments.Clear();
        IsSending = true;
        StatusMessage = string.Empty;

        try
        {
            await ProcessChatMessagesAsync(userMessage, attachments, ParseMaxCompletionTokens());
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSendMessage()
    {
        return !IsSending
            && !string.IsNullOrWhiteSpace(SelectedModel)
            && (!string.IsNullOrWhiteSpace(InputMessage) || PendingUserAttachments.Count > 0);
    }

    [RelayCommand(CanExecute = nameof(CanRetryMessage))]
    private async Task RetryMessageAsync()
    {
        if (ChatHistory.Count == 0)
        {
            StatusMessage = "No chat history available for regeneration.";
            return;
        }

        var lastMessagePair = ChatHistory[^1];
        var userMessage = lastMessagePair.UserMessage;
        var attachments = CloneAttachments(lastMessagePair.UserAttachments);

        ChatHistory.RemoveAt(ChatHistory.Count - 1);
        RetryMessageCommand.NotifyCanExecuteChanged();

        IsSending = true;
        StatusMessage = string.Empty;

        try
        {
            await ProcessChatMessagesAsync(userMessage, attachments, ParseMaxCompletionTokens());
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanRetryMessage()
    {
        return !IsSending
            && ChatHistory.Count > 0
            && !string.IsNullOrWhiteSpace(SelectedModel);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            ValidateSamplingSettings();
            var path = DesktopSettingsStore.Save(CaptureSettings());
            StatusMessage = $"Settings saved: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        ChatHistory.Clear();
        PendingUserAttachments.Clear();
        InputMessage = string.Empty;
        ImageUrlInput = string.Empty;
        StatusMessage = string.Empty;
        RetryMessageCommand.NotifyCanExecuteChanged();
    }

    private async Task ProcessChatMessagesAsync(string message, IReadOnlyList<ChatImageAttachmentViewModel> attachments, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(message) && attachments.Count == 0)
        {
            return;
        }

        var messagePair = new ChatMessagePairViewModel
        {
            UserMessage = message,
            AssistantMessage = string.Empty
        };

        foreach (var attachment in attachments)
        {
            messagePair.UserAttachments.Add(attachment.Clone());
        }

        ChatHistory.Add(messagePair);
        RetryMessageCommand.NotifyCanExecuteChanged();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetServerEndpoint()}{GetChatApiPath()}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        ApplyAuthorizationHeader();
        request.Content = BuildStreamRequestContent(maxTokens);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        if (UseResponsesApi)
        {
            await ProcessResponsesStreamAsync(reader, messagePair);
            return;
        }

        await ProcessChatCompletionsStreamAsync(reader, messagePair);
    }

    private async Task ProcessChatCompletionsStreamAsync(StreamReader reader, ChatMessagePairViewModel messagePair)
    {
        var inThink = false;
        while (true)
        {
            var sseEvent = await ReadSseEventAsync(reader);
            if (sseEvent is null)
            {
                return;
            }

            if (sseEvent.Data == "[DONE]")
            {
                return;
            }

            ChatCompletionChunkResponse? completionResponse;
            try
            {
                completionResponse = JsonSerializer.Deserialize<ChatCompletionChunkResponse>(sseEvent.Data);
            }
            catch (JsonException)
            {
                continue;
            }

            var delta = completionResponse?.choices.FirstOrDefault()?.delta;
            var text = ChatContentTextExtractor.GetText(delta?.content);
            var reasoning = delta?.reasoning_content;
            var additionalProperties = delta?.additional_properties;

            string assistantDelta = string.Empty;
            string reasoningDeltaFromContent = string.Empty;
            if (!string.IsNullOrEmpty(text))
            {
                var splitResult = SplitAssistantTextChunk(text, inThink);
                assistantDelta = splitResult.AssistantText;
                reasoningDeltaFromContent = splitResult.ReasoningText;
                inThink = splitResult.InThink;
            }

            var reasoningDelta = !string.IsNullOrEmpty(reasoning)
                ? reasoning
                : reasoningDeltaFromContent;

            if (!string.IsNullOrEmpty(reasoningDelta) || !string.IsNullOrEmpty(assistantDelta))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AppendAssistantReasoning(messagePair, reasoningDelta);
                    if (!string.IsNullOrEmpty(assistantDelta))
                    {
                        messagePair.AssistantMessage += assistantDelta;
                    }
                });
            }

            if (additionalProperties is { Count: > 0 })
            {
                var formattedAdditionalProperties = FormatAdditionalProperties(additionalProperties);
                if (!string.IsNullOrWhiteSpace(formattedAdditionalProperties))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => messagePair.AssistantAdditionalProperties = formattedAdditionalProperties);
                }
            }
        }
    }

    private async Task ProcessResponsesStreamAsync(StreamReader reader, ChatMessagePairViewModel messagePair)
    {
        while (true)
        {
            var sseEvent = await ReadSseEventAsync(reader);
            if (sseEvent is null)
            {
                return;
            }

            if (sseEvent.Data == "[DONE]")
            {
                return;
            }

            var eventType = sseEvent.EventName ?? TryGetJsonStringProperty(sseEvent.Data, "type");
            switch (eventType)
            {
                case "response.reasoning.delta":
                    var reasoningDelta = TryGetJsonStringProperty(sseEvent.Data, "delta");
                    if (!string.IsNullOrEmpty(reasoningDelta))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => AppendAssistantReasoning(messagePair, reasoningDelta));
                    }
                    break;
                case "response.reasoning.done":
                    var reasoningSnapshot = TryGetJsonStringProperty(sseEvent.Data, "text");
                    if (!string.IsNullOrWhiteSpace(reasoningSnapshot))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => ApplyAssistantReasoningSnapshot(messagePair, reasoningSnapshot));
                    }
                    break;
                case "response.output_text.delta":
                    var delta = TryGetJsonStringProperty(sseEvent.Data, "delta");
                    if (!string.IsNullOrEmpty(delta))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => messagePair.AssistantMessage += delta);
                    }
                    break;
                case "response.output_text.done":
                    var outputText = TryGetJsonStringProperty(sseEvent.Data, "text");
                    if (!string.IsNullOrWhiteSpace(outputText))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => ApplyAssistantMessageSnapshot(messagePair, outputText));
                    }
                    break;
                case "response.additional_properties.delta":
                    var additionalPropertiesPayload = TryGetResponseItemProperty(sseEvent.Data, "additional_properties");
                    await Dispatcher.UIThread.InvokeAsync(() => ApplyResponseAdditionalProperties(messagePair, additionalPropertiesPayload));
                    break;
                case "response.content_part.done":
                    var contentPart = TryGetResponseItemProperty(sseEvent.Data, "part");
                    var contentPartText = ExtractResponseTextFromContentPart(contentPart);
                    if (!string.IsNullOrWhiteSpace(contentPartText))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => ApplyAssistantMessageSnapshot(messagePair, contentPartText));
                    }
                    break;
                case "response.output_item.done":
                    var outputItem = TryGetResponseItemProperty(sseEvent.Data, "item");
                    await Dispatcher.UIThread.InvokeAsync(() => ApplyResponseOutputItem(messagePair, outputItem));
                    break;
                case "response.completed":
                    var completedResponse = TryGetResponseItemProperty(sseEvent.Data, "response");
                    await Dispatcher.UIThread.InvokeAsync(() => ApplyResponseCompletedPayload(messagePair, completedResponse));
                    break;
            }
        }
    }

    private int ParseMaxCompletionTokens()
    {
        return int.TryParse(MaxCompletionTokens, out var result) ? result : 4096;
    }

    private float ParseTemperature()
    {
        if (!float.TryParse(Temperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && !float.TryParse(Temperature, out result))
        {
            throw new Exception("Temperature must be a valid number.");
        }

        if (result is < 0 or > 2)
        {
            throw new Exception("Temperature must be between 0 and 2.");
        }

        return result;
    }

    private float ParseTopP()
    {
        if (!float.TryParse(TopP, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && !float.TryParse(TopP, out result))
        {
            throw new Exception("Top P must be a valid number.");
        }

        if (result is <= 0 or > 1)
        {
            throw new Exception("Top P must be between 0 and 1.");
        }

        return result;
    }

    private void ValidateSamplingSettings()
    {
        ParseTemperature();
        ParseTopP();
    }

    private static string FormatAdditionalProperties(Dictionary<string, object?>? additionalProperties)
    {
        if (additionalProperties is null || additionalProperties.Count == 0)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(additionalProperties, JsonOptions);
    }

    private StringContent BuildStreamRequestContent(int maxTokens)
    {
        return UseResponsesApi
            ? BuildResponsesStreamRequestContent(maxTokens)
            : BuildChatCompletionsStreamRequestContent(maxTokens);
    }

    private StringContent BuildChatCompletionsStreamRequestContent(int maxTokens)
    {
        var messages = new List<ChatCompletionMessage>();
        foreach (var item in ChatHistory)
        {
            if (!string.IsNullOrWhiteSpace(item.UserMessage) || item.UserAttachments.Count > 0)
            {
                messages.Add(new ChatCompletionMessage
                {
                    role = "user",
                    content = BuildUserContent(item.UserMessage, item.UserAttachments)
                });
            }

            if (!string.IsNullOrEmpty(item.AssistantMessage))
            {
                messages.Add(new ChatCompletionMessage
                {
                    role = "assistant",
                    content = item.AssistantMessage
                });
            }
        }

        var payload = JsonSerializer.Serialize(new ChatCompletionRequest
        {
            stream = true,
            messages = messages.ToArray(),
            model = SelectedModel,
            max_completion_tokens = maxTokens,
            temperature = ParseTemperature(),
            top_p = ParseTopP()
        }, JsonOptions);

        return new StringContent(payload, Encoding.UTF8, "application/json");
    }

    private static (string AssistantText, string ReasoningText, bool InThink) SplitAssistantTextChunk(string text, bool inThink)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (string.Empty, string.Empty, inThink);
        }

        var assistantBuilder = new StringBuilder();
        var reasoningBuilder = new StringBuilder();
        var cursor = 0;
        while (cursor < text.Length)
        {
            var thinkStartIndex = text.IndexOf("<think>", cursor, StringComparison.OrdinalIgnoreCase);
            var thinkEndIndex = text.IndexOf("</think>", cursor, StringComparison.OrdinalIgnoreCase);

            var nextMarkerIndex = -1;
            var isThinkStart = false;
            if (thinkStartIndex >= 0 && (thinkEndIndex < 0 || thinkStartIndex < thinkEndIndex))
            {
                nextMarkerIndex = thinkStartIndex;
                isThinkStart = true;
            }
            else if (thinkEndIndex >= 0)
            {
                nextMarkerIndex = thinkEndIndex;
            }

            if (nextMarkerIndex < 0)
            {
                AppendAssistantSegment(assistantBuilder, reasoningBuilder, text[cursor..], inThink);
                break;
            }

            if (nextMarkerIndex > cursor)
            {
                AppendAssistantSegment(assistantBuilder, reasoningBuilder, text[cursor..nextMarkerIndex], inThink);
            }

            if (isThinkStart)
            {
                inThink = true;
                cursor = nextMarkerIndex + "<think>".Length;
            }
            else
            {
                inThink = false;
                cursor = nextMarkerIndex + "</think>".Length;
            }
        }

        return (assistantBuilder.ToString(), reasoningBuilder.ToString(), inThink);
    }

    private static void AppendAssistantSegment(StringBuilder assistantBuilder, StringBuilder reasoningBuilder, string text, bool inThink)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (inThink)
        {
            reasoningBuilder.Append(text);
        }
        else
        {
            assistantBuilder.Append(text);
        }
    }

    private static void AppendAssistantReasoning(ChatMessagePairViewModel messagePair, string reasoning)
    {
        if (string.IsNullOrEmpty(reasoning))
        {
            return;
        }

        messagePair.AssistantReasoning += reasoning;
    }

    private static void ApplyAssistantReasoningSnapshot(ChatMessagePairViewModel messagePair, string reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(messagePair.AssistantReasoning)
            || reasoning.Length > messagePair.AssistantReasoning.Length)
        {
            messagePair.AssistantReasoning = reasoning;
        }
    }

    private static void ApplyAssistantMessageSnapshot(ChatMessagePairViewModel messagePair, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(messagePair.AssistantMessage)
            || message.Length > messagePair.AssistantMessage.Length)
        {
            messagePair.AssistantMessage = message;
        }
    }

    private StringContent BuildResponsesStreamRequestContent(int maxTokens)
    {
        var input = new List<object>();
        foreach (var item in ChatHistory)
        {
            if (!string.IsNullOrWhiteSpace(item.UserMessage) || item.UserAttachments.Count > 0)
            {
                input.Add(new
                {
                    role = "user",
                    content = BuildResponsesUserContent(item.UserMessage, item.UserAttachments)
                });
            }

            if (!string.IsNullOrEmpty(item.AssistantMessage))
            {
                input.Add(new
                {
                    role = "assistant",
                    content = item.AssistantMessage
                });
            }
        }

        var payload = JsonSerializer.Serialize(new ResponseRequest
        {
            stream = true,
            input = input.ToArray(),
            model = SelectedModel,
            max_output_tokens = maxTokens,
            temperature = ParseTemperature(),
            top_p = ParseTopP()
        }, JsonOptions);

        return new StringContent(payload, Encoding.UTF8, "application/json");
    }

    private static object BuildUserContent(string message, IEnumerable<ChatImageAttachmentViewModel> attachments)
    {
        var attachmentList = attachments.ToList();
        if (attachmentList.Count == 0)
        {
            return message;
        }

        var parts = new List<ChatCompletionContentPart>();
        if (!string.IsNullOrWhiteSpace(message))
        {
            parts.Add(new ChatCompletionContentPart
            {
                type = "text",
                text = message
            });
        }

        foreach (var attachment in attachmentList)
        {
            parts.Add(new ChatCompletionContentPart
            {
                type = "image_url",
                image_url = new ChatCompletionImageUrl
                {
                    url = attachment.OpenAIImageUrl
                }
            });
        }

        return parts.ToArray();
    }

    private static object BuildResponsesUserContent(string message, IEnumerable<ChatImageAttachmentViewModel> attachments)
    {
        var attachmentList = attachments.ToList();
        if (attachmentList.Count == 0)
        {
            return message;
        }

        var parts = new List<object>();
        if (!string.IsNullOrWhiteSpace(message))
        {
            parts.Add(new
            {
                type = "input_text",
                text = message
            });
        }

        foreach (var attachment in attachmentList)
        {
            parts.Add(new
            {
                type = "input_image",
                image_url = attachment.OpenAIImageUrl
            });
        }

        return parts.ToArray();
    }

    private string GetChatApiPath()
    {
        return UseResponsesApi ? "/responses" : "/chat/completions";
    }

    private void LoadSettings()
    {
        try
        {
            ApplySettings(DesktopSettingsStore.Load());
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    private DesktopAppSettings CaptureSettings()
    {
        return new DesktopAppSettings
        {
            ServerEndpoint = ServerEndpoint,
            ApiKey = ApiKey,
            SelectedModel = SelectedModel,
            MaxCompletionTokens = MaxCompletionTokens,
            Temperature = Temperature,
            TopP = TopP,
            UseResponsesApi = UseResponsesApi
        };
    }

    private void ApplySettings(DesktopAppSettings settings)
    {
        ServerEndpoint = settings.ServerEndpoint;
        ApiKey = settings.ApiKey;
        SelectedModel = settings.SelectedModel;
        MaxCompletionTokens = string.IsNullOrWhiteSpace(settings.MaxCompletionTokens) ? "4096" : settings.MaxCompletionTokens;
        Temperature = string.IsNullOrWhiteSpace(settings.Temperature) ? "0.9" : settings.Temperature;
        TopP = string.IsNullOrWhiteSpace(settings.TopP) ? "0.9" : settings.TopP;
        UseResponsesApi = settings.UseResponsesApi;
    }

    private static async Task<SseEvent?> ReadSseEventAsync(StreamReader reader)
    {
        string? eventName = null;
        var dataBuilder = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrEmpty(line))
            {
                if (!string.IsNullOrWhiteSpace(eventName) || dataBuilder.Length > 0)
                {
                    break;
                }

                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[6..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (dataBuilder.Length > 0)
                {
                    dataBuilder.AppendLine();
                }

                dataBuilder.Append(line[5..].Trim());
            }
        }

        if (string.IsNullOrWhiteSpace(eventName) && dataBuilder.Length == 0)
        {
            return null;
        }

        return new SseEvent(eventName, dataBuilder.ToString());
    }

    private static string? TryGetJsonStringProperty(string json, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static JsonElement? TryGetResponseItemProperty(string json, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return property.Clone();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static void ApplyResponseOutputItem(ChatMessagePairViewModel messagePair, JsonElement? itemElement)
    {
        if (itemElement is not JsonElement item || item.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryExtractResponseReasoning(item, out var reasoning))
        {
            ApplyAssistantReasoningSnapshot(messagePair, reasoning);
        }

        if (TryExtractResponseAdditionalProperties(item, out var additionalProperties))
        {
            messagePair.AssistantAdditionalProperties = additionalProperties;
        }

        var text = ExtractResponseTextFromItem(item);
        if (!string.IsNullOrWhiteSpace(text))
        {
            ApplyAssistantMessageSnapshot(messagePair, text);
        }
    }

    private static void ApplyResponseAdditionalProperties(ChatMessagePairViewModel messagePair, JsonElement? additionalPropertiesElement)
    {
        if (additionalPropertiesElement is not JsonElement additionalProperties
            || additionalProperties.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        var formattedAdditionalProperties = JsonSerializer.Serialize(additionalProperties, JsonOptions);
        if (!string.IsNullOrWhiteSpace(formattedAdditionalProperties))
        {
            messagePair.AssistantAdditionalProperties = formattedAdditionalProperties;
        }
    }

    private static void ApplyResponseCompletedPayload(ChatMessagePairViewModel messagePair, JsonElement? responseElement)
    {
        if (responseElement is not JsonElement response || response.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(messagePair.AssistantReasoning))
        {
            var reasoning = ExtractResponseReasoningFromResponse(response);
            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                messagePair.AssistantReasoning = reasoning;
            }
        }

        var text = ExtractResponseTextFromResponse(response);
        if (!string.IsNullOrWhiteSpace(text))
        {
            ApplyAssistantMessageSnapshot(messagePair, text);
        }

        if (string.IsNullOrWhiteSpace(messagePair.AssistantAdditionalProperties)
            && response.TryGetProperty("output", out var output)
            && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (TryExtractResponseAdditionalProperties(item, out var additionalProperties))
                {
                    messagePair.AssistantAdditionalProperties = additionalProperties;
                    break;
                }
            }
        }
    }

    private static string ExtractResponseTextFromResponse(JsonElement response)
    {
        if (response.TryGetProperty("output_text", out var outputText)
            && outputText.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(outputText.GetString()))
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (!response.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            var text = ExtractResponseTextFromItem(item);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append(text);
        }

        return builder.ToString();
    }

    private static string ExtractResponseReasoningFromResponse(JsonElement response)
    {
        if (!response.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (TryExtractResponseReasoning(item, out var reasoning))
            {
                return reasoning;
            }
        }

        return string.Empty;
    }

    private static string ExtractResponseTextFromItem(JsonElement item)
    {
        if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            var text = ExtractResponseTextFromContentPart(part);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append(text);
        }

        return builder.ToString();
    }

    private static string ExtractResponseTextFromContentPart(JsonElement? partElement)
    {
        if (partElement is not JsonElement part || part.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (!part.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return textElement.GetString() ?? string.Empty;
    }

    private static bool TryExtractResponseAdditionalProperties(JsonElement item, out string formattedAdditionalProperties)
    {
        formattedAdditionalProperties = string.Empty;
        if (!item.TryGetProperty("additional_properties", out var additionalProperties)
            || additionalProperties.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        formattedAdditionalProperties = JsonSerializer.Serialize(additionalProperties, JsonOptions);
        return !string.IsNullOrWhiteSpace(formattedAdditionalProperties);
    }

    private static bool TryExtractResponseReasoning(JsonElement item, out string reasoning)
    {
        reasoning = string.Empty;
        if (!item.TryGetProperty("additional_properties", out var additionalProperties)
            || additionalProperties.ValueKind != JsonValueKind.Object
            || !additionalProperties.TryGetProperty("reasoning_content", out var reasoningProperty)
            || reasoningProperty.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        reasoning = reasoningProperty.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(reasoning);
    }

    private string GetServerEndpoint()
    {
        if (string.IsNullOrWhiteSpace(ServerEndpoint))
        {
            throw new Exception("Server URL cannot be empty.");
        }

        return ServerEndpoint.TrimEnd('/');
    }

    private void ApplyAuthorizationHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(ApiKey)
            ? null
            : new AuthenticationHeaderValue("Bearer", ApiKey);
    }

    private void OnPendingUserAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasPendingAttachments));
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    private void AddPendingAttachment(ChatImageAttachmentViewModel attachment)
    {
        if (PendingUserAttachments.Any(existing => string.Equals(existing.Source, attachment.Source, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "This image has already been added.";
            return;
        }

        PendingUserAttachments.Add(attachment);
    }

    private static List<ChatImageAttachmentViewModel> CloneAttachments(IEnumerable<ChatImageAttachmentViewModel> attachments)
    {
        return attachments.Select(item => item.Clone()).ToList();
    }

    private static bool TryCreateUrlAttachment(string rawUrl, out ChatImageAttachmentViewModel? attachment, out string? errorMessage)
    {
        attachment = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            errorMessage = "Image URL cannot be empty.";
            return false;
        }

        var normalizedUrl = rawUrl.Trim();
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            errorMessage = "Image URL is invalid.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Only http, https or data image URLs are supported.";
            return false;
        }

        var displayName = GetDisplayNameFromUrl(uri);
        attachment = new ChatImageAttachmentViewModel
        {
            DisplayName = displayName,
            Source = normalizedUrl,
            OpenAIImageUrl = normalizedUrl,
            IsLocalFile = false
        };

        return true;
    }

    private static bool TryCreateLocalAttachment(string filePath, out ChatImageAttachmentViewModel? attachment, out string? errorMessage)
    {
        attachment = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Image path cannot be empty.";
            return false;
        }

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            errorMessage = "Selected image file does not exist.";
            return false;
        }

        var extension = Path.GetExtension(fullPath);
        if (!SupportedImageExtensions.Contains(extension))
        {
            errorMessage = "Only png, jpg, jpeg, gif, bmp and webp images are supported.";
            return false;
        }

        var mimeType = GetMimeType(extension);
        var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(fullPath))}";
        attachment = new ChatImageAttachmentViewModel
        {
            DisplayName = Path.GetFileName(fullPath),
            Source = fullPath,
            OpenAIImageUrl = dataUrl,
            IsLocalFile = true
        };

        return true;
    }

    private static string GetDisplayNameFromUrl(Uri uri)
    {
        if (string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
        {
            return "inline-image";
        }

        var segment = uri.Segments.LastOrDefault();
        if (!string.IsNullOrWhiteSpace(segment))
        {
            return Uri.UnescapeDataString(segment.Trim('/'));
        }

        return uri.Host;
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private sealed record SseEvent(string? EventName, string Data);
}
