using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace AlgorithmicEngine.Data
{
    public class ExtendedCandle
    {
        public long InstrumentToken { get; set; }
        public DateTime TimeStamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public long OI { get; set; }
        public string TimeFrame { get; set; } = string.Empty;

        // Associated Pivot Data for this Candle's Timestamp
        public PivotData? PivotContext { get; set; }
    }

    public class PivotData
    {
        public long InstrumentToken { get; set; }
        public DateTime TimeStamp { get; set; }
        public string TimeFrame { get; set; } = string.Empty;
        public decimal PP { get; set; }
        public decimal R1 { get; set; }
        public decimal R2 { get; set; }
        public decimal R3 { get; set; }
        public decimal R4 { get; set; }
        public decimal R5 { get; set; }
        public decimal S1 { get; set; }
        public decimal S2 { get; set; }
        public decimal S3 { get; set; }
        public decimal S4 { get; set; }
        public decimal S5 { get; set; }
        public bool PPR1 { get; set; }
        public bool PPS1 { get; set; }
        public decimal RefResistance { get; set; }
        public decimal RefSupport { get; set; }

        // Reference Start Candle OHLC
        public decimal RefStartCandleOpen { get; set; }
        public decimal RefStartCandleHigh { get; set; }
        public decimal RefStartCandleLow { get; set; }
        public decimal RefStartCandleClose { get; set; }

        // Reference End Candle OHLC
        public decimal RefEndCandleOpen { get; set; }
        public decimal RefEndCandleHigh { get; set; }
        public decimal RefEndCandleLow { get; set; }
        public decimal RefEndCandleClose { get; set; }
    }

    public class ResistanceSupportData
    {
        public long InstrumentToken { get; set; }
        public DateTime TimeStamp { get; set; }
        public string TimeFrame { get; set; } = string.Empty;
        public List<decimal> LineValue { get; set; } = new();

    }


    public class MarketDataRepository
    {
        private readonly string _connectionString;

        public MarketDataRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ExtendedCandle> GetMarketData(long instrumentToken, string timeFrame, out List<ResistanceSupportData> srData, DateTime? startDate = null, DateTime? endDate = null )
        {
            srData = new List<ResistanceSupportData>();
            // Fetch candle data for the specified instrument and time frame
            var candles = FetchCandles(instrumentToken, timeFrame, startDate, endDate);
            // Fetch pivot data for the same instrument. I am fetching both day and week pivots to ensure we have all relevant pivot points for the candles.
            var pivots = FetchPivots(instrumentToken, "timeframe not needed. made dummy and statically coded week and day in query", startDate, endDate);
            // Filter pivots to only those that have PPR1 or PPS1 set to true, and map them to ResistanceSupportData

            var supportResistance = pivots.Where(p => p.PPR1 || p.PPS1).Select(p => new ResistanceSupportData
            {
                InstrumentToken = p.InstrumentToken,
                TimeStamp = p.TimeStamp,
                TimeFrame = p.TimeFrame,
                LineValue = new decimal[] { p.RefResistance, p.RefSupport, p.RefEndCandleClose,
                    p.RefEndCandleHigh,p.RefEndCandleLow,p.RefEndCandleOpen, p.RefStartCandleClose,
                    p.RefStartCandleHigh,p.RefStartCandleLow,p.RefStartCandleOpen}.ToList()
            }).ToList();
            srData.AddRange(supportResistance);
            

            // 2. Map Daily/Weekly Pivots to Hourly Candles using .Date matching
            var pivotLookupByDate = pivots
                .GroupBy(p => p.TimeStamp.Date)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var candle in candles)
            {
                // Match the hourly candle date to the active daily/weekly pivot record
                if (pivotLookupByDate.TryGetValue(candle.TimeStamp.Date, out var matchingPivot))
                {
                    candle.PivotContext = matchingPivot;
                }
            }

            return candles;
        }

        private List<ExtendedCandle> FetchCandles(long instrumentToken, string timeFrame, DateTime? startDate, DateTime? endDate)
        {
            var candles = new List<ExtendedCandle>();

            string query = @"
                SELECT [InstrumentToken], [Open], [Close], [High], [Low], [OI], [Volume], [TimeStamp], [TimeFrame]
                FROM [Algo_Trading_CFCore].[dbo].[Instruments_OHLC]
                WHERE [InstrumentToken] = @InstrumentToken 
                  AND [TimeFrame] = @TimeFrame
                  AND (@StartDate IS NULL OR [TimeStamp] >= @StartDate)
                  AND (@EndDate IS NULL OR [TimeStamp] <= @EndDate)
                ORDER BY [TimeStamp] ASC;";

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@InstrumentToken", instrumentToken);
                cmd.Parameters.AddWithValue("@TimeFrame", timeFrame);
                cmd.Parameters.AddWithValue("@StartDate", (object?)startDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@EndDate", (object?)endDate ?? DBNull.Value);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        candles.Add(new ExtendedCandle
                        {
                            InstrumentToken = reader.GetInt64(0),
                            Open = reader.GetDecimal(1),
                            Close = reader.GetDecimal(2),
                            High = reader.GetDecimal(3),
                            Low = reader.GetDecimal(4),
                            OI = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                            Volume = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                            TimeStamp = reader.GetDateTime(7),
                            TimeFrame = reader.GetString(8)
                        });
                    }
                }
            }

            return candles;
        }

        private List<PivotData> FetchPivots(long instrumentToken, string timeFrame, DateTime? startDate, DateTime? endDate)
        {
            var pivots = new List<PivotData>();

            string query = @"
                SELECT [InstrumentToken], [TimeStamp], [TimeFrame], [PP], 
                       [R1], [R2], [R3], [R4], [R5], 
                       [S1], [S2], [S3], [S4], [S5], 
                       [PPR1], [PPS1], [Ref_Resistance], [Ref_Support],
                       [Ref_StartCandle_Open], [Ref_StartCandle_High], [Ref_StartCandle_Low], [Ref_StartCandle_Close],
                       [Ref_EndCandle_Open], [Ref_EndCandle_High], [Ref_EndCandle_Low], [Ref_EndCandle_Close]
                FROM [Algo_Trading_CFCore].[dbo].[Instruments_CalculatedPivots]
                WHERE [InstrumentToken] = @InstrumentToken 
                  AND [TimeFrame] IN ('day','week')
AND (@StartDate IS NULL OR [TimeStamp] >= @StartDate)
  AND (@EndDate IS NULL OR [TimeStamp] <= @EndDate)
                ORDER BY [TimeStamp] ASC;";

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@InstrumentToken", instrumentToken);
                cmd.Parameters.AddWithValue("@TimeFrame", timeFrame);
                cmd.Parameters.AddWithValue("@StartDate", (object?)startDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@EndDate", (object?)endDate ?? DBNull.Value);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        pivots.Add(new PivotData
                        {
                            InstrumentToken = reader.GetInt64(0),
                            TimeStamp = reader.GetDateTime(1),
                            TimeFrame = reader.GetString(2),
                            PP = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                            R1 = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                            R2 = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                            R3 = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                            R4 = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                            R5 = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                            S1 = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                            S2 = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                            S3 = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                            S4 = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                            S5 = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                            PPR1 = reader.IsDBNull(14) ? false : reader.GetBoolean(14),
                            PPS1 = reader.IsDBNull(15) ? false : reader.GetBoolean(15),
                            RefResistance = reader.IsDBNull(16) ? 0m : reader.GetDecimal(16),
                            RefSupport = reader.IsDBNull(17) ? 0m : reader.GetDecimal(17),

                            // Reference Start Candle Mapping
                            RefStartCandleOpen = reader.IsDBNull(18) ? 0m : reader.GetDecimal(18),
                            RefStartCandleHigh = reader.IsDBNull(19) ? 0m : reader.GetDecimal(19),
                            RefStartCandleLow = reader.IsDBNull(20) ? 0m : reader.GetDecimal(20),
                            RefStartCandleClose = reader.IsDBNull(21) ? 0m : reader.GetDecimal(21),

                            // Reference End Candle Mapping
                            RefEndCandleOpen = reader.IsDBNull(22) ? 0m : reader.GetDecimal(22),
                            RefEndCandleHigh = reader.IsDBNull(23) ? 0m : reader.GetDecimal(23),
                            RefEndCandleLow = reader.IsDBNull(24) ? 0m : reader.GetDecimal(24),
                            RefEndCandleClose = reader.IsDBNull(25) ? 0m : reader.GetDecimal(25)
                        });
                    }
                }
            }

            return pivots;
        }
    }
}