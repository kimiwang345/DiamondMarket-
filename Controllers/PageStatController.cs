namespace DiamondMarket.Controllers
{
    using global::DiamondMarket.Data;
    using global::DiamondMarket.Models;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/stat")]
    [ApiController]
    public class PageStatController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PageStatController(AppDbContext db)
        {
            _db = db;
        }

        public class StatRequest
        {
            public string page { get; set; }
            public string referrer { get; set; }
            public string ua { get; set; }
            public long ts { get; set; }
        }

        [HttpPost("page-view")]
        public async Task<IActionResult> PageView([FromBody] StatRequest req)
        {
            long userId = 0;
            var claim = User.FindFirst("user_id");
            if (claim != null)
                userId = long.Parse(claim.Value);
            int userType = 0;
            if (userId > 0)
            {
                var user = await _db.user_info.FindAsync(userId);
                userType = user.user_type;
            }
            if (userType==0) {
                var stat = new PageStat
                {
                    user_id = userId,
                    page = req.page,
                    referrer = req.referrer,
                    ua = req.ua,
                    visit_time = DateTime.Now
                };

                _db.page_stat.Add(stat);
                await _db.SaveChangesAsync();
            }
           

            return Ok(new { code = 0 });
        }
    }

}
