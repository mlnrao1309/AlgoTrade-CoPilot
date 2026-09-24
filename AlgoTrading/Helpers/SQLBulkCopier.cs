namespace AlgoTrading.Helpers
{
    using System;
    using System.Data;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;

    public static class SqlCandleBulkUploader
    {
        public static async Task BulkUploadCandleJsonAsync(
            string connectionString,
            long instrumentToken,
            string timeframe,
            string jsonResponse)
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
                        string timeStr = candle[0].GetString();
                        DateTime timestamp = DateTime.Parse(timeStr);
                        double open = candle[1].GetDouble();
                        double high = candle[2].GetDouble();
                        double low = candle[3].GetDouble();
                        double close = candle[4].GetDouble();
                        long volume = candle[5].GetInt64();

                        // Handle OI if present in array (Options), otherwise default to 0
                        long openInterest = candle.GetArrayLength() > 6 ? candle[6].GetInt64() : 0;

                        // Append row to in-memory DataTable
                        table.Rows.Add(instrumentToken, timestamp, timeframe, open, high, low, close, volume, openInterest);
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
                await connection.OpenAsync();

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
                    try
                    {
                        // Execute stream directly to database engine
                        await bulkCopy.WriteToServerAsync(table);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
        }

        //public static async Task BulkUploadCandleJsonAsync(
        //string connectionString,
        //long instrumentToken,
        //string timeframe,
        //string jsonResponse)
        //{

        //}

    }
}
