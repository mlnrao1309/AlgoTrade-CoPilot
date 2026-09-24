namespace AlgoTrading.Models.Rules;

/// <summary>Immutable event context. Next is optional and never used to decide whether a crossover occurred.</summary>
public sealed record CrossoverOccurrence(
    Guid EventId,
    string RuleName,
    string ConditionPath,
    string ConditionDescription,
    int InstrumentToken,
    string EvaluationTimeframe,
    ComparisonOperator Direction,
    DateTimeOffset OccurredAt,
    DateTimeOffset ObservedAt,
    CrossoverCandleSnapshot Previous,
    CrossoverCandleSnapshot Current,
    CrossoverCandleSnapshot? Next,
    RuleEvaluation OverallRuleEvaluation);
