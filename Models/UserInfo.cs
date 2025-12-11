namespace DiamondMarket.Models
{
    public class UserInfo
    {
        public long id { get; set; }
        public string nickname { get; set; } = "";
        public string login_name { get; set; } = "";
        public string login_pwd { get; set; } = "";
        public decimal amount { get; set; }
        public decimal freeze_amount { get; set; }
        public byte user_type { get; set; }
        public DateTime create_time { get; set; }
        public DateTime? last_login_time { get; set; }
        public DateTime? last_chat_time { get; set; }
    }
}
