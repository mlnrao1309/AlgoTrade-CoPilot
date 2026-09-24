namespace AlgoTrading.Models.Rules;

public sealed record RuleBindingPlan(IReadOnlyList<string> RequiredTimeframes, IReadOnlyList<string> IndicatorCalculations);

public static class RuleBinder
{
    /// <summary>Validates and freezes the definition, then expands all nested indicator dependencies.</summary>
    public static BoundRule Bind(RuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        var timeframes = new HashSet<string>(StringComparer.Ordinal);
        var indicators = new HashSet<IndicatorValue>();
        int visitedNodeCount = 0;
        void CheckDepth(int depth)
        {
            if (depth > 48 || ++visitedNodeCount > 2048)
                throw new ArgumentException("The rule is too large. Use at most 48 nesting levels and 2048 expressions.");
        }
        void AddTimeframe(string timeframe)
        {
            if (string.IsNullOrWhiteSpace(timeframe)) throw new ArgumentException("Choose a timeframe for every candle and indicator.");
            timeframes.Add(timeframe);
        }
        void CheckEnum<T>(T value) where T : struct, Enum
        {
            if (!Enum.IsDefined(value)) throw new ArgumentException($"Unsupported {typeof(T).Name}: {value}.");
        }
        void CheckCondition(ConditionDefinition condition, int depth)
        {
            ArgumentNullException.ThrowIfNull(condition);
            CheckDepth(depth);
            if (!condition.Enabled) return;
            switch (condition)
            {
                case ComparisonCondition comparison:
                    CheckEnum(comparison.Operator);
                    CheckValue(comparison.Left, depth + 1);
                    CheckValue(comparison.Right, depth + 1);
                    break;
                case ConditionGroup group:
                    CheckEnum(group.Operator);
                    if (group.Conditions == null || group.Conditions.Count == 0 || !group.Conditions.Any(child => child != null && child.Enabled))
                        throw new ArgumentException("A condition group needs at least one enabled condition.");
                    foreach (var child in group.Conditions) CheckCondition(child, depth + 1);
                    break;
                case NotCondition negation:
                    if (negation.Condition == null || !negation.Condition.Enabled)
                        throw new ArgumentException("Not needs an enabled condition.");
                    CheckCondition(negation.Condition, depth + 1);
                    break;
                default: throw new ArgumentException("Unknown condition type.");
            }
        }
        void CheckValue(ValueDefinition value, int depth)
        {
            ArgumentNullException.ThrowIfNull(value);
            CheckDepth(depth);
            switch (value)
            {
                case NumberValue number:
                    if (!double.IsFinite(number.Value)) throw new ArgumentException("A number must be finite.");
                    break;
                case CandleValue candle:
                    CheckEnum(candle.Field);
                    AddTimeframe(candle.Timeframe);
                    if (candle.CandlesAgo < 0) throw new ArgumentException("Candle offsets cannot refer to the future.");
                    break;
                case IndicatorValue indicator:
                    CheckEnum(indicator.Function);
                    AddTimeframe(indicator.Timeframe);
                    if (indicator.Length <= 0) throw new ArgumentException("An indicator length must be positive.");
                    if (!double.IsFinite(indicator.Multiplier) || indicator.Multiplier <= 0)
                        throw new ArgumentException("An indicator multiplier must be finite and positive.");
                    if (indicator.Function == IndicatorFunction.SuperTrend && indicator.Source != null)
                        throw new ArgumentException("SuperTrend uses high, low and close together; do not choose a separate source.");
                    if (indicator.Source != null) CheckValue(indicator.Source, depth + 1);
                    indicators.Add(indicator);
                    break;
                case ArithmeticValue arithmetic:
                    CheckEnum(arithmetic.Operator);
                    CheckValue(arithmetic.Left, depth + 1);
                    CheckValue(arithmetic.Right, depth + 1);
                    break;
                case RoundValue round:
                    if (round.DecimalPlaces < 0 || round.DecimalPlaces > 15) throw new ArgumentException("Round supports zero through fifteen decimal places.");
                    CheckValue(round.Source, depth + 1);
                    break;
                case PreviousValue previous:
                    AddTimeframe(previous.Timeframe);
                    if (previous.CandlesAgo < 0) throw new ArgumentException("Candle offsets cannot refer to the future.");
                    CheckValue(previous.Source, depth + 1);
                    break;
                case CountValue count:
                    AddTimeframe(count.Timeframe);
                    if (count.Length <= 0 || count.CandlesAgo < 0) throw new ArgumentException("Count needs a positive window and a nonnegative offset.");
                    if (count.Condition == null || !count.Condition.Enabled) throw new ArgumentException("Count needs an enabled condition.");
                    CheckCondition(count.Condition, depth + 1);
                    break;
                default: throw new ArgumentException("Unknown calculation type.");
            }
        }
        AddTimeframe(definition.EvaluationTimeframe);
        if (definition.Condition == null || !definition.Condition.Enabled) throw new ArgumentException("Enable the root condition before binding.");
        CheckCondition(definition.Condition, 0);
        // Detach all lists from the caller, so editing the builder cannot change a running rule.
        RuleDefinition snapshot = RuleDefinitionJson.Deserialize(RuleDefinitionJson.Serialize(definition));
        var plan = new RuleBindingPlan(Array.AsReadOnly(timeframes.Order(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(indicators.Select(RuleLanguage.Describe).Distinct().ToArray()));
        return new BoundRule(snapshot, plan);
    }
}

public sealed class BoundRule
{
    private readonly RuleDefinition definition;
    public RuleBindingPlan Plan { get; }
    public string Description => RuleLanguage.Describe(definition.Condition);

    /// <summary>Create one persistent monitor per instrument. startAfter excludes old events while retaining history for indicators.</summary>
    public RuleCrossoverMonitor CreateCrossoverMonitor(DateTimeOffset? startAfter = null)
    {
        return new RuleCrossoverMonitor(this, definition, startAfter);
    }
    internal BoundRule(RuleDefinition definition, RuleBindingPlan plan)
    {
        this.definition = definition;
        Plan = plan;
    }
    /// <summary>Bind the expanded rule to one immutable market-data snapshot. Reuse the result across a backtest.</summary>
    public BoundRuleExecution BindData(RuleMarketData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (string timeframe in Plan.RequiredTimeframes)
            if (!data.Contains(timeframe)) throw new ArgumentException($"Supply completed candles for '{timeframe}'.", nameof(data));
        return new BoundRuleExecution(definition, data);
    }
}

/// <summary>Friendly labels and previews for a future visual builder; no executable user text or dictionary keys.</summary>
public static class RuleLanguage
{
    public static string Describe(ConditionDefinition condition) => condition switch
    {
        ComparisonCondition comparison => $"{Describe(comparison.Left)} {ComparisonLabel(comparison.Operator)} {Describe(comparison.Right)}",
        ConditionGroup group => $"{(group.Operator == GroupOperator.All ? "All" : "Any")} of ({string.Join("; ", group.Conditions.Where(child => child.Enabled).Select(Describe))})",
        NotCondition negation => $"Not ({Describe(negation.Condition)})",
        _ => throw new ArgumentException("Unknown condition.")
    };
    public static string Describe(ValueDefinition value) => value switch
    {
        NumberValue number => number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        CandleValue candle => $"{candle.Timeframe} {candle.Field.ToString().ToLowerInvariant()}{(candle.CandlesAgo == 0 ? "" : $" ({candle.CandlesAgo} candles ago)")}",
        IndicatorValue indicator => $"{IndicatorLabel(indicator.Function)} (length {indicator.Length}, timeframe {indicator.Timeframe}{(indicator.Function is IndicatorFunction.SuperTrend or IndicatorFunction.BollingerUpperBand or IndicatorFunction.BollingerLowerBand ? $", multiplier {indicator.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : "")}{(indicator.Function == IndicatorFunction.SuperTrend ? "" : $", source {Describe(indicator.Source ?? new CandleValue(indicator.Timeframe))}")})",
        ArithmeticValue arithmetic => $"({Describe(arithmetic.Left)} {ArithmeticLabel(arithmetic.Operator)} {Describe(arithmetic.Right)})",
        RoundValue round => $"Round ({Describe(round.Source)}, {round.DecimalPlaces} decimal places)",
        PreviousValue previous => $"{Describe(previous.Source)} at {previous.CandlesAgo} previous {previous.Timeframe} candles",
        CountValue count => $"Count ({Describe(count.Condition)}) over {count.Length} {count.Timeframe} candles, ending {count.CandlesAgo} candles ago",
        _ => throw new ArgumentException("Unknown value.")
    };
    public static string ComparisonLabel(ComparisonOperator operation) => operation switch
    {
        ComparisonOperator.GreaterThan => "is greater than", ComparisonOperator.GreaterThanOrEqual => "is greater than or equal to",
        ComparisonOperator.LessThan => "is less than", ComparisonOperator.LessThanOrEqual => "is less than or equal to",
        ComparisonOperator.Equal => "equals", ComparisonOperator.NotEqual => "does not equal",
        ComparisonOperator.CrossedAbove => "crossed above", ComparisonOperator.CrossedBelow => "crossed below",
        _ => throw new ArgumentException("Unknown comparison.")
    };
    public static string IndicatorLabel(IndicatorFunction function) => function switch
    {
        IndicatorFunction.SimpleMovingAverage => "Simple Moving Average",
        IndicatorFunction.ExponentialMovingAverage => "Exponential Moving Average",
        IndicatorFunction.RelativeStrengthIndex => "Relative Strength Index",
        IndicatorFunction.BollingerMiddleBand => "Bollinger middle band",
        IndicatorFunction.BollingerUpperBand => "Bollinger upper band",
        IndicatorFunction.BollingerLowerBand => "Bollinger lower band",
        IndicatorFunction.SuperTrend => "SuperTrend", _ => throw new ArgumentException("Unknown indicator.")
    };
    private static string ArithmeticLabel(ArithmeticOperator operation) => operation switch
    {
        ArithmeticOperator.Add => "+", ArithmeticOperator.Subtract => "-", ArithmeticOperator.Multiply => "×",
        ArithmeticOperator.Divide => "÷", _ => throw new ArgumentException("Unknown arithmetic operation.")
    };
}

