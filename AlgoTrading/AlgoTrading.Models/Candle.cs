using System;

namespace AlgoTrading.Models
{
    public enum Indicator
    {
        EMA,
        SMA,
        RSI,
        PivotPoints,
        BollingerBands,
        SuperTrend
    }

    public enum PivotPointType
    {
        Classic,
        Fibonacci,
        Camarilla,
        Woodie,
        DeMark
    }

    public enum PivotTimeframe
    {
        Daily,
        Weekly,
        Monthly
    }

    public struct PivotPoints
    {
        public PivotPointType Type { get; }
        public double High { get; }
        public double Low { get; }
        public double Close { get; }

        public double PP { get; }
        public double S1 { get; }
        public double S2 { get; }
        public double S3 { get; }
        public double S4 { get; }
        public double S5 { get; }

        public double R1 { get; }
        public double R2 { get; }
        public double R3 { get; }
        public double R4 { get; }
        public double R5 { get; }

        public PivotTimeframe Timeframe { get; }

        public PivotPoints(double high, double low, double close, PivotPointType pivotPointType, PivotTimeframe pivotTimeframe)
        {
            High = high;
            Low = low;
            Close = close;
            Type = pivotPointType;
            Timeframe = pivotTimeframe;
            // Calculate pivot points based on the type
            switch (this.Type)
            {
                case PivotPointType.Classic:
                    PP = (High + Low + Close) / 3.0;
                    double range = High - Low;

                    R1 = (2 * PP) - Low;
                    S1 = (2 * PP) - High;

                    R2 = PP + range;
                    S2 = PP - range;

                    R3 = High + 2 * (PP - Low);
                    S3 = Low - 2 * (High - PP);

                    R4 = R3 + range;
                    S4 = S3 - range;

                    R5 = R4 + range;
                    S5 = S4 - range;
                    break;
                default:
                    throw new NotImplementedException($"Pivot point calculation for {this.Type} is not implemented.");
            }
        }

    }

    /// <summary>
    /// Immutable candle struct used across the application.
    /// Use unsigned types for values that can't be negative per requirements.
    /// </summary>
    public readonly struct Candle
    {
        public int InstrumentToken { get; }
        public string TimeframeMinutes { get; }
        public DateTime Timestamp { get; }
        public decimal Open { get; }
        public decimal High { get; }
        public decimal Low { get; }
        public decimal Close { get; }
        public long Volume { get; }

        public Candle(int instrumentToken, string timeframeMinutes, DateTime timestamp, decimal open, decimal high, decimal low, decimal close, long volume)
        {
            InstrumentToken = instrumentToken;
            TimeframeMinutes = timeframeMinutes;
            // Unspecified timestamps represent UTC, matching the existing storage contract.
            // Local timestamps must be converted to preserve the represented instant.
            Timestamp = timestamp.Kind == DateTimeKind.Local
                ? timestamp.ToUniversalTime()
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
        }
    }

    public readonly struct InstrumentCandleData
    {
        public int InstrumentToken { get; }
        public string TimeframeMinutes { get; }
        public Candle[] Candles { get; }

        public PivotPoints[]? PivotPoints { get; }
        public Dictionary<string, double[]>? IndicatorData { get; } = new();


        public InstrumentCandleData(int instrumentToken, string timeframeMinutes, Candle[] candles)
        {
            InstrumentToken = instrumentToken;
            TimeframeMinutes = timeframeMinutes;
            Candles = candles ?? throw new ArgumentNullException(nameof(candles));
            PivotPoints = candles.Where(c => c.TimeframeMinutes == "day").Select(x => new PivotPoints((double)x.High, (double)x.Low, (double)x.Close, PivotPointType.Classic, PivotTimeframe.Daily)).ToArray();
        }

        public void Add(string indicatorName, double[] indicatorValues)
        {
            if (IndicatorData == null)
                throw new InvalidOperationException("IndicatorData dictionary is not initialized.");
            IndicatorData[indicatorName] = indicatorValues;
        }

        public void AddPivotPoints(Candle[] candles)
        {
            // Implementation for adding pivot points based on candles
            
        }
    }
}

