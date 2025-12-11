using DiamondMarket.Attributes.DiamondMarket.Attributes;
using DiamondMarket.Data;
using DiamondMarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DiamondMarket.Controllers.DiamondController;

namespace DiamondMarket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UserController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost("queryUser")]
        [RateLimit(60, 60)]
        public async Task<IActionResult> GetUser()
        {
            var userId = long.Parse(User.FindFirst("user_id")!.Value);

            var user = await _db.user_info.FindAsync(userId);
            if (user == null) return NotFound(new { code = 404, msg = "用户不存在" });
            return Ok(new { code = 0, msg = "ok", data = user });
        }

        [HttpPost("balance-log")]
        public async Task<IActionResult> balanceLog([FromBody] QueryPageRequest request)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            var query = _db.user_balance_log.AsQueryable();

            // 状态过滤
            query = query.Where(o => o.user_id == userId);

            // 总数
            var total = await query.CountAsync();

            // 分页
            var list = await query
                .OrderByDescending(o => o.id)
                .Skip((request.pageIndex - 1) * request.pageSize)
                .Take(request.pageSize)
                .ToListAsync();

            return Ok(new
            {
                code = 0,
                msg = "ok",
                data = list,
                total = total,
                page = request.pageIndex,
                pageSize = request.pageSize
            });
        }
    }
}
