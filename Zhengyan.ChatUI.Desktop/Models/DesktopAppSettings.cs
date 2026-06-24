namespace Zhengyan.ChatUI.Desktop.Models;

public sealed class DesktopAppSettings
{
    public string ServerEndpoint { get; set; } = "http://localhost:9083/mcphost/api/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string SelectedModel { get; set; } = string.Empty;

    public string MaxCompletionTokens { get; set; } = "4096";

    public string Temperature { get; set; } = "0.9";

    public string TopP { get; set; } = "0.9";

    public bool UseResponsesApi { get; set; }
}
