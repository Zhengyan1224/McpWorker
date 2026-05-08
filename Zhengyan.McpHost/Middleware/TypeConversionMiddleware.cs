using System.Text;
using System.Text.Json;
using Serilog;

namespace Zhengyan.McpHost.Middleware
{
    /// <summary>
    /// 类型转换中间件
    /// </summary>
    public class TypeConversionMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// 处理 Completions 请求中的 stop 字段
        /// </summary>
        /// <param name="next"></param>
        public TypeConversionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// InvokeAsync
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Method == "POST")
            {
                if (context.Request.Path.ToString().Contains("completions") || context.Request.Path.ToString().Contains("embeddings"))
                {
                    context.Request.EnableBuffering();

                    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                    context.Request.Body.Position = 0;
                    try
                    {
                        // Json 可能出现异常
                        using (JsonDocument doc = JsonDocument.Parse(body))
                        {
                            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);

                            if (data != null)
                            {
                                if (data.TryGetValue("stop", out var stop) && stop.ValueKind == JsonValueKind.String)
                                {
                                    data["stop"] = JsonSerializer.Deserialize<JsonElement>(
                                        JsonSerializer.Serialize(new string[] { stop.ToString() })
                                    );
                                }

                                if (data.TryGetValue("input", out var input) && input.ValueKind == JsonValueKind.String)
                                {
                                    data["input"] = JsonSerializer.Deserialize<JsonElement>(
                                        JsonSerializer.Serialize(new string[] { input.ToString() })
                                    );
                                }

                                // 适配 max_tokens
                                if (data.TryGetValue("max_tokens", out var max_tokens) && max_tokens.ValueKind == JsonValueKind.Number)
                                {
                                    data["max_completion_tokens"] = JsonSerializer.Deserialize<JsonElement>(
                                        JsonSerializer.Serialize(max_tokens.GetInt32())
                                    );
                                }

                                var newBody = JsonSerializer.Serialize(data);
                                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(newBody));
                            }
                        }
                    }
                    catch
                    {
                        // 不处理异常，交给验证处理
                    }
                }
            }

            try
            {
                // 处理请求逻辑
                await _next(context);
            }
            catch (Exception ex)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Custom error message");
                }
                else
                {
                    // 响应已开始，记录日志但不修改响应
                    Log.Error(ex, "无法修改已启动的响应");
                }
            }
        }
    }
}
