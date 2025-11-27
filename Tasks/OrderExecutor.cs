namespace DiamondMarket.Tasks
{
    using DiamondMarket.Data;
    using DiamondMarket.Models;
    using Microsoft.EntityFrameworkCore;
    using System.Net.Http.Json;
    using static DiamondMarket.Data.Common;

    public class OrderExecutor
    {
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public OrderExecutor(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration config)
        {
            _db = db;
            _httpFactory = httpFactory;
            _config = config;
        }

        public async Task<bool> ExecuteBuy(long buyerId, long itemId, string gameType, string gameUser, string gamePass)
        {
            string serviceFeeStr = _config["Config:serviceFee"];
            decimal serviceFee = 0;
            if (!string.IsNullOrEmpty(serviceFeeStr))
                serviceFee = decimal.Parse(serviceFeeStr);

            // ========== 1. 加锁商品 ==========
            var item = await _db.diamond_sale_item
                .FromSqlRaw("SELECT * FROM diamond_sale_item WHERE id = {0} FOR UPDATE", itemId)
                .FirstOrDefaultAsync();

            // 加锁买家
            var buyer = await _db.user_info
                .FromSqlRaw("SELECT * FROM user_info WHERE id = {0} FOR UPDATE", buyerId)
                .FirstOrDefaultAsync();

            if (buyer == null || buyer.amount < item.total_price)
                return false;

            // 加锁卖家
            var seller = await _db.user_info
                .FromSqlRaw("SELECT * FROM user_info WHERE id = {0} FOR UPDATE", item.seller_id)
                .FirstOrDefaultAsync();

            if (seller == null)
                return false;

            string orderNo = $"OD{DateTime.Now:yyyyMMddHHmmssfff}{buyerId}";

            // 买家扣款
            var buyerBefore = buyer.amount;
            buyer.amount -= item.total_price;

            _db.user_balance_log.Add(new UserBalanceLog
            {
                user_id = buyer.id,
                amount = -item.total_price,
                before_amount = buyerBefore,
                after_amount = buyer.amount,
                type = 3,
                remark = $"购买钻石(任务),订单 {orderNo}",
                create_time = DateTime.Now
            });

            await _db.SaveChangesAsync();

            // 卖家收入
            var sellerBefore = seller.amount;
            seller.amount += item.total_price;

            _db.user_balance_log.Add(new UserBalanceLog
            {
                user_id = seller.id,
                amount = item.total_price,
                before_amount = sellerBefore,
                after_amount = seller.amount,
                type = 4,
                remark = $"出售钻石(任务),订单 {orderNo}",
                create_time = DateTime.Now
            });


            await _db.SaveChangesAsync();

            // ========== 6. 卖家手续费 ==========
            if (serviceFee > 0) { 
                var sellerBefore2 = seller.amount; 
                var sellerAfter2 = sellerBefore2 - item.total_price * serviceFee; 
                seller.amount = sellerAfter2; 
                _db.user_balance_log.Add(
                    new UserBalanceLog { 
                        user_id = seller.id, 
                        amount = -item.total_price * serviceFee, 
                        before_amount = sellerBefore2,
                        after_amount = sellerAfter2, 
                        type = 5, 
                        remark = $"出售钻石服务费,订单 {item.id}", create_time = DateTime.Now 
                    }); 
                await _db.SaveChangesAsync(); 
            }

            // 创建买家游戏账号
            var buyerGameAcc = new GameAccount
            {
                user_id = buyerId,
                game_type = gameType,
                game_user = gameUser,
                game_pass = gamePass,
                create_time = DateTime.Now
            };
            _db.game_account.Add(buyerGameAcc);
            await _db.SaveChangesAsync();

            // 创建订单
            var order = new OrderDiamond
            {
                order_no = orderNo,
                buyer_id = buyerId,
                seller_id = item.seller_id,
                item_id = item.id,
                buyer_account_id = buyerGameAcc.id,
                seller_account_id = item.account_id,
                diamond_amount = item.diamond_amount,
                unit_price = item.unit_price,
                total_price = item.total_price,
                status = 1,
                create_time = DateTime.Now
            };
            _db.order_diamond.Add(order);

            item.status = 2;
            await _db.SaveChangesAsync();

            // ---- 调用游戏平台赠送钻石 ----
            var sellerGameAcc = await _db.game_account.FindAsync(item.account_id);
            var http = _httpFactory.CreateClient();

            var res = await http.PostAsJsonAsync("http://150.109.156.45:81/zs/diamond/giftDiamond", new
            {
                loginName1 = sellerGameAcc.game_user,
                password1 = sellerGameAcc.game_pass,
                loginName2 = buyerGameAcc.game_user,
                password2 = buyerGameAcc.game_pass,
                giftDiamond = item.diamond_amount
            });

            var obj = await res.Content.ReadFromJsonAsync<GiftDiamondResponse>();
            if (obj == null || obj.code != 200)
                return false;

            order.diamond_record_id = obj.data.diamondRecordId;
            await _db.SaveChangesAsync();

            return true;
        }
    }

}
