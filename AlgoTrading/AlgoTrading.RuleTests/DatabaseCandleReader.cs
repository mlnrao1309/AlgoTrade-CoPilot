using AlgoTrading.Models;
using AlgoTrading.Models.Rules;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;

internal sealed class DatabaseCandleReader
{
    internal async Task<IReadOnlyList<DatabaseCandleRow>> ReadAsync(DatabaseCrossoverSettings settings, DateTimeOffset asOf)
    {
        TimeZoneInfo timestampTimezone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimestampTimeZoneId);
        var connectionSettings = new SqlConnectionStringBuilder(settings.ConnectionString) { ConnectTimeout = 15 };
        await using var connection = new SqlConnection(connectionSettings.ConnectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = """
            SELECT [InstrumentToken], [Open], [Close], [High], [Low], [OI], [Volume],
                   [TimeStamp], [LastUpdated], [TimeFrame]
            FROM [Algo_Trading_CFCore].[dbo].[Instruments_OHLC]
            WHERE [InstrumentToken] = @InstrumentToken AND [TimeFrame] = @Timeframe
            ORDER BY [TimeStamp] ASC;
            """;
        command.Parameters.Add("@InstrumentToken", SqlDbType.Int).Value = settings.InstrumentToken;
        command.Parameters.Add("@Timeframe", SqlDbType.VarChar, 30).Value = settings.Timeframe;
        var rows = new List<DatabaseCandleRow>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        DateTimeOffset? previousCloseTime = null;
        int excludedIncompleteRows = 0;
        while (await reader.ReadAsync())
        {
            int instrumentToken = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            decimal openingPrice = Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
            decimal closingPrice = Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
            decimal highestPrice = Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
            decimal lowestPrice = Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
            long? openInterest = reader.IsDBNull(5) ? null : Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture);
            long volume = Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture);
            DateTime timestamp = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Unspecified);
            DateTime? lastUpdated = reader.IsDBNull(8) ? null : reader.GetDateTime(8);
            string timeframe = reader.GetString(9);
            DateTimeOffset timestampInstant = new(TimeZoneInfo.ConvertTimeToUtc(timestamp, timestampTimezone));
            DateTimeOffset closedAt = settings.TimestampRepresentsCandleOpen
                ? timestampInstant.AddMinutes(settings.CandleLengthMinutes) : timestampInstant;
            if (closedAt > asOf) { excludedIncompleteRows++; continue; }
            if (previousCloseTime.HasValue && closedAt <= previousCloseTime.Value)
                throw new InvalidOperationException($"Duplicate or unordered database candle at {timestamp:O}; resolve the source data before replay.");
            if (highestPrice < lowestPrice || highestPrice < Math.Max(openingPrice, closingPrice)
                || lowestPrice > Math.Min(openingPrice, closingPrice) || volume < 0)
                throw new InvalidOperationException($"Invalid candle prices or volume at {timestamp:O}.");
            var candle = new Candle(instrumentToken, settings.Timeframe, timestampInstant.UtcDateTime,
                openingPrice, highestPrice, lowestPrice, closingPrice, volume);
            rows.Add(new DatabaseCandleRow(instrumentToken, openingPrice, closingPrice, highestPrice, lowestPrice,
                openInterest, volume, timestamp, lastUpdated, timeframe, new CompletedCandle(candle, closedAt)));
            previousCloseTime = closedAt;
        }
        Console.WriteLine($"Loaded {rows.Count} completed database candles; excluded {excludedIncompleteRows} unfinished or future rows.");
        return rows.AsReadOnly();
    }
}
