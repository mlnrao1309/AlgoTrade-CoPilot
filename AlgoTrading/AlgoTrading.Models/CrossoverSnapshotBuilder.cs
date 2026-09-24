namespace AlgoTrading.Models.Rules;

internal sealed class CrossoverSnapshotBuilder
{
    private readonly RuleMarketData data;
    private readonly BoundRuleExecution execution;

    internal CrossoverSnapshotBuilder(RuleMarketData data, BoundRuleExecution execution)
    {
        this.data = data;
        this.execution = execution;
    }

    internal CrossoverCandleSnapshot Capture(CrossoverSubscription subscription, CompletedCandle candle)
    {
        return new CrossoverCandleSnapshot(candle,
            CaptureValue(subscription.Condition.Left, candle.ClosedAt),
            CaptureValue(subscription.Condition.Right, candle.ClosedAt));
    }

    private CrossoverValueSnapshot CaptureValue(ValueDefinition value, DateTimeOffset timestamp)
    {
        double calculated = execution.SampleValue(value, timestamp);
        string? timeframe = null;
        CompletedCandle? sourceCandle = null;
        var inputs = new List<CrossoverValueSnapshot>();
        switch (value)
        {
            case CandleValue candle:
                timeframe = candle.Timeframe;
                sourceCandle = FindCandle(timeframe, timestamp, candle.CandlesAgo);
                break;
            case IndicatorValue indicator:
                timeframe = indicator.Timeframe;
                sourceCandle = FindCandle(timeframe, timestamp, 0);
                if (sourceCandle != null)
                {
                    if (indicator.Function == IndicatorFunction.SuperTrend)
                    {
                        inputs.Add(CaptureValue(new CandleValue(timeframe, CandleField.High), sourceCandle.ClosedAt));
                        inputs.Add(CaptureValue(new CandleValue(timeframe, CandleField.Low), sourceCandle.ClosedAt));
                        inputs.Add(CaptureValue(new CandleValue(timeframe, CandleField.Close), sourceCandle.ClosedAt));
                    }
                    else inputs.Add(CaptureValue(indicator.Source ?? new CandleValue(timeframe), sourceCandle.ClosedAt));
                }
                break;
            case ArithmeticValue arithmetic:
                inputs.Add(CaptureValue(arithmetic.Left, timestamp));
                inputs.Add(CaptureValue(arithmetic.Right, timestamp));
                break;
            case RoundValue round:
                inputs.Add(CaptureValue(round.Source, timestamp));
                break;
            case PreviousValue previous:
                timeframe = previous.Timeframe;
                sourceCandle = FindCandle(timeframe, timestamp, previous.CandlesAgo);
                if (sourceCandle != null) inputs.Add(CaptureValue(previous.Source, sourceCandle.ClosedAt));
                break;
            case CountValue count:
                timeframe = count.Timeframe;
                sourceCandle = FindCandle(timeframe, timestamp, count.CandlesAgo);
                // Count is reported as an aggregate. Replaying its history here must not emit more events.
                break;
        }
        IndicatorValue? indicatorDefinition = value as IndicatorValue;
        CandleValue? candleDefinition = value as CandleValue;
        double? multiplier = indicatorDefinition?.Function is IndicatorFunction.BollingerUpperBand
            or IndicatorFunction.BollingerLowerBand or IndicatorFunction.SuperTrend ? indicatorDefinition.Multiplier : null;
        return new CrossoverValueSnapshot(RuleLanguage.Describe(value), double.IsFinite(calculated) ? calculated : null,
            timestamp, timeframe, sourceCandle, inputs.AsReadOnly(), indicatorDefinition?.Function,
            indicatorDefinition?.Length, multiplier, candleDefinition?.Field);
    }

    private CompletedCandle? FindCandle(string timeframe, DateTimeOffset timestamp, int candlesAgo)
    {
        CompletedCandle[] candles = data.GetSeries(timeframe);
        int currentIndex = RuleMarketData.FindCompletedIndex(candles, timestamp);
        return currentIndex < candlesAgo ? null : candles[currentIndex - candlesAgo];
    }
}


