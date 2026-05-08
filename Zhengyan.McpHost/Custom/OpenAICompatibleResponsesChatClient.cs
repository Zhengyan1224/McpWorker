using System.ClientModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.AI;
using Zhengyan.OpenAIModels;

namespace Zhengyan.McpHost.Custom
{
    internal sealed class ResponsesChoiceRawData
    {
        public Dictionary<string, BinaryData> Values { get; } = new(StringComparer.Ordinal);
    }

    internal sealed class ResponsesStreamingUpdateRawData
    {
        public Dictionary<string, BinaryData> Values { get; } = new(StringComparer.Ordinal);

        public ResponseOutputItem? OutputItem { get; init; }
    }

    internal sealed class ResponsesStreamEvent(string? eventName, string data)
    {
        public string? EventName { get; } = eventName;

        public string Data { get; } = data;
    }

    internal sealed class ResponsesStreamingState
    {
        public string? ResponseId { get; set; }

        public string? ModelId { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }

        public ResponseUsage? Usage { get; set; }

        public StringBuilder ReasoningBuilder { get; } = new();

        public bool ReasoningDeltaReceived { get; set; }

        public bool HasEmittedContent { get; set; }

        public List<ResponsesOutputItemState> OutputItems { get; } = [];
    }

    internal sealed class ResponsesOutputItemState
    {
        public int OutputIndex { get; set; } = -1;

        public string ItemId { get; set; } = string.Empty;

        public string ItemType { get; set; } = "message";

        public string Role { get; set; } = "assistant";

        public string? Status { get; set; } = "in_progress";

        public string? FunctionName { get; set; }

        public string? CallId { get; set; }

        public StringBuilder TextBuilder { get; } = new();

        public bool TextDeltaReceived { get; set; }

        public bool TextEmitted { get; set; }

        public StringBuilder FunctionArgumentsBuilder { get; } = new();

        public bool FunctionCallReady { get; set; }

        public bool FunctionCallEmitted { get; set; }

        public ResponseOutputItem? CompletedItem { get; set; }

        public bool IsFunctionCall =>
            string.Equals(ItemType, "function_call", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class OpenAICompatibleResponsesChatClient : IChatClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _modelId;
        private readonly ChatClientMetadata _metadata;

        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public OpenAICompatibleResponsesChatClient(string endpoint, string? apiKey, string modelId)
        {
            _endpoint = endpoint.TrimEnd('/');
            _modelId = modelId;
            _metadata = new ChatClientMetadata("OpenAI-Compatible Responses", new Uri(_endpoint), modelId);

            _httpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = await SendResponseAsync(messages, options, cancellationToken);
            return ConvertToChatResponse(response, options);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = BuildResponseRequest(messages, options);
            request.stream = true;

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/responses")
            {
                Content = new StringContent(JsonSerializer.Serialize(request, _jsonSerializerOptions), Encoding.UTF8, "application/json")
            };

            using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Responses API streaming request failed with status code {(int)httpResponse.StatusCode}: {rawResponse}",
                    null,
                    httpResponse.StatusCode);
            }

            await using var responseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (var update in ReadStreamingResponseAsync(responseStream, options, cancellationToken))
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);

            if (serviceType == typeof(IChatClient))
            {
                return this;
            }

            if (serviceType == typeof(ChatClientMetadata))
            {
                return _metadata;
            }

            if (serviceType == typeof(HttpClient))
            {
                return _httpClient;
            }

