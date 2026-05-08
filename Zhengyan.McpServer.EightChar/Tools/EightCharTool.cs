using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Zhengyan.McpServer.EightChar.Services;
namespace Zhengyan.McpServer.EightChar.Tools;

[McpServerToolType]
public static class EightCharTool
{

    [McpServerTool(Name = "calculation"), Description("根据用户输入的阳历年月日时以及性别，计算出对应的阴历、八字、纳音、旬空等信息，并返回结果。")]
    public static async Task<string> Calculation(IEightCharCalculationService service, [Description("阳历的年份")] string year, [Description("阳历的月份")] string month, [Description("阳历的日期")] string day, [Description("阳历的时辰（0到23，24小时制）")] string hour,[Description("性别（男：1，女：0）")]string gender)
    {
        try
        {
            Log.Debug($"Year: {year}\tMonth: {month}\tDay: {day}\tHour: {hour}\tGender: {gender}");
            var ret = service.Calculation(int.Parse(year), int.Parse(month), int.Parse(day), int.Parse(hour),int.Parse(gender));
            Log.Debug($"Calculation result: {ret}");
            return ret;
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}\n{ex.StackTrace}");
            return $"Failed to calculate, error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "interpret"), Description("这是一个解命理工具，能够根据测算出来的八字详细内容以及用户的问题，进行解读和分析。")]
    public static async Task<string> InterpretAsync(IEightCharCalculationService service, ModelContextProtocol.Server.McpServer thisServer,CancellationToken cancellationToken, [Description("测算出的八字的详细的文字内容")] string eightChar, [Description("用户想问的问题")] string question)
    {
        try
        {
            Log.Debug($"EightChar: {eightChar}");
            Log.Debug($"Question: {question}");
            var ret = await service.InterpretAsync(eightChar, question, thisServer,cancellationToken);
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
