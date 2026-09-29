using System.Globalization;

namespace AlgoTrading.Models.MarketData.Ingestion
{
    /// <summary>Plans contiguous ranges and advances durable state only through the repository transaction.</summary>
    public sealed class HistoricalBackfillCoordinator
    {
        private readonly IHistoricalChunkSource source;
        private readonly IHistoricalIngestionRepository repository;
        private readonly IHistoricalBackfillClock clock;

        public HistoricalBackfillCoordinator(IHistoricalChunkSource source, IHistoricalIngestionRepository repository,
            IHistoricalBackfillClock clock)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            this.source = source;
            this.repository = repository;
            this.clock = clock;
        }

        public Action<string>? Progress { get; set; }

        public async Task<HistoricalBackfillReport> RunAsync(IReadOnlyList<int> instrumentTokens,
            HistoricalBackfillRequest request, CancellationToken cancellationToken)
        {
            if (instrumentTokens == null || instrumentTokens.Count == 0)
            {
                throw new ArgumentException("Instrument tokens cannot be null or empty.", nameof(instrumentTokens));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            DateTime effectiveStart = request.SourceCoverageStart;
            if (request.RequestedStart > effectiveStart)
            {
                effectiveStart = request.RequestedStart;
            }

            DateTime effectiveEnd = request.SourceCoverageEnd;
            if (request.RequestedEnd < effectiveEnd)
            {
                effectiveEnd = request.RequestedEnd;
            }
            if (effectiveEnd < effectiveStart)
            {
                return new HistoricalBackfillReport(0, 0, 0, false, new List<string>());
            }

            DateTime startedUtc = this.clock.UtcNow;
            List<string> failures = new List<string>();
            int requestedChunks = 0;
            int completedChunks = 0;
            int storedChunks = 0;
            bool budgetExhausted = false;

            using (CancellationTokenSource budgetCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                budgetCancellation.CancelAfter(request.Budget);
                for (int tokenIndex = 0; tokenIndex < instrumentTokens.Count && !budgetExhausted; tokenIndex++)
                {
                    int instrumentToken = instrumentTokens[tokenIndex];
                    if (instrumentToken <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(instrumentTokens), "Instrument tokens must be positive.");
                    }

                    for (int streamIndex = 0; streamIndex < request.StreamTypes.Count && !budgetExhausted; streamIndex++)
                    {
                        string streamType = request.StreamTypes[streamIndex];
                        DateTime rangeStart = effectiveStart;
                        if (!request.RefreshCorrections)
                        {
                            HistoricalIngestionCheckpoint? checkpoint = await this.repository.GetCheckpointAsync(
                                instrumentToken, streamType, budgetCancellation.Token);
                            if (checkpoint != null && checkpoint.ChunkEnd >= rangeStart)
                            {
                                rangeStart = checkpoint.ChunkEnd.AddDays(1.0);
                            }
                        }

                        while (rangeStart <= effectiveEnd)
                        {
                            if (this.HasBudgetExpired(startedUtc, request.Budget, budgetCancellation.Token,
                                cancellationToken))
                            {
                                budgetExhausted = true;
                                this.ReportProgress("Backfill budget of " + request.Budget
                                    + " elapsed; persisted checkpoints remain resumable.");
                                break;
                            }

                            int chunkDays = HistoricalStreamTypes.GetChunkDays(streamType);
                            DateTime rangeEnd = rangeStart.AddDays(chunkDays - 1);
                            if (rangeEnd > effectiveEnd)
                            {
                                rangeEnd = effectiveEnd;
                            }

                            HistoricalChunkRequest chunk = new HistoricalChunkRequest(instrumentToken, streamType,
                                rangeStart, rangeEnd);
                            requestedChunks++;
                            try
                            {
                                string payload = await this.source.DownloadChunkAsync(chunk, budgetCancellation.Token);
                                await this.repository.PersistChunkAndCheckpointAsync(chunk, payload,
                                    budgetCancellation.Token);
                                completedChunks++;
                                storedChunks++;
                                rangeStart = rangeEnd.AddDays(1.0);
                            }
                            catch (OperationCanceledException)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                budgetExhausted = true;
                                this.ReportProgress("Backfill budget of " + request.Budget
                                    + " elapsed; persisted checkpoints remain resumable.");
                                break;
                            }
                            catch (Exception exception)
                            {
                                string description = Describe(chunk);
                                failures.Add(description + ": " + exception.Message);
                                this.ReportProgress("Failed " + description + ": " + exception.Message);
                                break;
                            }
                        }
                    }
                }
            }

            HistoricalBackfillReport report = new HistoricalBackfillReport(requestedChunks, completedChunks,
                storedChunks, budgetExhausted, failures);
            report.ThrowIfIncomplete();
            return report;
        }

        private bool HasBudgetExpired(DateTime startedUtc, TimeSpan budget, CancellationToken budgetToken,
            CancellationToken callerToken)
        {
            if (!budgetToken.IsCancellationRequested && this.clock.UtcNow - startedUtc < budget)
            {
                return false;
            }

            callerToken.ThrowIfCancellationRequested();
            return true;
        }

        private static string Describe(HistoricalChunkRequest request)
        {
            return request.InstrumentToken.ToString(CultureInfo.InvariantCulture) + "/" + request.StreamType + " "
                + request.RangeStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".."
                + request.RangeEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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
