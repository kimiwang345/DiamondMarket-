namespace DiamondMarket.Data
{
    public class Common
    {

        public class QueryDiamondResponse
        {
            public int code { get; set; }
            public string message { get; set; }
            public QueryDiamondData data { get; set; }
        }

        public class QueryDiamondData
        {
            public string diamond { get; set; }
        }

        public class GiftDiamondResponse
        {
            public int code { get; set; }
            public string message { get; set; }
            public GiftDiamondData data { get; set; }
        }

        public class GiftDiamondData
        {
            public int diamondRecordId { get; set; }
        }


        public class QueryGoodListResponse
        {
            public int code { get; set; }
            public string message { get; set; }
            public List<GoodListData> data { get; set; }
        }
        public class GoodListData
        {
            public int id { get; set; }

            public string name { get; set; }

            public int diamond { get; set; }

            public string price { get; set; }

            public string goodTypeDesc { get; set; }
            public string discount { get; set; }
        }


        public class QueryPaymentChannelListResponse
        {
            public int code { get; set; }
            public string message { get; set; }
            public List<GoodListData> data { get; set; }
        }
        public class PaymentChannelData
        {
            public int id { get; set; }

            public string paymentChannelName { get; set; }

            public string paymentChannelCode { get; set; }
        }


        public class BuyDiamondResponse
        {
            public int code { get; set; }
            public string message { get; set; }
            public BuyDiamondData data { get; set; }
        }
        public class BuyDiamondData
        {
            public String orderNo { get; set; }
            public decimal payPrice { get; set; }
        }

    }
}
