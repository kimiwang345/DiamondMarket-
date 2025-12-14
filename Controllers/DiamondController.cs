using DiamondMarket.Data;
using DiamondMarket.Models;
using DiamondMarket.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using static DiamondMarket.Data.Common;

namespace DiamondMarket.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class DiamondController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        public DiamondController(AppDbContext db, IConfiguration config, IHttpClientFactory httpFactory)
        {
            _db = db;
            _config = config;
            _httpFactory = httpFactory;
        }

        public class QuerySaleListRequest
        {
            public string? tradeCode { get; set; } = "";
        }

        // 列出在售商品
        [HttpPost("salelist")]
        public async Task<IActionResult> GetSaleList([FromBody] QuerySaleListRequest req)
        {
            var list = await _db.diamond_sale_item_view
                .Where(x => x.status == 1&&x.trade_code==req.tradeCode)
                .OrderBy(x => x.unit_price)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = list });
        }

        // 列出在售商品
        [HttpPost("salelistRecent")]
        public async Task<IActionResult> salelistRecent()
        {
            DateTime start = DateTime.Now.AddDays(-3);
            List<DiamondSaleItem> list = await _db.diamond_sale_item.Where(x => x.status == 2 && x.create_time > start)
                .OrderBy(x => x.unit_price)
                .ToListAsync();
            int diamond_amount = 0;
            decimal unit_price = 0;
            decimal total_price = 0;
            int total_count = 0;
            foreach (DiamondSaleItem data in list) {

                diamond_amount += data.diamond_amount;
                total_price += data.total_price;
            }
            if (diamond_amount>0) {
                unit_price = (total_price /diamond_amount);
                total_count = list.Count;
                unit_price = Math.Round(unit_price, 3);
            }
            return Ok(new { code = 0, msg = "ok", unit_price, total_count, diamond_amount });
        }

        public class QueryPageRequest
        {
            public int pageIndex { get; set; }
            public int pageSize { get; set; }
        }
        // 列出在售商品
        [HttpPost("my-selling")]
        public async Task<IActionResult> GetMySaleingList([FromBody] QueryPageRequest request)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(claim.Value);

            var query = _db.diamond_sale_item.AsQueryable();

            // 状态过滤
            query = query.Where(x => x.status == 1 && x.seller_id == userId);

            // 总数
            var total = await query.CountAsync();

            // 分页
            var list = await query
                .OrderByDescending(o => o.create_time)
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


        public class PublishRequest
        {
            public string game_type { get; set; } = "";
            public string game_user { get; set; } = "";
            public string game_pass { get; set; } = "";
            public string trade_code { get; set; } = "";
            public int diamond_amount { get; set; }
            public decimal unit_price { get; set; }
        }

        // 发布出售
        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] PublishRequest req)
        {
            if (req.diamond_amount <= 0 || req.unit_price <= 0)
                return Ok(new { code = 400, msg = "数量和单价必须大于0" });
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            string minUnitpriceStr = _config["Config:minUnitprice"];
            string maxUnitpriceStr = _config["Config:maxUnitprice"];
            string agApiUrl = _config["Config:agApiUrl"];
            decimal minUnitprice = 0;
            decimal maxUnitprice = 0;
            if (!string.IsNullOrEmpty(minUnitpriceStr))
                minUnitprice = decimal.Parse(minUnitpriceStr);
            if (!string.IsNullOrEmpty(maxUnitpriceStr))
                maxUnitprice = decimal.Parse(maxUnitpriceStr);

            if (req.unit_price> maxUnitprice|| req.unit_price< minUnitprice) {
                return Ok(new { code = 400, msg = "单价错误,单价范围"+ minUnitprice+"-"+ maxUnitprice });
            }
            var http = _httpFactory.CreateClient();
            var reqObj = new
            {
                loginName = req.game_user,
                password = req.game_pass
            };
            var res = await http.PostAsJsonAsync(agApiUrl + "/zs/diamond/query", reqObj);
            var obj = await res.Content.ReadFromJsonAsync<QueryDiamondResponse>();
            LogManager.Info("请求ag接口[querydiamond],入参:" + reqObj + ",出参:" + await res.Content.ReadAsStringAsync());
            if (obj == null || obj.code != 200) {
                return Ok(new { code = 400, msg = "查询钻石余额异常,请检查游戏账号和密码是否正确"});
            }
            string diamondStr = obj.data.diamond;
            int diamond = int.Parse(diamondStr);
            if (diamond< req.diamond_amount) {
                return Ok(new { code = 400, msg = "钻石不足" });
            }

            // 创建/保存游戏账号
            var acc = new GameAccount
            {
                user_id = userId,
                game_type = req.game_type,
                game_user = req.game_user,
                game_pass = req.game_pass, // TODO: 加密
                create_time = DateTime.Now
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
                trade_code = req.trade_code,
                status = 1,
                create_time = DateTime.Now
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


        [HttpPost("queryGoodList")]///good/list
        public async Task<IActionResult> queryGoodList()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            string agApiUrl = _config["Config:agApiUrl"];

            var http = _httpFactory.CreateClient();
            var reqObj = new object();
            var res = await http.PostAsJsonAsync(agApiUrl + "/zs/diamond/good/list", reqObj);
            LogManager.Info("请求ag接口[querydiamond],入参:" + reqObj + ",出参:" + await res.Content.ReadAsStringAsync());
            var obj = await res.Content.ReadFromJsonAsync<QueryGoodListResponse>();
            
            if (obj == null || obj.code != 200)
            {
                return Ok(new { code = 400, msg = "未查询到商品" });
            }

            return Ok(new { code = 0, msg = "ok", data = obj.data });
        }


        public class BuyDiamondReq
        {
            public int goodId { get; set; } = 0;
            public string loginName { get; set; }
            public string password { get; set; }
            public decimal price { get; set; }
            public int diamondAmount { get; set; }
        }

        [HttpPost("buyDiamond")]///good/list
        public async Task<IActionResult> buyDiamond([FromBody] BuyDiamondReq req)
        {
            if (req.goodId<=0) {
                return Ok(new { code = 401, msg = "请选择钻石商品" });
            }
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            // 加锁买家
            var buyer = await _db.user_info
                .FromSqlRaw("SELECT * FROM user_info WHERE id = {0} FOR UPDATE", userId)
                .FirstOrDefaultAsync();
            if (buyer == null || buyer.amount < req.price) {
                return Ok(new { code = 401, msg = "余额不足" });
            }
            if (string.IsNullOrEmpty(req.loginName)||string.IsNullOrEmpty(req.password)) {
                return Ok(new { code = 401, msg = "请填写游戏账号密码" });
            }
            string agApiUrl = _config["Config:agApiUrl"];
            var http = _httpFactory.CreateClient();
            var reqQueryDiamondObj = new
            {
                loginName = req.loginName,
                password = req.password
            };
            var resQueryDiamond = await http.PostAsJsonAsync(agApiUrl + "/zs/diamond/query", reqQueryDiamondObj);
            var objQueryDiamond = await resQueryDiamond.Content.ReadFromJsonAsync<QueryDiamondResponse>();
            LogManager.Info("请求ag接口[querydiamond],入参:" + reqQueryDiamondObj + ",出参:" + await resQueryDiamond.Content.ReadAsStringAsync());
            if (objQueryDiamond == null || objQueryDiamond.code != 200)
            {
                return Ok(new { code = 400, msg = "请检查游戏账号和密码是否正确" });
            }
            var reqPaymentChannelObj = new object();
            var res = await http.PostAsJsonAsync(agApiUrl + "/zs/diamond/paymentChannel/list", reqPaymentChannelObj);
            LogManager.Info("请求ag接口[paymentChannelList],入参:" + reqPaymentChannelObj + ",出参:" + await res.Content.ReadAsStringAsync());
            var obj = await res.Content.ReadFromJsonAsync<QueryPaymentChannelListResponse>();

            if (obj == null || obj.code != 200)
            {
                return Ok(new { code = 400, msg = "未配置余额支付渠道，请联系管理" });
            }
            if (obj.data == null || obj.data.Count < 1)
            {
                return Ok(new { code = 400, msg = "未配置余额支付渠道，请联系管理" });
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            //扣除余额
            
            var buyerBefore = buyer.amount;
            buyer.amount -= req.price;

            _db.user_balance_log.Add(new UserBalanceLog
            {
                user_id = buyer.id,
                amount = -req.price,
                before_amount = buyerBefore,
                after_amount = buyer.amount,
                type = 3,
                remark = $"购买钻石(商城)",
                create_time = DateTime.Now
            });

            var orderSystemDiamond = new OrderSystemDiamond
            {
                buyer_id = userId,
                diamond_amount = req.diamondAmount,
                total_price = req.price,
                good_id = req.goodId,
                game_user = req.loginName,
                game_pass = req.password,
                status = 0,
                create_time = DateTime.Now,
            };
            //保存订单
            _db.order_system_diamond.Add(orderSystemDiamond);
            await _db.SaveChangesAsync();

           
            int zsOrderPaymentChannelId = obj.data[0].id;

            var reqObj = new {
                loginName=req.loginName,
                password = req.password,
                zsOrderGoodId = req.goodId,
                zsOrderPaymentChannelId = zsOrderPaymentChannelId,
            };
            var resBuy = await http.PostAsJsonAsync(agApiUrl + "/zs/diamond/buyDiamond", reqObj);
            LogManager.Info("请求ag接口[buyDiamond],入参:" + reqObj + ",出参:" + await res.Content.ReadAsStringAsync());
            var objBuy = await resBuy.Content.ReadFromJsonAsync<BuyDiamondResponse>();
            
            if (objBuy == null || objBuy.code != 200)
            {
                return Ok(new { code = 400, msg = "购买失败" });
            }
            var objBuyData = objBuy.data;
            if (objBuyData.payPrice!=req.price) {
                //金额不一致，有人串改价格
                buyer.amount -= (objBuyData.payPrice- req.price);
            }
            orderSystemDiamond.order_no = objBuyData.orderNo;
            orderSystemDiamond.status =1;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { code = 0, msg = "ok", data = obj.data });
        }
    }
}
