using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpServer.Divine.Config;
public class DivineConfig
{
    public string? ExplanationsFilePath { get; set; }
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });

    }
}