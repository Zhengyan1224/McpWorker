using Zhengyan.McpHost.Config;
using Zhengyan.OpenAIModels;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Collections;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using System.Reflection;
using Zhengyan.McpHost.Custom;
using ModelContextProtocol.Client;
using Zhengyan.McpHost.Extensions;
using OpenAIChatCompletionOptions = OpenAI.Chat.ChatCompletionOptions;


namespace Zhengyan.McpHost.Services
{
    /// <summary>
    /// Chat 模型服务
    /// </summary>
    public class McpChatModelService : IMcpChatModelService
    {
        private readonly ILogger<McpChatModelService> _logger;
        private readonly GlobalObjectPoolService _globalObjectPoolService;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public McpChatModelService(ILogger<McpChatModelService> logger, GlobalObjectPoolService globalObjectPoolService, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _globalObjectPoolService = globalObjectPoolService;
            _httpClientFactory = httpClientFactory;
        }

        #region ChatCompletion

        private bool CheckApiKey(AgentConfig agentConfig, string apiKey)
        {
            if (agentConfig.ApiKeyExpirations == null || agentConfig.ApiKeyExpirations.Count == 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(apiKey) && agentConfig.ApiKeyExpirations.TryGetValue(apiKey, out var expiration))
            {
                if (DateTime.TryParse(expiration, out var expirationDate))
                {
                    return expirationDate > DateTime.Now;
                }
            }
            return false;
        }

        /// <summary>
        /// 聊天完成
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<ChatCompletionResponse> CreateChatCompletionAsync(string apiKey, ChatCompletionRequest request, CancellationToken cancellationToken)
        {
            return CreateChatCompletionAsyncCore(apiKey, request, null, cancellationToken);
        }

        private async Task<ChatCompletionResponse> CreateChatCompletionAsyncCore(string apiKey, ChatCompletionRequest request, string? additionalSystemPrompt, CancellationToken cancellationToken)
        {
            var agentId = request.model;

            if (!_globalObjectPoolService.AgentConfigs.TryGetValue(agentId, out var agentConfig))
            {
                _logger.LogError($"AgentConfigs not found for ID: {agentId}");
                return new ChatCompletionResponse();
            }

            // 检查 API Key 是否过期
            if (!CheckApiKey(agentConfig, apiKey))
            {
                _logger.LogError($"API Key '{apiKey}' is invalid.");
                return new ChatCompletionResponse();
            }

            string chatClientID = agentConfig.ChatClientID;

            if (!_globalObjectPoolService.ChatClients.TryGetValue(chatClientID, out var chatClient))
            {
                _logger.LogError($"ChatClient not found for ID: {chatClientID}");
                return new ChatCompletionResponse();
            }

            _globalObjectPoolService.ChatClientConfigs.TryGetValue(chatClientID, out var chatClientConfig);

            var chatOptions = new ChatOptions()
            {
                Tools = new List<AITool>(),
                Temperature = request.temperature,
                TopP = request.top_p,
                MaxOutputTokens = request.max_completion_tokens,
                StopSequences = request.stop,
                PresencePenalty = request.presence_penalty,
                FrequencyPenalty = request.frequency_penalty,
                Seed = request.seed
            };
            ConfigureCompatibleChatOptions(chatOptions, chatClientConfig, request);
            List<ToolCallResult> toolCallResults = new List<ToolCallResult>();
            object toolCallResultsSync = new object();
            if (agentConfig.McpClientIDs != null)
                foreach (var mcpClientID in agentConfig.McpClientIDs)
                {
                    if (_globalObjectPoolService.McpClientTools.TryGetValue(mcpClientID, out var mcpTools))
                    {
                        foreach (var mcpTool in mcpTools)
                        {
                            if (mcpTool is not AIFunction functionTool)
                            {
                                chatOptions.Tools.Add(mcpTool);
                                continue;
                            }

                            var mcpexttool = new McpClientExtendTool(functionTool)
                            {
                                ValueCapture = (t, args, ret) =>
                                {
                                    lock (toolCallResultsSync)
                                    {
                                        toolCallResults.Add(new() { tool_name = t.Name, arguments = args, return_values = ret });
                                    }
                                }
                            };
                            chatOptions.Tools.Add(mcpexttool);
                        }
                    }
                    else
                    {
                        _logger.LogError($"McpClient not found for ID: {mcpClientID}");
                    }
                }

            // 没有消息
            if (request.messages is null || request.messages.Length == 0)
            {
                _logger.LogWarning("No message in chat history.");
                return new ChatCompletionResponse();
            }

            var chatHistory = GetChatHistory(request, BuildCombinedSystemPrompt(agentConfig.SystemPrompt, additionalSystemPrompt));

            var response = await chatClient.GetResponseAsync(chatHistory, chatOptions, cancellationToken);
            var responseText = ExtractChatResponseText(response);
            var reasoningContent = ExtractReasoningContent(response);

            if (string.IsNullOrWhiteSpace(responseText) && toolCallResults.Count > 0)
            {
                _logger.LogWarning("Primary response text is empty after tool invocation. Generating fallback answer. AgentId: {agentId}", agentId);
                var fallbackResponse = await GenerateFallbackAnswerAsync(chatClient, agentConfig.SystemPrompt, request, toolCallResults, cancellationToken);
                responseText = fallbackResponse.Text;
                if (string.IsNullOrWhiteSpace(reasoningContent))
                {
                    reasoningContent = fallbackResponse.ReasoningContent;
                }

                response.Usage ??= new();
                if (fallbackResponse.Usage != null)
                {
                    response.Usage.InputTokenCount = (response.Usage.InputTokenCount ?? 0) + (fallbackResponse.Usage.InputTokenCount ?? 0);
                    response.Usage.OutputTokenCount = (response.Usage.OutputTokenCount ?? 0) + (fallbackResponse.Usage.OutputTokenCount ?? 0);
                    response.Usage.TotalTokenCount = (response.Usage.TotalTokenCount ?? 0) + (fallbackResponse.Usage.TotalTokenCount ?? 0);
                }
            }

            var prompt_tokens = response.Usage?.InputTokenCount ?? 0;
            var completion_tokens = response.Usage?.OutputTokenCount ?? 0;
            var total_tokens = response.Usage?.TotalTokenCount ?? 0;

            _logger.LogDebug("Prompt tokens: {prompt_tokens}, Completion tokens: {completion_tokens}", prompt_tokens, completion_tokens);
            _logger.LogDebug("Completion result: {result}", responseText);


            return new ChatCompletionResponse
            {
                id = $"chatcmpl-{Guid.NewGuid():N}",
                model = request.model,
                created = DateTimeOffset.Now.ToUnixTimeSeconds(),
                choices =
                [
                    new ChatCompletionResponseChoice
                    {
                        index = 0,
                        finish_reason = completion_tokens >= request.max_completion_tokens ? "length" : "stop",
                        message = new ChatCompletionMessage
                        {
                            role = "assistant",
                            content = responseText,
                            reasoning_content = reasoningContent,
                            additional_properties = SetAdditionalProperties(toolCallResults, null)
                        }
                    }
                ],
                usage = new UsageInfo
                {
                    prompt_tokens = prompt_tokens,
                    completion_tokens = completion_tokens,
                    total_tokens = total_tokens
                }
            };
        }

