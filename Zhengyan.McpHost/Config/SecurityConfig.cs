using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpHost.Config
{
    public class SecurityConfig
    {
        /// <summary>
        /// API密钥
        /// </summary>
        public Dictionary<string,string> ApiKeyExpirations { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            });
        }
    }
}