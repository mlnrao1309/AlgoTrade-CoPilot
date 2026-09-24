namespace AlgoTrading.Models.Rules;

/// <summary>A calculated operand and its nested inputs, sampled without looking beyond SampledAt.</summary>
public sealed record CrossoverValueSnapshot(
    string Description,
    double? Value,
    DateTimeOffset SampledAt,
    string? Timeframe,
    CompletedCandle? SourceCandle,
    IReadOnlyList<CrossoverValueSnapshot> Inputs,
    IndicatorFunction? IndicatorFunction = null,
    int? Length = null,
    double? Multiplier = null,
    CandleField? CandleField = null);

