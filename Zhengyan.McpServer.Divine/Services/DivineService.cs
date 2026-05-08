using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Serilog;
using Zhengyan.McpServer.Divine.Config;

namespace Zhengyan.McpServer.Divine.Services;

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

public class DivineService : IDivineService
{
    // private readonly ModelContextProtocol.Server.McpServer _thisServer;
    private readonly DivineConfig _divineConfig;

    private Dictionary<int, Dictionary<int, Divination>> _divinations;

    public DivineService(DivineConfig divineConfig)
    {
        try
        {
            // _thisServer = thisServer;
            _divineConfig = divineConfig;
            LoadDivinations();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load divinations");
            throw;
        }
    }

    private void LoadDivinations()
    {
        _divinations = new Dictionary<int, Dictionary<int, Divination>>();
        var lines = File.ReadAllLines(_divineConfig.ExplanationsFilePath);
        foreach (var line in lines)
        {
            var _line = line.Trim();
            if (string.IsNullOrWhiteSpace(_line))
                continue;
            var cols = _line.Split(',');
            int up = int.Parse(cols[2]);
            int down = int.Parse(cols[3]);

            if (!_divinations.TryGetValue(up, out var downDict))
            {
                downDict = new Dictionary<int, Divination>();
                _divinations[up] = downDict;
            }

            Divination divination = new Divination
            {
                Gua = cols[1],
                YaoCi = cols[4].Split('|')
            };
            downDict.TryAdd(down, divination);
        }

        Log.Debug("Divinations loaded successfully");
        Log.Debug($"Divinations: {JsonSerializer.Serialize(_divinations, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) })}");
    }

    private Divination GetDivination(int up, int down)
    {
        if (_divinations.TryGetValue(up, out var downDict))
        {
            if (downDict.TryGetValue(down, out var divination))
            {
                return divination;
            }
        }
        throw new Exception($"No divination found for up: {up}, down: {down}");
    }

    private (string, string) DivineBy3Numbers(int num1, int num2, int num3)
    {
        int down = (8 - (num1 % 8)) % 8;
        int up = (8 - (num2 % 8)) % 8;

        int yao = (num3 - 1) % 6;
        var divination = GetDivination(up, down);
        return (divination.Gua, divination.YaoCi[yao]);
    }

    public string Divine(int num1, int num2, int num3)
    {
        var (gua, yaoCi) = DivineBy3Numbers(num1, num2, num3);

        return $"卦象：{gua}\n爻辞：{yaoCi}";
    }

    public async Task<string> InterpretAsync(string divination, string question, ModelContextProtocol.Server.McpServer thisServer, CancellationToken cancellationToken)
    {
        try
        {
            ChatMessage[] messages =
            [
                new(ChatRole.System, $"你是一个易经大师，擅长对易经的卦象和爻辞进行解签，请根据占卜得到的卦象和爻辞以及用户想要问的问题来给出具体的解释，并给出合理的解决方案以及引导。"),
                new(ChatRole.User, $"占卜得到的卦象和爻辞：\n{divination}\n用户想问的问题：{question}"),
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
