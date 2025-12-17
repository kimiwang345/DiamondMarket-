namespace DiamondMarket.Models
{
    public class PageStat
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public string page { get; set; }
        public string referrer { get; set; }
        public string ua { get; set; }
        public DateTime visit_time { get; set; }
    }
    public class PageStatView
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public string page { get; set; }
        public string referrer { get; set; }
        public string ua { get; set; }
        public string nickname { get; set; }
        public string login_name { get; set; }
        public DateTime visit_time { get; set; }
    }

}
