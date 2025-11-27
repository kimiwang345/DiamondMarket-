namespace DiamondMarket.Models
{
    public class DiamondSaleItemView
    {
        public long id { get; set; }
        public long seller_id { get; set; }
        public string nickname { get; set; }
        public long account_id { get; set; }
        public int diamond_amount { get; set; }
        public decimal unit_price { get; set; }
        public decimal total_price { get; set; }
        public byte status { get; set; }
        public DateTime create_time { get; set; }
    }
}
