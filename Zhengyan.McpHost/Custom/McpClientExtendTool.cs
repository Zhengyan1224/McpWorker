
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json.Serialization.Metadata;


namespace Zhengyan.McpHost.Custom;


public class ToolCallResult
{
    public string? tool_name { get; set; }
    public AIFunctionArguments? arguments { get; set; }
    public JsonElement? return_values { get; set; }
}

public delegate void AIFunctionValueCaptureAction(AITool triggerer, AIFunctionArguments arguments, JsonElement? return_value);

public class McpClientExtendTool : AIFunction
{
    private static readonly JsonSerializerOptions CaptureJsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly AIFunction _function;

    public Tool? ProtocolTool => _function switch
    {
        McpClientTool mcpClientTool => mcpClientTool.ProtocolTool,
        Services.AutoReconnectMcpClientTool autoReconnectTool => autoReconnectTool.ProtocolTool,
        _ => null
    };

    public override string Name => _function.Name;

    public override string Description => _function.Description;

    public override JsonElement JsonSchema => _function.JsonSchema;

    public override JsonSerializerOptions JsonSerializerOptions => _function.JsonSerializerOptions;

    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _function.AdditionalProperties;

    public AIFunctionValueCaptureAction? ValueCapture { get; set; } = null;

    public McpClientExtendTool(AIFunction function)
    {
        _function = function;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var return_value = await _function.InvokeAsync(arguments, cancellationToken);
        if (ValueCapture != null)
        {
            ValueCapture(this, arguments, ToJsonElement(return_value));
        }
        return return_value;
    }

    private static JsonElement? ToJsonElement(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is JsonElement jsonElement)
        {
            return jsonElement;
        }

        try
        {
            return JsonSerializer.SerializeToElement(value, value.GetType(), CaptureJsonOptions);
        }
        catch
        {
            try
            {
                return JsonSerializer.SerializeToElement(value.ToString(), CaptureJsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    public static explicit operator McpClientExtendTool(McpClientTool mcpClientTool)
    {
        return new McpClientExtendTool(mcpClientTool);
    }

    public static explicit operator McpClientExtendTool(Services.AutoReconnectMcpClientTool autoReconnectTool)
    {
        return new McpClientExtendTool(autoReconnectTool);
    }

}
