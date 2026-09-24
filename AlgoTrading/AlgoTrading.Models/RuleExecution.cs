namespace AlgoTrading.Models.Rules;

/// <summary>Thread-safe evaluation against a fixed data snapshot. Create a new binding when finalized market data changes.</summary>
public sealed class BoundRuleExecution
{
    private readonly RuleDefinition definition;
    private readonly RuleMarketData data;
    private readonly Dictionary<IndicatorValue, double[]> indicatorSeries = new();
    private readonly object evaluationLock = new();
    internal BoundRuleExecution(RuleDefinition definition, RuleMarketData data)
    {
        this.definition = definition;
        this.data = data;
    }

    public RuleEvaluation Evaluate(DateTimeOffset asOf)
    {
        lock (evaluationLock)
        {
            CompletedCandle[] clock = data.GetSeries(definition.EvaluationTimeframe);
            int evaluationIndex = RuleMarketData.FindCompletedIndex(clock, asOf);
            if (evaluationIndex < 0)
                return new RuleEvaluation(RuleStatus.InsufficientData, null, "No completed evaluation candle is available.");
            DateTimeOffset evaluationTime = clock[evaluationIndex].ClosedAt;
            var result = EvaluateCondition(definition.Condition, evaluationTime, definition.EvaluationTimeframe);
            return new RuleEvaluation(result.Status, evaluationTime, result.Explanation);
        }
    }

    internal double SampleValue(ValueDefinition value, DateTimeOffset timestamp)
    {
        lock (evaluationLock) return EvaluateValue(value, timestamp);
    }

    internal RuleStatus SampleCondition(ComparisonCondition condition, DateTimeOffset timestamp, string timeframe)
    {
        lock (evaluationLock) return EvaluateCondition(condition, timestamp, timeframe).Status;
    }

    private (RuleStatus Status, string Explanation) EvaluateCondition(ConditionDefinition condition, DateTimeOffset timestamp, string clockTimeframe)
    {
        switch (condition)
        {
            case ConditionGroup group:
                bool hasUnavailableChild = false;
                var explanations = new List<string>();
                // Evaluate ordinary comparisons first within all-groups only. Never move conditions across group boundaries.
                IEnumerable<ConditionDefinition> children = group.Conditions.Where(child => child.Enabled);
                if (group.Operator == GroupOperator.All) children = children.OrderBy(ContainsCrossover);
                foreach (ConditionDefinition child in children)
                {
                    var result = EvaluateCondition(child, timestamp, clockTimeframe);
                    explanations.Add(result.Explanation);
                    if (group.Operator == GroupOperator.All && result.Status == RuleStatus.NotMatched) return result;
                    if (group.Operator == GroupOperator.Any && result.Status == RuleStatus.Matched) return result;
                    hasUnavailableChild |= result.Status == RuleStatus.InsufficientData;
                }
                return (hasUnavailableChild ? RuleStatus.InsufficientData : group.Operator == GroupOperator.All ? RuleStatus.Matched : RuleStatus.NotMatched,
                    string.Join("; ", explanations));
            case NotCondition negation:
                var negated = EvaluateCondition(negation.Condition, timestamp, clockTimeframe);
                return (negated.Status switch { RuleStatus.Matched => RuleStatus.NotMatched, RuleStatus.NotMatched => RuleStatus.Matched, _ => RuleStatus.InsufficientData },
                    $"Not ({negated.Explanation})");
            case ComparisonCondition comparison:
                double left = EvaluateValue(comparison.Left, timestamp);
                double right = EvaluateValue(comparison.Right, timestamp);
                string description = RuleLanguage.Describe(comparison);
                if (!double.IsFinite(left) || !double.IsFinite(right))
                    return (RuleStatus.InsufficientData, $"Unavailable: {description}. Check candle history and calculation inputs.");
                bool matches;
                string previousDescription = "";
                if (comparison.Operator is ComparisonOperator.CrossedAbove or ComparisonOperator.CrossedBelow)
                {
                    CompletedCandle[] clock = data.GetSeries(clockTimeframe);
                    int currentIndex = RuleMarketData.FindCompletedIndex(clock, timestamp);
                    if (currentIndex < 1) return (RuleStatus.InsufficientData, $"Unavailable: {description}. A previous completed candle is required.");
                    DateTimeOffset previousTimestamp = clock[currentIndex - 1].ClosedAt;
                    double previousLeft = EvaluateValue(comparison.Left, previousTimestamp);
                    double previousRight = EvaluateValue(comparison.Right, previousTimestamp);
                    if (!double.IsFinite(previousLeft) || !double.IsFinite(previousRight))
                        return (RuleStatus.InsufficientData, $"Unavailable: {description}. Previous indicator values are not ready.");
                    matches = comparison.Operator == ComparisonOperator.CrossedAbove
                        ? previousLeft <= previousRight && left > right
                        : previousLeft >= previousRight && left < right;
                    previousDescription = $"; previous values {previousLeft:G8} and {previousRight:G8}";
                }
                else matches = comparison.Operator switch
                {
                    ComparisonOperator.GreaterThan => left > right,
                    ComparisonOperator.GreaterThanOrEqual => left >= right,
                    ComparisonOperator.LessThan => left < right,
                    ComparisonOperator.LessThanOrEqual => left <= right,
                    ComparisonOperator.Equal => left == right,
                    ComparisonOperator.NotEqual => left != right,
                    _ => throw new InvalidOperationException("Unsupported comparison.")
                };
                return (matches ? RuleStatus.Matched : RuleStatus.NotMatched,
                    $"{(matches ? "Matched" : "Not matched")}: {description} (current values {left:G8} and {right:G8}{previousDescription}).");
            default: throw new InvalidOperationException("Unsupported condition.");
        }
    }

