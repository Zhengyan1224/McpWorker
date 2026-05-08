using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Zhengyan.ChatUI.Desktop.Models;

namespace Zhengyan.ChatUI.Desktop.Services;

public static class DesktopSettingsStore
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

        return Path.Combine(rootDirectory, "Zhengyan.ChatUI.Desktop", "settings.json");
    }

    public static DesktopAppSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new DesktopAppSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DesktopAppSettings>(json, JsonOptions) ?? new DesktopAppSettings();
    }

    public static string Save(DesktopAppSettings settings)
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
