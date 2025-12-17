namespace DiamondMarket.Controllers
{
    using global::DiamondMarket.Data;
    using global::DiamondMarket.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Newtonsoft.Json.Linq;
    using System.Globalization;
    using System.Threading.Channels;
    using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

    [ApiController]
    [Route("api/stats")]
    public class StatsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StatsController(AppDbContext db)
        {
            _db = db;
        }

        public class QueryRequest
        {
            public int? days { get; set; }
            public string? startStr { get; set; }
            public string? endStr { get; set; }
        }

        private (DateTime start, DateTime end) GetRange(QueryRequest request)
        {
            if (request.days.HasValue)
            {
                DateTime end = DateTime.Today.AddDays(1).AddSeconds(-1);
                DateTime start = end.AddDays(-request.days.Value + 1);
                return (start, end);
            }

            DateTime startDate = DateTime.Parse(request.startStr);
            DateTime endDate = DateTime.Parse(request.endStr).AddDays(1).AddSeconds(-1);
            return (startDate, endDate);
        }



        /* -------------------------------------------------------
         * 0 总览统计 /api/stats/all
         * -----------------------------------------------------*/
        [HttpPost("all")]
        public async Task<IActionResult> AllStats()
        {

            var successRechargeAmount = await _db.recharge_log.Where(x => x.status == 1).SumAsync(l => l.amount);

            var totalFreeze = await _db.user_info.SumAsync(u => u.freeze_amount);
            var totalAsset = await _db.user_info.SumAsync(u => u.amount);
            var totalBuySystemDiamondPrice = await _db.order_system_diamond.SumAsync(u => u.total_price);

            var totalFee = await _db.user_balance_log.Where(x => x.type == 5).SumAsync(l => l.amount);

            //clubBillCheckList,查询记账充值用户list

            // 获取所有成功的充值记录
            // 获取所有成功的充值记录
            var rechargeLogs = await _db.recharge_log
                .Where(r => r.status == 1 && (r.pay_channel == "manual" || r.pay_channel == "usdt"))
                .ToListAsync();  // 客户端查询

            // 获取所有用户信息
            var users = await _db.user_info.ToListAsync();  // 客户端查询

            // 获取所有用户的结账记录
            var cashBills = await _db.club_cash_bill.ToListAsync();  // 客户端查询

            // 查询记账充值用户列表，包含用户昵称、余额和结账总金额
            var clubBillCheckList = (from r in rechargeLogs
                                     join u in users on r.user_id equals u.id
                                     group r by r.user_id into grouped
                                     select new
                                     {
                                         user_id = grouped.Key,
                                         nickname = users.FirstOrDefault(u => u.id == grouped.Key)?.nickname, // 获取用户昵称
                                         login_name = users.FirstOrDefault(u => u.id == grouped.Key)?.login_name, // 获取用户登录名
                                         balance = users.FirstOrDefault(u => u.id == grouped.Key)!=null? users.FirstOrDefault(u => u.id == grouped.Key).amount:new decimal(0),   // 获取用户余额
                                         total_manual_recharge = grouped.Where(recharge => recharge.pay_channel == "manual").Sum(recharge => recharge.amount),
                                         total_usdt_recharge = grouped.Where(recharge => recharge.pay_channel == "usdt").Sum(recharge => recharge.amount),
                                         total_cash_bill = cashBills.Where(cb => cb.user_id == grouped.Key).Sum(cb => cb.pay_price) // 获取结账总金额
                                     }).ToList();
            decimal totalBillPrice = 0;
            decimal totalPayPrice = 0;
            foreach (var clubBillCheck in clubBillCheckList) {
                decimal billPrice = clubBillCheck.total_manual_recharge  - clubBillCheck.balance ;
                totalPayPrice+=clubBillCheck.total_usdt_recharge + clubBillCheck.total_cash_bill;
                totalBillPrice += billPrice;
            }

            return Ok(new
            {
                code = 0,
                totalFreeze,//玩家冻结资产
                totalAsset, //玩家总资产
                totalFee,//总手续费
                successRechargeAmount,  //充值成功金额
                totalBuySystemDiamondPrice,
                clubBillCheckList,
                totalBillPrice,
                totalPayPrice,
                //totalSaleAmount, //出售总金额
                //totalBuyAmount,  //购买总金额
                //successWithdrawAmount, //提现成功金额
                //dealingWithdrawAmount, //提现中金额

            });
        }


        /* -------------------------------------------------------
         * 1️⃣ 出售钻石统计 /api/stats/sale
         * -----------------------------------------------------*/
        [HttpPost("sale")]
        public async Task<IActionResult> SaleStats([FromBody] QueryRequest request)
        {
            var (s, e) = GetRange(request);

            var query = await _db.diamond_sale_item
                .Where(d => d.create_time >= s && d.create_time <= e && d.status == 2)
                .Select(d => new
                {
                    date = d.create_time.Date,  // 只取日期
                    d.diamond_amount,
                    d.total_price
                })
                .ToListAsync();  // ❗让 EF 执行 SQL，进入内存


            var totalDiamond = query.Sum(x => (int?)x.diamond_amount) ?? 0;
            var totalAmount = query.Sum(x => (decimal?)x.total_price) ?? 0;
            var totalCount = query.Count();
            var avgPrice = totalDiamond > 0 ? totalAmount / totalDiamond : 0;

            // 趋势（按天）
            var trend = query
                .GroupBy(x => x.date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"), // ❗这里才可以格式化
                    diamond = g.Sum(e => e.diamond_amount),
                    amount = g.Sum(e => e.total_price),
                    count = g.Count(),
                    avg = g.Sum(e => e.total_price) / (decimal)g.Sum(e => e.diamond_amount)
                })
                .OrderBy(x => x.date)
                .ToList();



            return Ok(new
            {
                code = 0,
                summary = new
                {
                    totalDiamond,
                    totalAmount,
                    totalCount,
                    avgPrice = avgPrice.ToString("0.00")
                },
                trend,
                table = trend  // 表格数据直接复用
            });
        }



        /* -------------------------------------------------------
         * 2️⃣ 购买钻石统计 /api/stats/purchase
         * ----------------------------------------------------- */
        [HttpPost("purchase")]
        public async Task<IActionResult> PurchaseStats([FromBody] QueryRequest request)
        {
            var (s, e) = GetRange(request);

            var query = await _db.order_diamond
                .Where(x => x.create_time >= s && x.create_time <= e && x.status == 1)
                .Select(d => new
                {
                    date = d.create_time.Date,  // 只取日期
                    d.diamond_amount,
                    d.total_price,
                    d.buyer_id
                }).ToListAsync();

            var totalDiamond = query.Sum(x => (int?)x.diamond_amount) ?? 0;
            var totalAmount = query.Sum(x => (decimal?)x.total_price) ?? 0;
            var totalCount = query.Count();
            var userCount = query.Select(x => x.buyer_id).Distinct().Count();

            var trend = query
                .GroupBy(x => x.date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    diamond = g.Sum(x => x.diamond_amount),
                    amount = g.Sum(x => x.total_price),
                    count = g.Count(),
                    userCount = g.Select(x => x.buyer_id).Distinct().Count()
                })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new
            {
                code = 0,
                summary = new
                {
                    totalDiamond,
                    totalAmount,
                    totalCount,
                    userCount
                },
                trend,
                table = trend
            });
        }



        /* -------------------------------------------------------
         * 3️⃣ 提现统计 /api/stats/withdraw
         * ----------------------------------------------------- */
        [HttpPost("withdraw")]
        public async Task<IActionResult> WithdrawStats([FromBody] QueryRequest request)
        {
            var (s, e) = GetRange(request);

            var query = await _db.withdraw_log.Where(x => x.create_time >= s && x.create_time <= e)
                 .Select(d => new
                 {
                     date = d.create_time.Date,  // 只取日期
                     d.amount,
                     d.status,
                     d.pay_channel
                 }).ToListAsync();

            decimal totalAmount = query.Sum(x => (decimal?)x.amount) ?? 0;
            decimal successAmount = query.Where(x => x.status == 2).Sum(x => (decimal?)x.amount) ?? 0;
            decimal pendingAmount = query.Where(x => x.status == 1 || x.status == 0).Sum(x => (decimal?)x.amount) ?? 0;
            int totalCount = query.Count();

            // 渠道分布
            var channel = query
                .Where(w => w.status == 2)
                .GroupBy(x => x.pay_channel)
                .Select(g => new
                {
                    channel = g.Key,
                    amount = g.Sum(x => x.amount)
                })
                .ToList();

            // 趋势
            var trend = query
                .GroupBy(x => x.date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    total = g.Sum(x => x.amount),
                    success = g.Where(x => x.status == 2).Sum(x => x.amount),
                    pending = g.Where(x => x.status == 1 || x.status == 0).Sum(x => x.amount),
                    fail = g.Where(x => x.status == 3).Sum(x => x.amount),
                    count = g.Count()
                })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new
            {
                code = 0,
                summary = new
                {
                    totalAmount,
                    successAmount,
                    pendingAmount,
                    totalCount
                },
                channel,
                trend,
                table = trend
            });
        }



        /* -------------------------------------------------------
         * 4️⃣ 充值统计 /api/stats/recharge
         * ----------------------------------------------------- */
        [HttpPost("recharge")]
        public async Task<IActionResult> RechargeStats([FromBody] QueryRequest request)
        {
            var (s, e) = GetRange(request);

            var query = await _db.recharge_log.Where(x => x.create_time >= s && x.create_time <= e)
                .Select(d => new
                {
                    date = d.create_time.Date,  // 只取日期
                    d.amount,
                    d.status,
                    d.user_id,
                    d.pay_channel
                }).ToListAsync();

            decimal totalAmount = query.Sum(x => (decimal?)x.amount) ?? 0;
            decimal successAmount = query.Where(x => x.status == 1).Sum(x => (decimal?)x.amount) ?? 0;
            decimal pendingAmount = query.Where(x => x.status == 0).Sum(x => (decimal?)x.amount) ?? 0;
            int userCount = query.Where(x => x.status == 1).Select(x => x.user_id).Distinct().Count();

            var channel = query
                .Where(w => w.status == 1)
                .GroupBy(x => x.pay_channel)
                .Select(g => new
                {
                    channel = g.Key,
                    amount = g.Sum(x => x.amount)
                }).ToList();

            var trend = query
                .GroupBy(x => x.date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    total = g.Sum(x => x.amount),
                    success = g.Where(x => x.status == 1).Sum(x => x.amount),
                    pending = g.Where(x => x.status == 0).Sum(x => x.amount),
                    fail = g.Where(x => x.status == 2).Sum(x => x.amount),
                    count = g.Count(),
                    userCount = g.Select(x => x.user_id).Distinct().Count()
                })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new
            {
                code = 0,
                summary = new
                {
                    totalAmount,
                    successAmount,
                    pendingAmount,
                    userCount
                },
                channel,
                trend,
                table = trend
            });
        }



        /* -------------------------------------------------------
         * 5️⃣ 手续费统计 /api/stats/fee
         * ----------------------------------------------------- */
        [HttpPost("fee")]
        public async Task<IActionResult> FeeStats([FromBody] QueryRequest request)
        {
            var (s, e) = GetRange(request);

            // ===== 1) 手续费流水（type = 5）
            var logs = await _db.user_balance_log
                .Where(x => x.type == 5 && x.create_time >= s && x.create_time <= e)
                .ToListAsync();

            // ===== 2) 趋势图 - 按天聚合
            var trend = logs
                .GroupBy(x => x.create_time.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    fee = g.Sum(v => v.amount),       // 手续费金额
                    count = g.Count()
                })
                .OrderBy(x => x.date)
                .ToList();

            decimal totalFee = trend.Sum(x => x.fee);
            int totalCount = trend.Sum(x => x.count);

            // ===== 3) 交易总额（可从 diamond_sale_item 完成订单获取）
            var trade = await _db.diamond_sale_item
                .Where(x => x.status == 2 && x.create_time >= s && x.create_time <= e)
                .GroupBy(x => 1)
                .Select(g => new
                {
                    tradeAmount = g.Sum(v => v.total_price)
                })
                .FirstOrDefaultAsync();

            decimal tradeAmount = trade?.tradeAmount ?? 0;

            return Ok(new
            {
                code = 0,
                totalFee,
                totalCount,
                tradeAmount,
                trend
            });
        }



        public class PayClubBillReq
        {
            public int payPrice { get; set; } = 0;
            public string login_name { get; set; }
            public string nickname { get; set; }
        }

        [HttpPost("payClubBill")]///good/list
        public async Task<IActionResult> payClubBill([FromBody] PayClubBillReq req)
        {
            if (req.payPrice <= 0) {
                return Ok(new { code = 401, msg = "结账金额错误" });
            }
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            var user = await _db.user_info.FindAsync(userId);
            if (user == null) return NotFound(new { code = 404, msg = "用户不存在" });
            if (user.user_type != 1)
            {
                return NotFound(new { code = 404, msg = "无权限" });
            }

            var clubUser = await _db.user_info
                .FirstOrDefaultAsync(u => u.login_name == req.login_name);
            if (clubUser == null || clubUser.user_type != 2) {
                return Ok(new { code = 404, msg = "非俱乐部用户不能结账" });
            }
            ClubCashBill clubCashBill = new ClubCashBill
            {
                user_id = clubUser.id,
                nickname = clubUser.nickname,
                login_name = clubUser.login_name,
                pay_price = req.payPrice,
                create_time = DateTime.Now
            };
            _db.club_cash_bill.Add(clubCashBill);

            await _db.SaveChangesAsync();
            return Ok(new { code = 0, msg = "ok" });
        }
    }

}
