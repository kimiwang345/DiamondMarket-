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

    }
}
