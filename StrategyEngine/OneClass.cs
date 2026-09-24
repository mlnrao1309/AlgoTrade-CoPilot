using AlgorithmicEngine.Data;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AlgorithmicEngine
{
    // -------------------------------------------------------------
    // DATA MODELS & ENUMS
    // -------------------------------------------------------------
    public class Candle
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    public class PivotLevels
    {
        public decimal Pivot { get; set; }
        public decimal S1 { get; set; }
        public decimal S2 { get; set; }
        public decimal S3 { get; set; }
        public decimal R1 { get; set; }
        public decimal R2 { get; set; }
        public decimal R3 { get; set; }
    }

    public enum PositionState
    {
        Flat,
        ShortFull,      // 100% Short
        ShortScaled,    // 50% Short Remaining
    }

    // -------------------------------------------------------------
    // PARAMETERS & ML FEATURE SNAPSHOT
    // -------------------------------------------------------------
    public class StrategyParameters
    {
        public int EmaPeriod { get; set; } = 5;
        public int RsiPeriod { get; set; } = 14;
        public decimal RsiUpperBoundary { get; set; } = 65m;
        public decimal RsiLowerBoundary { get; set; } = 45m;
        public decimal RetestTolerancePct { get; set; } = 0.0075m; // 0.2% proximity to Pivot or DEMA
    }

    public class ExecutionLog
    {
        public DateTime Time { get; set; }
        public string EventType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Ema5 { get; set; }
        public decimal Dema5 { get; set; }
        public decimal Rsi { get; set; }
        public decimal NearestPivot { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // -------------------------------------------------------------
    // STRATEGY CORE
    // -------------------------------------------------------------

    public class BearishRetestStrategy
    {
        private readonly StrategyParameters _params;
        private PositionState _state = PositionState.Flat;

        private int _tradeCounter = 0;
        private Trade? _activeTrade = null;
        private decimal _stopLossPrice = 0m;
        private decimal _tp1Price = 0m;

        public List<Trade> ClosedTrades { get; } = new List<Trade>();
        public List<ExecutionLog> ExecutionLogs { get; } = new List<ExecutionLog>();

        public BearishRetestStrategy(StrategyParameters parameters)
        {
            _params = parameters;
        }

        public void ProcessBar(ExtendedCandle currentBar, ExtendedCandle prevBar, decimal rsi, decimal ema5, decimal dema5)
        {
            if (currentBar.PivotContext == null) return;

            var pivots = currentBar.PivotContext;
            decimal currentClose = currentBar.Close;
            decimal currentHigh = currentBar.High;
            decimal currentLow = currentBar.Low;

            Console.WriteLine($"ema5: {ema5}< dema5: {dema5} {ema5 < dema5}" );

            // ---------------------------------------------------------
            // 1. ENTRY LOGIC (Flat -> Short 100%)
            // ---------------------------------------------------------
            if (_state == PositionState.Flat)
            {
                // Condition 1: Bearish Regime (EMA5 below DEMA5 AND Price below PP)
                bool isBearishRegime = ema5 < dema5 && currentClose < pivots.PP;

                // Condition 2: Retest of PP, DEMA, or Ref Resistance
                // Option A: Tolerance percentage
                bool isRetestingPP = pivots.PP > 0 && Math.Abs(currentHigh - pivots.PP) / pivots.PP <= _params.RetestTolerancePct;
                bool isRetestingDema = dema5 > 0 && Math.Abs(currentHigh - dema5) / dema5 <= _params.RetestTolerancePct;
                bool isRetestingRefRes = pivots.RefResistance > 0 && Math.Abs(currentHigh - pivots.RefResistance) / pivots.RefResistance <= _params.RetestTolerancePct;

                // Option B: Wick overlap (High touched/penetrated level, but Close remained below)
                bool isWickRetestPP = currentHigh >= pivots.PP && currentClose < pivots.PP;

                bool isRetesting = isRetestingPP || isRetestingDema || isRetestingRefRes || isWickRetestPP;

                // Condition 3: Invalidation / Rejection Structure (Lower High or Rejection Wick)
                bool isLowerHighClose = currentHigh <= prevBar.High && currentClose < prevBar.Close;

                // Condition 4: RSI in Bearish Pullback Zone (e.g. 45 - 65)
                bool isRsiRejected = rsi >= _params.RsiLowerBoundary && rsi <= _params.RsiUpperBoundary;

                if (isBearishRegime && isRetesting && isLowerHighClose && isRsiRejected)
                {
                    _state = PositionState.ShortFull;
                    _tradeCounter++;

                    _stopLossPrice = Math.Max(prevBar.High, currentHigh);
                    _tp1Price = pivots.S1;

                    _activeTrade = new Trade
                    {
                        TradeId = _tradeCounter,
                        EntryTime = currentBar.TimeStamp,
                        EntryPrice = currentClose,
                        InitialStopLoss = _stopLossPrice,
                        InitialQuantity = 1.0m
                    };

                    LogEvent(currentBar.TimeStamp, "ENTRY_SHORT_100%", currentClose, ema5, dema5, rsi, pivots.PP,
                        $"Bearish Retest Confirmed. Entry at {currentClose:F2}, SL: {_stopLossPrice:F2}, TP1: {_tp1Price:F2}");
                }
            }

            // ---------------------------------------------------------
            // 2. POSITION MANAGEMENT & EXITS
            // ---------------------------------------------------------
            else if (_activeTrade != null && (_state == PositionState.ShortFull || _state == PositionState.ShortScaled))
            {
                // A. Stop-Loss Trigger Check
                if (currentHigh >= _stopLossPrice)
                {
                    decimal portionToClose = _state == PositionState.ShortFull ? 1.0m : 0.5m;

                    AddLeg(_activeTrade, currentBar.TimeStamp, _stopLossPrice, portionToClose, "STOP_LOSS");
                    LogEvent(currentBar.TimeStamp, "STOP_LOSS_EXIT", _stopLossPrice, ema5, dema5, rsi, pivots.PP,
                        $"Price hit Stop-Loss at {_stopLossPrice:F2}. Remaining position liquidated.");

                    FinalizeTrade();
                    return;
                }

                // B. Partial Scale-Out (50% at S1 Target)
                if (_state == PositionState.ShortFull && currentLow <= _tp1Price)
                {
                    AddLeg(_activeTrade, currentBar.TimeStamp, _tp1Price, 0.5m, "TP1_S1_50_PERCENT");

                    _state = PositionState.ShortScaled;
                    _stopLossPrice = _activeTrade.EntryPrice; // Move SL to Breakeven

                    LogEvent(currentBar.TimeStamp, "EXIT_TP1_50%", _tp1Price, ema5, dema5, rsi, pivots.PP,
                        $"S1 Target reached ({_tp1Price:F2}). 50% booked. SL moved to Breakeven ({_activeTrade.EntryPrice:F2}).");
                }

                // C. Trend Invalidation / Full Exit: EMA5 > DEMA5 or RSI > 60
                bool isTrendReversed = ema5 > dema5 || rsi > 60.0m;
                if (isTrendReversed)
                {
                    decimal portionToClose = _state == PositionState.ShortFull ? 1.0m : 0.5m;

                    AddLeg(_activeTrade, currentBar.TimeStamp, currentClose, portionToClose, "TREND_REVERSAL");
                    LogEvent(currentBar.TimeStamp, "EXIT_FULL_REVERSAL", currentClose, ema5, dema5, rsi, pivots.PP,
                        $"Trend Reversed (EMA5 > DEMA5 or RSI > 60). Closed remaining at {currentClose:F2}.");

                    FinalizeTrade();
                    return;
                }
            }
        }

        private void AddLeg(Trade trade, DateTime time, decimal exitPrice, decimal portion, string reason)
        {
            trade.ExecutedLegs.Add(new TradeLeg
            {
                LegId = trade.ExecutedLegs.Count + 1,
                ExitTime = time,
                ExitPrice = exitPrice,
                PortionClosed = portion,
                ExitReason = reason
            });
        }

        private void FinalizeTrade()
        {
            if (_activeTrade != null)
            {
                ClosedTrades.Add(_activeTrade);
                _activeTrade = null;
            }

            _state = PositionState.Flat;
            _stopLossPrice = 0m;
            _tp1Price = 0m;
        }

        private void LogEvent(DateTime time, string eventType, decimal price, decimal ema5, decimal dema5, decimal rsi, decimal pivot, string reason)
        {
            ExecutionLogs.Add(new ExecutionLog
            {
                Time = time,
                EventType = eventType,
                Price = price,
                Ema5 = ema5,
                Dema5 = dema5,
                Rsi = rsi,
                NearestPivot = pivot,
                Reason = reason
            });
        }
    }
}

