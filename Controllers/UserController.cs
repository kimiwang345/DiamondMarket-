using DiamondMarket.Data;
using DiamondMarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> GetUser()
        {
            var userId = long.Parse(User.FindFirst("user_id")!.Value);

            var user = await _db.user_info.FindAsync(userId);
            if (user == null) return NotFound(new { code = 404, msg = "用户不存在" });
            return Ok(new { code = 0, msg = "ok", data = user });
        }

        [HttpPost("balance-log")]
        public async Task<IActionResult> balanceLog()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            List<UserBalanceLog> orders = await _db.user_balance_log
                .Where(o => o.user_id == userId)
                .OrderByDescending(o => o.id)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = orders });
        }
    }
}
