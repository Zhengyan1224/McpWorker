using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Zhengyan.ChatUI.TUI.Models;

namespace Zhengyan.ChatUI.TUI.Services;

public static class TuiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string GetSettingsPath()
    {
        var rootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            rootDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(rootDirectory, "Zhengyan.ChatUI.TUI", "settings.json");
    }

    public static TuiAppSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new TuiAppSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TuiAppSettings>(json, JsonOptions) ?? new TuiAppSettings();
    }

    public static string Save(TuiAppSettings settings)
    {
        var path = GetSettingsPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
        return path;
    }
}
