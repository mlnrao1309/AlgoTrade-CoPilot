using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoTrading.Models
{
    namespace MarketData.Entities
    {
        public class InstrumentEq
        {
            public string Id { get; set; } = string.Empty;
            public long InstrumentToken { get; set; }
            public string TradingSymbol { get; set; } = string.Empty;
            public string Segment { get; set; } = string.Empty;
            public string Exchange { get; set; } = string.Empty;
            public long LotSize { get; set; }
        }

        public class InstrumentFo
        {
            public string Id { get; set; } = string.Empty;
            public long InstrumentToken { get; set; }
            public string TradingSymbol { get; set; } = string.Empty;
            public string Segment { get; set; } = string.Empty;
            public string Exchange { get; set; } = string.Empty;
            public long? LotSize { get; set; }
            public DateTime Expiry { get; set; }
        }
    }
}
