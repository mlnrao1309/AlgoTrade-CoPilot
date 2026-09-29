namespace AlgoTrading.Models.MarketData.Ingestion
{
    /// <summary>One inclusive provider range. It is persisted atomically with its checkpoint.</summary>
    public sealed class HistoricalChunkRequest
    {
        public HistoricalChunkRequest(long instrumentToken, string streamType, DateTime rangeStart, DateTime rangeEnd)
        {
            if (instrumentToken <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instrumentToken));
            }

            HistoricalStreamTypes.Validate(streamType);
            if (rangeEnd < rangeStart)
            {
                throw new ArgumentException("Historical chunk end must not precede its start.", nameof(rangeEnd));
            }

            this.InstrumentToken = instrumentToken;
            this.StreamType = streamType;
            this.RangeStart = rangeStart;
            this.RangeEnd = rangeEnd;
        }

        public long InstrumentToken { get; }

        public string StreamType { get; }

        public string ProviderInterval
        {
            get
            {
                return HistoricalStreamTypes.GetProviderInterval(this.StreamType);
            }
        }

        public DateTime RangeStart { get; }

        public DateTime RangeEnd { get; }
    }
}
