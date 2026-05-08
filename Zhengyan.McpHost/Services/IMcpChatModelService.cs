using System.Runtime.CompilerServices;
using Zhengyan.OpenAIModels;

namespace Zhengyan.McpHost.Services
{
    public interface IMcpChatModelService
    {
        Task<ChatCompletionResponse> CreateChatCompletionAsync(string apiKey, ChatCompletionRequest request, CancellationToken cancellationToken);
        IAsyncEnumerable<string> CreateChatCompletionStreamAsync(string apiKey, ChatCompletionRequest request, CancellationToken cancellationToken);
        Task<ChatCompletionResponse> CreateChatClientCompletionAsync(string chatClientId, ChatCompletionRequest request, CancellationToken cancellationToken);
        Task<ResponseResponse> CreateResponseAsync(string apiKey, ResponseRequest request, CancellationToken cancellationToken);
        IAsyncEnumerable<string> CreateResponseStreamAsync(string apiKey, ResponseRequest request, CancellationToken cancellationToken);
        Task<string> CreateChatClientResponseAsync(string chatClientId, ResponseRequest request, CancellationToken cancellationToken);
    }
}
