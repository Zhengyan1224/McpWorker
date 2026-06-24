namespace Zhengyan.ChatUI.CLI.Models;

public sealed class CliAppSettings
{
    public string ServerEndpoint { get; set; } = "http://localhost:9083/mcphost/api/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int MaxTokens { get; set; } = 4096;

    public float Temperature { get; set; } = 0.9f;

    public float TopP { get; set; } = 0.9f;

    public bool UseResponsesApi { get; set; }
}
