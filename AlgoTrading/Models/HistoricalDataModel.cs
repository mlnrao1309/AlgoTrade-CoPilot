
namespace AlgoTrading.Models
{
    using AlgoTrading.Helpers;
    using Microsoft.Data.SqlClient;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;

    public class HistoryCandleInterval
    {
        public const string OneMinute = "minute";
        public const string ThreeMinutes = "3minute";
        public const string FiveMinutes = "5minute";
        public const string TenMinutes = "10minute";
        public const string FifteenMinutes = "15minute";
        public const string ThirtyMinutes = "30minute";
        public const string Hour = "hour";
        public const string Daily = "day";

        public static readonly string[] AllIntervals =
        [
            OneMinute,
        ThreeMinutes,
        FiveMinutes,
        TenMinutes,
        FifteenMinutes,
        Hour,
        Daily
        ];
    }

    public sealed class InstrumentHistory
    {
        public int InstrumentToken { get; init; }

        public Dictionary<string, List<Candle>> Intervals { get; } = new();
    }
    internal class HistoricalDataModel
    {
        string baseUrl = "https://kite.zerodha.com/oms/instruments/historical/";

        private Dictionary<int, List<Candle>> _historicalData = new Dictionary<int, List<Candle>>();

        public Dictionary<int, List<Candle>> HistoricalData { get => _historicalData; }

        private readonly string? _accessToken = null;

        public string? AccessToken { get => _accessToken; }

        private const int ChunkSizeInDays = 80; // Zerodha limit for 5min interval is ~100 days; we use 80-day chunks for safety

        private readonly HttpClient _httpClient;

        public string ConnectionString { get; set; } // Replace with your actual connection string
        public HistoricalDataModel(
            HttpClient httpClient,
            string accessToken)
        {
            _httpClient = httpClient;
            _accessToken = accessToken;
        }
        private int GetChunkDays(string interval) =>
    interval switch
    {
        HistoryCandleInterval.OneMinute => 30,
        HistoryCandleInterval.ThreeMinutes => 60,
        HistoryCandleInterval.FiveMinutes => 80,
        HistoryCandleInterval.FifteenMinutes => 180,
        HistoryCandleInterval.Hour => 365,
        _ => 800
    };

        public async Task GetHistoricalDataFromApiAsync(int[] instrumentToken, string[]? intervals, int years = 5,
    CancellationToken cancellationToken = default)
        {
            if (instrumentToken == null || instrumentToken.Length == 0)
            {
                throw new ArgumentException("Instrument token array cannot be null or empty.", nameof(instrumentToken));
            }
            if (years <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(years),
                    "Years must be greater than zero.");

            string[] intervals_iterate = intervals ?? HistoryCandleInterval.AllIntervals;


            // Define date range to fetch data for the last 'years' years
            DateTime endDate = DateTime.Now;

            //using (var client = new System.Net.Http.HttpClient())
            //{
            //    client.DefaultRequestHeaders.Add("Authorization", $"enctoken {AccessToken}");

            foreach (var token in instrumentToken)
            {
                foreach (var interval in intervals_iterate)
                {
                    DateTime currentStart = endDate.AddYears(-years);
                    while (currentStart < endDate)
                    {
                        DateTime currentEnd = currentStart.AddDays(GetChunkDays(interval));
                        if (currentEnd > endDate)
                        {
                            currentEnd = endDate;
                        }

                        // Format dates as YYYY-MM-DD
                        string fromStr = currentStart.ToString("yyyy-MM-dd");
                        string toStr = currentEnd.ToString("yyyy-MM-dd");

                        string url = $"{baseUrl}{token}/{interval}?from={fromStr}&to={toStr}";
                        await _httpClient.GetAsync(url, cancellationToken);

                        using (System.Net.Http.HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken))
                            if (response.IsSuccessStatusCode)
                            {
                                string json = await response.Content.ReadAsStringAsync();
                                await SqlCandleBulkUploader.BulkUploadCandleJsonAsync(ConnectionString, token, interval, json);
                            }
                            else
                            {
                                // Handle rate limits or session errors
                                Console.WriteLine($"Failed to fetch range {fromStr} to {toStr}: {response.StatusCode}");
                            }
                        // Mandatory delay to respect Zerodha's 3 req/sec rate limit
                        await Task.Delay(1000, cancellationToken);
                        // Advance the start date for the next chunk
                        currentStart = currentEnd.AddDays(1);

                    }
                }
            }
        }
    }
}
