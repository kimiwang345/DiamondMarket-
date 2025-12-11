namespace DiamondMarket.Tasks
{
    using DiamondMarket.Data;
    using DiamondMarket.Models;
    using DiamondMarket.Utils;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static QRCoder.PayloadGenerator;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    public class UsdtWatcher : BackgroundService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<UsdtWatcher> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;

        // 如果要查数据库里的记录可以用 _scopeFactory 创建 DbContext
        public UsdtWatcher(
            IHttpClientFactory httpFactory,
            ILogger<UsdtWatcher> logger,
            IConfiguration config,
            IServiceScopeFactory scopeFactory)
        {
            _httpFactory = httpFactory;
            _logger = logger;
            _config = config;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckUSDT();
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (Exception) {
                
                }
            }
        }

        private async Task CheckUSDT()
        {
            try
            {
                string tronAddress = _config["Config:usdtPayUrl"];
                if (string.IsNullOrEmpty(tronAddress))
                {
                    return;
                }
                // 当前时间
                DateTime nowBeijing = DateTime.Now;

                // 查询最近5分钟（你的充值回调需求）
                long start = ToUtcMs(nowBeijing.AddMinutes(-50));
                long end = ToUtcMs(nowBeijing);

                string url = $"https://usdt.tokenview.io/api/usdt/addresstxlist/" + tronAddress + "/1/50";


                // 发起请求
                using var http = new HttpClient();
                var result = await http.GetStringAsync(url);

                var json = JObject.Parse(result);

                var list = json["data"]["txs"] as JArray;
                if (list == null || list.Count == 0)
                {

                    LogManager.Info("未检测到USDT入账");
                    return;
                }
                LogManager.Info("🟢 检测到 {0} 条链上充值交易", list.Count);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                foreach (var tx in list)
                {
                    long amount = tx["value"].ToObject<long>();
                    long chainTimeUnix = tx["time"].ToObject<long>();

                    // 区块时间(Timestamp秒)转 DateTime
                    DateTime chainTime = UnixTimestampToDateTime(chainTimeUnix);

                    DateTime startTime = chainTime.AddMinutes(-5);  // 区块前5分钟
                    DateTime endTime = chainTime.AddMinutes(0);     // 防止延迟多 预留1分钟也可写 chainTime

                    // ========== 查询匹配订单 ==========
                    var order = await db.recharge_log.Where(o =>
                        o.pay_amount * 1000000 == amount &&
                        o.status == 0 &&                           // 未支付
                        o.pay_channel == "usdt" &&                           // 未支付
                        o.create_time >= startTime &&
                        o.create_time <= endTime
                    ).OrderByDescending(o => o.create_time)
                    .FirstOrDefaultAsync();
                    if (order == null)
                    {
                        continue;
                    }
                    // ========= 🔥 写入数据库/充值到账 =========
                    order.status = 1;
                    LogManager.Info("处理订单"+order.order_no);
                    // ========== 加余额举例 ==============
                    var user = db.user_info.FirstOrDefault(u => u.id == order.user_id);

                    if (user != null)
                    {
                        var buyerAfter = user.amount + order.amount;

                        var balanceLog = new UserBalanceLog
                        {
                            user_id = user.id,
                            amount = order.amount,
                            before_amount = user.amount,
                            after_amount = buyerAfter,

                            type = 1,  // 购买扣款
                            remark = $"充值{order.order_no}",
                            create_time = DateTime.Now
                        };
                        user.amount += order.amount;
                        db.user_balance_log.Add(balanceLog);
                    }

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception e) {
                LogManager.Info("充值交易处理报错"+ e.Message);
            }
        }
        // 北京时间 → UTC毫秒时间戳
        long ToUtcMs(DateTime beijingTime)
        {
            return new DateTimeOffset(beijingTime.ToUniversalTime()).ToUnixTimeMilliseconds();
        }
        public static DateTime UnixTimestampToDateTime(long timestampMillis)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestampMillis).LocalDateTime;
        }

    }
}
