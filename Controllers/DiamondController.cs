using DiamondMarket.Data;
using DiamondMarket.Data;
using DiamondMarket.Models;
using DiamondMarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiamondMarket.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class DiamondController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DiamondController(AppDbContext db)
        {
            _db = db;
        }

        // 列出在售商品
        [HttpGet("sale-list")]
        public async Task<IActionResult> GetSaleList()
        {
            var list = await _db.diamond_sale_item_view
                .Where(x => x.status == 1)
                .OrderBy(x => x.unit_price)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = list });
        }

        // 列出在售商品
        [HttpPost("my-selling")]
        public async Task<IActionResult> GetMySaleingList()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(claim.Value);
            var list = await _db.diamond_sale_item
                .Where(x => x.status == 1 && x.seller_id== userId)
                .OrderByDescending(x => x.create_time)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = list });
        }


        public class PublishRequest
        {
            public string game_type { get; set; } = "";
            public string game_user { get; set; } = "";
            public string game_pass { get; set; } = "";
            public int diamond_amount { get; set; }
            public decimal unit_price { get; set; }
        }

        // 发布出售
        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] PublishRequest req)
        {
            if (req.diamond_amount <= 0 || req.unit_price <= 0)
                return BadRequest(new { code = 400, msg = "数量和单价必须大于0" });
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            // 创建/保存游戏账号
            var acc = new GameAccount
            {
                user_id = userId,
                game_type = req.game_type,
                game_user = req.game_user,
                game_pass = req.game_pass, // TODO: 加密
                create_time = DateTime.UtcNow
            };
            _db.game_account.Add(acc);
            await _db.SaveChangesAsync();

            var total = req.diamond_amount * req.unit_price;

            var item = new DiamondSaleItem
            {
                seller_id = userId,
                account_id = acc.id,
                diamond_amount = req.diamond_amount,
                unit_price = req.unit_price,
                total_price = total,
                status = 1,
                create_time = DateTime.UtcNow
            };

            _db.diamond_sale_item.Add(item);
            await _db.SaveChangesAsync();

            return Ok(new { code = 0, msg = "发布成功", data = item });
        }

        // 下架
        [HttpPost("off-sale/{id:long}")]
        public async Task<IActionResult> OffSale(long id)
        {
            var item = await _db.diamond_sale_item.FindAsync(id);
            if (item == null) return NotFound(new { code = 404, msg = "商品不存在" });

            item.status = 3; // 下架
            await _db.SaveChangesAsync();

            return Ok(new { code = 0, msg = "已下架" });
        }


        [HttpPost("query_recycling_tasks")]
        public async Task<IActionResult> queryRecyclingTasks()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(claim.Value);
            var list = await _db.recycling_tasks
                .Where(x => x.buyer_id == userId)
                .OrderByDescending(x => x.create_time)
                .ToListAsync();
            if (list.Count==1) {
                
                return Ok(new { code = 0, msg = "ok", data = list.FirstOrDefault()});
            }

            return Ok(new { code = 0, msg = "ok" });
        }

        public class AddRecyclingTasksReq
        {
            public long id { get; set; } = 0;
            public int request_diamond { get; set; } = 0;
            public decimal min_unit_price { get; set; }
            public decimal max_unit_price { get; set; }
            public string game_type { get; set; }
            public string game_user { get; set; }
            public string game_pass { get; set; }
        }

        [HttpPost("create_recycling_tasks")]
        public async Task<IActionResult> addRecyclingTasks([FromBody] AddRecyclingTasksReq req)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(claim.Value);

            if (req.min_unit_price <= 0 || req.max_unit_price <= 0|| req.min_unit_price> req.max_unit_price)
                return BadRequest(new { code = 400, msg = "回收单价不正确" });

            if (req.request_diamond <= 0)
                return BadRequest(new { code = 400, msg = "回收数量不正确" });

            if (string.IsNullOrEmpty(req.game_type)|| string.IsNullOrEmpty(req.game_user) || string.IsNullOrEmpty(req.game_pass))
                return BadRequest(new { code = 400, msg = "游戏账号不正确" });

            if (req.id > 0)
            {
                var item = new RecyclingTasks
                {
                    id = req.id,
                    buyer_id = userId,
                    game_type = req.game_type,
                    game_user = req.game_user,
                    game_pass = req.game_pass,
                    request_diamond = req.request_diamond,
                    min_unit_price = req.min_unit_price,
                    max_unit_price = req.max_unit_price,
                    fulfilled_diamond = 0,
                    create_time = DateTime.Now,
                    status = 0
                };

                _db.recycling_tasks.Update(item);
            }
            else {
                var item = new RecyclingTasks
                {
                    buyer_id = userId,
                    game_type = req.game_type,
                    game_user = req.game_user,
                    game_pass = req.game_pass,
                    request_diamond = req.request_diamond,
                    min_unit_price = req.min_unit_price,
                    max_unit_price = req.max_unit_price,
                    fulfilled_diamond = 0,
                    create_time = DateTime.Now,
                    status = 0
                };

                _db.recycling_tasks.Add(item);
            }
               
            await _db.SaveChangesAsync();
            return Ok(new { code = 0, msg = "ok" });
        }
        [HttpPost("stop_recycling_tasks")]
        public async Task<IActionResult> stopRecyclingTasks([FromBody] AddRecyclingTasksReq req)
        {

            if (req.id > 0)
            {
                var tasks = await _db.recycling_tasks.FindAsync(req.id);
                tasks.status = - 1;
                _db.recycling_tasks.Update(tasks);
                await _db.SaveChangesAsync();
            }
            return Ok(new { code = 0, msg = "ok" });
        }
    }
}
