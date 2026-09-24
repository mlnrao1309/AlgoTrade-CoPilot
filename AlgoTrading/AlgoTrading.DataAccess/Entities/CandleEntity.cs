using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlgoTrading.DataAccess.Entities
{
    [Table("Instruments_OHLC")]
    public class CandleEntity
    {
        [Key]
        public long Id { get; set; }

        public int InstrumentToken { get; set; }

        public string Timeframe { get; set; }

        public DateTime Timestamp { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Open { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal High { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Low { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Close { get; set; }

        public long Volume { get; set; }
    }
}
