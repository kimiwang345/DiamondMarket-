using DiamondMarket.Data;
using Microsoft.EntityFrameworkCore;

namespace DiamondMarket.Attributes
{
    public class VisitStatMiddleware
    {
        private readonly RequestDelegate _next;

        public VisitStatMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        private static string voidPath = "/api/chat/latest,/api/chat/unread,/api/adminStats/realtime";
        public async Task Invoke(HttpContext ctx, AppDbContext db)
        {
            try
            {
                // 获取登录用户
                long userId = 0;
                var claim = ctx.User.FindFirst("user_id");
                int userType = 0;
                if (claim != null)
                    userId = long.Parse(claim.Value);
                if (userId>0) {
                    var user_type = ctx.User.FindFirst("user_type");
                    userType = int.Parse(user_type.Value);
                }

                // 页面 = 请求路径
                string page = ctx.Request.Path.ToString();
                if (!voidPath.Contains(page)&& userType==0)
                {
                    // IP
                    string ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "";

                    // UA
                    string ua = ctx.Request.Headers["User-Agent"].ToString();


                    // ======================
                    // 1）插入访问日志（细粒度）
                    // ======================
                    await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO page_visit_log (user_id, page, ip, ua)
                VALUES ({userId}, {page}, {ip}, {ua});
            ");


                    // ======================
                    // 2）每日统计（PV + UV）
                    // ======================
                    var today = DateTime.Now.Date;

                    // PV：递增一次
                    await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO page_visit_stat (page, stat_date, pv, uv)
                VALUES ({page}, {today}, 1, 0)
                ON DUPLICATE KEY UPDATE pv = pv + 1;
            ");

                    // UV：当天是否统计过这用户
                    await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE page_visit_stat
                SET uv = uv + 1
                WHERE page = {page} 
                AND stat_date = {today}
                AND NOT EXISTS (
                    SELECT 1 FROM page_visit_log 
                    WHERE user_id = {userId}
                    AND page = {page}
                    AND DATE(create_time) = {today}
                );
            ");

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Visit stat error: " + ex.Message);
            }

            await _next(ctx);
        }
    }

}
