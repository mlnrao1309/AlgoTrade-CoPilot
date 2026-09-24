namespace AlgoTrading.Models.Rules;

/// <summary>The evaluation-clock candle and the two compared expressions sampled at its close.</summary>
public sealed record CrossoverCandleSnapshot(
    CompletedCandle Candle,
    CrossoverValueSnapshot Left,
    CrossoverValueSnapshot Right);
