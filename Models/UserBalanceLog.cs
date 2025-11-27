namespace DiamondMarket.Models
{
    public class UserBalanceLog
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public byte type { get; set; }
        public decimal amount { get; set; }
        public decimal before_amount { get; set; }
        public decimal after_amount { get; set; }
        public string? remark { get; set; }
        public DateTime create_time { get; set; }
    }
}
