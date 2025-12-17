namespace DiamondMarket.Utils
{
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc;
    using System.Diagnostics;
    using Newtonsoft.Json;

    public class LoggingFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var stopwatch = Stopwatch.StartNew();

            // ===== 记录入参 =====
            var request = context.HttpContext.Request;

            string method = request.Method;
            string path = request.Path;
            string query = request.QueryString.Value;

            object bodyObj = context.ActionArguments;

            LogManager.Info(
                $"Request: {method} {path} {query}\n" +
                $"Body = {JsonConvert.SerializeObject(bodyObj)}");

            // ===== 执行方法 =====
            var executedContext = await next();

            stopwatch.Stop();

            // ===== 记录返回值 =====
            var result = executedContext.Result as ObjectResult;
            string response = "";
            if (result != null)
            {
                response = JsonConvert.SerializeObject(result.Value);
            }

            LogManager.Info(
                $"⬅️ Response: {method} {path}\n" +
                $"Result = {response}\n" +
                $"用时 = {stopwatch.ElapsedMilliseconds}ms");
        }
    }

}
