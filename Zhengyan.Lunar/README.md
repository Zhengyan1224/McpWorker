# Zhengyan.Lunar

`Zhengyan.Lunar` 是农历、历法和八字相关基础库。当前仓库中的 `Zhengyan.McpServer.EightChar` 使用它完成阳历转农历、八字、纳音、旬空等计算。

## 主要能力

- 阳历和农历互转。
- 八字相关计算。
- 节气、干支、生肖、星座等历法信息。
- 吉凶宜忌、方位、神煞等传统历法信息。

## 基础示例

```csharp
using Lunar;

var solar = new Solar(1986, 5, 29);
var lunar = solar.Lunar;

Console.WriteLine(lunar.FullString);
Console.WriteLine(solar.FullString);
```

## 在本仓库中的用途

- `Zhengyan.McpServer.EightChar`：根据用户输入的阳历年月日时和性别计算八字信息。
- `Zhengyan.Tests`：包含部分历法和八字相关手工测试入口。

这个项目是库项目，不单独启动服务。
