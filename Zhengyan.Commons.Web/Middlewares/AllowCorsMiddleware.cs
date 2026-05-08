using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Zhengyan.Commons.Web.Middlewares
{
    public class AllowCorsMiddleware
    {
        private readonly RequestDelegate _next;
        public AllowCorsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", context.Request.Headers["Origin"]);
                //context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
                // context.Response.Headers.Add("Access-Control-Allow-Headers", "token");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "*");

            }
            await _next(context);
        }
    }
}