        public async Task<ChatCompletionResponse> CreateChatClientCompletionAsync(string chatClientId, ChatCompletionRequest request, CancellationToken cancellationToken)
        {
            if (!_globalObjectPoolService.ChatClientConfigs.TryGetValue(chatClientId, out var chatClientConfig))
            {
                _logger.LogError("ChatClientConfig not found for ID: {chatClientId}", chatClientId);
                return new ChatCompletionResponse();
            }

            if (request.messages is null || request.messages.Length == 0)
            {
                _logger.LogWarning("No message in chat history.");
                return new ChatCompletionResponse();
            }

            var outboundRequest = new ChatCompletionRequest
            {
                model = string.IsNullOrWhiteSpace(request.model) ? chatClientConfig.ModelId : request.model,
                messages = request.messages,
                stream = false,
                n = request.n,
                temperature = request.temperature,
                top_p = request.top_p,
                stop = request.stop,
                max_completion_tokens = request.max_completion_tokens,
                presence_penalty = request.presence_penalty,
                frequency_penalty = request.frequency_penalty,
                seed = request.seed,
                user = request.user,
                tool_choice = request.tool_choice,
                tools = request.tools,
                additional_properties = CloneJsonElementDictionary(request.additional_properties)
            };

            ApplyCompatibleChatRequestDefaults(chatClientConfig, outboundRequest);

            using var httpClient = _httpClientFactory.CreateClient();
            if (!string.IsNullOrWhiteSpace(chatClientConfig.ApiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chatClientConfig.ApiKey);
            }

            var endpoint = $"{chatClientConfig.Endpoint.TrimEnd('/')}/chat/completions";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(outboundRequest, _jsonSerializerOptions), Encoding.UTF8, "application/json")
            };

