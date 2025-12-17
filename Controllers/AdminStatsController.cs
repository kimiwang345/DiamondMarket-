using DiamondMarket.Attributes.DiamondMarket.Attributes;
using DiamondMarket.Data;
using DiamondMarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiamondMarket.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    namespace DiamondMarket.Controllers
    {
        [ApiController]
        [Route("api/adminStats")]
        public class AdminStatsController : ControllerBase
        {
            private readonly AppDbContext _db;

            public AdminStatsController(AppDbContext db)
            {
                _db = db;
            }


            /*------------------------------------------------------
             🧿 1) 初始化数据（默认 7 天）
             ------------------------------------------------------*/
            [HttpPost("initData")]
            public async Task<IActionResult> InitData(int days = 7)
            {
                DateTime start = DateTime.Now.Date.AddDays(-days);

                // ========= 总 PV =========
                int totalPV = await _db.page_stat.CountAsync(x => x.visit_time >= start);

                // ========= 总 UV（按用户区分）=========
                int totalUV = await _db.page_stat
                    .Where(x => x.visit_time >= start)
                    .Select(x => x.user_id)
                    .Distinct()
                    .CountAsync();

                // ========= 趋势图 =========
                var trends = await _db.page_stat
                    .Where(x => x.visit_time >= start)
                    .GroupBy(x => x.visit_time.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        pv = g.Count(),
                        uv = g.Select(x => x.user_id).Distinct().Count()
                    })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                // ========= 页面排行 =========
                var rank = await _db.page_stat
                    .Where(x => x.visit_time >= start)
                    .GroupBy(x => x.page)
                    .Select(g => new
                    {
                        page = g.Key,
                        pv = g.Count(),
                        uv = g.Select(x => x.user_id).Distinct().Count()
                    })
                    .OrderByDescending(x => x.pv)
                    .Take(20)
                    .ToListAsync();

                // ========= 表格展示 =========
                var table = rank.Select(x => new
                {
                    x.page,
                    x.pv,
                    x.uv,
                    avgStay = $"{new Random().Next(1, 5)}m {new Random().Next(0, 59)}s", // 可以后续实现停留时间
                    bounce = $"{new Random().Next(20, 80)}%"
                });

                return Ok(new
                {
                    code = 0,
                    msg = "ok",
                    data = new
                    {
                        totalPV,
                        totalUV,
                        avgPV = totalUV == 0 ? 0 : Math.Round((decimal)totalPV / totalUV, 2),
                        trends,
                        rank,
                        table
                    }
                });
            }


            /*------------------------------------------------------
             🧿 2) 自定义日期范围查询
             ------------------------------------------------------*/
            [HttpGet("dateRange")]
            public async Task<IActionResult> DateRange(string startDate, string endDate)
            {
                DateTime start = DateTime.Parse(startDate);
                DateTime end = DateTime.Parse(endDate).AddDays(1);

                // 同上，复用逻辑
                var data = await InitDataByDate(start, end);
                return Ok(new { code = 0, data });
            }

            private async Task<object> InitDataByDate(DateTime start, DateTime end)
            {
                int totalPV = await _db.page_stat.CountAsync(x => x.visit_time >= start && x.visit_time < end);

                int totalUV = await _db.page_stat
                    .Where(x => x.visit_time >= start && x.visit_time < end)
                    .Select(x => x.user_id)
                    .Distinct()
                    .CountAsync();

                var trends = await _db.page_stat
                    .Where(x => x.visit_time >= start && x.visit_time < end)
                    .GroupBy(x => x.visit_time.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        pv = g.Count(),
                        uv = g.Select(x => x.user_id).Distinct().Count()
                    })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                var rank = await _db.page_stat
                    .Where(x => x.visit_time >= start && x.visit_time < end)
                    .GroupBy(x => x.page)
                    .Select(g => new
                    {
                        page = g.Key,
                        pv = g.Count(),
                        uv = g.Select(x => x.user_id).Distinct().Count()
                    })
                    .OrderByDescending(x => x.pv)
                    .Take(20)
                    .ToListAsync();

                return new
                {
                    totalPV,
                    totalUV,
                    avgPV = totalUV == 0 ? 0 : Math.Round((decimal)totalPV / totalUV, 2),
                    trends,
                    rank
                };
            }



            /*------------------------------------------------------
             🧿 3) 实时访问（最近 60 秒）
             ------------------------------------------------------*/
            [HttpPost("realtime")]
            public async Task<IActionResult> Realtime()
            {
                DateTime start = DateTime.Now.AddSeconds(-120);

                var list = await _db.page_stat_view
                    .Where(x => x.visit_time >= start)
                    .OrderByDescending(x => x.visit_time)
                    .Take(50)
                    .Select(x => new
                    {
                        x.page,
                        x.user_id,
                        x.nickname,
                        time = x.visit_time
                    })
                    .ToListAsync();

                /*--------------------------------------
                 2) 在线玩家统计（新增）
                    5分钟内登录视为在线
                 --------------------------------------*/
                DateTime onlineStart = DateTime.Now.AddMinutes(-5);

                int onlineCount = await _db.user_info
                    .CountAsync(u => u.last_login_time != null
                                 && u.last_login_time >= onlineStart);

                return Ok(new { code = 0, list, onlineCount });
            }
        }
    }

}
