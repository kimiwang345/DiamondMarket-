namespace DiamondMarket.Tasks
{
    using DiamondMarket.Data;
    using DiamondMarket.Models;
    using DiamondMarket.Utils;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System.Threading;
    using System.Threading.Tasks;
    using static DiamondMarket.Data.Common;

    public class RecyclingTaskWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;

        public RecyclingTaskWorker(IServiceProvider serviceProvider, IConfiguration config, IHttpClientFactory httpFactory)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _httpFactory = httpFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessTasks();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessTasks()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            // 找出未完成的任务
            var tasks = await db.recycling_tasks
                .Where(t => t.status == 0)
                .OrderBy(t => Guid.NewGuid())  // 随机
                .ToListAsync();


            // 查询所有在售的订单
            var items = await db.diamond_sale_item
                .Where(x => x.status == 1)
                .OrderBy(x => x.unit_price)
                .ToListAsync();
            if (tasks != null && tasks.Count > 0) {
                Random rand = new Random();
                foreach (var item in items)
                {
                    //每次订单都随机一个任务去处理
                    // 随机一个任务
                    var task = tasks[rand.Next(tasks.Count)];

                    await HandleTask(db, httpFactory, task, item);
                }
                await db.SaveChangesAsync();
            }

            
        }

        /// <summary>
        /// 执行单个任务
        /// </summary>
        private async Task HandleTask(AppDbContext db, IHttpClientFactory httpFactory, RecyclingTasks task,DiamondSaleItem item)
        {
            //判断在售订单的钻石余额是否足够
            var http = _httpFactory.CreateClient();
            var gameAccount = await db.game_account.FindAsync(item.account_id);
            string agApiUrl = _config["Config:agApiUrl"];
            var reqObj = new
            {
                loginName = gameAccount.game_user,
                password = gameAccount.game_pass
            };
            var res = await http.PostAsJsonAsync(agApiUrl + "/zs/diamond/query", reqObj);
            var obj = await res.Content.ReadFromJsonAsync<QueryDiamondResponse>();
            LogManager.Info("请求ag接口[querydiamond],入参:" + reqObj + ",出参:" + await res.Content.ReadAsStringAsync());
            if (obj == null || obj.code != 200)
            {
                item.status = 3;
                await db.SaveChangesAsync();
                return;
            }
            string diamondStr = obj.data.diamond;
            int diamond = int.Parse(diamondStr);
            if (diamond < item.diamond_amount)
            {
                item.status = 3;
                await db.SaveChangesAsync();
                return;
            }


            int needDiamond = task.request_diamond - task.fulfilled_diamond;
            if (needDiamond <= 0)
            {
                task.status = 1;
                return;
            }
            if (item.unit_price>= task.min_unit_price&& task.max_unit_price>= item.unit_price) {
                // 单笔订单独立事务
                using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var orderController = new OrderExecutor(db, httpFactory, _config);

                    var ok = await orderController.ExecuteBuy(
                        buyerId: task.buyer_id,
                        itemId: item.id,
                        gameType: task.game_type,
                        gameUser: task.game_user,
                        gamePass: task.game_pass
                    );

                    if (!ok)
                    {
                        await tx.RollbackAsync();
                        return; // 失败不影响下一个
                    }

                    // 成功
                    task.fulfilled_diamond += item.diamond_amount;
                    needDiamond = task.request_diamond - task.fulfilled_diamond;
                    if (task.fulfilled_diamond >= task.request_diamond)
                        task.status = 1;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                }
            }

            
        }
    

        /// <summary>
        /// 执行单个任务
        /// </summary>
        private async Task HandleTask(AppDbContext db, IHttpClientFactory httpFactory, RecyclingTasks task)
        {
            int needDiamond = task.request_diamond - task.fulfilled_diamond;
            if (needDiamond <= 0)
            {
                task.status = 1;
                return;
            }

            // 找到符合价格区间、最便宜的商品
            var items = await db.diamond_sale_item
                .Where(x => x.status == 1 &&
                            x.unit_price >= task.min_unit_price &&
                            x.unit_price <= task.max_unit_price)
                .OrderBy(x => x.unit_price)
                .ToListAsync();

            foreach (var item in items)
            {
                if (needDiamond <= 0) break;

                // 购买数量不支持部分购买，你的逻辑要求整单买
                if (item.diamond_amount > needDiamond)
                    continue;

                // 单笔订单独立事务
                using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var orderController = new OrderExecutor(db, httpFactory, _config);

                    var ok = await orderController.ExecuteBuy(
                        buyerId: task.buyer_id,
                        itemId: item.id,
                        gameType: task.game_type,
                        gameUser: task.game_user,
                        gamePass: task.game_pass
                    );

                    if (!ok)
                    {
                        await tx.RollbackAsync();
                        continue; // 失败不影响下一个
                    }

                    // 成功
                    task.fulfilled_diamond += item.diamond_amount;
                    needDiamond = task.request_diamond - task.fulfilled_diamond;

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                }
            }

            if (task.fulfilled_diamond >= task.request_diamond)
                task.status = 1;
        }
    
    }

}
