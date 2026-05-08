using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using Serilog;
using Zhengyan.McpHost.Config;

namespace Zhengyan.McpHost.Services;

public sealed class AutoReconnectMcpClient : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly object _notificationLock = new();
    private readonly JsonSerializerOptions? _toolJsonSerializerOptions;
    private readonly List<NotificationRegistration> _notificationRegistrations = [];

    private McpClient? _mcpClient;
    private IChatClient? _samplingChatClient;
    private IReadOnlyList<AITool>? _chatTools;

    private AutoReconnectMcpClient(
        McpClientConfig mcpClientConfig,
        ChatClientConfig? samplingChatClientConfig,
        JsonSerializerOptions? toolJsonSerializerOptions)
    {
        McpClientConfig = mcpClientConfig;
        SamplingChatClientConfig = samplingChatClientConfig;
        _toolJsonSerializerOptions = toolJsonSerializerOptions;
    }

    public McpClientConfig McpClientConfig { get; }

    public ChatClientConfig? SamplingChatClientConfig { get; }

    public ServerCapabilities ServerCapabilities => GetClientOrThrow().ServerCapabilities;

    public Implementation ServerInfo => GetClientOrThrow().ServerInfo;

    public string? ServerInstructions => GetClientOrThrow().ServerInstructions;

    public string? SessionId => _mcpClient?.SessionId;

    public static async Task<AutoReconnectMcpClient> CreateAsync(
        McpClientConfig mcpClientConfig,
        ChatClientConfig? samplingChatClientConfig,
        JsonSerializerOptions? toolJsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var autoReconnectMcpClient = new AutoReconnectMcpClient(mcpClientConfig, samplingChatClientConfig, toolJsonSerializerOptions);
        await autoReconnectMcpClient.InitMcpClientAsync(cancellationToken);
        return autoReconnectMcpClient;
    }

    public async Task InitMcpClientAsync(CancellationToken cancellationToken = default)
    {
        await ReconnectAsync(null, forceReconnect: true, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await DisposeConnectedClientAsync();
            lock (_notificationLock)
            {
                _notificationRegistrations.Clear();
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public IAsyncDisposable RegisterNotificationHandler(string method, Func<JsonRpcNotification, CancellationToken, ValueTask> handler)
    {
        var registration = new NotificationRegistration(this, method, handler);
        lock (_notificationLock)
        {
            _notificationRegistrations.Add(registration);
        }

        try
        {
            var client = EnsureConnectedClientAsync().GetAwaiter().GetResult();
            registration.ActiveRegistration = client.RegisterNotificationHandler(method, handler);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Register notification handler failed for MCP client {McpClientId}. The handler will be registered after reconnect.", McpClientConfig.ID);
        }

        return registration;
    }

    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        _ = await ExecuteWithReconnectAsync<object?>(
            async client =>
            {
                await client.SendMessageAsync(message, cancellationToken);
                return null;
            },
            "SendMessageAsync",
            cancellationToken);
    }

    public Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteWithReconnectAsync(
            client => client.SendRequestAsync(request, cancellationToken),
            "SendRequestAsync",
            cancellationToken);
    }

    public Task<IList<McpClientTool>> ListToolsAsync(RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)
    {
        return ExecuteWithReconnectAsync(
            async client =>
            {
                var tools = await client.ListToolsAsync(requestOptions, cancellationToken);
                return (IList<McpClientTool>)[.. tools];
            },
            "ListToolsAsync",
            cancellationToken);
    }

    public async Task<IList<AITool>> GetChatToolsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _chatTools != null)
        {
            return [.. _chatTools];
        }

        var requestOptions = new RequestOptions
        {
            JsonSerializerOptions = _toolJsonSerializerOptions
        };

        var tools = await ListToolsAsync(requestOptions, cancellationToken);
        _chatTools = [.. tools.Select(tool => (AITool)new AutoReconnectMcpClientTool(this, tool.ProtocolTool, _toolJsonSerializerOptions))];
        return [.. _chatTools];
    }

    public Task<object?> CallToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithReconnectAsync(
            async client =>
            {
                dynamic dynamicClient = client;
                return (object?)await dynamicClient.CallToolAsync(toolName, arguments, null, requestOptions, cancellationToken);
            },
            $"CallToolAsync:{toolName}",
            cancellationToken);
    }

    internal Task<object?> InvokeToolAsync(
        Tool protocolTool,
        AIFunctionArguments arguments,
        JsonSerializerOptions? serializerOptions,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithReconnectAsync(
            async client =>
            {
                var currentTool = new McpClientTool(client, protocolTool, serializerOptions);
                return await currentTool.InvokeAsync(arguments, cancellationToken);
            },
            $"InvokeToolAsync:{protocolTool.Name}",
            cancellationToken);
    }

    private async Task<McpClient> EnsureConnectedClientAsync(CancellationToken cancellationToken = default)
    {
        if (_mcpClient != null)
        {
            return _mcpClient;
        }

        return await ReconnectAsync(null, forceReconnect: false, cancellationToken);
    }

    private async Task<McpClient> ReconnectAsync(McpClient? failingClient, bool forceReconnect, CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceReconnect && _mcpClient != null && (failingClient == null || !ReferenceEquals(_mcpClient, failingClient)))
            {
                return _mcpClient;
            }

            await DisposeConnectedClientAsync();

            _samplingChatClient = CreateSamplingChatClient(SamplingChatClientConfig);
            _mcpClient = await CreateInnerClientAsync(McpClientConfig, _samplingChatClient, cancellationToken);
            RestoreNotificationHandlers(_mcpClient);
            Log.Information("Reconnect MCP client success. McpClientID: {McpClientId}", McpClientConfig.ID);
            return _mcpClient;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<T> ExecuteWithReconnectAsync<T>(
        Func<McpClient, Task<T>> action,
        string operationName,
        CancellationToken cancellationToken)
    {
        var client = await EnsureConnectedClientAsync(cancellationToken);
        try
        {
            return await action(client);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MCP client operation failed and will retry after reconnect. McpClientID: {McpClientId}, Operation: {OperationName}", McpClientConfig.ID, operationName);
            client = await ReconnectAsync(client, forceReconnect: false, cancellationToken);
            return await action(client);
        }
    }

    private McpClient GetClientOrThrow()
    {
        return _mcpClient ?? throw new InvalidOperationException($"MCP client {McpClientConfig.ID} is not connected.");
    }

    private async Task DisposeConnectedClientAsync()
    {
        _chatTools = null;

        if (_mcpClient != null)
        {
            try
            {
                await _mcpClient.DisposeAsync();
                Log.Information("Dispose MCP client success. McpClientID: {McpClientId}", McpClientConfig.ID);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Dispose MCP client failed. McpClientID: {McpClientId}", McpClientConfig.ID);
            }
            finally
            {
                _mcpClient = null;
            }
        }

        if (_samplingChatClient != null)
        {
            try
            {
                _samplingChatClient.Dispose();
                Log.Information("Dispose sampling chat client success. McpClientID: {McpClientId}", McpClientConfig.ID);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Dispose sampling chat client failed. McpClientID: {McpClientId}", McpClientConfig.ID);
            }
            finally
            {
                _samplingChatClient = null;
            }
        }
    }

    private void RestoreNotificationHandlers(McpClient client)
    {
        List<NotificationRegistration> registrations;
        lock (_notificationLock)
        {
            registrations = [.. _notificationRegistrations];
        }

        foreach (var registration in registrations)
        {
            registration.ActiveRegistration = client.RegisterNotificationHandler(registration.Method, registration.Handler);
        }
    }

    private async ValueTask UnregisterNotificationAsync(NotificationRegistration registration)
    {
        lock (_notificationLock)
        {
            _notificationRegistrations.Remove(registration);
        }

        if (registration.ActiveRegistration != null)
        {
            await registration.ActiveRegistration.DisposeAsync();
            registration.ActiveRegistration = null;
        }
    }

    private static IChatClient? CreateSamplingChatClient(ChatClientConfig? samplingChatClientConfig)
    {
        if (samplingChatClientConfig == null)
        {
            return null;
        }

        return GlobalObjectPoolService.CreateManagedChatClient(samplingChatClientConfig, enableFunctionInvocation: false);
    }

    private static McpClientOptions? CreateMcpClientOptions(IChatClient? samplingChatClient)
    {
        if (samplingChatClient == null)
        {
            return null;
        }

        return new McpClientOptions
        {
            Capabilities = new ClientCapabilities
            {
                Sampling = new SamplingCapability()
            },
            Handlers = new McpClientHandlers
            {
                SamplingHandler = samplingChatClient.CreateSamplingHandler()
            },
        };
    }

    private static async Task<McpClient> CreateInnerClientAsync(
        McpClientConfig mcpClientConfig,
        IChatClient? samplingChatClient,
        CancellationToken cancellationToken)
    {
        var mcpClientOptions = CreateMcpClientOptions(samplingChatClient);

        if (mcpClientConfig.StdioConfig != null)
        {
            return await McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Name = mcpClientConfig.Name,
                Command = mcpClientConfig.StdioConfig.Command,
                Arguments = mcpClientConfig.StdioConfig.Arguments,
                EnvironmentVariables = mcpClientConfig.StdioConfig.EnvironmentVariables,
                WorkingDirectory = mcpClientConfig.StdioConfig.WorkingDirectory,
                ShutdownTimeout = TimeSpan.FromSeconds(mcpClientConfig.StdioConfig.ShutdownTimeout),
            }), mcpClientOptions, cancellationToken: cancellationToken);
        }

        if (mcpClientConfig.StreamableHttpConfig != null)
        {
            return await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Name = mcpClientConfig.Name,
                Endpoint = new Uri(mcpClientConfig.StreamableHttpConfig.Endpoint),
                AdditionalHeaders = mcpClientConfig.StreamableHttpConfig.AdditionalHeaders,
                ConnectionTimeout = TimeSpan.FromSeconds(mcpClientConfig.StreamableHttpConfig.ConnectionTimeout),
                TransportMode = HttpTransportMode.StreamableHttp
            }), mcpClientOptions, cancellationToken: cancellationToken);
        }

        if (mcpClientConfig.SseConfig != null)
        {
            return await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Name = mcpClientConfig.Name,
                Endpoint = new Uri(mcpClientConfig.SseConfig.Endpoint),
                AdditionalHeaders = mcpClientConfig.SseConfig.AdditionalHeaders,
                ConnectionTimeout = TimeSpan.FromSeconds(mcpClientConfig.SseConfig.ConnectionTimeout),
                TransportMode = HttpTransportMode.Sse
            }), mcpClientOptions, cancellationToken: cancellationToken);
        }

        throw new InvalidOperationException($"McpClient: {mcpClientConfig} is not valid, please check the config.");
    }

    private sealed class NotificationRegistration : IAsyncDisposable
    {
        private readonly AutoReconnectMcpClient _owner;

        public NotificationRegistration(
            AutoReconnectMcpClient owner,
            string method,
            Func<JsonRpcNotification, CancellationToken, ValueTask> handler)
        {
            _owner = owner;
            Method = method;
            Handler = handler;
        }

        public string Method { get; }

        public Func<JsonRpcNotification, CancellationToken, ValueTask> Handler { get; }

        public IAsyncDisposable? ActiveRegistration { get; set; }

        public async ValueTask DisposeAsync()
        {
            await _owner.UnregisterNotificationAsync(this);
        }
    }
}

