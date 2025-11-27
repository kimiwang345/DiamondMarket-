namespace DiamondMarket.Models
{
    public class OrderDiamond
    {
        public long id { get; set; }
        public string order_no { get; set; } = "";
        public long buyer_id { get; set; }
        public long seller_id { get; set; }
        public long seller_account_id { get; set; }
        public long buyer_account_id { get; set; }
        public long diamond_record_id { get; set; }
        public long item_id { get; set; }
        public int diamond_amount { get; set; }
        public decimal unit_price { get; set; }
        public decimal total_price { get; set; }
        public byte status { get; set; }
        public DateTime create_time { get; set; }
    }
}
