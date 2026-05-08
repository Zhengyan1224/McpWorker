using Zhengyan.McpHost.Config;
using Zhengyan.McpHost.Services;
using Microsoft.AspNetCore.Mvc;

namespace Zhengyan.McpHost.Controllers
{
    public record ConfigModels
    {
        /// <summary>
        /// 当前使用的模型
        /// </summary>
        public int Current { get; set; }
        /// <summary>
        /// 模型列表
        /// </summary>
        public List<ConfigModel> Models { get; set; }

    }

    public record ConfigModel
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public string Name { get; set; }
    }


    /// <summary>
    /// 模型配置控制器
    /// </summary>
    // [Route("[controller]")]
    [ApiController]
    public class McpModelController : ControllerBase
    {
        private readonly ILogger<McpModelController> _logger;


        /// <summary>
        /// 返回已配置的模型信息
        /// </summary>
        [HttpGet("/v1/models/config")]
        public ConfigModels GetConfigModels([FromServices] GlobalObjectPoolService service)
        {
            return new ConfigModels
            {
                Models = service.AgentConfigs.Select(x => new ConfigModel
                {
                    Name = x.Key
                }).ToList(),
                Current = GlobalSettings.CurrentModelIndex
            };
        }

        /// <summary>
        /// 切换到指定模型
        /// </summary>
        /// <param name="id">模型ID</param>
        [HttpPut("/v1/models/switch")]
        public IActionResult SwitchModel(int id,[FromServices] GlobalObjectPoolService service)
        {
            if (id < 0 || id >= service.AgentConfigs.Count)
            {
                return BadRequest("Invalid model id");
            }

            GlobalSettings.CurrentModelIndex = id;
            return Ok();
        }
    }
}