public sealed class AutoReconnectMcpClientTool : AIFunction
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties = new Dictionary<string, object?>();
    private static readonly JsonSerializerOptions DefaultToolJsonSerializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly AutoReconnectMcpClient _autoReconnectMcpClient;
    private readonly JsonSerializerOptions? _jsonSerializerOptions;

    public AutoReconnectMcpClientTool(
        AutoReconnectMcpClient autoReconnectMcpClient,
        Tool protocolTool,
        JsonSerializerOptions? jsonSerializerOptions)
    {
        _autoReconnectMcpClient = autoReconnectMcpClient;
        ProtocolTool = protocolTool;
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    public Tool ProtocolTool { get; }

    public override string Name => ProtocolTool.Name;

    public override string Description => ProtocolTool.Description ?? string.Empty;

    public override JsonElement JsonSchema => ProtocolTool.InputSchema;

    public override JsonSerializerOptions JsonSerializerOptions => EnsureToolJsonSerializerOptions(_jsonSerializerOptions);

    public override IReadOnlyDictionary<string, object?> AdditionalProperties => EmptyProperties;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return new ValueTask<object?>(_autoReconnectMcpClient.InvokeToolAsync(ProtocolTool, arguments, JsonSerializerOptions, cancellationToken));
    }

    private static JsonSerializerOptions EnsureToolJsonSerializerOptions(JsonSerializerOptions? source)
    {
        if (source?.TypeInfoResolver != null)
        {
            return source;
        }

        if (source == null)
        {
            return DefaultToolJsonSerializerOptions;
        }

        return new JsonSerializerOptions(source)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
    }
}
