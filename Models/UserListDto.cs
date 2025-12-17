namespace DiamondMarket.Models
{
    public class UserListDto
    {
        public long user_id { get; set; }
        public string nickname { get; set; }

        // 昵称首字
        public string nickname_first { get; set; }

        // 在线 0/1
        public int online { get; set; }

        // 聊天内容
        public string? last_msg { get; set; }

        // 原始时间
        public DateTime? last_time { get; set; }

        // 缩写时间 （'刚刚', '10分钟前', '昨天 16:21', '11-26 13:10'）
        public string? last_time_short { get; set; }

        // 未读数量
        public int unread { get; set; }
    }


}
