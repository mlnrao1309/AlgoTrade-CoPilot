using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlgoTrading.Models.Rules;

public enum ComparisonOperator { GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Equal, NotEqual, CrossedAbove, CrossedBelow }
public enum GroupOperator { All, Any }
public enum ArithmeticOperator { Add, Subtract, Multiply, Divide }
public enum CandleField { Open, High, Low, Close, Volume }
public enum IndicatorFunction { SimpleMovingAverage, ExponentialMovingAverage, RelativeStrengthIndex, BollingerMiddleBand, BollingerUpperBand, BollingerLowerBand, SuperTrend }
public enum RuleStatus { Matched, NotMatched, InsufficientData }

/// <summary>A saveable strategy condition. EvaluationTimeframe supplies the completed-candle clock for crossovers.</summary>
public sealed record RuleDefinition([property: JsonRequired] string Name, [property: JsonRequired] string EvaluationTimeframe, [property: JsonRequired] ConditionDefinition Condition);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ComparisonCondition), "comparison")]
[JsonDerivedType(typeof(ConditionGroup), "group")]
[JsonDerivedType(typeof(NotCondition), "not")]
public abstract record ConditionDefinition
{
    public bool Enabled { get; init; } = true;
    public string? Comment { get; init; }
}
public sealed record ComparisonCondition([property: JsonRequired] ValueDefinition Left, [property: JsonRequired] ComparisonOperator Operator, [property: JsonRequired] ValueDefinition Right) : ConditionDefinition;
public sealed record ConditionGroup([property: JsonRequired] GroupOperator Operator, [property: JsonRequired] IReadOnlyList<ConditionDefinition> Conditions) : ConditionDefinition;
public sealed record NotCondition([property: JsonRequired] ConditionDefinition Condition) : ConditionDefinition;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NumberValue), "number")]
[JsonDerivedType(typeof(CandleValue), "candle")]
[JsonDerivedType(typeof(IndicatorValue), "indicator")]
[JsonDerivedType(typeof(ArithmeticValue), "arithmetic")]
[JsonDerivedType(typeof(RoundValue), "round")]
[JsonDerivedType(typeof(PreviousValue), "previous")]
[JsonDerivedType(typeof(CountValue), "count")]
public abstract record ValueDefinition;
public sealed record NumberValue([property: JsonRequired] double Value) : ValueDefinition;
public sealed record CandleValue([property: JsonRequired] string Timeframe, CandleField Field = CandleField.Close, int CandlesAgo = 0) : ValueDefinition;
/// <summary>Source is a candle value, calculation, or another indicator. SuperTrend uses candle high, low and close and requires a null source.</summary>
public sealed record IndicatorValue([property: JsonRequired] IndicatorFunction Function, [property: JsonRequired] string Timeframe, [property: JsonRequired] int Length, ValueDefinition? Source = null, double Multiplier = 2) : ValueDefinition;
public sealed record ArithmeticValue([property: JsonRequired] ValueDefinition Left, [property: JsonRequired] ArithmeticOperator Operator, [property: JsonRequired] ValueDefinition Right) : ValueDefinition;
public sealed record RoundValue([property: JsonRequired] ValueDefinition Source, int DecimalPlaces = 0) : ValueDefinition;
/// <summary>Samples Source at a previous completed candle on the specified clock. Negative offsets are forbidden.</summary>
public sealed record PreviousValue([property: JsonRequired] ValueDefinition Source, [property: JsonRequired] string Timeframe, int CandlesAgo = 1) : ValueDefinition;
/// <summary>Counts matches over Length completed candles, including the latest candle unless CandlesAgo is specified.</summary>
public sealed record CountValue([property: JsonRequired] ConditionDefinition Condition, [property: JsonRequired] string Timeframe, [property: JsonRequired] int Length, int CandlesAgo = 0) : ValueDefinition;

public static class RuleDefinitionJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        MaxDepth = 128,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static string Serialize(RuleDefinition definition) => JsonSerializer.Serialize(definition, Options);
    public static RuleDefinition Deserialize(string document) => JsonSerializer.Deserialize<RuleDefinition>(document, Options)
        ?? throw new ArgumentException("The rule document is empty.", nameof(document));
}

public sealed record RuleEvaluation(RuleStatus Status, DateTimeOffset? EvaluatedAt, string Explanation)
{
    public bool IsMatch => Status == RuleStatus.Matched;
}

/// <summary>The data provider must supply actual exchange/session close times, not infer them from Candle.Timestamp.</summary>
public sealed record CompletedCandle(Candle Candle, DateTimeOffset ClosedAt);

/// <summary>An immutable snapshot for one instrument, containing finalized candles only. Future rows are allowed for backtests and are never sampled early.</summary>
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

