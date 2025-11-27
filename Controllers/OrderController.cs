using DiamondMarket.Data;
using DiamondMarket.Models;
using DiamondMarket.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using static DiamondMarket.Data.Common;
using static System.Net.WebRequestMethods;

namespace DiamondMarket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public OrderController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public class BuyRequest
        {
            public long item_id { get; set; }
            public string game_type { get; set; } = "";
            public string game_user { get; set; } = "";
            public string game_pass { get; set; } = "";
            public int diamond_amount { get; set; }
        }

        // 简单：买家一次买完整个商品
        [HttpPost("buy")]
        public async Task<IActionResult> Buy([FromBody] BuyRequest req, [FromServices] IHttpClientFactory httpFactory)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            // 单笔订单独立事务
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var orderController = new OrderExecutor(_db, httpFactory, _config);

                var ok = await orderController.ExecuteBuy(
                    buyerId: userId,
                    itemId: req.item_id,
                    gameType: req.game_type,
                    gameUser: req.game_user,
                    gamePass: req.game_pass
                );

                if (!ok)
                {
                    await tx.RollbackAsync();
                    return Ok(new { code = 101, msg = "购买失败", data = req });
                }

                await tx.CommitAsync();
                return Ok(new { code = 0, msg = "购买成功", data = req });
            }
            catch(Exception e)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { code = 500, msg = "服务器异常", detail = e.Message });
            }
        }






        // 查询我购买的
        [HttpPost("my-purchased")]
        public async Task<IActionResult> queryMyPurchased()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            List<OrderDiamondView> orders = await _db.order_diamond_view
                .Where(o => o.buyer_id == userId)
                .OrderByDescending(o => o.create_time)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = orders });
        }

        // 查询我卖出的
        [HttpPost("my-sold")]
        public async Task<IActionResult> queryMySold()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            var orders = await _db.order_diamond_view
                .Where(o => o.seller_id == userId)
                .OrderByDescending(o => o.create_time)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = orders });
        }
    }
}
