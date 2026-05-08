using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpHost.Config;

/// <summary>
/// 存储配置
/// </summary>
public class StorageConfig
{

    /// <summary>
    /// 存储目录路径
    /// </summary>
    public string StorageFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// 存储文件扩展名
    /// </summary>
    public string FileExtension { get; set; } = ".json";

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });
    }
}