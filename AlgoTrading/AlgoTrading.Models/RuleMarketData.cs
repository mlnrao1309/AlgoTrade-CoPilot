namespace AlgoTrading.Models.Rules;

public sealed class RuleMarketData
{
    private readonly Dictionary<string, CompletedCandle[]> series = new(StringComparer.Ordinal);
    public int InstrumentToken { get; }

    public RuleMarketData(int instrumentToken, IReadOnlyDictionary<string, IReadOnlyList<CompletedCandle>> completedSeries)
    {
        ArgumentNullException.ThrowIfNull(completedSeries);
        InstrumentToken = instrumentToken;
        foreach (var entry in completedSeries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Key);
            ArgumentNullException.ThrowIfNull(entry.Value);
            CompletedCandle[] candles = entry.Value.ToArray();
            for (int candleIndex = 0; candleIndex < candles.Length; candleIndex++)
            {
                CompletedCandle current = candles[candleIndex] ?? throw new ArgumentException("A completed candle cannot be null.");
                if (current.Candle.InstrumentToken != instrumentToken)
                    throw new ArgumentException("All candles must belong to the requested instrument.");
                if (candleIndex > 0 && candles[candleIndex - 1].ClosedAt >= current.ClosedAt)
                    throw new ArgumentException($"Completed candles for '{entry.Key}' must have unique, increasing close times.");
            }
            series.Add(entry.Key, candles);
        }
    }

    internal CompletedCandle[] GetSeries(string timeframe) => series.TryGetValue(timeframe, out var candles)
        ? candles : Array.Empty<CompletedCandle>();
    internal bool Contains(string timeframe) => series.ContainsKey(timeframe);
    internal static int FindCompletedIndex(CompletedCandle[] candles, DateTimeOffset timestamp)
    {
        int lowerIndex = 0;
        int upperIndex = candles.Length - 1;
        while (lowerIndex <= upperIndex)
        {
            int middleIndex = lowerIndex + (upperIndex - lowerIndex) / 2;
            if (candles[middleIndex].ClosedAt <= timestamp) lowerIndex = middleIndex + 1;
            else upperIndex = middleIndex - 1;
        }
        return upperIndex;
    }
}


