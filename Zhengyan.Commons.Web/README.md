# Zhengyan.Commons.Web

`Zhengyan.Commons.Web` 是 Web 项目通用扩展库，为 `McpHost`、`KBServer`、`FSServer` 等 ASP.NET Core 服务提供统一配置能力。

## 目标框架

```text
net8.0
net9.0
```

## 主要能力

- `AddProfiles("profiles")` 约定式加载 profiles 目录配置。
- `AddSwaggerGen` / `ConfigureSwagger` 配置 Swagger。
- `AddCentralRoutePrefix` 为控制器统一加路由前缀。
- `UseStaticFiles(configuration)` 按配置启用静态文件和 MIME 映射。
- `UseSerilog` / `ConfigureSerilog` 按配置启用日志。
- CORS 和基础中间件支持。

## 典型使用

```csharp
builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
    .AddProfiles("profiles");

builder.Host.UseSerilog(builder.Configuration);

builder.Services.AddSwaggerGen(builder.Configuration)
    .AddCentralRoutePrefix(builder.Configuration);

app.ConfigureSwagger(builder.Configuration);
app.UseStaticFiles(builder.Configuration);
```

这个项目不单独运行，由 Web 服务项目引用。
