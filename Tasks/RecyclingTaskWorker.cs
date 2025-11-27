namespace DiamondMarket.Tasks
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using DiamondMarket.Data;
    using DiamondMarket.Models;

    public class RecyclingTaskWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        public RecyclingTaskWorker(IServiceProvider serviceProvider, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessTasks();
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
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
                .ToListAsync();

            foreach (var task in tasks)
            {
                await HandleTask(db, httpFactory, task);
            }

            await db.SaveChangesAsync();
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
