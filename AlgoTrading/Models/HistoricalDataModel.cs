namespace AlgoTrading.Models
{
    using AlgoTrading.Helpers;
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
            FifteenMinutes,
            Hour,
            Daily
        };
    }

    public sealed class InstrumentHistory
    {
        public int InstrumentToken { get; init; }

        public Dictionary<string, List<Candle>> Intervals { get; } = new Dictionary<string, List<Candle>>();
    }

    /// <summary>Outcome of one historical backfill pass across every requested instrument and interval.</summary>
    internal sealed class HistoricalBackfillReport
    {
        private readonly List<string> failures;

        internal HistoricalBackfillReport(int requestedChunks, int completedChunks, int storedChunks, bool budgetExhausted,
            List<string> failures)
        {
            this.RequestedChunks = requestedChunks;
            this.CompletedChunks = completedChunks;
            this.StoredChunks = storedChunks;
            this.BudgetExhausted = budgetExhausted;
            this.failures = failures;
        }

        internal int RequestedChunks { get; }

        internal int CompletedChunks { get; }

        internal int StoredChunks { get; }

        internal bool BudgetExhausted { get; }

        internal IReadOnlyList<string> Failures
        {
            get
            {
                return this.failures.AsReadOnly();
            }
        }

        internal bool IsComplete
        {
            get
            {
                return this.failures.Count == 0 && !this.BudgetExhausted;
            }
        }

        /// <summary>Failures are never discarded silently; they are handed to the caller as an exception.</summary>
        internal void ThrowIfIncomplete()
        {
            if (this.failures.Count == 0)
            {
                return;
            }

            const int maximumReported = 10;
            int reported = this.failures.Count < maximumReported ? this.failures.Count : maximumReported;
            List<string> lines = new List<string>();
            for (int index = 0; index < reported; index++)
            {
                lines.Add(this.failures[index]);
            }

            string suffix = this.failures.Count > reported
                ? " (+" + (this.failures.Count - reported).ToString(CultureInfo.InvariantCulture) + " more)"
                : string.Empty;
            throw new InvalidOperationException("Historical backfill failed for " + this.failures.Count.ToString(CultureInfo.InvariantCulture)
                + " chunk(s): " + string.Join(" | ", lines) + suffix);
        }
    }

    /// <summary>
    /// Historical (peace-time) ingestion of Zerodha Kite candles. The 2-hour default budget keeps the
    /// backfill deliberately slow so live throughput is never competing with historical requests.
    /// </summary>
    internal sealed class HistoricalDataModel
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
        /// Fetches every requested range once, stores each completed chunk immediately and never hides a failure.
        /// Partial progress survives a thrown report so a later pass can resume from the stored chunks.
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

            string[] requestedIntervals = intervals ?? HistoryCandleInterval.AllIntervals;
            DateTime marketNow = MarketTimestamp.ToDatabase(MarketTimestamp.FromDateTime(DateTime.UtcNow));

            TimeSpan budget = this.BackfillBudget > TimeSpan.Zero ? this.BackfillBudget : DefaultBackfillBudget;
            using (CancellationTokenSource backfillBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                backfillBudget.CancelAfter(budget);
                List<string> failures = new List<string>();
                int requestedChunks = 0;
                int completedChunks = 0;
                int storedChunks = 0;
                bool budgetExhausted = false;

                for (int tokenIndex = 0; tokenIndex < instrumentTokens.Length && !budgetExhausted; tokenIndex++)
                {
                    int instrumentToken = instrumentTokens[tokenIndex];
                    for (int intervalIndex = 0; intervalIndex < requestedIntervals.Length && !budgetExhausted; intervalIndex++)
                    {
                        string interval = requestedIntervals[intervalIndex];
                        DateTime rangeStart = marketNow.AddYears(-years);
                        while (rangeStart < marketNow && !budgetExhausted)
                        {
                            if (backfillBudget.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                budgetExhausted = true;
                                this.ReportProgress("Backfill budget of " + budget + " elapsed; resuming is required.");
                                break;
                            }

                            DateTime rangeEnd = rangeStart.AddDays(GetChunkDays(interval));
                            if (rangeEnd > marketNow)
                            {
                                rangeEnd = marketNow;
                            }

                            requestedChunks++;
                            try
                            {
                                string json = await this.DownloadChunkAsync(instrumentToken, interval, rangeStart, rangeEnd, backfillBudget.Token);
                                if (!string.IsNullOrWhiteSpace(json))
                                {
                                    await SqlCandleBulkUploader.BulkUploadCandleJsonAsync(this.ConnectionString, instrumentToken, interval, json, backfillBudget.Token);
                                    storedChunks++;
                                }
                                completedChunks++;
                            }
                            catch (OperationCanceledException)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                budgetExhausted = true;
                                this.ReportProgress("Backfill budget of " + budget + " elapsed; resuming is required.");
                                break;
                            }
                            catch (Exception exception)
                            {
                                string description = Describe(instrumentToken, interval, rangeStart, rangeEnd);
                                failures.Add(description + ": " + exception.Message);
                                this.ReportProgress("Failed " + description + ": " + exception.Message);
                            }

                            rangeStart = rangeEnd.AddDays(1);
                        }
                    }
                }

                HistoricalBackfillReport report = new HistoricalBackfillReport(requestedChunks, completedChunks, storedChunks,
                    budgetExhausted, failures);
                report.ThrowIfIncomplete();
                return report;
            }
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

        private async Task<string> DownloadChunkAsync(int instrumentToken, string interval, DateTime from, DateTime to,
            CancellationToken cancellationToken)
        {
            string url = BaseUrl + instrumentToken.ToString(CultureInfo.InvariantCulture) + "/" + GetApiInterval(interval)
                + "?from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + "&to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

        private static int GetChunkDays(string interval)
        {
            if (interval == HistoryCandleInterval.OneMinute)
            {
                return 30;
            }

            if (interval == HistoryCandleInterval.ThreeMinutes)
            {
                return 60;
            }

            if (interval == HistoryCandleInterval.FiveMinutes)
            {
                return 80;
            }

            if (interval == HistoryCandleInterval.FifteenMinutes)
            {
                return 180;
            }

            if (interval == HistoryCandleInterval.Hour)
            {
                return 365;
            }

            return 800;
        }

        /// <summary>Kite names the hourly historical interval "60minute"; "hour" is only this application's label.</summary>
        private static string GetApiInterval(string interval)
        {
            if (interval == HistoryCandleInterval.Hour)
            {
                return "60minute";
            }

            return interval;
        }

        private static string Describe(int instrumentToken, string interval, DateTime from, DateTime to)
        {
            return instrumentToken.ToString(CultureInfo.InvariantCulture) + "/" + interval + " "
                + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".."
                + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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
