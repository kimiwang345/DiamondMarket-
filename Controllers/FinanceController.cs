
using DiamondMarket.Data;
using DiamondMarket.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace DiamondMarket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinanceController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public FinanceController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public class RechargeRequest
        {
            public int amount { get; set; }
            public string? pay_channel { get; set; }
        }

        [HttpPost("recharge")]
        public async Task<IActionResult> Recharge([FromBody] RechargeRequest req)
        {
            // 金额必须是 100 的整数倍
            if (req.amount % 50 != 0)
                return Ok(new { code = 400, msg = "充值金额必须是 100 的整数倍" });

            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            var user = await _db.user_info.FindAsync(userId);
            if (user == null) return Ok(new { code = 404, msg = "用户不存在" });

            if (req.amount <= 0) return Ok(new { code = 400, msg = "金额必须大于0" });
            RechargeLog rechargeLog = new RechargeLog();
            string dataUrl = "";
            if (req.pay_channel == "usdt")
            {

                string usdtPayUrl = _config["Config:usdtPayUrl"];
                double usdtToCny = Double.Parse(_config["Config:usdtToCny"].ToString());
                if (string.IsNullOrEmpty(usdtPayUrl))
                {
                    return Ok(new { code = 400, msg = "支付失败,通道未配置" });
                }
                dataUrl = QrCodeHelper.GenerateQrDataUrl(usdtPayUrl);
                // 生成 100.00000 ~ 100.99999
                Random rand = new Random();

                double result = req.amount/ usdtToCny+rand.NextDouble();

                // 格式化五位小数
                string value = result.ToString("F5");
                rechargeLog = new RechargeLog
                {
                    order_no = $"RC{DateTime.Now:yyyyMMddHHmmssfff}{userId}",
                    user_id = userId,
                    amount = req.amount,
                    pay_amount = decimal.Parse(value),
                    pay_channel = req.pay_channel,
                    pay_url = usdtPayUrl,
                    pay_info = "",
                    withdraw_log_id = 0,
                    status = 0, 
                    create_time = DateTime.Now
                };
                _db.recharge_log.Add(rechargeLog);
            }
            else if (req.pay_channel == "manual")
            {
                if (user.user_type!=2) {
                    return Ok(new { code = 400, msg = "该用户无法申请人工支付" });
                }
                rechargeLog = new RechargeLog
                {
                    order_no = $"RC{DateTime.Now:yyyyMMddHHmmssfff}{userId}",
                    user_id = userId,
                    amount = req.amount,
                    pay_amount = req.amount,
                    pay_channel = req.pay_channel,
                    pay_url = "",
                    pay_info = "",
                    withdraw_log_id = 0,
                    status = 0, // 人工充值：直接成功
                    create_time = DateTime.Now
                };
                _db.recharge_log.Add(rechargeLog);
                
            }
            else {
                return Ok(new { code = 404, msg = "支付渠道不存在" });
            }



            await _db.SaveChangesAsync();

            return Ok(new { code = 0, msg = "充值成功", data = rechargeLog, dataUrl= dataUrl });
        }

        // 充值手动处理完成
        [HttpPost("completeRecharge/{id:long}")]
        public async Task<IActionResult> completeRecharge(long id)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(User.FindFirst("user_id")!.Value);
            var u = await _db.user_info.FindAsync(userId);
            if (u == null) return NotFound(new { code = 404, msg = "用户不存在" });
            if (u.user_type != 1)
            {
                return NotFound(new { code = 404, msg = "无权限" });
            }
            var rechargeLog = await _db.recharge_log.FindAsync(id);
            var rechargeUser = await _db.user_info.FindAsync(rechargeLog.user_id);

            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var buyerAfter = rechargeUser.amount + rechargeLog.amount;

            var balanceLog = new UserBalanceLog
            {
                user_id = rechargeLog.user_id,
                amount = rechargeLog.amount,
                before_amount = rechargeUser.amount,
                after_amount = buyerAfter,

                type = 1,  // 购买扣款
                remark = $"充值{rechargeLog.order_no}",
                create_time = DateTime.Now
            };
            rechargeUser.amount += rechargeLog.amount;
            _db.user_balance_log.Add(balanceLog);
            rechargeLog.status = 1;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { code = 0, msg = "ok" });
        }

        // 充值手动处理完成
        [HttpPost("rejectRecharge/{id:long}")]
        public async Task<IActionResult> rejectRecharge(long id)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(User.FindFirst("user_id")!.Value);
            var u = await _db.user_info.FindAsync(userId);
            if (u == null) return NotFound(new { code = 404, msg = "用户不存在" });
            if (u.user_type != 1)
            {
                return NotFound(new { code = 404, msg = "无权限" });
            }
            var rechargeLog = await _db.recharge_log.FindAsync(id);
           
            rechargeLog.status = 2;
            await _db.SaveChangesAsync();
            return Ok(new { code = 0, msg = "ok" });
        }

        public class WithdrawRequest
        {
            public decimal amount { get; set; }
            public string pay_channel { get; set; } = "";
            public string name { get; set; } = "";
            public string? pay_info { get; set; }
            public string? pay_url { get; set; }
        }


        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest req)
        {
            // 金额必须是 100 的整数倍
            if (req.amount <50)
                return Ok(new { code = 400, msg = "提现金额必须大于是 50" });
            if (req.amount %10!=0)
                return Ok(new { code = 400, msg = "提现金额必须10的倍数" });

            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            // 悲观锁，确保不会并发扣余额
            var user = await _db.user_info
                .FromSql($"SELECT * FROM user_info WHERE id = {userId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (user == null)
                return BadRequest(new { code = 400, msg = "用户不存在" });

            if (user.amount < req.amount)
                return BadRequest(new { code = 400, msg = "余额不足" });

            if (user.user_type == 2)
            {
                return Ok(new { code = 400, msg = "该用户无法提现" });
            }
            if (req.pay_channel== "WX"|| req.pay_channel == "ZFB") {
                if (string.IsNullOrEmpty(req.pay_url)) {
                    return Ok(new { code = 400, msg = "请上传收款码" });
                }
                if (!req.pay_url.Contains("upload"))
                {
                    return Ok(new { code = 400, msg = "请重新上传收款码" });
                }
            }
            // ========== 4. 余额日志（提现冻结） ==========
            var buyerBefore = user.amount;
            var buyerAfter = buyerBefore - req.amount;

            // 扣余额 + 加冻结
            user.amount -= req.amount;
            user.freeze_amount += req.amount;

           

            // 写提现记录
            var withdraw = new WithdrawLog
            {
                order_no = $"OD{DateTime.Now:yyyyMMddHHmmssfff}{userId}",
                user_id = userId,
                amount = req.amount,
                pay_channel = req.pay_channel,
                name = req.name,
                pay_info = req.pay_info,
                pay_url = req.pay_url,
                status = 0,
                create_time = DateTime.Now
            };
            _db.withdraw_log.Add(withdraw);

            var balanceLog = new UserBalanceLog
            {
                user_id = user.id,
                amount = -req.amount,
                before_amount = buyerBefore,
                after_amount = buyerAfter,
                type = 2,  // 购买扣款
                remark = $"提现{withdraw.order_no}",
                create_time = DateTime.Now
            };
            _db.user_balance_log.Add(balanceLog);
           

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { code = 0, msg = "提现申请已提交" });
        }

        // 查询提现记录
        [HttpPost("withdraw-logs")]
        public async Task<IActionResult> querywithdraws()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            List<WithdrawLog> orders = await _db.withdraw_log
                .Where(o => o.user_id == userId)
                .OrderByDescending(o => o.create_time)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = orders });
        }

        // 查询充值记录
        [HttpPost("recharge-logs")]
        public async Task<IActionResult> queryrecharges()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            List<RechargeLog> orders = await _db.recharge_log
                .Where(o => o.user_id == userId)
                .OrderByDescending(o => o.create_time)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = orders });
        }

        public class QueryAdminWithdrawListRequest
        {
            public int status { get; set; }
            public int pageIndex { get; set; }
            public int pageSize { get; set; }
        }

        // 查询充值记录
        [HttpPost("admin-recharge-logs")]
        public async Task<IActionResult> queryAdminRecharges([FromBody] QueryAdminWithdrawListRequest request)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            var user = await _db.user_info.FindAsync(userId);
            if (user == null) return Ok(new { code = 404, msg = "用户不存在" });
            if (user.user_type != 1)
            {
                return Ok(new { code = 404, msg = "无权限" });
            }
            var query = _db.recharge_log_view.AsQueryable();

            // 状态过滤
            query = query.Where(o => o.status == request.status);

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



        // 查询提现记录
        [HttpPost("adminWithdrawLogs")]

        public async Task<IActionResult> queryAdminWithdrawLogs([FromBody] QueryAdminWithdrawListRequest request)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            var user = await _db.user_info.FindAsync(userId);
            if (user == null)
                return Ok(new { code = 404, msg = "用户不存在" });

            if (user.user_type != 1)
                return Ok(new { code = 403, msg = "无权限" });

            var query = _db.withdraw_log_view.AsQueryable();

            // 状态过滤
            if (request.status != -1)
                query = query.Where(o => o.status == request.status);

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




        // 提现改成处理中
        [HttpPost("dealWithdraw/{id:long}")]
        public async Task<IActionResult> dealWithdraw(long id)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(User.FindFirst("user_id")!.Value);
            var user = await _db.user_info.FindAsync(userId);
            if (user == null) return NotFound(new { code = 404, msg = "用户不存在" });
            if (user.user_type != 1)
            {
                return NotFound(new { code = 404, msg = "无权限" });
            }

            var item = await _db.withdraw_log.FindAsync(id);
            if (item == null) return NotFound(new { code = 404, msg = "提现不存在" });
            if (item.status !=0) return NotFound(new { code = 404, msg = "状态不正确" });
            item.status = 1; // 处理中
            await _db.SaveChangesAsync();

            return Ok(new { code = 0, msg = "ok"});
        }



        // 提现处理完成
        [HttpPost("completeWithdraw/{id:long}")]
        public async Task<IActionResult> completeWithdraw(long id)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(User.FindFirst("user_id")!.Value);

            // 悲观锁，确保不会并发扣余额
            var dealUser = await _db.user_info
                .FromSql($"SELECT * FROM user_info WHERE id = {userId} FOR UPDATE")
                .FirstOrDefaultAsync();
            if (dealUser == null) return Ok(new { code = 404, msg = "用户不存在" });
            if (dealUser.user_type != 1)
            {
                return Ok(new { code = 404, msg = "无权限" });
            }

            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var item = await _db.withdraw_log.FindAsync(id);
            if (item == null) return NotFound(new { code = 404, msg = "提现不存在" });
            if (item.status !=1) return NotFound(new { code = 404, msg = "状态不正确" });
            item.status = 2; // 完成
            // 悲观锁，确保不会并发扣余额
            var user = await _db.user_info
                .FromSql($"SELECT * FROM user_info WHERE id = {item.user_id} FOR UPDATE")
                .FirstOrDefaultAsync();
            //冻结金额扣除
            user.freeze_amount -= item.amount;

            var dealAfter = dealUser.amount + item.amount;

            var balanceLog = new UserBalanceLog
            {
                user_id = dealUser.id,
                amount = item.amount,
                before_amount = dealUser.amount,
                after_amount = dealAfter,

                type = 7,  // 处理提现
                remark = $"处理提现{item.order_no}",
                create_time = DateTime.Now
            };
            dealUser.amount += item.amount;
            _db.user_balance_log.Add(balanceLog);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { code = 0, msg = "ok"});
        }



       

        public class CloseWithdrawRequest
        {
            public long id { get; set; }
            public string remark { get; set; }
        }

        // 提现关闭
        [HttpPost("closeWithdraw")]
        public async Task<IActionResult> closeWithdraw([FromBody] CloseWithdrawRequest req)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });
            var userId = long.Parse(User.FindFirst("user_id")!.Value);
            var u = await _db.user_info.FindAsync(userId);
            if (u == null) return Ok(new { code = 404, msg = "用户不存在" });
            if (u.user_type != 1)
            {
                return Ok(new { code = 404, msg = "无权限" });
            }

            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var item = await _db.withdraw_log.FindAsync(req.id);
            if (item == null) return Ok(new { code = 404, msg = "提现不存在" });
            if (item.status !=1) return Ok(new { code = 404, msg = "状态不正确" });
            item.status = 3; // 关闭
            item.remark = req.remark;
            // 悲观锁，确保不会并发扣余额
            var user = await _db.user_info
                .FromSql($"SELECT * FROM user_info WHERE id = {item.user_id} FOR UPDATE")
                .FirstOrDefaultAsync();
            //冻结金额扣除
            user.freeze_amount -= item.amount;

            // ========== 4. 余额日志（提现冻结） ==========
            var buyerBefore = user.amount;
            var buyerAfter = buyerBefore + item.amount;
            user.amount += item.amount;

            var balanceLog = new UserBalanceLog
            {
                user_id = user.id,
                amount = item.amount,
                before_amount = buyerBefore,
                after_amount = buyerAfter,
                type = 6,  // 提现失败退回
                remark = $"提现退回{item.order_no}",
                create_time = DateTime.Now
            };
            _db.user_balance_log.Add(balanceLog);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { code = 0, msg = "ok"});
        }

    }
}
