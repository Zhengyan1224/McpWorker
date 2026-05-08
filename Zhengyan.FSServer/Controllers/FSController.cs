using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Zhengyan.FSServer.Services;

namespace Zhengyan.FSServer.Controllers
{
    /// <summary>
    /// Agent配置控制器
    /// </summary>
    // [Route("[controller]")]
    [ApiController]
    public class FSController : ControllerBase
    {
        private readonly ILogger<FSController> _logger;
        private readonly IFSService _fsService;

        public FSController(ILogger<FSController> logger, IFSService fsService)
        {
            _logger = logger;
            _fsService = fsService;
        }

        /// <summary>
        /// 上传文件接口，支持多文件上传，返回文件相对路径列表
        /// </summary>
        /// <param name="file">文件列表</param>
        /// <returns>文件相对路径列表</returns>
        [HttpPost("/fs/upload")]
        public async Task<IActionResult> UploadFile(List<IFormFile> file)
        {
            if (file == null || file.Count == 0)
            {
                return BadRequest("No file uploaded.");
            }
            List<string> returnPaths = new List<string>();
            foreach (var item in file)
            {
                if (item.Length > 0)
                {
                    var returnPath = await _fsService.UploadFileAsync(item, HttpContext.RequestAborted);
                    returnPaths.Add(returnPath);
                    _logger.LogInformation($"File {item.FileName} uploaded to {returnPath}");
                }
            }
            return Ok(returnPaths);
        }

    }
}
