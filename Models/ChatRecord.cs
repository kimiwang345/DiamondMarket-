namespace DiamondMarket.Models
{
    public class ChatRecord
    {
        public long id { get; set; }
        public long from_id { get; set; }
        public long to_id { get; set; }
        public int msg_type { get; set; } // 1=文本 2=图片
        public string content { get; set; }
        public int status { get; set; } = 1;
        public int read_status { get; set; } = 0;
        public DateTime create_time { get; set; }
    }

}