            return null;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private async Task<ResponseResponse> SendResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
        {
            var request = BuildResponseRequest(messages, options);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/responses")
            {
                Content = new StringContent(JsonSerializer.Serialize(request, _jsonSerializerOptions), Encoding.UTF8, "application/json")
            };

            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Responses API request failed with status code {(int)httpResponse.StatusCode}: {rawResponse}",
                    null,
                    httpResponse.StatusCode);
            }

            return JsonSerializer.Deserialize<ResponseResponse>(rawResponse, _jsonSerializerOptions) ?? new ResponseResponse();
        }

        private ResponseRequest BuildResponseRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
        {
            var additionalProperties = new Dictionary<string, JsonElement>();

            if (options?.StopSequences is { Count: > 0 } stopSequences)
            {
                additionalProperties["stop"] = JsonSerializer.SerializeToElement(stopSequences, _jsonSerializerOptions);
            }

            if (options?.PresencePenalty is not null)
            {
                additionalProperties["presence_penalty"] = JsonSerializer.SerializeToElement(options.PresencePenalty.Value, _jsonSerializerOptions);
            }

            if (options?.FrequencyPenalty is not null)
            {
                additionalProperties["frequency_penalty"] = JsonSerializer.SerializeToElement(options.FrequencyPenalty.Value, _jsonSerializerOptions);
            }

            if (options?.Seed is not null)
            {
                additionalProperties["seed"] = JsonSerializer.SerializeToElement(options.Seed.Value, _jsonSerializerOptions);
            }

            return new ResponseRequest
            {
                model = string.IsNullOrWhiteSpace(options?.ModelId) ? _modelId : options.ModelId,
                input = BuildInputItems(messages),
                instructions = options?.Instructions,
                stream = false,
                temperature = options?.Temperature,
                top_p = options?.TopP,
                max_output_tokens = options?.MaxOutputTokens,
                parallel_tool_calls = options?.AllowMultipleToolCalls,
                tool_choice = BuildToolChoice(options),
                tools = BuildTools(options),
                additional_properties = additionalProperties.Count == 0 ? null : additionalProperties
            };
        }

        private List<object> BuildInputItems(IEnumerable<ChatMessage> messages)
        {
            var inputItems = new List<object>();

            foreach (var message in messages)
            {
                var role = ToResponsesRole(message.Role);
                var contentItems = new List<object>();

                void FlushMessage()
                {
                    if (contentItems.Count == 0)
                    {
                        return;
                    }

                    inputItems.Add(new Dictionary<string, object?>
                    {
                        ["role"] = role,
                        ["content"] = contentItems.ToArray()
                    });

                    contentItems = new List<object>();
                }

                foreach (var content in message.Contents)
                {
                    switch (content)
                    {
                        case TextContent textContent when !string.IsNullOrWhiteSpace(textContent.Text):
                            contentItems.Add(new Dictionary<string, object?>
                            {
                                ["type"] = "input_text",
                                ["text"] = textContent.Text
                            });
                            break;
                        case DataContent dataContent:
                            contentItems.Add(new Dictionary<string, object?>
                            {
                                ["type"] = "input_image",
                                ["image_url"] = dataContent.Uri
                            });
                            break;
                        case UriContent uriContent:
                            contentItems.Add(new Dictionary<string, object?>
                            {
                                ["type"] = "input_image",
                                ["image_url"] = uriContent.Uri?.ToString()
                            });
                            break;
                        case FunctionCallContent functionCallContent:
                            FlushMessage();
                            inputItems.Add(new Dictionary<string, object?>
                            {
                                ["type"] = "function_call",
                                ["name"] = functionCallContent.Name,
                                ["call_id"] = functionCallContent.CallId,
                                ["arguments"] = SerializeFunctionArguments(functionCallContent.Arguments)
                            });
                            break;
                        case FunctionResultContent functionResultContent:
                            FlushMessage();
                            inputItems.Add(new Dictionary<string, object?>
                            {
                                ["type"] = "function_call_output",
                                ["call_id"] = functionResultContent.CallId,
                                ["output"] = SerializeFunctionResult(functionResultContent.Result)
                            });
                            break;
                        default:
                            var fallbackText = content?.ToString();
                            if (!string.IsNullOrWhiteSpace(fallbackText))
                            {
                                contentItems.Add(new Dictionary<string, object?>
                                {
                                    ["type"] = "input_text",
                                    ["text"] = fallbackText
                                });
                            }
                            break;
                    }
                }

                if (contentItems.Count == 0 && !string.IsNullOrWhiteSpace(message.Text))
                {
                    contentItems.Add(new Dictionary<string, object?>
                    {
                        ["type"] = "input_text",
                        ["text"] = message.Text
                    });
                }

                FlushMessage();
            }

            return inputItems;
        }

        private object? BuildToolChoice(ChatOptions? options)
        {
            if (options?.ToolMode == null)
            {
                return options?.Tools is { Count: > 0 } ? "auto" : null;
            }

            var toolModeType = options.ToolMode.GetType().Name;
            if (string.Equals(toolModeType, "NoneChatToolMode", StringComparison.Ordinal))
            {
                return "none";
            }

            if (string.Equals(toolModeType, "AutoChatToolMode", StringComparison.Ordinal))
            {
                return "auto";
            }

            if (string.Equals(toolModeType, "RequiredChatToolMode", StringComparison.Ordinal))
            {
                var requiredFunctionName = options.ToolMode
                    .GetType()
                    .GetProperty("RequiredFunctionName")
                    ?.GetValue(options.ToolMode) as string;

                return string.IsNullOrWhiteSpace(requiredFunctionName)
                    ? "required"
                    : new Dictionary<string, object?>
                    {
                        ["type"] = "function",
                        ["name"] = requiredFunctionName
                    };
            }

            return "auto";
        }

        private object? BuildTools(ChatOptions? options)
        {
            if (options?.Tools is not { Count: > 0 } tools)
            {
                return null;
            }

            var results = new List<object>();
            foreach (var tool in tools)
            {
                var jsonSchemaProperty = tool.GetType().GetProperty("JsonSchema");
                if (jsonSchemaProperty?.GetValue(tool) is not JsonElement parametersSchema || parametersSchema.ValueKind == JsonValueKind.Undefined)
                {
                    continue;
                }

                results.Add(new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = parametersSchema.Clone()
                });
            }

            return results.Count == 0 ? null : results;
        }

        private ChatResponse ConvertToChatResponse(ResponseResponse response, ChatOptions? options)
        {
            var responseMessages = new List<ChatMessage>();
            var reasoningContent = ExtractReasoningContent(response.output);
            var hasFunctionCalls = false;

            foreach (var outputItem in response.output ?? Array.Empty<ResponseOutputItem>())
            {
                switch (outputItem.type?.ToLowerInvariant())
                {
                    case "message":
                        responseMessages.Add(CreateAssistantMessage(outputItem));
                        break;
                    case "function_call":
                        responseMessages.Add(CreateFunctionCallMessage(outputItem));
                        hasFunctionCalls = true;
                        break;
                }
            }

            if (responseMessages.Count == 0)
            {
                responseMessages.Add(new ChatMessage(ChatRole.Assistant, string.Empty));
            }

            var chatResponse = new ChatResponse(responseMessages)
            {
                ResponseId = response.id,
                ModelId = string.IsNullOrWhiteSpace(response.model) ? (string.IsNullOrWhiteSpace(options?.ModelId) ? _modelId : options.ModelId) : response.model,
                CreatedAt = response.created_at > 0 ? DateTimeOffset.FromUnixTimeSeconds(response.created_at) : null,
                FinishReason = hasFunctionCalls ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
                Usage = CreateUsageDetails(response.usage),
                RawRepresentation = CreateChoiceRawData(reasoningContent)
            };

            return chatResponse;
        }

        private async IAsyncEnumerable<ChatResponseUpdate> ReadStreamingResponseAsync(
            Stream responseStream,
            ChatOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var state = new ResponsesStreamingState
            {
                ModelId = string.IsNullOrWhiteSpace(options?.ModelId) ? _modelId : options.ModelId
            };

            await foreach (var streamEvent in ReadServerSentEventsAsync(responseStream, cancellationToken))
            {
                foreach (var update in ApplyStreamingEvent(streamEvent, state))
                {
                    yield return update;
                }
            }
        }

        private IReadOnlyList<ChatResponseUpdate> ApplyStreamingEvent(ResponsesStreamEvent streamEvent, ResponsesStreamingState state)
        {
            if (string.IsNullOrWhiteSpace(streamEvent.Data))
            {
                return [];
            }

            if (string.Equals(streamEvent.Data, "[DONE]", StringComparison.Ordinal))
            {
                return [];
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(streamEvent.Data);
            }
            catch (JsonException)
            {
                return [];
            }

            using (document)
            {
                var root = document.RootElement;
                var eventType = GetString(root, "type") ?? streamEvent.EventName;
                if (string.IsNullOrWhiteSpace(eventType))
                {
                    return [];
                }

                UpdateStreamingState(root, state);
                var normalizedEventType = eventType.ToLowerInvariant();
                var updates = new List<ChatResponseUpdate>();

                if (normalizedEventType.Contains("reasoning", StringComparison.Ordinal))
                {
                    ApplyReasoningEvent(root, normalizedEventType, state, updates);
                    return updates;
                }

                switch (normalizedEventType)
                {
                    case "response.created":
                    case "response.in_progress":
                    case "response.content_part.added":
                    case "response.content_part.done":
                        break;

                    case "response.output_item.added":
                        if (TryGetProperty(root, "item", out var addedItemElement))
                        {
                            var addedItemState = GetOrCreateOutputItemState(state, GetInt32(root, "output_index"), GetString(addedItemElement, "id"));
                            ApplyOutputItemState(addedItemState, addedItemElement);
                        }
                        break;

                    case "response.output_text.delta":
                    case "response.refusal.delta":
                    {
                        var delta = GetString(root, "delta");
                        if (string.IsNullOrEmpty(delta))
                        {
                            break;
                        }

                        var textItem = GetOrCreateOutputItemState(state, GetInt32(root, "output_index"), GetString(root, "item_id"));
                        if (textItem.TextBuilder.Length == 0 && TryGetProperty(root, "part", out var partElement))
                        {
                            AppendOutputPartText(textItem, partElement);
                        }

                        textItem.TextDeltaReceived = true;
                        textItem.TextBuilder.Append(delta);
                        updates.Add(CreateTextUpdate(state, textItem, delta));
                        state.HasEmittedContent = true;
                        break;
                    }

                    case "response.output_text.done":
                    case "response.refusal.done":
                    {
                        var textItem = GetOrCreateOutputItemState(state, GetInt32(root, "output_index"), GetString(root, "item_id"));
                        var completedText = GetString(root, "text");
                        if (string.IsNullOrWhiteSpace(completedText) && TryGetProperty(root, "part", out var textPartElement))
                        {
                            completedText = ExtractOutputPartText(textPartElement);
                        }

                        if (!string.IsNullOrWhiteSpace(completedText))
                        {
                            ReplaceBuilder(textItem.TextBuilder, completedText);
                            if (!textItem.TextDeltaReceived && !textItem.TextEmitted)
                            {
                                updates.Add(CreateTextUpdate(state, textItem, completedText));
                                textItem.TextEmitted = true;
                                state.HasEmittedContent = true;
                            }
                        }
                        break;
                    }

                    case "response.function_call_arguments.delta":
                    {
                        var functionItem = GetOrCreateOutputItemState(state, GetInt32(root, "output_index"), GetString(root, "item_id"));
                        if (TryGetProperty(root, "item", out var functionItemElement))
                        {
                            ApplyOutputItemState(functionItem, functionItemElement);
                        }

                        var argumentsDelta = GetString(root, "delta");
                        if (!string.IsNullOrEmpty(argumentsDelta))
                        {
                            functionItem.FunctionArgumentsBuilder.Append(argumentsDelta);
                        }
                        break;
                    }

                    case "response.function_call_arguments.done":
                    {
                        var functionItem = GetOrCreateOutputItemState(state, GetInt32(root, "output_index"), GetString(root, "item_id"));
                        if (TryGetProperty(root, "item", out var functionItemElement))
                        {
                            ApplyOutputItemState(functionItem, functionItemElement);
                        }

                        var completedArguments = GetString(root, "arguments");
                        if (!string.IsNullOrWhiteSpace(completedArguments))
                        {
                            ReplaceBuilder(functionItem.FunctionArgumentsBuilder, completedArguments);
                        }

                        functionItem.FunctionCallReady = true;
                        break;
                    }

                    case "response.output_item.done":
                        if (TryGetProperty(root, "item", out var completedItemElement))
                        {
                            var completedItem = GetOrCreateOutputItemState(state, GetInt32(root, "output_index"), GetString(completedItemElement, "id"));
                            ApplyOutputItemState(completedItem, completedItemElement);

                            if (completedItem.IsFunctionCall)
                            {
                                completedItem.FunctionCallReady = true;
                                break;
                            }

                            var completedText = ExtractOutputItemText(completedItem.CompletedItem);
                            if (!string.IsNullOrWhiteSpace(completedText))
                            {
                                ReplaceBuilder(completedItem.TextBuilder, completedText);
                                if (!completedItem.TextDeltaReceived && !completedItem.TextEmitted)
                                {
                                    updates.Add(CreateTextUpdate(state, completedItem, completedText));
                                    completedItem.TextEmitted = true;
                                    state.HasEmittedContent = true;
                                }
                            }
                        }
                        break;

                    case "response.completed":
                        updates.AddRange(FlushCompletedResponse(state));
                        break;

                    case "response.failed":
                    case "response.incomplete":
                        throw new InvalidOperationException($"Responses streaming terminated with event '{eventType}': {streamEvent.Data}");
                }

                return updates;
            }
        }

        private void ApplyReasoningEvent(JsonElement root, string normalizedEventType, ResponsesStreamingState state, List<ChatResponseUpdate> updates)
        {
            string? reasoningText = null;

            if (normalizedEventType.EndsWith(".delta", StringComparison.Ordinal))
            {
                reasoningText = GetString(root, "delta") ?? GetString(root, "text");
            }
            else if (normalizedEventType.EndsWith(".done", StringComparison.Ordinal))
            {
                reasoningText = GetString(root, "text");
            }

            if (string.IsNullOrWhiteSpace(reasoningText))
            {
                if (TryGetProperty(root, "part", out var partElement))
                {
                    reasoningText = ExtractOutputPartText(partElement);
                }
                else if (TryGetProperty(root, "item", out var itemElement))
                {
                    reasoningText = ExtractOutputItemText(itemElement);
                }
            }

            if (string.IsNullOrWhiteSpace(reasoningText))
            {
                return;
            }

            if (normalizedEventType.EndsWith(".done", StringComparison.Ordinal))
            {
                ReplaceBuilder(state.ReasoningBuilder, reasoningText);
                if (!state.ReasoningDeltaReceived)
                {
                    updates.Add(CreateReasoningUpdate(state, GetString(root, "item_id"), reasoningText));
                    state.HasEmittedContent = true;
                }
                return;
            }

            state.ReasoningDeltaReceived = true;
            state.ReasoningBuilder.Append(reasoningText);
            updates.Add(CreateReasoningUpdate(state, GetString(root, "item_id"), reasoningText));
            state.HasEmittedContent = true;
        }

        private List<ChatResponseUpdate> FlushCompletedResponse(ResponsesStreamingState state)
        {
            var updates = new List<ChatResponseUpdate>();
            var orderedItems = state.OutputItems.OrderBy(item => item.OutputIndex).ThenBy(item => item.ItemId, StringComparer.Ordinal).ToList();
            var pendingFunctionCalls = new List<ResponsesOutputItemState>();

            foreach (var item in orderedItems)
            {
                if (item.IsFunctionCall)
                {
                    if (item.FunctionCallReady && !item.FunctionCallEmitted)
                    {
                        pendingFunctionCalls.Add(item);
                    }
                    continue;
                }

                var text = item.TextBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(text) && !item.TextDeltaReceived && !item.TextEmitted)
                {
                    updates.Add(CreateTextUpdate(state, item, text));
                    item.TextEmitted = true;
                    state.HasEmittedContent = true;
                }
            }

            for (var index = 0; index < pendingFunctionCalls.Count; index++)
            {
                var item = pendingFunctionCalls[index];
                ChatFinishReason? finishReason = index == pendingFunctionCalls.Count - 1 ? ChatFinishReason.ToolCalls : null;
                updates.Add(CreateFunctionCallUpdate(state, item, finishReason));
                item.FunctionCallEmitted = true;
                state.HasEmittedContent = true;
            }

            if (updates.Count == 0 && !state.HasEmittedContent)
            {
                updates.Add(new ChatResponseUpdate(ChatRole.Assistant, string.Empty)
                {
                    ResponseId = state.ResponseId,
                    CreatedAt = state.CreatedAt,
                    ModelId = state.ModelId,
                    FinishReason = ChatFinishReason.Stop,
                    RawRepresentation = CreateStreamingRawData(
                        state.ReasoningBuilder.Length == 0 ? null : state.ReasoningBuilder.ToString(),
                        null)
                });
            }

            return updates;
        }

        private ChatResponseUpdate CreateTextUpdate(ResponsesStreamingState state, ResponsesOutputItemState item, string text)
        {
            return new ChatResponseUpdate(ChatRole.Assistant, text)
            {
                MessageId = string.IsNullOrWhiteSpace(item.ItemId) ? null : item.ItemId,
                ResponseId = state.ResponseId,
                CreatedAt = state.CreatedAt,
                ModelId = state.ModelId,
                RawRepresentation = CreateStreamingRawData(null, item.CompletedItem ?? BuildOutputItem(item))
            };
        }

        private ChatResponseUpdate CreateReasoningUpdate(ResponsesStreamingState state, string? itemId, string reasoningContent)
        {
            return new ChatResponseUpdate(ChatRole.Assistant, Array.Empty<AIContent>())
            {
                MessageId = itemId,
                ResponseId = state.ResponseId,
                CreatedAt = state.CreatedAt,
                ModelId = state.ModelId,
                RawRepresentation = CreateStreamingRawData(reasoningContent, null)
            };
        }

        private ChatResponseUpdate CreateFunctionCallUpdate(ResponsesStreamingState state, ResponsesOutputItemState item, ChatFinishReason? finishReason)
        {
            var argumentsJson = item.FunctionArgumentsBuilder.Length == 0 ? "{}" : item.FunctionArgumentsBuilder.ToString();
            var functionCall = new FunctionCallContent(
                item.CallId ?? item.ItemId,
                item.FunctionName ?? string.Empty,
                DeserializeFunctionArguments(argumentsJson));

            return new ChatResponseUpdate(ChatRole.Assistant, [functionCall])
            {
                MessageId = string.IsNullOrWhiteSpace(item.ItemId) ? null : item.ItemId,
                ResponseId = state.ResponseId,
                CreatedAt = state.CreatedAt,
                ModelId = state.ModelId,
                FinishReason = finishReason,
                RawRepresentation = CreateStreamingRawData(
                    state.ReasoningBuilder.Length == 0 ? null : state.ReasoningBuilder.ToString(),
                    item.CompletedItem ?? BuildOutputItem(item))
            };
        }

        private object? CreateStreamingRawData(string? reasoningContent, ResponseOutputItem? outputItem)
        {
            if (string.IsNullOrWhiteSpace(reasoningContent))
            {
                return outputItem;
            }

            var rawData = new ResponsesStreamingUpdateRawData
            {
                OutputItem = outputItem
            };

            rawData.Values["reasoning_content"] = BinaryData.FromString(reasoningContent);
            return rawData;
        }

        private ResponsesOutputItemState GetOrCreateOutputItemState(ResponsesStreamingState state, int? outputIndex, string? itemId)
        {
            ResponsesOutputItemState? item = null;

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                item = state.OutputItems.FirstOrDefault(existing => string.Equals(existing.ItemId, itemId, StringComparison.Ordinal));
            }

            if (item == null && outputIndex is not null && outputIndex.Value >= 0)
            {
                item = state.OutputItems.FirstOrDefault(existing =>
                    existing.OutputIndex == outputIndex.Value
                    && string.IsNullOrWhiteSpace(existing.ItemId));
            }

            if (item != null)
            {
                if (outputIndex is not null && outputIndex.Value >= 0)
                {
                    item.OutputIndex = outputIndex.Value;
                }

                if (!string.IsNullOrWhiteSpace(itemId))
                {
                    item.ItemId = itemId;
                }

                return item;
            }

            item = new ResponsesOutputItemState();
            if (outputIndex is not null && outputIndex.Value >= 0)
            {
                item.OutputIndex = outputIndex.Value;
            }

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                item.ItemId = itemId;
            }

            state.OutputItems.Add(item);
            return item;
        }

        private void ApplyOutputItemState(ResponsesOutputItemState state, JsonElement itemElement)
        {
            state.ItemId = GetString(itemElement, "id") ?? state.ItemId;
            state.ItemType = GetString(itemElement, "type") ?? state.ItemType;
            state.Role = GetString(itemElement, "role") ?? state.Role;
            state.Status = GetString(itemElement, "status") ?? state.Status;
            state.FunctionName = GetString(itemElement, "name") ?? state.FunctionName;
            state.CallId = GetString(itemElement, "call_id") ?? state.CallId;

            if (TryGetProperty(itemElement, "arguments", out var argumentsElement))
            {
                var arguments = ExtractStringValue(argumentsElement);
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    ReplaceBuilder(state.FunctionArgumentsBuilder, arguments);
                }
            }

            try
            {
                state.CompletedItem = JsonSerializer.Deserialize<ResponseOutputItem>(itemElement.GetRawText(), _jsonSerializerOptions);
            }
            catch (JsonException)
            {
                state.CompletedItem = null;
            }

            var completedText = ExtractOutputItemText(itemElement);
            if (!string.IsNullOrWhiteSpace(completedText))
            {
                ReplaceBuilder(state.TextBuilder, completedText);
            }
        }

        private static void ReplaceBuilder(StringBuilder builder, string value)
        {
            builder.Clear();
            builder.Append(value);
        }

        private static void AppendOutputPartText(ResponsesOutputItemState state, JsonElement partElement)
        {
            var text = ExtractOutputPartText(partElement);
            if (!string.IsNullOrWhiteSpace(text))
            {
                state.TextBuilder.Append(text);
            }
        }

        private static string? ExtractOutputItemText(ResponseOutputItem? outputItem)
        {
            if (outputItem?.content == null || outputItem.content.Length == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var part in outputItem.content)
            {
                if (part == null)
                {
                    continue;
                }

                if (!string.Equals(part.type, "output_text", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(part.type, "text", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(part.type, "refusal", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(part.text))
                {
                    continue;
                }

                builder.Append(part.text);
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static string? ExtractOutputItemText(JsonElement itemElement)
        {
            if (!TryGetProperty(itemElement, "content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var partElement in contentElement.EnumerateArray())
            {
                var text = ExtractOutputPartText(partElement);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.Append(text);
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static string? ExtractOutputPartText(JsonElement partElement)
        {
            var partType = GetString(partElement, "type");
            if (!string.Equals(partType, "output_text", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(partType, "text", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(partType, "refusal", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(partType, "summary_text", StringComparison.OrdinalIgnoreCase))
            {
                return GetString(partElement, "text");
            }

            return GetString(partElement, "text");
        }

        private void UpdateStreamingState(JsonElement payload, ResponsesStreamingState state)
        {
            var responseElement = payload;
            if (TryGetProperty(payload, "response", out var nestedResponse))
            {
                responseElement = nestedResponse;
            }

            state.ResponseId = GetString(responseElement, "id") ?? state.ResponseId;
            state.ModelId = GetString(responseElement, "model") ?? state.ModelId;

            var createdAt = GetInt64(responseElement, "created_at");
            if (createdAt is > 0)
            {
                state.CreatedAt = DateTimeOffset.FromUnixTimeSeconds(createdAt.Value);
            }

            if (TryGetProperty(responseElement, "usage", out var usageElement))
            {
                try
                {
                    state.Usage = JsonSerializer.Deserialize<ResponseUsage>(usageElement.GetRawText(), _jsonSerializerOptions);
                }
                catch (JsonException)
                {
                }
            }
        }

        private static string? ExtractStringValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => value.GetString(),
                _ => value.GetRawText()
            };
        }

        private async IAsyncEnumerable<ResponsesStreamEvent> ReadServerSentEventsAsync(
            Stream responseStream,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            string? eventName = null;
            var dataLines = new List<string>();

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    if (eventName != null || dataLines.Count > 0)
                    {
                        yield return new ResponsesStreamEvent(eventName, string.Join("\n", dataLines));
                        eventName = null;
                        dataLines.Clear();
                    }

                    continue;
                }

                if (line[0] == ':')
                {
                    continue;
                }

                if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                {
                    eventName = GetSseFieldValue(line, "event:".Length);
                    continue;
                }

                if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    dataLines.Add(GetSseFieldValue(line, "data:".Length));
                }
            }

            if (eventName != null || dataLines.Count > 0)
            {
                yield return new ResponsesStreamEvent(eventName, string.Join("\n", dataLines));
            }
        }

        private static string GetSseFieldValue(string line, int prefixLength)
        {
            var value = line[prefixLength..];
            return value.StartsWith(' ') ? value[1..] : value;
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out propertyValue))
            {
                return true;
            }

            propertyValue = default;
            return false;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return TryGetProperty(element, propertyName, out var value) ? ExtractStringValue(value) : null;
        }

        private static int? GetInt32(JsonElement element, string propertyName)
        {
            if (!TryGetProperty(element, propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
                _ => null
            };
        }

        private static long? GetInt64(JsonElement element, string propertyName)
        {
            if (!TryGetProperty(element, propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
                _ => null
            };
        }

        private ResponseOutputItem BuildOutputItem(ResponsesOutputItemState item)
        {
            var outputItem = new ResponseOutputItem
            {
                id = item.ItemId,
                type = item.ItemType,
                status = item.Status,
                role = item.Role,
                content = item.TextBuilder.Length == 0
                    ? null
                    : [
                        new ResponseContentPart
                        {
                            type = "output_text",
                            text = item.TextBuilder.ToString(),
                            annotations = Array.Empty<object>()
                        }
                    ]
            };

            var extensionData = new Dictionary<string, JsonElement>();
            if (!string.IsNullOrWhiteSpace(item.FunctionName))
            {
                extensionData["name"] = JsonSerializer.SerializeToElement(item.FunctionName, _jsonSerializerOptions);
            }

            if (!string.IsNullOrWhiteSpace(item.CallId))
            {
                extensionData["call_id"] = JsonSerializer.SerializeToElement(item.CallId, _jsonSerializerOptions);
            }

            if (item.FunctionArgumentsBuilder.Length > 0)
            {
                extensionData["arguments"] = JsonSerializer.SerializeToElement(item.FunctionArgumentsBuilder.ToString(), _jsonSerializerOptions);
            }

            outputItem.extension_data = extensionData.Count == 0 ? null : extensionData;
            return outputItem;
        }

        private ChatMessage CreateAssistantMessage(ResponseOutputItem outputItem)
        {
            var contents = new List<AIContent>();
            foreach (var contentPart in outputItem.content ?? Array.Empty<ResponseContentPart>())
            {
                if (string.Equals(contentPart.type, "output_text", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(contentPart.type, "text", StringComparison.OrdinalIgnoreCase))
                {
                    contents.Add(new TextContent(contentPart.text ?? string.Empty));
                }
            }

            var message = new ChatMessage(ChatRole.Assistant, contents)
            {
                MessageId = outputItem.id,
                RawRepresentation = outputItem
            };

            return message;
        }

        private ChatMessage CreateFunctionCallMessage(ResponseOutputItem outputItem)
        {
            var callId = TryGetExtensionString(outputItem, "call_id") ?? outputItem.id;
            var functionName = TryGetExtensionString(outputItem, "name") ?? string.Empty;
            var argumentsJson = TryGetExtensionString(outputItem, "arguments") ?? "{}";

            var functionCall = new FunctionCallContent(callId, functionName, DeserializeFunctionArguments(argumentsJson));
            var message = new ChatMessage(ChatRole.Assistant, [functionCall])
            {
                MessageId = outputItem.id,
                RawRepresentation = outputItem
            };

            return message;
        }

        private UsageDetails? CreateUsageDetails(ResponseUsage? usage)
        {
            if (usage == null)
            {
                return null;
            }

            return new UsageDetails
            {
                InputTokenCount = usage.input_tokens,
                OutputTokenCount = usage.output_tokens,
                TotalTokenCount = usage.total_tokens,
                CachedInputTokenCount = usage.input_tokens_details?.cached_tokens,
                ReasoningTokenCount = usage.output_tokens_details?.reasoning_tokens
            };
        }

        private ResponsesChoiceRawData? CreateChoiceRawData(string? reasoningContent)
        {
            if (string.IsNullOrWhiteSpace(reasoningContent))
            {
                return null;
            }

            var rawData = new ResponsesChoiceRawData();
            rawData.Values["reasoning_content"] = BinaryData.FromString(reasoningContent);
            return rawData;
        }

        private string? ExtractReasoningContent(ResponseOutputItem[]? outputItems)
        {
            if (outputItems == null || outputItems.Length == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var outputItem in outputItems)
            {
                AppendReasoningFromOutputItem(builder, outputItem);
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static void AppendReasoningFromOutputItem(StringBuilder builder, ResponseOutputItem? outputItem)
        {
            if (outputItem == null)
            {
                return;
            }

            AppendReasoningFromObjectDictionary(builder, outputItem.additional_properties);
            AppendReasoningFromJsonDictionary(builder, outputItem.extension_data);

            if (!string.Equals(outputItem.type, "reasoning", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var contentPart in outputItem.content ?? Array.Empty<ResponseContentPart>())
            {
                AppendReasoningText(builder, contentPart?.text);
                AppendReasoningFromJsonDictionary(builder, contentPart?.extension_data);
            }

            if (outputItem.extension_data == null)
            {
                return;
            }

            foreach (var propertyName in new[] { "content", "summary", "summaries", "text", "delta" })
            {
                if (outputItem.extension_data.TryGetValue(propertyName, out var element))
                {
                    AppendReasoningTextFromJsonValue(builder, element);
                }
            }
        }

        private static void AppendReasoningFromObjectDictionary(StringBuilder builder, IReadOnlyDictionary<string, object?>? values)
        {
            if (values == null)
            {
                return;
            }

            foreach (var key in new[] { "reasoning_content", "reasoningContent", "reasoning_text", "reasoningText", "thinking_content", "thinkingContent" })
            {
                if (values.TryGetValue(key, out var value) && TryReadStringValue(value, out var text))
                {
                    AppendReasoningText(builder, text);
                }
            }
        }

        private static void AppendReasoningFromJsonDictionary(StringBuilder builder, IReadOnlyDictionary<string, JsonElement>? values)
        {
            if (values == null)
            {
                return;
            }

            foreach (var key in new[] { "reasoning_content", "reasoningContent", "reasoning_text", "reasoningText", "thinking_content", "thinkingContent" })
            {
                if (values.TryGetValue(key, out var value))
                {
                    AppendReasoningTextFromJsonValue(builder, value);
                }
            }
        }

        private static void AppendReasoningTextFromJsonValue(StringBuilder builder, JsonElement element, int depth = 0)
        {
            if (depth > 12)
            {
                return;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    AppendReasoningText(builder, element.GetString());
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (IsReasoningTextPropertyName(property.Name)
                            || property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        {
                            AppendReasoningTextFromJsonValue(builder, property.Value, depth + 1);
                        }
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        AppendReasoningTextFromJsonValue(builder, item, depth + 1);
                    }
                    break;
            }
        }

        private static bool IsReasoningTextPropertyName(string propertyName)
        {
            return string.Equals(propertyName, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "delta", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "content", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "reasoning_content", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "reasoningContent", StringComparison.Ordinal)
                || string.Equals(propertyName, "reasoning_text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "reasoningText", StringComparison.Ordinal)
                || string.Equals(propertyName, "thinking_content", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "thinkingContent", StringComparison.Ordinal);
        }

        private static bool TryReadStringValue(object? value, out string? text)
        {
            text = null;
            switch (value)
            {
                case null:
                    return false;
                case string stringValue:
                    text = stringValue;
                    return !string.IsNullOrWhiteSpace(text);
                case JsonElement jsonElement:
                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        text = jsonElement.GetString();
                        return !string.IsNullOrWhiteSpace(text);
                    }

                    var builder = new StringBuilder();
                    AppendReasoningTextFromJsonValue(builder, jsonElement);
                    text = builder.Length == 0 ? jsonElement.GetRawText() : builder.ToString();
                    return !string.IsNullOrWhiteSpace(text);
                case BinaryData binaryData:
                    text = binaryData.ToString();
                    return !string.IsNullOrWhiteSpace(text);
                default:
                    text = value.ToString();
                    return !string.IsNullOrWhiteSpace(text);
            }
        }

        private static void AppendReasoningText(StringBuilder builder, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append(text.Trim());
        }

        private string SerializeFunctionArguments(IDictionary<string, object?>? arguments)
        {
            return JsonSerializer.Serialize(arguments ?? new Dictionary<string, object?>(), _jsonSerializerOptions);
        }

        private string SerializeFunctionResult(object? result)
        {
            if (result == null)
            {
                return "null";
            }

            if (result is string stringResult)
            {
                return stringResult;
            }

            if (result is JsonElement jsonElement)
            {
                return jsonElement.GetRawText();
            }

            return JsonSerializer.Serialize(result, _jsonSerializerOptions);
        }

        private Dictionary<string, object?> DeserializeFunctionArguments(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            try
            {
                using var document = JsonDocument.Parse(argumentsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new Dictionary<string, object?>(StringComparer.Ordinal);
                }

                var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    arguments[property.Name] = property.Value.Clone();
                }

                return arguments;
            }
            catch (JsonException)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["input"] = argumentsJson
                };
            }
        }

        private string ToResponsesRole(ChatRole role)
        {
            if (role == ChatRole.Assistant)
            {
                return "assistant";
            }

            if (role == ChatRole.System)
            {
                return "system";
            }

            if (role == ChatRole.Tool)
            {
                return "tool";
            }

            return "user";
        }

        private static string? TryGetExtensionString(ResponseOutputItem outputItem, string key)
        {
            if (outputItem.extension_data == null
                || !outputItem.extension_data.TryGetValue(key, out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }
    }
}
