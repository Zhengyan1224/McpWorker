using System.Collections.Generic;

namespace Zhengyan.ChatUI.Desktop.Models;

public class ConfigModels
{
    public int Current { get; set; }
    public List<ConfigModel> Models { get; set; } = new();
}

public class ConfigModel
{
    public string Name { get; set; } = string.Empty;
}
