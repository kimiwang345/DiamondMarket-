namespace DiamondMarket.Models
{
    public class GameAccount
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public string game_type { get; set; } = "";
        public string game_user { get; set; } = "";
        public string game_pass { get; set; } = "";
        public DateTime create_time { get; set; }
    }
}
