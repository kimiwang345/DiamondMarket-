namespace DiamondMarket.Attributes
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using StackExchange.Redis;
    using System;

    namespace DiamondMarket.Attributes
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class RateLimitAttribute : ActionFilterAttribute
        {
            private readonly int _maxRequests;
            private readonly int _seconds;

            public RateLimitAttribute(int maxRequests, int seconds)
            {
                _maxRequests = maxRequests;
                _seconds = seconds;
            }

            public override void OnActionExecuting(ActionExecutingContext context)
            {
                var redis = context.HttpContext.RequestServices.GetService<IConnectionMultiplexer>();
                var db = redis.GetDatabase();

                

                // IP 或 用户
                var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userId = context.HttpContext.User.FindFirst("user_id")?.Value;
                var key = $"rl:{userId ?? ip}:{context.HttpContext.Request.Path}";

                // 自增一次
                var count = db.StringIncrement(key);

                // 第一次访问设置过期时间
                if (count == 1)
                    db.KeyExpire(key, TimeSpan.FromSeconds(_seconds));

                if (count > _maxRequests)
                {
                    context.Result = new ContentResult
                    {
                        Content = $"Too many requests. Limit {_maxRequests} times per {_seconds} seconds",
                        StatusCode = 429
                    };
                }
            }
        }
    }

}
