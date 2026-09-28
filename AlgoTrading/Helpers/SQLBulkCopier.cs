namespace AlgoTrading.Helpers
{
    using AlgoTrading.Models;
    using Microsoft.Data.SqlClient;
    using System;
    using System.Data;
    using System.Globalization;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// High-speed ingestion writers. The legacy Instruments_OHLC table stores TimeStamp as an IST wall time,
    /// TimeFrame as the short code returned by GetTimeFrame, OHLC as decimal(18,8) and Volume/OI as bigint.
    /// </summary>
    public static class SqlCandleBulkUploader
    {
        private static string GetTimeFrame(string interval) =>
interval switch
{
    HistoryCandleInterval.OneMinute => "1",
    HistoryCandleInterval.ThreeMinutes => "3",
    HistoryCandleInterval.FiveMinutes => "5",
    HistoryCandleInterval.FifteenMinutes => "15",
    HistoryCandleInterval.Hour => "60",
    _ => "D"
};
        /// <summary>Zerodha sends an offset timestamp; persistence uses the application's IST wall-time convention.</summary>
        private static DateTime ToIstWallTime(string timestamp)
        {
            DateTimeOffset instant = DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None);
            return MarketTimestamp.ToDatabase(instant);
        }

        public static async Task BulkUploadCandleJsonAsync(
            string connectionString,
            long instrumentToken,
            string timeframe,
            string jsonResponse,
            CancellationToken cancellationToken = default)
        {

            // 1. Prepare an In-Memory DataTable matching the Target SQL Schema
            using var table = new DataTable();
            table.Columns.Add("InstrumentToken", typeof(long));
            table.Columns.Add("TimeStamp", typeof(DateTime));
            table.Columns.Add("TimeFrame", typeof(string));
            table.Columns.Add("Open", typeof(double));
            table.Columns.Add("High", typeof(double));
            table.Columns.Add("Low", typeof(double));
            table.Columns.Add("Close", typeof(double));
            table.Columns.Add("Volume", typeof(long));
            table.Columns.Add("OI", typeof(long)).AllowDBNull = true;

            // 2. Fast Parse JSON using System.Text.Json Document
            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
            {
                JsonElement root = doc.RootElement;

                // Navigate to data.candles array in Zerodha payload
                if (root.TryGetProperty("data", out JsonElement dataElement) &&
                    dataElement.TryGetProperty("candles", out JsonElement candlesElement))
                {
                    foreach (JsonElement candle in candlesElement.EnumerateArray())
                    {
                        // Zerodha Candle Format: 
                        // [ Timestamp (string), Open, High, Low, Close, Volume, OpenInterest (optional) ]
                        string? timeStr = candle[0].GetString();
                        if (string.IsNullOrWhiteSpace(timeStr))
                        {
                            throw new InvalidOperationException("A Zerodha candle row did not carry a timestamp.");
                        }

                        DateTime timestamp = ToIstWallTime(timeStr);
                        double open = candle[1].GetDouble();
                        double high = candle[2].GetDouble();
                        double low = candle[3].GetDouble();
                        double close = candle[4].GetDouble();
                        long volume = candle[5].GetInt64();

                        // Handle OI if present in array (Options), otherwise default to 0
                        long openInterest = candle.GetArrayLength() > 6 ? candle[6].GetInt64() : 0;

                        // Append row to in-memory DataTable
                        table.Rows.Add(instrumentToken, timestamp, GetTimeFrame(timeframe), open, high, low, close, volume, openInterest);
                    }
                }
            }

            if (table.Rows.Count == 0) return;
            foreach (DataRow item in table.Rows)
            {
                if (item.IsNull("OI"))
                    item["OI"] = 0;
            }

            // 3. Perform High-Speed SqlBulkCopy
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.UseInternalTransaction, null))
                {
                    bulkCopy.DestinationTableName = "dbo.Instruments_OHLC";
                    bulkCopy.BatchSize = 10000;
                    bulkCopy.BulkCopyTimeout = 60; // 60 seconds

                    // Map C# DataTable columns to SQL Server Table columns
                    bulkCopy.ColumnMappings.Add("InstrumentToken", "InstrumentToken");
                    bulkCopy.ColumnMappings.Add("TimeStamp", "TimeStamp");
                    bulkCopy.ColumnMappings.Add("TimeFrame", "TimeFrame");
                    bulkCopy.ColumnMappings.Add("Open", "Open");
                    bulkCopy.ColumnMappings.Add("High", "High");
                    bulkCopy.ColumnMappings.Add("Low", "Low");
                    bulkCopy.ColumnMappings.Add("Close", "Close");
                    bulkCopy.ColumnMappings.Add("Volume", "Volume");
                    bulkCopy.ColumnMappings.Add("OI", "OI");
                    // Execute the stream directly to the database engine; failures must surface, never be swallowed.
                    await bulkCopy.WriteToServerAsync(table, cancellationToken);
                }
            }
        }

        //        As an expert of MS SQL Server,


        //CREATE TABLE[dbo].[Indicator_config]
        //        (

        //    [Indicator][nvarchar](50) NOT NULL,

        //    [Parameter1] [decimal](18, 2) NOT NULL,

        //    [Parameter2] [decimal](18, 2) NULL,
        //	[IsActive][bit] NULL,
        //	[ParentId][nvarchar] (50) NULL)

        //Indicator Parameter1  Parameter2 IsActive    ParentId
        //BB  20.00	2.00	1	NULL
        //EMA 5.00    NULL	1	NULL
        //RSI 14.00   NULL	1	NULL
        //SMA 5.00    NULL	1	EMA
        //SuperTrend  10.00	3.00	1	NULL

        //public static async Task BulkUploadCandleJsonAsync(
        //string connectionString,
        //long instrumentToken,
        //string timeframe,
        //string jsonResponse)
        //{

        //}

        // Instrument master writes are owned by IInstrumentImportService; the duplicate, unsafe
        // SqlBulkCopy implementation that wrote Instruments_FO rows into Instruments_EQ was removed.
    }

}
