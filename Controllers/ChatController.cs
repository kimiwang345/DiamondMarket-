namespace DiamondMarket.Controllers
{
    using global::DiamondMarket.Data;
    using global::DiamondMarket.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using static QRCoder.PayloadGenerator;

    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ChatController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// 分页加载聊天记录
        /// </summary>
        [HttpPost("records")]
        public async Task<IActionResult> GetRecords(long maxId = long.MaxValue, int limit = 20,int chatUserId=0)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            long ownUserId = userId;
            var user = await _db.user_info.FindAsync(userId);
            if (user.user_type == 1)
            {
                //客服
                ownUserId = 0;
            }
            var list = await _db.chat_record
                .Where(x =>
                    x.id < maxId &&
                    ((x.from_id == ownUserId && x.to_id == chatUserId) ||
                     (x.from_id == chatUserId && x.to_id == ownUserId))
                )
               .OrderByDescending(x => x.id)
                .Take(limit)
                .ToListAsync();

            // 正序返回
            list = list.OrderBy(x => x.id).ToList();

            return Ok(new { code = 0, msg = "ok", data = list });
        }

        [HttpPost("latest")]
        public async Task<IActionResult> GetLatest(long afterId = 0, int chatUserId = 0)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            long ownUserId = userId;
            var user = await _db.user_info.FindAsync(userId);
            if (user.user_type==1) {
                //客服
                ownUserId = 0;
            }
            var list = await _db.chat_record
                .Where(x =>
                    x.id > afterId &&
                    ((x.from_id == ownUserId && x.to_id == chatUserId) ||
                     (x.from_id == chatUserId && x.to_id == ownUserId))
                )
                .OrderBy(x => x.id)
                .ToListAsync();

            return Ok(new { code = 0, msg = "ok", data = list });
        }

        public class SendChatRequest
        {
            public long to_id { get; set; }
            public int msg_type { get; set; }      // 1文本 2图片
            public string content { get; set; }
        }


        /// <summary>
        /// 发送消息（文本或图片）
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendChatRequest req)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            long ownUserId = userId;
            var user = await _db.user_info.FindAsync(userId);

            if (user.user_type == 1)
            {
                //客服
                ownUserId = 0;
            }
            else
            {
                user.last_chat_time = DateTime.Now;
            }
            var model = new ChatRecord
            {
                from_id = ownUserId,
                to_id = req.to_id,
                msg_type = req.msg_type,
                content = req.content,
                read_status = 0,
                create_time = DateTime.Now
            };

            _db.chat_record.Add(model);
            await _db.SaveChangesAsync();

            return Ok(new { code = 0, msg = "ok", data = model });
        }

        [HttpPost("unread")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            var user = await _db.user_info.FindAsync(userId);
            int count = 0;
            if (user.user_type == 1)
            {
                count = await _db.chat_record
                    .Where(x => x.to_id == 0 && x.read_status == 0)
                    .CountAsync();
            }
            else
            {
                count = await _db.chat_record
                        .Where(x => x.to_id == userId && x.read_status == 0)
                        .CountAsync();
            }
            return Ok(new { code = 0, msg = "ok", count });
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> ReadAll()
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);

            await _db.chat_record
                .Where(x => x.to_id == userId && x.read_status == 0)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.read_status, 1));

            return Ok(new { code = 0, msg = "ok" });
        }

        [HttpPost("read-all-admin")]
        public async Task<IActionResult> ReadAllAdmin(long fromId)
        {
            var claim = User.FindFirst("user_id");
            if (claim == null)
                return Unauthorized(new { code = 401, msg = "未登录或 token 失效" });

            var userId = long.Parse(claim.Value);
            var user = await _db.user_info.FindAsync(userId);
            if (user.user_type!=1) {
                return Ok(new { code = 500, msg = "无权限" });
            }
            await _db.chat_record
                .Where(x => x.from_id == fromId&&x.to_id==0 && x.read_status == 0)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.read_status, 1));

            return Ok(new { code = 0, msg = "ok" });
        }



        /* =====================================================
         * 1）用户列表（未读 + 最后消息）
         * GET /api/chat/users
         ====================================================== */
        [HttpPost("users")]
        public async Task<IActionResult> GetUserList(string search = "")
        {
            string sql = @"
       SELECT 
    u.id AS user_id,
    u.nickname,

    -- 昵称首字
    LEFT(u.nickname, 1) AS nickname_first,

    -- 在线状态
    IF(u.last_login_time >= DATE_SUB(NOW(), INTERVAL 5 MINUTE), 1, 0) AS online,

    -- 最后一条聊天内容
    c_last.content AS last_msg,
    c_last.create_time AS last_time,

    -- 最后聊天时间缩写（友好格式）
    CASE 
        WHEN c_last.create_time >= DATE_SUB(NOW(), INTERVAL 1 MINUTE)
            THEN '刚刚'
        WHEN c_last.create_time >= DATE_SUB(NOW(), INTERVAL 60 MINUTE)
            THEN CONCAT(TIMESTAMPDIFF(MINUTE, c_last.create_time, NOW()), '分钟前')
        WHEN DATE(c_last.create_time) = CURDATE()
            THEN DATE_FORMAT(c_last.create_time, '%H:%i')
        WHEN DATE(c_last.create_time) = DATE_SUB(CURDATE(), INTERVAL 1 DAY)
            THEN CONCAT('昨天 ', DATE_FORMAT(c_last.create_time, '%H:%i'))
        ELSE DATE_FORMAT(c_last.create_time, '%m-%d %H:%i')
    END AS last_time_short,

    -- 未读消息数量
    (
        SELECT COUNT(*) 
        FROM chat_record cr
        WHERE cr.from_id = u.id AND cr.to_id = 0 AND cr.read_status = 0
    ) AS unread

FROM user_info u

LEFT JOIN (
    SELECT x.*
    FROM chat_record x
    INNER JOIN (
        SELECT 
            LEAST(from_id, to_id) AS uid,
            GREATEST(from_id, to_id) AS cuid,
            MAX(id) AS max_id
        FROM chat_record
        WHERE (to_id = 0 OR from_id = 0)
        GROUP BY uid, cuid
    ) t ON x.id = t.max_id
) c_last
ON (c_last.from_id = u.id OR c_last.to_id = u.id)
where 1=1 " + (string.IsNullOrEmpty(search) ? "" : "and  nickname like '%" + search + "%'") +
@"ORDER BY 
    u.last_chat_time DESC,
    u.last_login_time DESC
    limit 50
    ";

            var list = await _db.Database.SqlQueryRaw<UserListDto>(sql).ToListAsync();
            return Ok(new { code = 0, msg = "ok", data = list });
        }


    }

}