            using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(rawResponse, _jsonSerializerOptions);
            return NormalizeChatCompletionReasoningContent(completionResponse ?? new ChatCompletionResponse(), rawResponse);
        }

        public async Task<string> CreateChatClientResponseAsync(string chatClientId, ResponseRequest request, CancellationToken cancellationToken)
        {
            if (!_globalObjectPoolService.ChatClientConfigs.TryGetValue(chatClientId, out var chatClientConfig))
            {
                _logger.LogError("ChatClientConfig not found for ID: {chatClientId}", chatClientId);
                return "{}";
            }

            var outboundRequest = CloneResponseRequest(request, string.IsNullOrWhiteSpace(request.model) ? chatClientConfig.ModelId : request.model);
            outboundRequest.stream = false;

            using var httpClient = _httpClientFactory.CreateClient();
            if (!string.IsNullOrWhiteSpace(chatClientConfig.ApiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chatClientConfig.ApiKey);
            }

            var endpoint = $"{chatClientConfig.Endpoint.TrimEnd('/')}/responses";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(outboundRequest, _jsonSerializerOptions), Encoding.UTF8, "application/json")
            };

            using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            return NormalizeResponseReasoningContent(rawResponse);
        }

        public async Task<ResponseResponse> CreateResponseAsync(string apiKey, ResponseRequest request, CancellationToken cancellationToken)
        {
            var chatRequest = ConvertToChatCompletionRequest(request, stream: false);
            var chatResponse = await CreateChatCompletionAsyncCore(apiKey, chatRequest, request.instructions, cancellationToken);
            return BuildResponseResponse(request, chatResponse);
        }

        /// <summary>
        /// 流式生成-聊天完成
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<string> CreateChatCompletionStreamAsync(string apiKey, ChatCompletionRequest request, CancellationToken cancellationToken)
        {
            return CreateChatCompletionStreamAsyncCore(apiKey, request, null, cancellationToken);
        }

        public async IAsyncEnumerable<string> CreateResponseStreamAsync(string apiKey, ResponseRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var streamingRequest = CloneResponseRequest(request, request.model);
            streamingRequest.stream = true;

            var chatRequest = ConvertToChatCompletionRequest(streamingRequest, stream: true);
            var responseId = $"resp_{Guid.NewGuid():N}";
            var outputItemId = $"msg_{Guid.NewGuid():N}";
            var createdAt = DateTimeOffset.Now.ToUnixTimeSeconds();
            var outputText = new StringBuilder();
            var reasoningContent = new StringBuilder();
            Dictionary<string, object?>? outputAdditionalProperties = null;

            yield return CreateSseEvent("response.created", new
            {
                type = "response.created",
                response = CreateStreamingResponseEnvelope(responseId, createdAt, request, "in_progress")
            });

            yield return CreateSseEvent("response.in_progress", new
            {
                type = "response.in_progress",
                response = CreateStreamingResponseEnvelope(responseId, createdAt, request, "in_progress")
            });

            yield return CreateSseEvent("response.output_item.added", new
            {
                type = "response.output_item.added",
                output_index = 0,
                item = new
                {
                    id = outputItemId,
                    type = "message",
                    status = "in_progress",
                    role = "assistant",
                    content = Array.Empty<object>()
                }
            });

            yield return CreateSseEvent("response.content_part.added", new
            {
                type = "response.content_part.added",
                output_index = 0,
                content_index = 0,
                item_id = outputItemId,
                part = new
                {
                    type = "output_text",
                    text = string.Empty,
                    annotations = Array.Empty<object>()
                }
            });

            await foreach (var chatEvent in CreateChatCompletionStreamAsyncCore(apiKey, chatRequest, request.instructions, cancellationToken))
            {
                if (!TryParseChatStreamEvent(chatEvent, out var chunk, out var done))
                {
                    if (done)
                    {
                        break;
                    }

                    continue;
                }

                var delta = chunk?.choices?.FirstOrDefault()?.delta;
                if (delta == null)
                {
                    continue;
                }

                var deltaText = ChatContentTextExtractor.GetText(delta.content);
                if (!string.IsNullOrEmpty(deltaText))
                {
                    outputText.Append(deltaText);
                    yield return CreateSseEvent("response.output_text.delta", new
                    {
                        type = "response.output_text.delta",
                        output_index = 0,
                        content_index = 0,
                        item_id = outputItemId,
                        delta = deltaText
                    });
                }

                if (!string.IsNullOrWhiteSpace(delta.reasoning_content))
                {
                    reasoningContent.Append(delta.reasoning_content);
                    outputAdditionalProperties = MergeAdditionalProperties(outputAdditionalProperties, new Dictionary<string, object?>
                    {
                        ["reasoning_content"] = reasoningContent.ToString()
                    });

                    yield return CreateSseEvent("response.reasoning.delta", new
                    {
                        type = "response.reasoning.delta",
                        output_index = 0,
                        item_id = outputItemId,
                        delta = delta.reasoning_content
                    });
                }

                if (delta.additional_properties != null && delta.additional_properties.Count > 0)
                {
                    outputAdditionalProperties = MergeAdditionalProperties(
                        outputAdditionalProperties,
                        SetAdditionalProperties(null, delta.additional_properties));

                    yield return CreateSseEvent("response.additional_properties.delta", new
                    {
                        type = "response.additional_properties.delta",
                        output_index = 0,
                        item_id = outputItemId,
                        additional_properties = outputAdditionalProperties
                    });
                }
            }

            var outputItem = new ResponseOutputItem
            {
                id = outputItemId,
                type = "message",
                status = "completed",
                role = "assistant",
                content =
                [
                    new ResponseContentPart
                    {
                        type = "output_text",
                        text = outputText.ToString(),
                        annotations = Array.Empty<object>()
                    }
                ],
                additional_properties = outputAdditionalProperties
            };

            var completedResponse = CreateStreamingResponseEnvelope(responseId, createdAt, request, "completed");
            completedResponse.output = [outputItem];
            completedResponse.usage = new ResponseUsage
            {
                input_tokens = 0,
                output_tokens = 0,
                total_tokens = 0
            };

            yield return CreateSseEvent("response.output_text.done", new
            {
                type = "response.output_text.done",
                output_index = 0,
                content_index = 0,
                item_id = outputItemId,
                text = outputText.ToString()
            });

            yield return CreateSseEvent("response.content_part.done", new
            {
                type = "response.content_part.done",
                output_index = 0,
                content_index = 0,
                item_id = outputItemId,
                part = outputItem.content?.FirstOrDefault()
            });

            yield return CreateSseEvent("response.output_item.done", new
            {
                type = "response.output_item.done",
                output_index = 0,
                item = outputItem
            });

            if (reasoningContent.Length > 0)
            {
                yield return CreateSseEvent("response.reasoning.done", new
                {
                    type = "response.reasoning.done",
                    output_index = 0,
                    item_id = outputItemId,
                    text = reasoningContent.ToString()
                });
            }

            yield return CreateSseEvent("response.completed", new
            {
                type = "response.completed",
                response = completedResponse
            });

            yield return "data: [DONE]\n\n";
        }

        private async IAsyncEnumerable<string> CreateChatCompletionStreamAsyncCore(string apiKey, ChatCompletionRequest request, string? additionalSystemPrompt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {

            var agentId = request.model;

            if (!_globalObjectPoolService.AgentConfigs.TryGetValue(agentId, out var agentConfig))
            {
                _logger.LogError($"AgentConfigs not found for ID: {agentId}");
                yield break;
            }

            // 检查 API Key 是否过期
            if (!CheckApiKey(agentConfig, apiKey))
            {
                _logger.LogError($"API Key '{apiKey}' is invalid.");
                yield break;
            }

            string chatClientID = agentConfig.ChatClientID;

            if (!_globalObjectPoolService.ChatClients.TryGetValue(chatClientID, out var chatClient))
            {
                _logger.LogError($"ChatClient not found for ID: {chatClientID}");
                yield break;
            }

            _globalObjectPoolService.ChatClientConfigs.TryGetValue(chatClientID, out var chatClientConfig);

            var chatOptions = new ChatOptions
            {
                Tools = new List<AITool>(),
                Temperature = request.temperature,
                TopP = request.top_p,
                MaxOutputTokens = request.max_completion_tokens,
                StopSequences = request.stop,
                PresencePenalty = request.presence_penalty,
                FrequencyPenalty = request.frequency_penalty,
                Seed = request.seed
            };
            ConfigureCompatibleChatOptions(chatOptions, chatClientConfig, request);

            List<ToolCallResult> toolCallResults = new List<ToolCallResult>();
            object toolCallResultsSync = new object();

            if (agentConfig.McpClientIDs != null)
                foreach (var mcpClientID in agentConfig.McpClientIDs)
                {
                    if (_globalObjectPoolService.McpClientTools.TryGetValue(mcpClientID, out var mcpTools))
                    {
                        foreach (var mcpTool in mcpTools)
                        {
                            if (mcpTool is not AIFunction functionTool)
                            {
                                chatOptions.Tools.Add(mcpTool);
                                continue;
                            }

                            var mcpexttool = new McpClientExtendTool(functionTool)
                            {
                                ValueCapture = (t, args, ret) =>
                                {
                                    lock (toolCallResultsSync)
                                    {
                                        toolCallResults.Add(new() { tool_name = t.Name, arguments = args, return_values = ret });
                                    }
                                }
                            };
                            chatOptions.Tools.Add(mcpexttool);
                        }
                    }
                    else
                    {
                        _logger.LogError($"McpClient not found for ID: {mcpClientID}");
                    }
                }

            // 没有消息
            if (request.messages is null || request.messages.Length == 0)
            {
                _logger.LogWarning("No message in chat history.");
                yield break;
            }

            var chatHistory = GetChatHistory(request, BuildCombinedSystemPrompt(agentConfig.SystemPrompt, additionalSystemPrompt));

            var id = $"chatcmpl-{Guid.NewGuid():N}";
            var created = DateTimeOffset.Now.ToUnixTimeSeconds();

            int index = 0;

            // 第一个消息，带着角色名称
            var chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
            {
                id = id,
                created = created,
                model = request.model,
                choices = [
                    new ChatCompletionChunkResponseChoice
                    {
                        index = index,
                        delta = new ChatCompletionMessage
                        {
                            role = "assistant"
                        },
                        finish_reason = null
                    }
                ]
            }, _jsonSerializerOptions);
            yield return $"data: {chunk}\n\n";

            // 处理模型输出
            var emittedToolCallResultsCount = 0;
            await foreach (var responseChunk in chatClient.GetStreamingResponseAsync(chatHistory, chatOptions, cancellationToken))
            {
                _logger.LogTrace("Message: {output}", responseChunk);

                var content = responseChunk.Text;
                var reasoningContent = ExtractReasoningContent(responseChunk);

                if (TrySnapshotToolCallResults(toolCallResults, toolCallResultsSync, ref emittedToolCallResultsCount, out var currentToolCallResults))
                {
                    chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
                    {
                        id = id,
                        created = created,
                        model = request.model,
                        choices = [
                            new ChatCompletionChunkResponseChoice
                            {
                                index = ++index,
                                delta = new ChatCompletionMessage
                                {
                                    role = null,
                                    content = null,
                                    reasoning_content = null,
                                    additional_properties = SetAdditionalProperties(currentToolCallResults, null)
                                },
                                finish_reason = null
                            }
                        ]

                    }, _jsonSerializerOptions);

                    yield return $"data: {chunk}\n\n";
                }

                if (string.IsNullOrEmpty(content) && string.IsNullOrWhiteSpace(reasoningContent))
                {
                    continue;
                }

                chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
                {
                    id = id,
                    created = created,
                    model = request.model,
                    choices = [
                           new ChatCompletionChunkResponseChoice
                          {
                            index = ++index,
                            delta = new ChatCompletionMessage
                            {
                                 role = null,
                                 content = content,
                                 reasoning_content = reasoningContent,
                            },
                            finish_reason= null
                          }
                      ]

                }, _jsonSerializerOptions);


                yield return $"data: {chunk}\n\n";
            }

            if (TrySnapshotToolCallResults(toolCallResults, toolCallResultsSync, ref emittedToolCallResultsCount, out var remainingToolCallResults))
            {
                chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
                {
                    id = id,
                    created = created,
                    model = request.model,
                    choices = [
                        new ChatCompletionChunkResponseChoice
                        {
                            index = ++index,
                            delta = new ChatCompletionMessage
                            {
                                role = null,
                                content = null,
                                reasoning_content = null,
                                additional_properties = SetAdditionalProperties(remainingToolCallResults, null)
                            },
                            finish_reason = null
                        }
                    ]
                }, _jsonSerializerOptions);
                yield return $"data: {chunk}\n\n";
            }

             

            // 结束
            chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
            {
                id = id,
                created = created,
                model = request.model,
                choices = [
                    new ChatCompletionChunkResponseChoice
                    {
                        index = ++index,
                        delta = new ChatCompletionMessage
                        {
                            role = null,
                            content = null,
                            reasoning_content = null,
                            additional_properties = SetAdditionalProperties(SnapshotToolCallResults(toolCallResults, toolCallResultsSync), null)
                        },
                        finish_reason = "stop"
                    }
                ]
            }, _jsonSerializerOptions);
            yield return $"data: {chunk}\n\n";
            yield return "data: [DONE]\n\n";
            yield break;
        }

        private static Dictionary<string, object?>? SetAdditionalProperties(List<ToolCallResult>? toolCallResults, Dictionary<string, object?>? additional_properties)
        {
            if (toolCallResults != null && toolCallResults.Count > 0)
            {
                if (additional_properties == null)
                {
                    additional_properties = new Dictionary<string, object>();
                }

                additional_properties.TryAdd("tool_call_results", toolCallResults);
            }
            return additional_properties;
        }

        private static Dictionary<string, object?>? MergeAdditionalProperties(
            Dictionary<string, object?>? current,
            Dictionary<string, object?>? incoming)
        {
            if (incoming == null || incoming.Count == 0)
            {
                return current;
            }

            current ??= new Dictionary<string, object?>();
            foreach (var pair in incoming)
            {
                current[pair.Key] = pair.Value;
            }

            return current;
        }

        private static List<ToolCallResult> SnapshotToolCallResults(List<ToolCallResult> toolCallResults, object syncRoot)
        {
            lock (syncRoot)
            {
                return [.. toolCallResults];
            }
        }

        private static bool TrySnapshotToolCallResults(
            List<ToolCallResult> toolCallResults,
            object syncRoot,
            ref int emittedCount,
            out List<ToolCallResult>? snapshot)
        {
            lock (syncRoot)
            {
                if (toolCallResults.Count <= emittedCount)
                {
                    snapshot = null;
                    return false;
                }

                emittedCount = toolCallResults.Count;
                snapshot = [.. toolCallResults];
                return true;
            }
        }

        private static string? BuildCombinedSystemPrompt(string? systemPrompt, string? additionalSystemPrompt)
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                return additionalSystemPrompt;
            }

            if (string.IsNullOrWhiteSpace(additionalSystemPrompt))
            {
                return systemPrompt;
            }

            return $"{systemPrompt}\n\n{additionalSystemPrompt}";
        }

        private ChatCompletionRequest ConvertToChatCompletionRequest(ResponseRequest request, bool stream)
        {
            return new ChatCompletionRequest
            {
                model = request.model,
                stream = stream,
                temperature = request.temperature ?? 1.0f,
                top_p = request.top_p ?? 1.0f,
                max_completion_tokens = request.max_output_tokens,
                user = request.user,
                messages = ConvertResponseInputToMessages(request.input),
                additional_properties = CloneJsonElementDictionary(request.additional_properties)
            };
        }

        private ChatCompletionMessage[] ConvertResponseInputToMessages(object? input)
        {
            var messages = new List<ChatCompletionMessage>();

            switch (input)
            {
                case null:
                    return Array.Empty<ChatCompletionMessage>();
                case string text when !string.IsNullOrWhiteSpace(text):
                    messages.Add(new ChatCompletionMessage
                    {
                        role = "user",
                        content = text
                    });
                    return [.. messages];
                case JsonElement jsonElement:
                    AppendResponseInputToMessages(jsonElement, messages);
                    return [.. messages];
                default:
                    messages.Add(new ChatCompletionMessage
                    {
                        role = "user",
                        content = input.ToString()
                    });
                    return [.. messages];
            }
        }

        private void AppendResponseInputToMessages(JsonElement input, List<ChatCompletionMessage> messages)
        {
            switch (input.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return;
                case JsonValueKind.String:
                    var text = input.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        messages.Add(new ChatCompletionMessage
                        {
                            role = "user",
                            content = text
                        });
                    }
                    return;
                case JsonValueKind.Object:
                    if (!TryConvertResponseMessage(input, out var message))
                    {
                        message = new ChatCompletionMessage
                        {
                            role = "user",
                            content = input.Clone()
                        };
                    }

                    messages.Add(message);
                    return;
                case JsonValueKind.Array:
                    var items = input.EnumerateArray().ToArray();
                    if (items.Length == 0)
                    {
                        return;
                    }

                    if (items.All(IsResponseMessageLike))
                    {
                        foreach (var item in items)
                        {
                            if (TryConvertResponseMessage(item, out var responseMessage))
                            {
                                messages.Add(responseMessage);
                            }
                        }
                    }
                    else
                    {
                        messages.Add(new ChatCompletionMessage
                        {
                            role = "user",
                            content = input.Clone()
                        });
                    }
                    return;
                default:
                    messages.Add(new ChatCompletionMessage
                    {
                        role = "user",
                        content = input.Clone()
                    });
                    return;
            }
        }

        private static bool IsResponseMessageLike(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return item.TryGetProperty("role", out _)
                || (GetStringProperty(item, "type") is string itemType
                    && string.Equals(itemType, "message", StringComparison.OrdinalIgnoreCase));
        }

        private bool TryConvertResponseMessage(JsonElement item, out ChatCompletionMessage message)
        {
            message = new ChatCompletionMessage();
            if (item.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var role = GetStringProperty(item, "role");
            if (string.Equals(role, "developer", StringComparison.OrdinalIgnoreCase))
            {
                role = "system";
            }

            object? content = null;
            if (item.TryGetProperty("content", out var contentElement))
            {
                content = contentElement.Clone();
            }
            else if (item.TryGetProperty("text", out var textElement))
            {
                content = textElement.ValueKind == JsonValueKind.String ? textElement.GetString() : textElement.Clone();
            }
            else if (GetStringProperty(item, "type") is string itemType
                && (string.Equals(itemType, "input_text", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemType, "input_image", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemType, "text", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemType, "image_url", StringComparison.OrdinalIgnoreCase)))
            {
                role ??= "user";
                content = item.Clone();
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                role = "user";
            }

            if (content == null)
            {
                return false;
            }

            message = new ChatCompletionMessage
            {
                role = role,
                content = content
            };
            return true;
        }

        private static string? GetStringProperty(JsonElement item, string propertyName)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
        }

        private ResponseResponse BuildResponseResponse(ResponseRequest request, ChatCompletionResponse chatResponse)
        {
            var assistantMessage = chatResponse.choices?.FirstOrDefault()?.message ?? new ChatCompletionMessage();
            var additionalProperties = assistantMessage.additional_properties == null
                ? null
                : new Dictionary<string, object?>(assistantMessage.additional_properties);

            var reasoningContent = assistantMessage.reasoning_content;
            if (string.IsNullOrWhiteSpace(reasoningContent)
                && additionalProperties != null
                && TryReadStringValue(additionalProperties.GetValueOrDefault("reasoning_content"), out var additionalReasoningContent))
            {
                reasoningContent = additionalReasoningContent;
            }

            RemoveReasoningAdditionalProperties(additionalProperties);

            var outputItems = new List<ResponseOutputItem>();
            if (!string.IsNullOrWhiteSpace(reasoningContent))
            {
                outputItems.Add(CreateResponseReasoningOutputItem(reasoningContent));
            }

            outputItems.Add(new ResponseOutputItem
            {
                id = $"msg_{Guid.NewGuid():N}",
                type = "message",
                status = "completed",
                role = "assistant",
                content =
                [
                    new ResponseContentPart
                    {
                        type = "output_text",
                        text = ChatContentTextExtractor.GetText(assistantMessage.content) ?? string.Empty,
                        annotations = Array.Empty<object>()
                    }
                ],
                additional_properties = additionalProperties is { Count: > 0 } ? additionalProperties : null
            });

            var response = new ResponseResponse
            {
                id = $"resp_{Guid.NewGuid():N}",
                created_at = chatResponse.created == 0 ? DateTimeOffset.Now.ToUnixTimeSeconds() : chatResponse.created,
                status = "completed",
                instructions = request.instructions,
                max_output_tokens = request.max_output_tokens,
                model = string.IsNullOrWhiteSpace(chatResponse.model) ? request.model : chatResponse.model,
                output = [.. outputItems],
                parallel_tool_calls = request.parallel_tool_calls,
                temperature = request.temperature,
                text = new ResponseTextConfig
                {
                    format = new ResponseTextFormat
                    {
                        type = "text"
                    }
                },
                tool_choice = request.tool_choice,
                tools = request.tools,
                top_p = request.top_p,
                usage = new ResponseUsage
                {
                    input_tokens = chatResponse.usage?.prompt_tokens ?? 0,
                    output_tokens = chatResponse.usage?.completion_tokens ?? 0,
                    total_tokens = chatResponse.usage?.total_tokens ?? 0,
                    input_tokens_details = new ResponseTokenDetails
                    {
                        cached_tokens = 0
                    },
                    output_tokens_details = new ResponseTokenDetails
                    {
                        reasoning_tokens = 0
                    }
                },
                user = request.user,
                metadata = request.metadata == null ? null : new Dictionary<string, JsonElement>(request.metadata)
            };

            return response;
        }

        private ResponseResponse CreateStreamingResponseEnvelope(string responseId, long createdAt, ResponseRequest request, string status)
        {
            return new ResponseResponse
            {
                id = responseId,
                created_at = createdAt,
                status = status,
                instructions = request.instructions,
                max_output_tokens = request.max_output_tokens,
                model = request.model,
                output = Array.Empty<ResponseOutputItem>(),
                parallel_tool_calls = request.parallel_tool_calls,
                temperature = request.temperature,
                text = new ResponseTextConfig
                {
                    format = new ResponseTextFormat
                    {
                        type = "text"
                    }
                },
                tool_choice = request.tool_choice,
                tools = request.tools,
                top_p = request.top_p,
                usage = new ResponseUsage(),
                user = request.user,
                metadata = request.metadata == null ? null : new Dictionary<string, JsonElement>(request.metadata)
            };
        }

        private string CreateSseEvent(string eventName, object payload)
        {
            return $"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, _jsonSerializerOptions)}\n\n";
        }

        private bool TryParseChatStreamEvent(string rawEvent, out ChatCompletionChunkResponse? chunk, out bool done)
        {
            chunk = null;
            done = false;

            if (string.IsNullOrWhiteSpace(rawEvent))
            {
                return false;
            }

            var lines = rawEvent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var dataLine = lines.FirstOrDefault(line => line.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
            if (dataLine == null)
            {
                return false;
            }

            var payload = dataLine[5..].Trim();
            if (string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                done = true;
                return false;
            }

            chunk = JsonSerializer.Deserialize<ChatCompletionChunkResponse>(payload, _jsonSerializerOptions);
            return chunk != null;
        }

        private ChatCompletionResponse NormalizeChatCompletionReasoningContent(ChatCompletionResponse response, string? rawResponse)
        {
            var assistantMessage = response.choices?.FirstOrDefault()?.message;
            if (assistantMessage == null || !string.IsNullOrWhiteSpace(assistantMessage.reasoning_content))
            {
                return response;
            }

            var reasoningContent = ExtractReasoningContentFromChatCompletionJson(rawResponse)
                ?? ExtractReasoningContentFromRawJson(rawResponse);
            if (!string.IsNullOrWhiteSpace(reasoningContent))
            {
                assistantMessage.reasoning_content = reasoningContent;
            }

            return response;
        }

        private string NormalizeResponseReasoningContent(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return rawResponse;
            }

            try
            {
                var response = JsonSerializer.Deserialize<ResponseResponse>(rawResponse, _jsonSerializerOptions);
                if (response == null)
                {
                    return rawResponse;
                }

                if (HasResponseReasoningOutputItem(response))
                {
                    return rawResponse;
                }

                var reasoningContent = ExtractReasoningContent(response)
                    ?? ExtractReasoningContentFromRawJson(rawResponse);
                if (string.IsNullOrWhiteSpace(reasoningContent))
                {
                    return rawResponse;
                }

                return AttachResponseReasoningOutputItem(response, reasoningContent)
                    ? JsonSerializer.Serialize(response, _jsonSerializerOptions)
                    : rawResponse;
            }
            catch (JsonException)
            {
                return rawResponse;
            }
        }

        private static bool AttachResponseReasoningOutputItem(ResponseResponse response, string reasoningContent)
        {
            if (string.IsNullOrWhiteSpace(reasoningContent))
            {
                return false;
            }

            if (HasResponseReasoningOutputItem(response))
            {
                return false;
            }

            var outputItems = response.output?.ToList() ?? new List<ResponseOutputItem>();
            var reasoningOutputItem = CreateResponseReasoningOutputItem(reasoningContent);
            var firstMessageIndex = outputItems.FindIndex(item =>
                string.Equals(item.type, "message", StringComparison.OrdinalIgnoreCase));

            if (firstMessageIndex >= 0)
            {
                outputItems.Insert(firstMessageIndex, reasoningOutputItem);
            }
            else
            {
                outputItems.Add(reasoningOutputItem);
            }

            foreach (var messageItem in outputItems.Where(item =>
                string.Equals(item.type, "message", StringComparison.OrdinalIgnoreCase)))
            {
                RemoveReasoningAdditionalProperties(messageItem.additional_properties);
                if (messageItem.additional_properties is { Count: 0 })
                {
                    messageItem.additional_properties = null;
                }
            }

            response.output = [.. outputItems];
            return true;
        }

        private static bool HasResponseReasoningOutputItem(ResponseResponse? response)
        {
            return response?.output?.Any(item =>
                string.Equals(item.type, "reasoning", StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static ResponseOutputItem CreateResponseReasoningOutputItem(string reasoningContent)
        {
            return new ResponseOutputItem
            {
                id = $"msg_{Guid.NewGuid():N}",
                type = "reasoning",
                status = null,
                role = null,
                content = null,
                extension_data = new Dictionary<string, JsonElement>
                {
                    ["summary"] = JsonSerializer.SerializeToElement(new[]
                    {
                        new
                        {
                            text = reasoningContent,
                            type = "summary_text"
                        }
                    })
                }
            };
        }

        private static void RemoveReasoningAdditionalProperties(Dictionary<string, object?>? additionalProperties)
        {
            if (additionalProperties == null)
            {
                return;
            }

            foreach (var key in new[] { "reasoning_content", "reasoningContent", "reasoning_text", "reasoningText", "thinking_content", "thinkingContent" })
            {
                additionalProperties.Remove(key);
            }
        }

        private static string? ExtractReasoningContent(ResponseResponse? response)
        {
            if (response?.output == null || response.output.Length == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var outputItem in response.output)
            {
                AppendReasoningFromResponseOutputItem(builder, outputItem);
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static void AppendReasoningFromResponseOutputItem(StringBuilder builder, ResponseOutputItem? outputItem)
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

        private static string? ExtractReasoningContentFromChatCompletionJson(string? rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("choices", out var choicesElement)
                    || choicesElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var builder = new StringBuilder();
                foreach (var choiceElement in choicesElement.EnumerateArray())
                {
                    if (choiceElement.ValueKind != JsonValueKind.Object
                        || !choiceElement.TryGetProperty("message", out var messageElement))
                    {
                        continue;
                    }

                    AppendReasoningFromJsonElement(builder, messageElement);
                }

                return builder.Length == 0 ? null : builder.ToString();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ExtractReasoningContentFromRawJson(string? rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                var builder = new StringBuilder();
                AppendReasoningFromJsonElement(builder, document.RootElement);
                return builder.Length == 0 ? null : builder.ToString();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void AppendReasoningFromJsonElement(StringBuilder builder, JsonElement element, int depth = 0)
        {
            if (depth > 12)
            {
                return;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (IsReasoningTypedObject(element))
                    {
                        AppendReasoningTextFromJsonValue(builder, element, depth + 1);
                    }

                    foreach (var property in element.EnumerateObject())
                    {
                        if (IsReasoningContentPropertyName(property.Name))
                        {
                            AppendReasoningTextFromJsonValue(builder, property.Value, depth + 1);
                            continue;
                        }

                        if (IsReasoningContainerPropertyName(property.Name))
                        {
                            AppendReasoningTextFromJsonValue(builder, property.Value, depth + 1);
                            continue;
                        }

                        AppendReasoningFromJsonElement(builder, property.Value, depth + 1);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        AppendReasoningFromJsonElement(builder, item, depth + 1);
                    }
                    break;
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
                            || IsReasoningContentPropertyName(property.Name)
                            || IsReasoningContainerPropertyName(property.Name))
                        {
                            AppendReasoningTextFromJsonValue(builder, property.Value, depth + 1);
                        }
                        else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
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

        private static bool IsReasoningTypedObject(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var type = typeElement.GetString();
            return !string.IsNullOrWhiteSpace(type)
                && (type.Contains("reasoning", StringComparison.OrdinalIgnoreCase)
                    || type.Contains("thinking", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsReasoningContentPropertyName(string propertyName)
        {
            return string.Equals(propertyName, "reasoning_content", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "reasoningContent", StringComparison.Ordinal)
                || string.Equals(propertyName, "reasoning_text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "reasoningText", StringComparison.Ordinal)
                || string.Equals(propertyName, "thinking_content", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "thinkingContent", StringComparison.Ordinal);
        }

        private static bool IsReasoningContainerPropertyName(string propertyName)
        {
            return string.Equals(propertyName, "reasoning", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "thinking", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReasoningTextPropertyName(string propertyName)
        {
            return string.Equals(propertyName, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "delta", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "content", StringComparison.OrdinalIgnoreCase)
                || IsReasoningContentPropertyName(propertyName);
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
                    text = ReadChoiceStringValue(binaryData);
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

            var normalizedText = text.Trim();
            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append(normalizedText);
        }

        private static ResponseRequest CloneResponseRequest(ResponseRequest request, string? fallbackModel)
        {
            return new ResponseRequest
            {
                model = string.IsNullOrWhiteSpace(request.model) ? fallbackModel ?? string.Empty : request.model,
                input = request.input,
                instructions = request.instructions,
                stream = request.stream,
                temperature = request.temperature,
                top_p = request.top_p,
                max_output_tokens = request.max_output_tokens,
                parallel_tool_calls = request.parallel_tool_calls,
                tool_choice = request.tool_choice,
                tools = request.tools,
                user = request.user,
                metadata = request.metadata == null ? null : new Dictionary<string, JsonElement>(request.metadata),
                additional_properties = request.additional_properties == null ? null : new Dictionary<string, JsonElement>(request.additional_properties)
            };
        }

        private void ConfigureCompatibleChatOptions(ChatOptions chatOptions, ChatClientConfig? chatClientConfig, ChatCompletionRequest request)
        {
            var additionalProperties = GetEffectiveChatAdditionalProperties(chatClientConfig, request);
            if (additionalProperties == null || additionalProperties.Count == 0)
            {
                return;
            }

            chatOptions.RawRepresentationFactory = _ => CreateCompatibleRawChatCompletionOptions(additionalProperties);
        }

        private static OpenAIChatCompletionOptions? CreateCompatibleRawChatCompletionOptions(
            IReadOnlyDictionary<string, JsonElement>? additionalProperties)
        {
            if (additionalProperties == null || additionalProperties.Count == 0)
            {
                return null;
            }

            var rawOptions = new OpenAIChatCompletionOptions();
            if (!TryGetMutableSerializedAdditionalRawData(rawOptions, out var serializedAdditionalRawData))
            {
                return null;
            }

            foreach (var pair in additionalProperties)
            {
                serializedAdditionalRawData[pair.Key] = BinaryData.FromString(pair.Value.GetRawText());
            }

            return rawOptions;
        }

        private static bool TryGetMutableSerializedAdditionalRawData(
            object target,
            out IDictionary<string, BinaryData> serializedAdditionalRawData)
        {
            serializedAdditionalRawData = null!;

            if (target == null)
            {
                return false;
            }

            var property = target.GetType().GetProperty(
                "SerializedAdditionalRawData",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property?.GetValue(target) is IDictionary<string, BinaryData> propertyValue)
            {
                serializedAdditionalRawData = propertyValue;
                return true;
            }

            var field = target.GetType().GetField(
                "_serializedAdditionalRawData",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? target.GetType().GetField(
                    "serializedAdditionalRawData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field?.GetValue(target) is IDictionary<string, BinaryData> fieldValue)
            {
                serializedAdditionalRawData = fieldValue;
                return true;
            }

            return false;
        }

        private static Dictionary<string, JsonElement>? GetEffectiveChatAdditionalProperties(
            ChatClientConfig? chatClientConfig,
            ChatCompletionRequest request)
        {
            var additionalProperties = CloneJsonElementDictionary(request.additional_properties);

            if (ShouldEnableDashScopeThinking(chatClientConfig)
                && (additionalProperties == null || !additionalProperties.ContainsKey("enable_thinking")))
            {
                additionalProperties ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                additionalProperties["enable_thinking"] = JsonSerializer.SerializeToElement(true);
            }

            return additionalProperties;
        }

        private static void ApplyCompatibleChatRequestDefaults(ChatClientConfig? chatClientConfig, ChatCompletionRequest request)
        {
            var additionalProperties = GetEffectiveChatAdditionalProperties(chatClientConfig, request);
            request.additional_properties = additionalProperties;
        }

        private static bool ShouldEnableDashScopeThinking(ChatClientConfig? chatClientConfig)
        {
            if (chatClientConfig == null
                || string.IsNullOrWhiteSpace(chatClientConfig.Endpoint)
                || !Uri.TryCreate(chatClientConfig.Endpoint, UriKind.Absolute, out var endpointUri))
            {
                return false;
            }

            return endpointUri.Host.Contains("dashscope.aliyuncs.com", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, JsonElement>? CloneJsonElementDictionary(Dictionary<string, JsonElement>? source)
        {
            return source == null ? null : new Dictionary<string, JsonElement>(source, StringComparer.Ordinal);
        }

        /// <summary>
        /// 生成对话历史
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="systemPrompt">系统提示词</param>
        /// <returns></returns>
        private IList<ChatMessage> GetChatHistory(ChatCompletionRequest request, string? systemPrompt)
        {
            IList<ChatMessage> history = new List<ChatMessage>();

            var messages = request.messages;

            // 不存在系统消息时，需要添加默认或者空白的系统提示
            var firstRole = messages.FirstOrDefault()?.role;
            var hasLeadingSystemMessage = string.Equals(firstRole, "system", StringComparison.OrdinalIgnoreCase)
                || string.Equals(firstRole, "developer", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(systemPrompt) && !hasLeadingSystemMessage)
            {
                _logger.LogDebug($"Add system prompt.{systemPrompt}");
                messages = messages.Prepend(new ChatCompletionMessage
                {
                    role = "system",
                    content = systemPrompt
                }).ToArray();
            }

            foreach (var message in messages)
            {
                if (!TryMapChatRole(message.role, out var chatRole))
                {
                    continue;
                }

                history.Add(BuildChatMessage(chatRole, message.content));
            }
            return history;
        }

        /// <summary>
        /// 将 OpenAI 的消息内容（纯文本 / 多模态数组）转换为 Microsoft.Extensions.AI 消息
        /// </summary>
        private ChatMessage BuildChatMessage(ChatRole role, object? rawContent)
        {
            if (TryParseMultiModalContents(rawContent, out var contents))
            {
                return new ChatMessage(role, contents);
            }

            return new ChatMessage(role, GetFallbackText(rawContent));
        }

        private bool TryMapChatRole(string? role, out ChatRole chatRole)
        {
            switch (role?.ToLowerInvariant())
            {
                case "user":
                    chatRole = ChatRole.User;
                    return true;
                case "assistant":
                    chatRole = ChatRole.Assistant;
                    return true;
                case "system":
                    chatRole = ChatRole.System;
                    return true;
                case "developer":
                    chatRole = ChatRole.System;
                    return true;
                case "tool":
                    chatRole = ChatRole.Tool;
                    return true;
                default:
                    chatRole = ChatRole.User;
                    _logger.LogWarning("Ignore unsupported chat role: {role}", role);
                    return false;
            }
        }

        private bool TryParseMultiModalContents(object? rawContent, out List<AIContent> contents)
        {
            contents = new List<AIContent>();

            switch (rawContent)
            {
                case null:
                    return false;
                case string textContent:
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        contents.Add(new TextContent(textContent));
                    }
                    return contents.Count > 0;
                case JsonElement jsonElement:
                    return TryParseJsonElementContent(jsonElement, contents);
                default:
                    return false;
            }
        }

        private bool TryParseJsonElementContent(JsonElement jsonElement, List<AIContent> contents)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    var text = jsonElement.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        contents.Add(new TextContent(text));
                    }
                    return contents.Count > 0;
                case JsonValueKind.Array:
                    foreach (var part in jsonElement.EnumerateArray())
                    {
                        AddContentPart(part, contents);
                    }
                    return contents.Count > 0;
                case JsonValueKind.Object:
                    AddContentPart(jsonElement, contents);
                    return contents.Count > 0;
                default:
                    return false;
            }
        }

        private void AddContentPart(JsonElement part, List<AIContent> contents)
        {
            if (part.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var partType = part.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

            if (string.Equals(partType, "text", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(partType, "input_text", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(partType))
            {
                if (part.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        contents.Add(new TextContent(text));
                        return;
                    }
                }
            }

            if (string.Equals(partType, "image_url", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(partType, "input_image", StringComparison.OrdinalIgnoreCase) ||
                part.TryGetProperty("image_url", out _))
            {
                if (TryGetImageUrl(part, out var imageUrl))
                {
                    var imageContent = CreateImageContent(imageUrl!);
                    if (imageContent != null)
                    {
                        contents.Add(imageContent);
                        return;
                    }
                }
            }

            contents.Add(new TextContent(part.GetRawText()));
        }

        private bool TryGetImageUrl(JsonElement part, out string? imageUrl)
        {
            imageUrl = null;
            if (!part.TryGetProperty("image_url", out var imageUrlElement))
            {
                return false;
            }

            if (imageUrlElement.ValueKind == JsonValueKind.String)
            {
                imageUrl = imageUrlElement.GetString();
                return !string.IsNullOrWhiteSpace(imageUrl);
            }

            if (imageUrlElement.ValueKind == JsonValueKind.Object &&
                imageUrlElement.TryGetProperty("url", out var urlElement) &&
                urlElement.ValueKind == JsonValueKind.String)
            {
                imageUrl = urlElement.GetString();
                return !string.IsNullOrWhiteSpace(imageUrl);
            }

            return false;
        }

        private AIContent? CreateImageContent(string imageUrl)
        {
            try
            {
                if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    return new DataContent(imageUrl, GetMediaTypeFromDataUri(imageUrl));
                }

                return new UriContent(imageUrl, "image/*");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid image content url: {imageUrl}", imageUrl);
                return null;
            }
        }

        private static string GetMediaTypeFromDataUri(string dataUri)
        {
            const string defaultMediaType = "image/*";
            if (!dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return defaultMediaType;
            }

            var commaIndex = dataUri.IndexOf(',');
            if (commaIndex <= 5)
            {
                return defaultMediaType;
            }

            var header = dataUri[5..commaIndex];
            if (string.IsNullOrWhiteSpace(header))
            {
                return defaultMediaType;
            }

            var mediaType = header
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.Contains('/'))
            {
                return defaultMediaType;
            }

            return mediaType;
        }

        private static string? GetFallbackText(object? rawContent)
        {
            if (rawContent == null)
            {
                return null;
            }

            if (rawContent is string textContent)
            {
                return textContent;
            }

            if (rawContent is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return jsonElement.GetString();
                }

                if (jsonElement.ValueKind == JsonValueKind.Null || jsonElement.ValueKind == JsonValueKind.Undefined)
                {
                    return null;
                }

                return jsonElement.GetRawText();
            }

            return rawContent.ToString();
        }

        private static string ExtractChatResponseText(ChatResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response?.Text))
            {
                return response.Text;
            }

            if (response?.Messages == null || response.Messages.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var message in response.Messages)
            {
                AppendReflectedText(builder, message);
            }

            if (builder.Length == 0)
            {
                foreach (var message in response.Messages)
                {
                    AppendRawRepresentationText(builder, message?.RawRepresentation);
                }
            }

            return builder.ToString();
        }

        private static void AppendReflectedText(StringBuilder builder, object? target)
        {
            if (target == null)
            {
                return;
            }

            var targetType = target.GetType();
            var textProperty = targetType.GetProperty("Text");
            if (textProperty?.GetValue(target) is string textValue && !string.IsNullOrWhiteSpace(textValue))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine().AppendLine();
                }
                builder.Append(textValue.Trim());
            }

            var contentsProperty = targetType.GetProperty("Contents");
            if (contentsProperty?.GetValue(target) is System.Collections.IEnumerable contents)
            {
                foreach (var content in contents)
                {
                    var contentType = content?.GetType();
                    var contentTextProperty = contentType?.GetProperty("Text");
                    if (contentTextProperty?.GetValue(content) is string contentText && !string.IsNullOrWhiteSpace(contentText))
                    {
                        if (builder.Length > 0)
                        {
                            builder.AppendLine().AppendLine();
                        }
                        builder.Append(contentText.Trim());
                    }
                }
            }
        }

        private static void AppendRawRepresentationText(StringBuilder builder, object? target, int depth = 0)
        {
            if (target == null || depth > 4)
            {
                return;
            }

            if (target is string text)
            {
                AppendText(builder, text);
                return;
            }

            if (target is JsonElement jsonElement)
            {
                AppendJsonElementText(builder, jsonElement, depth + 1);
                return;
            }

            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var targetType = target.GetType();

            foreach (var propertyName in new[] { "Text", "Content", "Value" })
            {
                var propertyValue = targetType.GetProperty(propertyName, flags)?.GetValue(target);
                if (propertyValue is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                {
                    AppendText(builder, stringValue);
                }
            }

            foreach (var propertyName in new[] { "Message", "Messages", "Choices", "Contents", "Content", "ContentItems", "Parts" })
            {
                var propertyValue = targetType.GetProperty(propertyName, flags)?.GetValue(target);
                if (propertyValue is System.Collections.IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                    {
                        AppendRawRepresentationText(builder, item, depth + 1);
                    }
                }
                else
                {
                    AppendRawRepresentationText(builder, propertyValue, depth + 1);
                }
            }
        }

        private static void AppendJsonElementText(StringBuilder builder, JsonElement element, int depth)
        {
            if (depth > 4)
            {
                return;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    AppendText(builder, element.GetString());
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        AppendJsonElementText(builder, item, depth + 1);
                    }
                    break;
                case JsonValueKind.Object:
                    foreach (var propertyName in new[] { "text", "content", "value", "message", "messages", "choices", "contents" })
                    {
                        if (element.TryGetProperty(propertyName, out var propertyElement))
                        {
                            AppendJsonElementText(builder, propertyElement, depth + 1);
                        }
                    }
                    break;
            }
        }

        private static void AppendText(StringBuilder builder, string? text)
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

        private async Task<FallbackAnswerResult> GenerateFallbackAnswerAsync(
            IChatClient chatClient,
            string? systemPrompt,
            ChatCompletionRequest originalRequest,
            List<ToolCallResult> toolCallResults,
            CancellationToken cancellationToken)
        {
            var fallbackMessages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                fallbackMessages.Add(new ChatMessage(ChatRole.System, $"{systemPrompt}\n\nYou already have the tool outputs. Answer the user directly and do not call any more tools."));
            }
            else
            {
                fallbackMessages.Add(new ChatMessage(ChatRole.System, "You already have the tool outputs. Answer the user directly and do not call any more tools."));
            }

            fallbackMessages.Add(new ChatMessage(ChatRole.User, BuildFallbackPrompt(originalRequest, toolCallResults)));

            var fallbackOptions = new ChatOptions
            {
                Temperature = originalRequest.temperature,
                TopP = originalRequest.top_p,
                MaxOutputTokens = originalRequest.max_completion_tokens,
                StopSequences = originalRequest.stop,
                PresencePenalty = originalRequest.presence_penalty,
                FrequencyPenalty = originalRequest.frequency_penalty,
                Seed = originalRequest.seed
            };

            var fallbackResponse = await chatClient.GetResponseAsync(fallbackMessages, fallbackOptions, cancellationToken);
            return new FallbackAnswerResult
            {
                Text = ExtractChatResponseText(fallbackResponse),
                ReasoningContent = ExtractReasoningContent(fallbackResponse),
                Usage = fallbackResponse.Usage
            };
        }

        private static string? ExtractReasoningContent(ChatResponseUpdate responseUpdate)
        {
            var reasoningContent = ExtractReasoningContentFromContents(responseUpdate?.Contents);
            if (!string.IsNullOrWhiteSpace(reasoningContent))
            {
                return reasoningContent;
            }

            reasoningContent = ReadChoiceStringValue(responseUpdate.GetChoiceValue("reasoning_content"));
            if (!string.IsNullOrWhiteSpace(reasoningContent))
            {
                return reasoningContent;
            }

            return ExtractReasoningContentFromKnownRawRepresentation(responseUpdate?.RawRepresentation);
        }

        private static string? ExtractReasoningContent(ChatResponse response)
        {
            if (response?.Messages != null)
            {
                foreach (var message in response.Messages)
                {
                    var messageReasoning = ExtractReasoningContentFromContents(message?.Contents);
                    if (!string.IsNullOrWhiteSpace(messageReasoning))
                    {
                        return messageReasoning;
                    }

                    messageReasoning = ExtractReasoningContentFromKnownRawRepresentation(message?.RawRepresentation);
                    if (!string.IsNullOrWhiteSpace(messageReasoning))
                    {
                        return messageReasoning;
                    }
                }
            }

            var reasoningContent = ReadChoiceStringValue(response.GetChoiceValue("reasoning_content"));
            if (!string.IsNullOrWhiteSpace(reasoningContent))
            {
                return reasoningContent;
            }

            return ExtractReasoningContentFromKnownRawRepresentation(response?.RawRepresentation);
        }

        private static string? ExtractReasoningContentFromKnownRawRepresentation(object? rawRepresentation)
        {
            switch (rawRepresentation)
            {
                case null:
                    return null;
                case ResponseResponse response:
                    return ExtractReasoningContent(response);
                case ResponseOutputItem outputItem:
                    var outputItemBuilder = new StringBuilder();
                    AppendReasoningFromResponseOutputItem(outputItemBuilder, outputItem);
                    return outputItemBuilder.Length == 0 ? null : outputItemBuilder.ToString();
                case JsonElement jsonElement:
                    var jsonBuilder = new StringBuilder();
                    AppendReasoningFromJsonElement(jsonBuilder, jsonElement);
                    return jsonBuilder.Length == 0 ? null : jsonBuilder.ToString();
                case string rawJson:
                    return ExtractReasoningContentFromRawJson(rawJson);
                case BinaryData binaryData:
                    return ReadChoiceStringValue(binaryData);
                default:
                    return null;
            }
        }

        private static string? ExtractReasoningContentFromContents(IEnumerable? contents)
        {
            if (contents == null)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var content in contents)
            {
                if (content == null)
                {
                    continue;
                }

                var contentType = content.GetType();
                if (!contentType.Name.Contains("Reasoning", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = contentType.GetProperty("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(content) as string;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                builder.Append(text);
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static string? ReadChoiceStringValue(System.BinaryData? value)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                return value.ToObjectFromJson<string>();
            }
            catch (JsonException)
            {
                var rawText = value.ToString();
                return string.IsNullOrWhiteSpace(rawText) ? null : rawText;
            }
        }

        private string BuildFallbackPrompt(ChatCompletionRequest originalRequest, List<ToolCallResult> toolCallResults)
        {
            var builder = new StringBuilder();
            builder.AppendLine("The previous completion called tools but did not provide the final assistant answer.");
            builder.AppendLine("Please generate the final assistant reply for the user based on the original conversation and the tool call results.");
            builder.AppendLine();
            builder.AppendLine("Conversation:");
            builder.AppendLine(JsonSerializer.Serialize(originalRequest.messages, _jsonSerializerOptions));
            builder.AppendLine();
            builder.AppendLine("Tool Call Results:");
            builder.AppendLine(JsonSerializer.Serialize(toolCallResults, _jsonSerializerOptions));
            builder.AppendLine();
            builder.AppendLine("Requirements:");
            builder.AppendLine("1. Answer the user's latest request directly.");
            builder.AppendLine("2. Use the tool outputs as evidence.");
            builder.AppendLine("3. Do not call any tools again.");
            return builder.ToString();
        }

        private sealed class FallbackAnswerResult
        {
            public string Text { get; set; } = string.Empty;

            public string? ReasoningContent { get; set; }

            public UsageDetails? Usage { get; set; }
        }

        #endregion

    }
}
