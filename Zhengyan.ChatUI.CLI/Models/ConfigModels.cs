namespace Zhengyan.ChatUI.CLI.Models;

public sealed class ConfigModels
{
    public int Current { get; set; }

    public List<ConfigModel> Models { get; set; } = [];
}

public sealed class ConfigModel
{
    public string Name { get; set; } = string.Empty;
}
