using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Serilog;
using Zhengyan.Lunar;
using Zhengyan.Lunar.EightChar;

namespace Zhengyan.McpServer.EightChar.Services;

public class Divination
{
    public string Gua { get; set; }
    public string[] YaoCi { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });

    }
}

public class EightCharCalculationService : IEightCharCalculationService
{
    // private readonly ModelContextProtocol.Server.McpServer _thisServer;

    public EightCharCalculationService()
    {
        // _thisServer = thisServer;
    }

    public string Calculation(int year, int month, int day, int hour, int gender)
    {
        StringBuilder result = new StringBuilder();
        var solar = new Solar(year, month, day, hour, 0);
        result.AppendLine($"阳历：{solar}");
        result.AppendLine($"阳历全称：{solar.FullString}");
        var lunar = solar.Lunar;
        result.AppendLine($"阴历：{lunar}");
        result.AppendLine($"阴历全称：{lunar.FullString}");
        var baZi = lunar.EightChar;
        result.AppendLine($"八字：{baZi.Year} {baZi.Month} {baZi.Day} {baZi.Time}");
        result.AppendLine($"八字纳音：{baZi.YearNaYin} {baZi.MonthNaYin} {baZi.DayNaYin} {baZi.TimeNaYin}");
        result.AppendLine($"八字旬空：{baZi.YearXunKong} {baZi.MonthXunKong} {baZi.DayXunKong} {baZi.TimeXunKong}");
        result.AppendLine($"八字旬：{baZi.YearXun} {baZi.MonthXun} {baZi.DayXun} {baZi.TimeXun}");
        result.AppendLine($"八字五行：{baZi.YearWuXing} {baZi.MonthWuXing} {baZi.DayWuXing} {baZi.TimeWuXing}");
        result.AppendLine($"八字天干十神：{baZi.YearShiShenGan} {baZi.MonthShiShenGan} {baZi.DayShiShenGan} {baZi.TimeShiShenGan}");
        result.AppendLine($"八字地支十神：{baZi.YearShiShenZhi[0]} {baZi.MonthShiShenZhi[0]} {baZi.DayShiShenZhi[0]} {baZi.TimeShiShenZhi[0]}");
        result.Append($"八字年支十神：");
        foreach (var s in baZi.YearShiShenZhi)
        {
            result.Append($"{s} ");
        }
        result.AppendLine();
        result.Append($"八字月支十神：");
        foreach (var s in baZi.MonthShiShenZhi)
        {
            result.Append($"{s} ");
        }
        result.AppendLine();
        result.Append($"八字日支十神：");
        foreach (var s in baZi.DayShiShenZhi)
        {
            result.Append($"{s} ");
        }
        result.AppendLine();
        result.Append($"八字时支十神：");
        foreach (var s in baZi.TimeShiShenZhi)
        {
            result.Append($"{s} ");
        }
        result.AppendLine();

        // 八字胎元
        result.AppendLine($"八字胎元：{baZi.TaiYuan}");
        // 八字胎息
        result.AppendLine($"八字胎息：{baZi.TaiXi}");

        // 八字命宫
        result.AppendLine($"八字命宫：{baZi.MingGong}");

        // 八字身宫
        result.AppendLine($"八字身宫：{baZi.ShenGong}");

        result.AppendLine($"骨重：{solar.GetBoneWeight()}");
        result.AppendLine($"称骨歌：{solar.GetBoneWeight().GetComment(gender)}");
        var yun = baZi.GetYun(gender);
        Console.WriteLine($"出生{yun.StartYear}年{yun.StartMonth}个月{yun.StartDay}天后起运");
        Console.WriteLine($"阳历{yun.StartSolar.Ymd}后起运");
        return result.ToString();

    }


    public async Task<string> InterpretAsync(string eightChar, string question, ModelContextProtocol.Server.McpServer thisServer, CancellationToken cancellationToken)
    {
        try
        {
            ChatMessage[] messages =
            [
                new(ChatRole.System, $"你是一位中国的命理学大师，擅长对生辰八字以及测算结果进行分析。请根据测算出的生辰八字等信息，结合用户想问的问题给出合理的解决方案以及引导。"),
                new(ChatRole.User, $"用户的生辰八字详细信息如下：\n{eightChar}\n用户想问的问题：{question}"),
            ];

            ChatOptions options = new()
            {
                MaxOutputTokens = 4096,
                Temperature = 0.3f,
            };

            var ret = await thisServer.AsSamplingChatClient().GetResponseAsync(messages, options, cancellationToken);
            return ret.Text;
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}\n{ex.StackTrace}");
            return $"Error: {ex.Message}";
        }
    }
}
