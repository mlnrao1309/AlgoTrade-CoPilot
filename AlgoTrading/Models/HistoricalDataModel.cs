namespace AlgoTrading.Models
{
    using AlgoTrading.DataAccess.Historical;
    using AlgoTrading.Models.MarketData.Ingestion;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;

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

        public static readonly string[] AllIntervals = new string[]
        {
            FiveMinutes,
            Daily
        };
    }

    public sealed class InstrumentHistory
    {
        public int InstrumentToken { get; init; }

        public Dictionary<string, List<Candle>> Intervals { get; } = new Dictionary<string, List<Candle>>();
    }

    /// <summary>
    /// Historical (peace-time) ingestion of Zerodha Kite candles. The 2-hour default budget keeps the
    /// backfill deliberately slow so live throughput is never competing with historical requests.
    /// </summary>
    internal sealed class HistoricalDataModel : IHistoricalChunkSource
    {
        private const int RequestsPerSecond = 3;
        private const int MaximumAttempts = 4;
        private const int TooManyRequestsStatus = 429;
        private static readonly TimeSpan DefaultBackfillBudget = TimeSpan.FromHours(2);
        private const string BaseUrl = "https://kite.zerodha.com/oms/instruments/historical/";

        private readonly HttpClient httpClient;
        private readonly KiteRequestPacer pacer;
        private readonly string? accessToken;
        private readonly Dictionary<int, List<Candle>> historicalData;

        public HistoricalDataModel(HttpClient httpClient, string accessToken)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            this.httpClient = httpClient;
            this.accessToken = accessToken;
            this.pacer = new KiteRequestPacer(RequestsPerSecond);
            this.historicalData = new Dictionary<int, List<Candle>>();
            this.BackfillBudget = DefaultBackfillBudget;
            this.ConnectionString = string.Empty;
        }

        public Dictionary<int, List<Candle>> HistoricalData
        {
            get
            {
                return this.historicalData;
            }
        }

        public string? AccessToken
        {
            get
            {
                return this.accessToken;
            }
        }

        public string ConnectionString { get; set; }

        /// <summary>Peace-time window in which historical chunks may be fetched. Exhausting it is not an error.</summary>
        public TimeSpan BackfillBudget { get; set; }

        public Action<string>? Progress { get; set; }

        /// <summary>
        /// Compatibility entry point for callers that specify a relative range. New background callers should use
        /// the explicit-range overload so configured source coverage, rather than an assumed current date, controls
        /// the requested range.
        /// </summary>
        public async Task<HistoricalBackfillReport> GetHistoricalDataFromApiAsync(int[] instrumentTokens, string[]? intervals,
            int years = 5, CancellationToken cancellationToken = default)
        {
            if (instrumentTokens == null || instrumentTokens.Length == 0)
            {
                throw new ArgumentException("Instrument token array cannot be null or empty.", nameof(instrumentTokens));
            }

            if (years <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(years), "Years must be greater than zero.");
            }

            DateTime marketNow = MarketTimestamp.ToDatabase(MarketTimestamp.FromDateTime(DateTime.UtcNow));
            TimeSpan budget = this.BackfillBudget > TimeSpan.Zero ? this.BackfillBudget : DefaultBackfillBudget;
            IReadOnlyList<string> streamTypes = ConvertIntervalsToSourceStreams(intervals);
            HistoricalBackfillRequest request = new HistoricalBackfillRequest(marketNow.AddYears(-years), marketNow,
                marketNow.AddYears(-years), marketNow, streamTypes, budget, false);
            return await this.GetHistoricalDataFromApiAsync(instrumentTokens, request, cancellationToken);
        }

        /// <summary>Runs a resumable backfill over the intersection of requested and configured source coverage.</summary>
        public async Task<HistoricalBackfillReport> GetHistoricalDataFromApiAsync(int[] instrumentTokens,
            HistoricalBackfillRequest request, CancellationToken cancellationToken = default)
        {
            if (instrumentTokens == null || instrumentTokens.Length == 0)
            {
                throw new ArgumentException("Instrument token array cannot be null or empty.", nameof(instrumentTokens));
            }

            IHistoricalIngestionRepository repository = new SqlHistoricalIngestionRepository(this.ConnectionString);
            HistoricalBackfillCoordinator coordinator = new HistoricalBackfillCoordinator(this, repository,
                new SystemHistoricalBackfillClock());
            coordinator.Progress = this.Progress;
            return await coordinator.RunAsync(instrumentTokens, request, cancellationToken);
        }

        private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
        {
            if (response.Headers.RetryAfter != null)
            {
                if (response.Headers.RetryAfter.Delta.HasValue)
                {
                    return response.Headers.RetryAfter.Delta.Value;
                }

                if (response.Headers.RetryAfter.Date.HasValue)
                {
                    TimeSpan difference = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (difference > TimeSpan.Zero)
                    {
                        return difference;
                    }
                }
            }

            return TimeSpan.FromSeconds(Math.Pow(2.0, attempt));
        }

        public async Task<string> DownloadChunkAsync(HistoricalChunkRequest chunk, CancellationToken cancellationToken)
        {
            string url = BaseUrl + chunk.InstrumentToken.ToString(CultureInfo.InvariantCulture) + "/"
                + chunk.ProviderInterval + "?from="
                + chunk.RangeStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + "&to=" + chunk.RangeEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            Exception? lastFailure = null;
            for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                await this.pacer.WaitForTurnAsync(cancellationToken);
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        if (!string.IsNullOrWhiteSpace(this.accessToken))
                        {
                            request.Headers.TryAddWithoutValidation("Authorization", "enctoken " + this.accessToken);
                        }

                        using (HttpResponseMessage response = await this.httpClient.SendAsync(request,
                            HttpCompletionOption.ResponseContentRead, cancellationToken))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                return await response.Content.ReadAsStringAsync(cancellationToken);
                            }

                            int statusCode = (int)response.StatusCode;
                            bool retryable = statusCode == TooManyRequestsStatus || statusCode >= 500;
                            if (!retryable || attempt == MaximumAttempts)
                            {
                                throw new HttpRequestException("Kite rejected the historical request with " + statusCode
                                    + " (" + response.ReasonPhrase + ") for " + url);
                            }

                            TimeSpan delay = GetRetryDelay(response, attempt);
                            this.ReportProgress("Kite returned " + statusCode + "; retrying in " + delay + ". " + url);
                            await Task.Delay(delay, cancellationToken);
                        }
                    }
                }
                catch (HttpRequestException exception) when (attempt < MaximumAttempts)
                {
                    lastFailure = exception;
                    TimeSpan backoff = TimeSpan.FromSeconds(Math.Pow(2.0, attempt));
                    this.ReportProgress("Historical request failed; retrying in " + backoff + ". " + url);
                    await Task.Delay(backoff, cancellationToken);
                }
            }

            throw new HttpRequestException("Historical request failed after " + MaximumAttempts + " attempts: " + url, lastFailure);
        }

        private static IReadOnlyList<string> ConvertIntervalsToSourceStreams(string[]? intervals)
        {
            string[] requestedIntervals = intervals ?? HistoryCandleInterval.AllIntervals;
            List<string> streamTypes = new List<string>();
            for (int index = 0; index < requestedIntervals.Length; index++)
            {
                string interval = requestedIntervals[index];
                if (interval == HistoryCandleInterval.FiveMinutes)
                {
                    streamTypes.Add(HistoricalStreamTypes.IntradayFiveMinute);
                }
                else if (interval == HistoryCandleInterval.Daily)
                {
                    streamTypes.Add(HistoricalStreamTypes.DailyOneDay);
                }
                else
                {
                    throw new ArgumentException("Historical ingestion accepts only authoritative 5-minute and daily "
                        + "source streams. Derived timeframe requested: " + interval + ".", nameof(intervals));
                }
            }

            return streamTypes.AsReadOnly();
        }

        private void ReportProgress(string message)
        {
            Action<string>? progress = this.Progress;
            if (progress != null)
            {
                progress(message);
            }
        }
    }
}
