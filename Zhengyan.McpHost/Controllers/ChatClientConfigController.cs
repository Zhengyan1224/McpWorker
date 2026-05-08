using Zhengyan.McpHost.Config;
using Zhengyan.McpHost.Services;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Client;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Text.Unicode;
using Zhengyan.OpenAIModels;

namespace Zhengyan.McpHost.Controllers
{
    /// <summary>
    /// ChatClient配置控制器
    /// </summary>
    // [Route("[controller]")]
    [ApiController]
    public class ChatClientConfigController : ControllerBase
    {
        private readonly ILogger<ChatClientConfigController> _logger;

        private static JsonSerializerOptions defaultJsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        public ChatClientConfigController(ILogger<ChatClientConfigController> logger)
        {
            _logger = logger;
        }
        #region ChatClient
        /// <summary>
        /// 添加ChatClient配置
        /// </summary>
        /// <param name="config">ChatClient配置</param>
        [HttpPost("/config/chatclient/add")]
        public IActionResult AddChatClient([FromServices] GlobalObjectPoolService service, [FromBody] ChatClientConfig config)
        {
            if (config == null)
            {
                return BadRequest("Invalid config");
            }

            return service.AddChatClientConfig(config, true) ?
                Ok() :
                BadRequest("Failed to add ChatClient config");
        }

        /// <summary>
        /// 更新ChatClient配置
        /// </summary>
        /// <param name="config">ChatClient配置</param>
        [HttpPut("/config/chatclient/update")]
        public IActionResult UpdateChatClient([FromServices] GlobalObjectPoolService service, [FromBody] ChatClientConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.ID))
            {
                return BadRequest("Invalid config");
            }

            return service.UpdateChatClientConfig(config, true)
                ? Ok()
                : BadRequest("Failed to update ChatClient config");
        }

        /// <summary>
        /// 删除ChatClient配置
        /// </summary>
        /// <param name="id">ChatClient ID</param>
        [HttpDelete("/config/chatclient/delete")]
        public IActionResult DeleteChatClient([FromServices] GlobalObjectPoolService service, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid ChatClient ID");
            }

            return service.DeleteChatClientConfig(id, true) ?
                Ok() :
                BadRequest("Failed to delete ChatClient config");
        }

        /// <summary>
        /// 获取ChatClient配置列表
        /// </summary>
        [HttpGet("/config/chatclient/list")]
        public IActionResult ListChatClient([FromServices] GlobalObjectPoolService service)
        {
            var chatClientConfigs = service.ChatClientConfigs.Values.ToList();
            return Ok(chatClientConfigs);
        }

        /// <summary>
        /// 使用指定ChatClient进行对话测试
        /// </summary>
        [HttpPost("/config/chatclient/chat/completions")]
        public async Task<IActionResult> CreateChatCompletionAsync(
            [FromServices] GlobalObjectPoolService service,
            [FromServices] IMcpChatModelService chatModelService,
            [FromBody] ChatCompletionRequest request,
            string id,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Invalid ChatClient ID");
            }

            if (!service.ChatClientConfigs.ContainsKey(id))
            {
                return NotFound("ChatClient not found");
            }

            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            return Ok(await chatModelService.CreateChatClientCompletionAsync(id, request, cancellationToken));
        }

        /// <summary>
        /// 使用指定 ChatClient 进行 Responses API 测试
        /// </summary>
        [HttpPost("/config/chatclient/responses")]
        public async Task<IActionResult> CreateResponseAsync(
            [FromServices] GlobalObjectPoolService service,
            [FromServices] IMcpChatModelService chatModelService,
            [FromBody] ResponseRequest request,
            string id,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Invalid ChatClient ID");
            }

            if (!service.ChatClientConfigs.ContainsKey(id))
            {
                return NotFound("ChatClient not found");
            }

            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            if (request.stream)
            {
                return BadRequest("Streaming is not supported on /config/chatclient/responses. Use /v1/responses instead.");
            }

            var rawResponse = await chatModelService.CreateChatClientResponseAsync(id, request, cancellationToken);
            return Content(rawResponse, "application/json");
        }
        #endregion
    }
}