    private static bool ContainsCrossover(ConditionDefinition condition) => condition switch
    {
        ComparisonCondition comparison => comparison.Operator is ComparisonOperator.CrossedAbove or ComparisonOperator.CrossedBelow
            || ContainsCrossover(comparison.Left) || ContainsCrossover(comparison.Right),
        ConditionGroup group => group.Conditions.Where(child => child.Enabled).Any(ContainsCrossover),
        NotCondition negation => ContainsCrossover(negation.Condition), _ => false
    };
    private static bool ContainsCrossover(ValueDefinition value) => value switch
    {
        CountValue count => ContainsCrossover(count.Condition),
        IndicatorValue indicator => indicator.Source != null && ContainsCrossover(indicator.Source),
        ArithmeticValue arithmetic => ContainsCrossover(arithmetic.Left) || ContainsCrossover(arithmetic.Right),
        RoundValue round => ContainsCrossover(round.Source),
        PreviousValue previous => ContainsCrossover(previous.Source), _ => false
    };

    private double EvaluateValue(ValueDefinition value, DateTimeOffset timestamp)
    {
        double result;
        switch (value)
        {
            case NumberValue number: return number.Value;
            case CandleValue candle:
                CompletedCandle[] candles = data.GetSeries(candle.Timeframe);
                int candleIndex = RuleMarketData.FindCompletedIndex(candles, timestamp);
                if (candleIndex < candle.CandlesAgo) return double.NaN;
                Candle selected = candles[candleIndex - candle.CandlesAgo].Candle;
                return candle.Field switch
                {
                    CandleField.Open => (double)selected.Open, CandleField.High => (double)selected.High,
                    CandleField.Low => (double)selected.Low, CandleField.Close => (double)selected.Close,
                    CandleField.Volume => selected.Volume, _ => double.NaN
                };
            case IndicatorValue indicator:
                int indicatorIndex = RuleMarketData.FindCompletedIndex(data.GetSeries(indicator.Timeframe), timestamp);
                if (indicatorIndex < 0) return double.NaN;
                return GetIndicatorSeries(indicator)[indicatorIndex];
            case ArithmeticValue arithmetic:
                double left = EvaluateValue(arithmetic.Left, timestamp);
                double right = EvaluateValue(arithmetic.Right, timestamp);
                result = arithmetic.Operator switch
                {
                    ArithmeticOperator.Add => left + right, ArithmeticOperator.Subtract => left - right,
                    ArithmeticOperator.Multiply => left * right, ArithmeticOperator.Divide => right == 0 ? double.NaN : left / right,
                    _ => double.NaN
                };
                break;
            case RoundValue round:
                result = Math.Round(EvaluateValue(round.Source, timestamp), round.DecimalPlaces, MidpointRounding.AwayFromZero);
                break;
            case PreviousValue previous:
                CompletedCandle[] previousClock = data.GetSeries(previous.Timeframe);
                int previousIndex = RuleMarketData.FindCompletedIndex(previousClock, timestamp);
                if (previousIndex < previous.CandlesAgo) return double.NaN;
                return EvaluateValue(previous.Source, previousClock[previousIndex - previous.CandlesAgo].ClosedAt);
            case CountValue count:
                CompletedCandle[] countClock = data.GetSeries(count.Timeframe);
                int endIndex = RuleMarketData.FindCompletedIndex(countClock, timestamp);
                if (endIndex < count.CandlesAgo) return double.NaN;
                endIndex -= count.CandlesAgo;
                if (endIndex < count.Length - 1) return double.NaN;
                int matchCount = 0;
                for (int windowIndex = endIndex - count.Length + 1; windowIndex <= endIndex; windowIndex++)
                {
                    var counted = EvaluateCondition(count.Condition, countClock[windowIndex].ClosedAt, count.Timeframe);
                    if (counted.Status == RuleStatus.InsufficientData) return double.NaN;
                    if (counted.Status == RuleStatus.Matched) matchCount++;
                }
                return matchCount;
            default: throw new InvalidOperationException("Unsupported calculation.");
        }
        return double.IsFinite(result) ? result : double.NaN;
    }

    private double[] GetIndicatorSeries(IndicatorValue indicator)
    {
        if (indicatorSeries.TryGetValue(indicator, out double[]? cached)) return cached;
        CompletedCandle[] candles = data.GetSeries(indicator.Timeframe);
        double[] calculated;
        if (indicator.Function == IndicatorFunction.SuperTrend)
            calculated = RuleSeriesCalculations.SuperTrend(candles, indicator.Length, indicator.Multiplier);
        else
        {
            ValueDefinition source = indicator.Source ?? new CandleValue(indicator.Timeframe);
            // Every source sample is evaluated as of that candle's close. Later rows never affect an earlier result.
            double[] sourceValues = candles.Select(candle => EvaluateValue(source, candle.ClosedAt)).ToArray();
            calculated = RuleSeriesCalculations.Calculate(indicator, sourceValues);
        }
        indicatorSeries.Add(indicator, calculated);
        return calculated;
    }
}

