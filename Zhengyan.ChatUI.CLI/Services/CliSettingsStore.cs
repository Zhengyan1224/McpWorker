using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Zhengyan.ChatUI.CLI.Models;

namespace Zhengyan.ChatUI.CLI.Services;

public static class CliSettingsStore
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

        return Path.Combine(rootDirectory, "Zhengyan.ChatUI.CLI", "settings.json");
    }

    public static CliAppSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new CliAppSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CliAppSettings>(json, JsonOptions) ?? new CliAppSettings();
    }

    public static string Save(CliAppSettings settings)
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
