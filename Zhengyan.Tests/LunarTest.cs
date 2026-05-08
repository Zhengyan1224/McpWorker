using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Zhengyan.Lunar;
using Zhengyan.Lunar.EightChar;
using Zhengyan.Lunar.Util;
using Zhengyan.WebSearch;

public class LunarTest
{
    public static async Task Run(string[] args)
    {
        // 阳历
        var solar = new Solar(2021, 9, 7, 9, 0);
        Console.WriteLine(solar);
        Console.WriteLine(solar.FullString);

        // 阴历
        var lunar = solar.Lunar;
        Console.WriteLine(lunar);
        Console.WriteLine(lunar.FullString);

        // 八字
        var baZi = lunar.EightChar;
        Console.WriteLine($"八字：{baZi.Year} {baZi.Month} {baZi.Day} {baZi.Time}");

        // 八字纳音
        Console.WriteLine($"八字纳音：{baZi.YearNaYin} {baZi.MonthNaYin} {baZi.DayNaYin} {baZi.TimeNaYin}");

        // 八字旬空
        Console.WriteLine($"八字旬空：{baZi.YearXunKong} {baZi.MonthXunKong} {baZi.DayXunKong} {baZi.TimeXunKong}");

        // 八字旬
        Console.WriteLine($"八字旬：{baZi.YearXun} {baZi.MonthXun} {baZi.DayXun} {baZi.TimeXun}");

        // 八字五行
        Console.WriteLine($"八字五行：{baZi.YearWuXing} {baZi.MonthWuXing} {baZi.DayWuXing} {baZi.TimeWuXing}");

        // 八字天干十神
        Console.WriteLine($"八字天干十神：{baZi.YearShiShenGan} {baZi.MonthShiShenGan} {baZi.DayShiShenGan} {baZi.TimeShiShenGan}");

        // 八字地支十神
        Console.WriteLine($"八字地支十神：{baZi.YearShiShenZhi[0]} {baZi.MonthShiShenZhi[0]} {baZi.DayShiShenZhi[0]} {baZi.TimeShiShenZhi[0]}");

        // 八字年支十神
        Console.WriteLine($"八字年支十神：");
        foreach (var s in baZi.YearShiShenZhi)
        {
            Console.Write($"{s} ");
        }
        Console.WriteLine();

        // 八字月支十神
        Console.WriteLine($"八字月支十神：");
        foreach (var s in baZi.MonthShiShenZhi)
        {
            Console.Write($"{s} ");
        }
        Console.WriteLine();

        // 八字日支十神
        Console.WriteLine($"八字日支十神：");
        foreach (var s in baZi.DayShiShenZhi)
        {
            Console.Write($"{s} ");
        }
        Console.WriteLine();

        // 八字时支十神
        Console.WriteLine($"八字时支十神：");
        foreach (var s in baZi.TimeShiShenZhi)
        {
            Console.Write($"{s} ");
        }
        Console.WriteLine();

        // 八字胎元
        Console.WriteLine($"八字胎元：{baZi.TaiYuan}");
        // 八字胎息
        Console.WriteLine($"八字胎息：{baZi.TaiXi}");

        // 八字命宫
        Console.WriteLine($"八字命宫：{baZi.MingGong}");

        // 八字身宫
        Console.WriteLine($"八字身宫：{baZi.ShenGong}");

        Console.WriteLine($"骨重：{solar.GetBoneWeight()}");
        Console.WriteLine($"称骨歌：{solar.GetBoneWeight().GetComment(0)}");

        Console.WriteLine();
        solar = new Solar(1999, 2, 2, 9);
        lunar = solar.Lunar;
        baZi = lunar.EightChar;

        // 男运
        var yun = baZi.GetYun(1);
        Console.WriteLine($"阳历{solar.YmdHms}出生");
        Console.WriteLine($"出生{yun.StartYear}年{yun.StartMonth}个月{yun.StartDay}天后起运");
        Console.WriteLine($"阳历{yun.StartSolar.Ymd}后起运");
        Console.WriteLine($"骨重：{solar.GetBoneWeight()}");
        Console.WriteLine($"称骨歌：{solar.GetBoneWeight().GetComment(1)}");
        Console.WriteLine();

        // 节假日
        var holidays = HolidayUtil.GetHolidays(2025);
        foreach (var holiday in holidays)
        {
            Console.WriteLine(holiday);
        }
        Console.WriteLine();

        // 八字转阳历
        var solarList = Solar.FromBaZi(baZi.Year, baZi.Month, baZi.Day, baZi.Time);
        foreach (var d in solarList)
        {
            Console.WriteLine(d.FullString);
        }
        Console.WriteLine();

    }
}