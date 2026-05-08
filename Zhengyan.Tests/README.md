# Zhengyan.Tests

`Zhengyan.Tests` 是控制台式手工测试和实验项目，不是标准 xUnit/NUnit 测试工程。它引用搜索、向量库、历法、SSH、MCP 等项目，用来临时验证功能。

## 启动

```powershell
dotnet run --project Zhengyan.Tests\Zhengyan.Tests.csproj
```

运行前先查看 `Program.cs` 当前启用的是哪个测试入口。

## 已包含的测试方向

- Web 搜索和网页抓取。
- WebSearcher MCP 调用。
- SSH MCP 调用。
- 向量库和 HNSW。
- 本地数据存储。
- 正则表达式实验。
- 农历/八字相关实验。

## 注意事项

- 该项目面向开发者手工验证，不保证每个入口都适合自动化 CI。
- 某些测试需要外部网络、SSH 主机或本地配置。
- 涉及凭据的测试应使用本机环境配置，不要把真实凭据写入仓库。
