using System;
using System.Collections.Generic;

namespace AlgorithmicEngine
{
    public class TradeLeg
    {
        public int LegId { get; set; }
        public DateTime ExitTime { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal PortionClosed { get; set; } // e.g., 0.5 for 50%, 1.0 for 100%
        public string ExitReason { get; set; } = string.Empty;
    }

    public class Trade
    {
        public int TradeId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal InitialStopLoss { get; set; }
        public decimal InitialQuantity { get; set; } = 1.0m;

        public List<TradeLeg> ExecutedLegs { get; set; } = new List<TradeLeg>();

        public bool IsFullyClosed => ExecutedLegs.Sum(l => l.PortionClosed) >= 1.0m;
        public DateTime? FinalExitTime => ExecutedLegs.LastOrDefault()?.ExitTime;

        /// <summary>
        /// Weighted average exit price calculated across all partial scale-outs.
        /// </summary>
        public decimal NetExitPrice
        {
            get
            {
                if (!ExecutedLegs.Any()) return 0m;
                return ExecutedLegs.Sum(l => l.ExitPrice * l.PortionClosed);
            }
        }

        // Net PnL metrics for SHORT position: (Entry - NetExit)
        public decimal RealizedPoints => EntryPrice - NetExitPrice;
        public decimal RealizedReturnPct => EntryPrice == 0 ? 0m : (RealizedPoints / EntryPrice) * 100m;

        public decimal RiskPoints => Math.Abs(InitialStopLoss - EntryPrice);
        public decimal RiskRewardRatio => RiskPoints == 0 ? 0m : RealizedPoints / RiskPoints;

        public bool IsWin => RealizedPoints > 0;
        public bool IsLoss => RealizedPoints < 0;
        public bool IsBreakEven => RealizedPoints == 0;
    }


    public class PerformanceMetrics
    {
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal WinRatePct => TotalTrades == 0 ? 0 : (decimal)WinningTrades / TotalTrades * 100m;

        public decimal TotalPointsGained { get; set; }
        public decimal GrossProfitPoints { get; set; }
        public decimal GrossLossPoints { get; set; }
        public decimal ProfitFactor => GrossLossPoints == 0 ? GrossProfitPoints : Math.Abs(GrossProfitPoints / GrossLossPoints);

        public decimal AverageTradePoints => TotalTrades == 0 ? 0 : TotalPointsGained / TotalTrades;
        public decimal MaxDrawdownPoints { get; set; }
        public decimal MaxDrawdownPct { get; set; }
    }
}