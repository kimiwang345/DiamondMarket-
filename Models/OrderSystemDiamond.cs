using Microsoft.AspNetCore.Http.HttpResults;

namespace DiamondMarket.Models
{
    public class OrderSystemDiamond
    {
        public long id { get; set; }
        public string order_no { get; set; }
        public long buyer_id { get; set; }
        public int diamond_amount { get; set; }
        public decimal total_price { get; set; } // 1=文本 2=图片
        public int good_id { get; set; }
        public int status { get; set; } = 0;
        public DateTime create_time { get; set; }
        public string game_user { get; set; }
        public string game_pass { get; set; }
    }

}
//CREATE TABLE order_system_diamond (
//    id BIGINT PRIMARY KEY AUTO_INCREMENT,
//    order_no VARCHAR(50) NOT NULL UNIQUE COMMENT '订单号',
//    buyer_id BIGINT NOT NULL COMMENT '购买人',
//    diamond_amount INT NOT NULL COMMENT '钻石数量',
//    total_price DECIMAL(12,2) NOT NULL COMMENT '总价',
//    good_id INT NOT NULL COMMENT '商品id',
//    status TINYINT NOT NULL DEFAULT 0 COMMENT '0待处理 1已完成',
//    create_time DATETIME DEFAULT CURRENT_TIMESTAMP,
//    game_user VARCHAR(100) NOT NULL COMMENT '游戏账号',
//    game_pass VARCHAR(100) NOT NULL COMMENT '游戏密码',
//    FOREIGN KEY (buyer_id) REFERENCES user_info(id)
//) COMMENT '购买系统钻石记录';