namespace DiamondMarket.Models
{
    public class ClubCashBill
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public string nickname { get; set; }
        public string login_name { get; set; }
        public int pay_price { get; set; }
        public DateTime create_time { get; set; }
    }

}
