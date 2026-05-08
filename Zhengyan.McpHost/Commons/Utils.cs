namespace Zhengyan.McpHost.Commons
{
    public static class Utils
    {
        /// <summary>
        /// 获取ApiKey
        /// </summary>
        /// <param name="context"></param>
        /// <returns>ApiKey</returns>
        public static string GetApiKey(HttpContext context)
        {
            // 先尝试取一下 Authorization
            var found = context.Request.Headers.TryGetValue("Authorization", out var key);

            // 不存在，尝试取一下 api-key
            if (!found)
            {
                found = context.Request.Headers.TryGetValue("Api-Key", out key);
            }

            key = key.ToString().Split(" ")[^1];
            return key;
        }

    }
}