namespace AlgoTrading.Models.MarketData.Ingestion
{
    /// <summary>Authoritative provider source streams used by historical and live market-data processing.</summary>
    public static class HistoricalStreamTypes
    {
        public const string IntradayFiveMinute = "INTRADAY_5M";

        public const string DailyOneDay = "DAILY_1D";

        public static string GetProviderInterval(string streamType)
        {
            if (streamType == IntradayFiveMinute)
            {
                return "5minute";
            }

            if (streamType == DailyOneDay)
            {
                return "day";
            }

            throw new ArgumentException("Unsupported historical source stream: " + streamType + ".", nameof(streamType));
        }

        public static string GetDatabaseTimeframe(string streamType)
        {
            if (streamType == IntradayFiveMinute)
            {
                return "5";
            }

            if (streamType == DailyOneDay)
            {
                return "D";
            }

            throw new ArgumentException("Unsupported historical source stream: " + streamType + ".", nameof(streamType));
        }

        public static int GetChunkDays(string streamType)
        {
            if (streamType == IntradayFiveMinute)
            {
                return 80;
            }

            if (streamType == DailyOneDay)
            {
                return 800;
            }

            throw new ArgumentException("Unsupported historical source stream: " + streamType + ".", nameof(streamType));
        }

        public static void Validate(string streamType)
        {
            GetProviderInterval(streamType);
        }
    }
}
