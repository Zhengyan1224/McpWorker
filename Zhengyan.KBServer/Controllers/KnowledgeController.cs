using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Zhengyan.KBServer.Models;

namespace Zhengyan.KBServer.Controllers
{
    /// <summary>
    /// Agent配置控制器
    /// </summary>
    // [Route("[controller]")]
    [ApiController]
    public class KnowledgeController : ControllerBase
    {
        private readonly ILogger<KnowledgeController> _logger;


        public KnowledgeController(ILogger<KnowledgeController> logger)
        {
            _logger = logger;
        }

        #region Knowledge

        /// <summary>
        /// 添加知识
        /// </summary>
        /// <param name="service"></param>
        /// <param name="dbName">知识库名</param>
        /// <param name="knowledgeContents">知识列表</param>
        /// <param name="chunkSize">切片大小（0为不切片）</param>
        /// <returns>添加成功数</returns>
        [HttpPost("/knowledge/add")]
        public async Task<IActionResult> AddKnowledge([FromServices] IKnowledgeBaseService service, string dbName, [FromBody] KnowledgeContent[] knowledgeContents, int chunkSize = 0)
        {
            if (string.IsNullOrEmpty(dbName) || knowledgeContents == null || knowledgeContents.Length == 0)
            {
                return BadRequest("Invalid parameters");
            }

            var result = await service.AddKnowledgeAsync(dbName, knowledgeContents, chunkSize);
            return result > 0 ? Ok(result) : BadRequest("Failed to add knowledge");
        }

        /// <summary>
        /// 查询知识
        /// </summary>
        /// <param name="service"></param>
        /// <param name="dbName">知识库名</param>
        /// <param name="query">查询内容</param>
        /// <param name="topK">返回前K条</param>
        /// <returns>查询结果</returns>
        [HttpGet("/knowledge/search")]
        public async Task<IActionResult> SearchKnowledge([FromServices] IKnowledgeBaseService service, string dbName, string query, int topK = 5)
        {
            if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(query))
            {
                return BadRequest("Invalid parameters");
            }

            var result = await service.SearchKnowledgeAsync(dbName, query, topK);
            return result == null ? BadRequest("Failed to search knowledge") : Ok(result);
        }

        /// <summary>
        /// 删除知识
        /// </summary>
        /// <param name="service"></param>
        /// <param name="dbName">知识库名</param>
        /// <param name="id">知识ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("/knowledge/delete")]
        public async Task<IActionResult> DeleteKnowledge([FromServices] IKnowledgeBaseService service, string dbName, int id)
        {
            if (string.IsNullOrEmpty(dbName))
            {
                return BadRequest("Invalid parameters");
            }

            var result = await service.DeleteKnowledgeAsync(dbName, id);
            return result ? Ok("Knowledge deleted successfully") : BadRequest("Failed to delete knowledge");
        }

        /// <summary>
        /// 删除知识库
        /// </summary>
        /// <param name="service"></param>
        /// <param name="dbName">知识库名</param>
        /// <returns>删除结果</returns>
        [HttpDelete("/knowledge/deletedb")]
        public async Task<IActionResult> DeleteKnowledgeBase([FromServices] IKnowledgeBaseService service, string dbName)
        {
            if (string.IsNullOrEmpty(dbName))
            {
                return BadRequest("Invalid parameters");
            }

            var result = await service.DeleteKnowledgeBaseAsync(dbName);
            return result ? Ok("Knowledge base deleted successfully") : BadRequest("Failed to delete knowledge base");
        }
        #endregion
    }
}
