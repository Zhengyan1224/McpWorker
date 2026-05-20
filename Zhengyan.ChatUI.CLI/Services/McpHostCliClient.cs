using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Zhengyan.ChatUI.CLI.Models;
using Zhengyan.OpenAIModels;

namespace Zhengyan.ChatUI.CLI.Services;

public sealed class McpHostCliClient : IDisposable
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

    public async Task<ConfigModels> GetModelsAsync(string serverEndpoint, string apiKey, CancellationToken cancellationToken = default)
    {
        ApplyAuthorizationHeader(apiKey);

        var response = await _httpClient.GetFromJsonAsync<ConfigModels>(
            $"{NormalizeServerEndpoint(serverEndpoint)}/models/config",
            cancellationToken);

        if (response?.Models is not { Count: > 0 })
        {
            throw new InvalidOperationException("Failed to fetch models from the server.");
        }

        return response;
    }

    public async Task SwitchModelAsync(string serverEndpoint, string apiKey, int modelIndex, CancellationToken cancellationToken = default)
    {
        ApplyAuthorizationHeader(apiKey);

        using var response = await _httpClient.PutAsync(
            $"{NormalizeServerEndpoint(serverEndpoint)}/models/switch?id={modelIndex}",
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to switch model.");
        }
    }

    public ChatImageAttachment CreateUrlAttachment(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new InvalidOperationException("Image URL cannot be empty.");
        }

        var normalizedUrl = rawUrl.Trim();
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Image URL is invalid.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only http, https or data image URLs are supported.");
        }

        return new ChatImageAttachment
        {
            DisplayName = GetDisplayNameFromUrl(uri),
            Source = normalizedUrl,
            OpenAIImageUrl = normalizedUrl,
            IsLocalFile = false
        };
    }

    public ChatImageAttachment CreateLocalAttachment(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Image path cannot be empty.");
        }

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException("Selected image file does not exist.");
        }

        var extension = Path.GetExtension(fullPath);
        if (!SupportedImageExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only png, jpg, jpeg, gif, bmp and webp images are supported.");
        }

        var mimeType = GetMimeType(extension);
        return new ChatImageAttachment
        {
            DisplayName = Path.GetFileName(fullPath),
            Source = fullPath,
            OpenAIImageUrl = $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(fullPath))}",
            IsLocalFile = true
        };
    }

    public async Task StreamChatAsync(
        string serverEndpoint,
        string apiKey,
        string model,
        bool useResponsesApi,
        int maxTokens,
        float temperature,
        float topP,
        IReadOnlyList<ChatTurn> chatHistory,
        ChatTurn activeTurn,
        Action refreshUi,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{NormalizeServerEndpoint(serverEndpoint)}{GetChatApiPath(useResponsesApi)}");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        ApplyAuthorizationHeader(apiKey);
        request.Content = BuildStreamRequestContent(chatHistory, model, maxTokens, temperature, topP, useResponsesApi);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        if (useResponsesApi)
        {
            await ProcessResponsesStreamAsync(reader, activeTurn, refreshUi);
            return;
        }

        await ProcessChatCompletionsStreamAsync(reader, activeTurn, refreshUi);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static async Task ProcessChatCompletionsStreamAsync(StreamReader reader, ChatTurn activeTurn, Action refreshUi)
    {
        var inThink = false;

        while (true)
        {
            var sseEvent = await ReadSseEventAsync(reader);
            if (sseEvent is null || sseEvent.Data == "[DONE]")
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
            var changed = false;

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

            if (!string.IsNullOrEmpty(reasoningDelta))
            {
                changed |= AppendAssistantReasoning(activeTurn, reasoningDelta);
            }

            if (!string.IsNullOrEmpty(assistantDelta))
            {
                activeTurn.AssistantMessage += assistantDelta;
                changed = true;
            }

            if (additionalProperties is { Count: > 0 })
            {
                activeTurn.AssistantAdditionalProperties = FormatAdditionalProperties(additionalProperties);
                changed = true;
            }

            if (changed)
            {
                refreshUi();
            }
        }
    }

    private static async Task ProcessResponsesStreamAsync(StreamReader reader, ChatTurn activeTurn, Action refreshUi)
    {
        while (true)
        {
            var sseEvent = await ReadSseEventAsync(reader);
            if (sseEvent is null || sseEvent.Data == "[DONE]")
            {
                return;
            }

            var eventType = sseEvent.EventName ?? TryGetJsonStringProperty(sseEvent.Data, "type");
            switch (eventType)
            {
                case "response.reasoning.delta":
                    var reasoningDelta = TryGetJsonStringProperty(sseEvent.Data, "delta");
                    if (AppendAssistantReasoning(activeTurn, reasoningDelta))
                    {
                        refreshUi();
                    }
                    break;
                case "response.reasoning.done":
                    var reasoningSnapshot = TryGetJsonStringProperty(sseEvent.Data, "text");
                    if (ApplyAssistantReasoningSnapshot(activeTurn, reasoningSnapshot))
                    {
                        refreshUi();
                    }
                    break;
                case "response.output_text.delta":
                    var delta = TryGetJsonStringProperty(sseEvent.Data, "delta");
                    if (!string.IsNullOrEmpty(delta))
                    {
                        activeTurn.AssistantMessage += delta;
                        refreshUi();
                    }
                    break;
                case "response.output_text.done":
                    var outputText = TryGetJsonStringProperty(sseEvent.Data, "text");
                    if (ApplyAssistantMessageSnapshot(activeTurn, outputText))
                    {
                        refreshUi();
                    }
                    break;
                case "response.additional_properties.delta":
                    var additionalPropertiesPayload = TryGetJsonProperty(sseEvent.Data, "additional_properties");
                    if (ApplyResponseAdditionalProperties(activeTurn, additionalPropertiesPayload))
                    {
                        refreshUi();
                    }
                    break;
                case "response.content_part.done":
                    var contentPart = TryGetJsonProperty(sseEvent.Data, "part");
                    var contentPartText = ExtractResponseTextFromContentPart(contentPart);
                    if (ApplyAssistantMessageSnapshot(activeTurn, contentPartText))
                    {
                        refreshUi();
                    }
                    break;
                case "response.output_item.done":
                    var outputItem = TryGetJsonProperty(sseEvent.Data, "item");
                    if (ApplyResponseOutputItem(activeTurn, outputItem))
                    {
                        refreshUi();
                    }
                    break;
                case "response.completed":
                    var response = TryGetJsonProperty(sseEvent.Data, "response");
                    if (ApplyResponseCompletedPayload(activeTurn, response))
                    {
                        refreshUi();
                    }
                    break;
            }
        }
    }

    private static bool ApplyResponseOutputItem(ChatTurn activeTurn, JsonElement? itemElement)
    {
        if (itemElement is not JsonElement item || item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var changed = false;

        if (TryExtractResponseReasoning(item, out var reasoning))
        {
            changed |= ApplyAssistantReasoningSnapshot(activeTurn, reasoning);
        }

        if (TryExtractResponseAdditionalProperties(item, out var additionalProperties)
            && !string.Equals(activeTurn.AssistantAdditionalProperties, additionalProperties, StringComparison.Ordinal))
        {
            activeTurn.AssistantAdditionalProperties = additionalProperties;
            changed = true;
        }

        var text = ExtractResponseTextFromItem(item);
        if (!string.IsNullOrWhiteSpace(text))
        {
            changed |= ApplyAssistantMessageSnapshot(activeTurn, text);
        }

        return changed;
    }

    private static bool ApplyResponseAdditionalProperties(ChatTurn activeTurn, JsonElement? additionalPropertiesElement)
    {
        if (additionalPropertiesElement is not JsonElement additionalProperties
            || additionalProperties.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        var formattedAdditionalProperties = JsonSerializer.Serialize(additionalProperties, JsonOptions);
        if (string.IsNullOrWhiteSpace(formattedAdditionalProperties)
            || string.Equals(activeTurn.AssistantAdditionalProperties, formattedAdditionalProperties, StringComparison.Ordinal))
        {
            return false;
        }

        activeTurn.AssistantAdditionalProperties = formattedAdditionalProperties;
        return true;
    }

    private static bool ApplyResponseCompletedPayload(ChatTurn activeTurn, JsonElement? responseElement)
    {
        if (responseElement is not JsonElement response || response.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var changed = false;

        if (string.IsNullOrWhiteSpace(activeTurn.AssistantReasoning))
        {
            var reasoning = ExtractResponseReasoningFromResponse(response);
            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                activeTurn.AssistantReasoning = reasoning;
                changed = true;
            }
        }

        var text = ExtractResponseTextFromResponse(response);
        if (!string.IsNullOrWhiteSpace(text))
        {
            changed |= ApplyAssistantMessageSnapshot(activeTurn, text);
        }

        if (string.IsNullOrWhiteSpace(activeTurn.AssistantAdditionalProperties)
            && response.TryGetProperty("output", out var output)
            && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (TryExtractResponseAdditionalProperties(item, out var additionalProperties))
                {
                    activeTurn.AssistantAdditionalProperties = additionalProperties;
                    changed = true;
                    break;
                }
            }
        }

        return changed;
    }

    private static StringContent BuildStreamRequestContent(
        IEnumerable<ChatTurn> chatHistory,
        string model,
        int maxTokens,
        float temperature,
        float topP,
        bool useResponsesApi)
    {
        return useResponsesApi
            ? BuildResponsesStreamRequestContent(chatHistory, model, maxTokens, temperature, topP)
            : BuildChatCompletionsStreamRequestContent(chatHistory, model, maxTokens, temperature, topP);
    }

    private static StringContent BuildChatCompletionsStreamRequestContent(
        IEnumerable<ChatTurn> chatHistory,
        string model,
        int maxTokens,
        float temperature,
        float topP)
    {
        var messages = new List<ChatCompletionMessage>();
        foreach (var turn in chatHistory)
        {
            if (!string.IsNullOrWhiteSpace(turn.UserMessage) || turn.Attachments.Count > 0)
            {
                messages.Add(new ChatCompletionMessage
                {
                    role = "user",
                    content = BuildUserContent(turn.UserMessage, turn.Attachments)
                });
            }

            if (!string.IsNullOrWhiteSpace(turn.AssistantMessage))
            {
                messages.Add(new ChatCompletionMessage
                {
                    role = "assistant",
                    content = turn.AssistantMessage
                });
            }
        }

        var payload = JsonSerializer.Serialize(new ChatCompletionRequest
        {
            stream = true,
            messages = messages.ToArray(),
            model = model,
            max_completion_tokens = maxTokens,
            temperature = temperature,
            top_p = topP
        }, JsonOptions);

        return new StringContent(payload, Encoding.UTF8, "application/json");
    }

    private static StringContent BuildResponsesStreamRequestContent(
        IEnumerable<ChatTurn> chatHistory,
        string model,
        int maxTokens,
        float temperature,
        float topP)
    {
        var input = new List<object>();
        foreach (var turn in chatHistory)
        {
            if (!string.IsNullOrWhiteSpace(turn.UserMessage) || turn.Attachments.Count > 0)
            {
                input.Add(new
                {
                    role = "user",
                    content = BuildResponsesUserContent(turn.UserMessage, turn.Attachments)
                });
            }

            if (!string.IsNullOrWhiteSpace(turn.AssistantMessage))
            {
                input.Add(new
                {
                    role = "assistant",
                    content = turn.AssistantMessage
                });
            }
        }

        var payload = JsonSerializer.Serialize(new ResponseRequest
        {
            stream = true,
            input = input.ToArray(),
            model = model,
            max_output_tokens = maxTokens,
            temperature = temperature,
            top_p = topP
        }, JsonOptions);

        return new StringContent(payload, Encoding.UTF8, "application/json");
    }

    private static object BuildUserContent(string message, IEnumerable<ChatImageAttachment> attachments)
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

    private static object BuildResponsesUserContent(string message, IEnumerable<ChatImageAttachment> attachments)
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

    private static JsonElement? TryGetJsonProperty(string json, string propertyName)
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

    private static string FormatAdditionalProperties(Dictionary<string, object?>? additionalProperties)
    {
        if (additionalProperties is null || additionalProperties.Count == 0)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(additionalProperties, JsonOptions);
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

    private static bool AppendAssistantReasoning(ChatTurn activeTurn, string? reasoning)
    {
        if (string.IsNullOrEmpty(reasoning))
        {
            return false;
        }

        activeTurn.AssistantReasoning += reasoning;
        return true;
    }

    private static bool ApplyAssistantReasoningSnapshot(ChatTurn activeTurn, string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(activeTurn.AssistantReasoning)
            || reasoning.Length > activeTurn.AssistantReasoning.Length)
        {
            activeTurn.AssistantReasoning = reasoning;
            return true;
        }

        return false;
    }

    private static bool ApplyAssistantMessageSnapshot(ChatTurn activeTurn, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(activeTurn.AssistantMessage)
            || message.Length > activeTurn.AssistantMessage.Length)
        {
            activeTurn.AssistantMessage = message;
            return true;
        }

        return false;
    }

    private static string NormalizeServerEndpoint(string serverEndpoint)
    {
        if (string.IsNullOrWhiteSpace(serverEndpoint))
        {
            throw new InvalidOperationException("Server URL cannot be empty.");
        }

        return serverEndpoint.TrimEnd('/');
    }

    private void ApplyAuthorizationHeader(string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : new AuthenticationHeaderValue("Bearer", apiKey.Trim());
    }

    private static string GetChatApiPath(bool useResponsesApi)
    {
        return useResponsesApi ? "/responses" : "/chat/completions";
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
