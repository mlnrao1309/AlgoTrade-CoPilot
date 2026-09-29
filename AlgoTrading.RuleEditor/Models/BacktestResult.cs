using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AlgoTrading.RuleEditor.Models
{
    public sealed class BacktestResult
    {
        public BacktestResult(IReadOnlyList<BacktestTrade> trades, int candlesProcessed)
        {
            Trades = trades;
            CandlesProcessed = candlesProcessed;
        }

        public IReadOnlyList<BacktestTrade> Trades { get; }
        public int CandlesProcessed { get; }
        public decimal NetProfit => Trades.Sum(trade => trade.Profit);
        public int WinningTrades => Trades.Count(trade => trade.Profit > 0);
        public int LosingTrades => Trades.Count(trade => trade.Profit < 0);

        public string ToReport()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("BACKTEST RESULT");
            text.AppendLine("Candles: " + CandlesProcessed.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("Trades: " + Trades.Count.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("Wins / losses: " + WinningTrades.ToString(CultureInfo.InvariantCulture) + " / " + LosingTrades.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("Net P&L: " + NetProfit.ToString("0.00####", CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine("Entry time,Entry price,Exit time,Exit price,Quantity,P&L,Reason");
            foreach (BacktestTrade trade in Trades)
            {
                text.AppendLine(string.Join(",",
                    trade.EntryTime.ToString("O", CultureInfo.InvariantCulture),
                    trade.EntryPrice.ToString(CultureInfo.InvariantCulture),
                    trade.ExitTime.ToString("O", CultureInfo.InvariantCulture),
                    trade.AverageExitPrice.ToString(CultureInfo.InvariantCulture),
                    trade.Quantity.ToString(CultureInfo.InvariantCulture),
                    trade.Profit.ToString(CultureInfo.InvariantCulture),
                    Escape(trade.ExitReason)));
            }
            return text.ToString().TrimEnd();
        }

        private static string Escape(string value)
        {
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? value
                : "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    public sealed class BacktestTrade
    {
        public required DateTimeOffset EntryTime { get; init; }
        public required decimal EntryPrice { get; init; }
        public required DateTimeOffset ExitTime { get; init; }
        public required decimal AverageExitPrice { get; init; }
        public required decimal Quantity { get; init; }
        public required decimal Profit { get; init; }
        public required string ExitReason { get; init; }
    }
}
