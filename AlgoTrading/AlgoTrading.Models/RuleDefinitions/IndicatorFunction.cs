using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AlgoTrading.Models.Rules
{
    public enum IndicatorFunction
    {
        SimpleMovingAverage,
        ExponentialMovingAverage,
        RelativeStrengthIndex,
        BollingerMiddleBand,
        BollingerUpperBand,
        BollingerLowerBand,
        SuperTrend
    }
}


