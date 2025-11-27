namespace DiamondMarket.Models
{
    public class RechargeLog
    {
        public long id { get; set; }
        public string order_no { get; set; } = "";
        public long user_id { get; set; }
        public long? withdraw_log_id { get; set; }
        public decimal amount { get; set; }
        public decimal pay_amount { get; set; }
        public string? pay_channel { get; set; }
        public string? pay_info { get; set; }
        public string? pay_url { get; set; }
        public byte status { get; set; }
        public DateTime create_time { get; set; }
    }
}
