using Zhengyan.McpHost.Config;
using Zhengyan.McpHost.Services;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Text.Unicode;

namespace Zhengyan.McpHost.Controllers
{
    /// <summary>
    /// McpClient配置控制器
    /// </summary>
    // [Route("[controller]")]
    [ApiController]
    public class McpClientConfigController : ControllerBase
    {
        private readonly ILogger<McpClientConfigController> _logger;

        private static JsonSerializerOptions defaultJsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        public McpClientConfigController(ILogger<McpClientConfigController> logger)
        {
            _logger = logger;
        }

        #region McpClient
        /// <summary>
        /// 添加McpClient配置
        /// </summary>
        /// <param name="config">McpClient配置</param>
        [HttpPost("/config/mcpclient/add")]
        public async Task<IActionResult> AddMcpClientAsync([FromServices] GlobalObjectPoolService service, [FromBody] McpClientConfig config)
        {
            if (config == null)
            {
                return BadRequest("Invalid config");
            }

            return await service.AddMcpClientConfigAsync(config, true) ?
                Ok() :
                BadRequest("Failed to add McpClient config");
        }

        /// <summary>
        /// 更新McpClient配置
        /// </summary>
        /// <param name="config">McpClient配置</param>
        [HttpPut("/config/mcpclient/update")]
        public async Task<IActionResult> UpdateMcpClientAsync([FromServices] GlobalObjectPoolService service, [FromBody] McpClientConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.ID))
            {
                return BadRequest("Invalid config");
            }

            return await service.UpdateMcpClientConfigAsync(config, true)
                ? Ok()
                : BadRequest("Failed to update McpClient config");
        }

        /// <summary>
        /// 删除McpClient配置
        /// </summary>
        /// <param name="id">McpClient ID</param>
        [HttpDelete("/config/mcpclient/delete")]
        public async Task<IActionResult> DeleteMcpClientAsync([FromServices] GlobalObjectPoolService service, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid McpClient ID");
            }

            return await service.DeleteMcpClientConfigAsync(id, true) ?
                Ok() :
                BadRequest("Failed to delete McpClient config");
        }

        /// <summary>
        /// 获取McpClient配置列表
        /// </summary>
        [HttpGet("/config/mcpclient/list")]
        public IActionResult ListMcpClient([FromServices] GlobalObjectPoolService service)
        {
            var mcpClientConfigs = service.McpClientConfigs.Values.ToList();
            return Ok(mcpClientConfigs);
        }

        /// <summary>
        /// 获取McpClient的工具列表
        /// </summary>
        /// <param name="id">McpClient ID</param>
        [HttpGet("/config/mcpclient/tools")]
        public async Task<IActionResult> McpClientToolsAsync([FromServices] GlobalObjectPoolService service, string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid McpClient ID");
            }

            service.McpClients.TryGetValue(id, out var mcpClient);
            if (mcpClient == null)
            {
                return NotFound("McpClient not found");
            }

            var requestOptions = new RequestOptions
            {
                JsonSerializerOptions = defaultJsonOptions
            };
            return Ok(await mcpClient.ListToolsAsync(requestOptions, cancellationToken));
        }

        /// <summary>
        /// 调用McpClient的工具
        /// </summary>
        /// <param name="id">McpClient ID</param>
        /// <param name="toolName">工具名称</param>
        /// <param name="arguments">工具参数</param>
        [HttpPost("/config/mcpclient/calltool")]
        public async Task<IActionResult> McpClientCallToolAsync([FromServices] GlobalObjectPoolService service, string id, string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid McpClient ID");
            }

            service.McpClients.TryGetValue(id, out var mcpClient);
            if (mcpClient == null)
            {
                return NotFound("McpClient not found");
            }
            if (string.IsNullOrEmpty(toolName))
            {
                return BadRequest("Invalid tool name");
            }
            var requestOptions = new RequestOptions
            {
                JsonSerializerOptions = defaultJsonOptions
            };
            return Ok(await mcpClient.CallToolAsync(toolName, arguments, requestOptions, cancellationToken));
        }

        /// <summary>
        /// 重启McpClient
        /// </summary>
        /// <param name="id">McpClient ID</param>
        [HttpPost("/config/mcpclient/restart")]
        public async Task<IActionResult> RestartMcpClientAsync([FromServices] GlobalObjectPoolService service, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid McpClient ID");
            }

            return await service.RestartMcpClientAsync(id) ?
                Ok() :
                BadRequest("Failed to restart McpClient");
        }

        /// <summary>
        /// 停止McpClient
        /// </summary>
        /// <param name="id">McpClient ID</param>
        [HttpPost("/config/mcpclient/stop")]
        public async Task<IActionResult> StopMcpClientAsync([FromServices] GlobalObjectPoolService service, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid McpClient ID");
            }

            return await service.StopMcpClientAsync(id) ?
                Ok() :
                BadRequest("Failed to stop McpClient");
        }


        #endregion
    }
}
