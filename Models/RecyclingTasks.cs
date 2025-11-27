namespace DiamondMarket.Models
{
    public class RecyclingTasks
    {
        public long id { get; set; }
        public long buyer_id { get; set; }

        public string game_type { get; set; } = "";
        public string game_user { get; set; } = "";
        public string game_pass { get; set; } = "";
        public int request_diamond { get; set; }
        public int fulfilled_diamond { get; set; }
        public decimal min_unit_price { get; set; }
        public decimal max_unit_price { get; set; }
        public int status { get; set; }
        public DateTime create_time { get; set; }
    }
}
//CREATE TABLE recycling_tasks (
//id BIGINT PRIMARY KEY AUTO_INCREMENT,
//buyer_id BIGINT NOT NULL,
//request_diamond INT NOT NULL ,       -- 用户输入数量
//	min_unit_price DECIMAL(12,4) NOT NULL COMMENT '最低单价',
//max_unit_price DECIMAL(12,4) NOT NULL COMMENT '最高单价',
//fulfilled_diamond INT DEFAULT 0 COMMENT '已购买数量',
//status TINYINT DEFAULT 0 COMMENT '0处理中 1完成 2部分完成 -1停止回收',
//create_time DATETIME DEFAULT CURRENT_TIMESTAMP,
//FOREIGN KEY (buyer_id) REFERENCES user_info(id)
//) COMMENT '回收任务';