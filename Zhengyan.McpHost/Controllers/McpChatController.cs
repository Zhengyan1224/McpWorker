using Zhengyan.OpenAIModels;
using Zhengyan.McpHost.Services;
using Microsoft.AspNetCore.Mvc;
using Zhengyan.McpHost.Commons;

namespace Zhengyan.McpHost.Controllers
{
    /// <summary>
    /// 对话完成控制器
    /// </summary>
    [ApiController]
    // [Route("[controller]")]
    [Produces("application/json")]
    public class McpChatController : ControllerBase
    {

        private readonly ILogger<McpChatController> _logger;

        /// <summary>
        /// 对话完成控制器
        /// </summary>
        /// <param name="logger">日志</param>
        public McpChatController(ILogger<McpChatController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 对话完成请求
        /// </summary>
        /// <param name="request"></param>
        /// <param name="service"></param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// 默认不开启流式，需要主动设置 stream:true
        /// </remarks>
        /// <response code="200">模型对话结果</response>
        /// <response code="400">错误信息</response>
        [HttpPost("/v1/chat/completions")]
        // [HttpPost("/chat/completions")]
        // [HttpPost("/openai/deployments/{model}/chat/completions")]
        [Produces("text/event-stream")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChatCompletionResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        public async Task<IResult> CreateChatCompletionAsync([FromBody] ChatCompletionRequest request, [FromServices] IMcpChatModelService service, CancellationToken cancellationToken)
        {
            try
            {
                if (request.stream)
                {

                    string first = " ";
                    await foreach (var item in service.CreateChatCompletionStreamAsync(Utils.GetApiKey(HttpContext), request, cancellationToken))
                    {
                        if (first == " ")
                        {
                            first = item;
                        }
                        else
                        {
                            if (first.Length > 1)
                            {
                                Response.Headers.ContentType = "text/event-stream";
                                Response.Headers.CacheControl = "no-cache";
                                await Response.Body.FlushAsync();
                                await Response.WriteAsync(first);
                                await Response.Body.FlushAsync();
                                first = "";
                            }
                            await Response.WriteAsync(item);
                            await Response.Body.FlushAsync();
                        }
                    }
                    return Results.Empty;
                }
                else
                {
                    return Results.Ok(await service.CreateChatCompletionAsync(Utils.GetApiKey(HttpContext),request, cancellationToken));
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateChatCompletionAsync");
                return Results.Problem($"{ex.Message}");
            }

        }

        /// <summary>
        /// Responses API 请求
        /// </summary>
        [HttpPost("/v1/responses")]
        [Produces("text/event-stream")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResponseResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        public async Task<IResult> CreateResponseAsync([FromBody] ResponseRequest request, [FromServices] IMcpChatModelService service, CancellationToken cancellationToken)
        {
            try
            {
                if (request.stream)
                {
                    string first = " ";
                    await foreach (var item in service.CreateResponseStreamAsync(Utils.GetApiKey(HttpContext), request, cancellationToken))
                    {
                        if (first == " ")
                        {
                            first = item;
                        }
                        else
                        {
                            if (first.Length > 1)
                            {
                                Response.Headers.ContentType = "text/event-stream";
                                Response.Headers.CacheControl = "no-cache";
                                await Response.Body.FlushAsync();
                                await Response.WriteAsync(first);
                                await Response.Body.FlushAsync();
                                first = "";
                            }

                            await Response.WriteAsync(item);
                            await Response.Body.FlushAsync();
                        }
                    }

                    return Results.Empty;
                }

                return Results.Ok(await service.CreateResponseAsync(Utils.GetApiKey(HttpContext), request, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateResponseAsync");
                return Results.Problem($"{ex.Message}");
            }
        }
    }
}
