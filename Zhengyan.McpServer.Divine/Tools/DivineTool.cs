using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Zhengyan.McpServer.Divine.Services;
namespace Zhengyan.McpServer.Divine.Tools;

[McpServerToolType]
public static class DivineTool
{

    [McpServerTool(Name = "divine_by_3_numbers"), Description("这是一个数字卦占卜工具，需要提供的三个大于100的整数，这个工具会根据这三个数进行卜卦，并返回卦象和爻辞。")]
    public static async Task<string> DivineBy3Numbers(IDivineService divineService, [Description("第一个数（必须大于100）")] string num1, [Description("第二个数（必须大于100）")] string num2, [Description("第三个数（必须大于100）")] string num3)
    {
        try
        {
            Log.Debug($"Num1: {num1}\tNum2: {num2}\tNum3: {num3}");
            var ret = divineService.Divine(int.Parse(num1), int.Parse(num2), int.Parse(num3));
            Log.Debug($"Divine result: {ret}");
            return ret;
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}\n{ex.StackTrace}");
            return $"Failed to divine, error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "interpret"), Description("这是一个解签工具，能够根据占卜得到的卦象和爻辞以及用户想问的问题进行解签，并返回解签的内容。")]
    public static async Task<string> InterpretAsync(IDivineService divineService, ModelContextProtocol.Server.McpServer thisServer, CancellationToken cancellationToken, [Description("占卜得到的卦象和爻辞内容")] string divination, [Description("用户想问的问题")] string question)
    {
        try
        {
            Log.Debug($"Divination: {divination}");
            Log.Debug($"Question: {question}");
            var ret = await divineService.InterpretAsync(divination, question, thisServer, cancellationToken);
            Log.Debug($"Interpret result: {ret}");
            return ret;
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}\n{ex.StackTrace}");
            return $"Failed to interpret, error: {ex.Message}";
        }
    }
}
