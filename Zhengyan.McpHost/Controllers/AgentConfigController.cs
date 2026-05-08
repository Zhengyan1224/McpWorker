using Zhengyan.McpHost.Config;
using Zhengyan.McpHost.Services;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Client;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Text.Unicode;

namespace Zhengyan.McpHost.Controllers
{
    /// <summary>
    /// Agent配置控制器
    /// </summary>
    // [Route("[controller]")]
    [ApiController]
    public class AgentConfigController : ControllerBase
    {
        private readonly ILogger<AgentConfigController> _logger;
        

        private static JsonSerializerOptions defaultJsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        public AgentConfigController(ILogger<AgentConfigController> logger)
        {
            _logger = logger;
        }

        #region Agent
        /// <summary>
        /// 添加Agent配置
        /// </summary>
        [HttpPost("/config/agent/add")]
        public IActionResult AddAgent([FromServices] GlobalObjectPoolService service, [FromBody] AgentConfig config)
        {
            if (config == null)
            {
                return BadRequest("Invalid config");
            }

            return service.AddAgentConfig(config, true) ?
                Ok() :
                BadRequest("Failed to add Agent config");
        }

        /// <summary>
        /// 更新Agent配置
        /// </summary>
        [HttpPut("/config/agent/update")]
        public IActionResult UpdateAgent([FromServices] GlobalObjectPoolService service, [FromBody] AgentConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.ID))
            {
                return BadRequest("Invalid config");
            }

            return service.UpdateAgentConfig(config, true)
                ? Ok()
                : BadRequest("Failed to update Agent config");
        }

        /// <summary>
        /// 删除Agent配置
        /// </summary>
        /// <param name="id">Agent ID</param>
        [HttpDelete("/config/agent/delete")]
        public IActionResult DeleteAgent([FromServices] GlobalObjectPoolService service, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid Agent ID");
            }

            return service.DeleteAgentConfig(id, true) ?
                Ok() :
                BadRequest("Failed to delete Agent config");
        }

        /// <summary>
        /// 获取Agent配置列表
        /// </summary>
        [HttpGet("/config/agent/list")]
        public IActionResult ListAgent([FromServices] GlobalObjectPoolService service)
        {
            var agentConfigs = service.AgentConfigs.Values.ToList();
            return Ok(agentConfigs);
        }
        #endregion
    }
}
