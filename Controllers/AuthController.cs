using DiamondMarket.Attributes.DiamondMarket.Attributes;
using DiamondMarket.Data;
using DiamondMarket.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DiamondMarket.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // 生成 Token
        private string GenerateToken(UserInfo user)
        {
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("user_id", user.id.ToString()),
                new Claim("user_type", user.user_type.ToString()),
                new Claim("login_name", user.login_name)
            };

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(int.Parse(jwt["ExpiresHours"])),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 登录
        [HttpPost("login")]
        [RateLimit(5, 5)]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _db.user_info
                .FirstOrDefaultAsync(u => u.login_name == req.login_name && u.login_pwd == req.login_pwd);

            if (user == null)
                return Unauthorized(new { code = 401, msg = "用户名或密码错误" });
            user.last_login_time = DateTime.Now;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                code = 0,
                msg = "ok",
                token = GenerateToken(user),
                user = user
            });
        }

        // 注册
        [HttpPost("register")]
        [RateLimit(1, 10)]
        public async Task<IActionResult> Register([FromBody] LoginRequest req)
        {
            if (await _db.user_info.AnyAsync(u => u.login_name == req.login_name))
                return BadRequest(new { code = 400, msg = "账号已存在" });

            var user = new UserInfo
            {
                login_name = req.login_name,
                login_pwd = req.login_pwd,
                nickname = req.nickname,
                amount = 0,
                freeze_amount = 0,
                user_type = 0,
                create_time = DateTime.Now
            };

            _db.user_info.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                code = 0,
                msg = "注册成功",
                token = GenerateToken(user),
                user = user
            });
        }

        public class LoginRequest
        {
            public string login_name { get; set; }
            public string login_pwd { get; set; }
            public string nickname { get; set; } = "";
        }
    }
}
