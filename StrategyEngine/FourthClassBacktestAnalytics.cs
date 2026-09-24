using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AlgorithmicEngine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    namespace AlgorithmicEngine
    {
        public static class BacktestAnalytics
        {
            public static PerformanceMetrics CalculateMetrics(List<Trade> trades)
            {
                var metrics = new PerformanceMetrics
                {
                    TotalTrades = trades.Count,
                    WinningTrades = trades.Count(t => t.IsWin),
                    LosingTrades = trades.Count(t => t.IsLoss),
                    TotalPointsGained = trades.Sum(t => t.RealizedPoints),
                    GrossProfitPoints = trades.Where(t => t.IsWin).Sum(t => t.RealizedPoints),
                    GrossLossPoints = trades.Where(t => t.IsLoss).Sum(t => t.RealizedPoints)
                };

                decimal peakPoints = 0m;
                decimal currentCumulative = 0m;
                decimal maxDdPoints = 0m;

                foreach (var trade in trades)
                {
                    currentCumulative += trade.RealizedPoints;
                    if (currentCumulative > peakPoints)
                    {
                        peakPoints = currentCumulative;
                    }

                    decimal dd = peakPoints - currentCumulative;
                    if (dd > maxDdPoints)
                    {
                        maxDdPoints = dd;
                    }
                }

                metrics.MaxDrawdownPoints = maxDdPoints;
                return metrics;
            }

            /// <summary>
            /// Prints backtest metrics summary directly to console output.
            /// </summary>
            public static void PrintSummary(PerformanceMetrics m)
            {
                Console.WriteLine("\n=================================================");
                Console.WriteLine("             BACKTEST PERFORMANCE SUMMARY        ");
                Console.WriteLine("=================================================");
                Console.WriteLine($"Total Trades Executed : {m.TotalTrades}");
                Console.WriteLine($"Winning Trades       : {m.WinningTrades}");
                Console.WriteLine($"Losing Trades        : {m.LosingTrades}");
                Console.WriteLine($"Win Rate             : {m.WinRatePct:F2}%");
                Console.WriteLine("-------------------------------------------------");
                Console.WriteLine($"Total Realized Points: {m.TotalPointsGained:F2}");
                Console.WriteLine($"Gross Profit Points  : {m.GrossProfitPoints:F2}");
                Console.WriteLine($"Gross Loss Points    : {m.GrossLossPoints:F2}");
                Console.WriteLine($"Profit Factor        : {m.ProfitFactor:F2}");
                Console.WriteLine($"Avg Points / Trade   : {m.AverageTradePoints:F2}");
                Console.WriteLine($"Max Drawdown (Points): {m.MaxDrawdownPoints:F2}");
                Console.WriteLine("=================================================\n");
            }

            public static void ExportToCsv(List<Trade> trades, List<ExecutionLog> logs, string outputFolder)
            {
                Directory.CreateDirectory(outputFolder);

                // Export Trades CSV with Partial Leg Details
                string tradesPath = Path.Combine(outputFolder, "Trade_History.csv");
                var sbTrades = new StringBuilder();

                sbTrades.AppendLine("TradeId,EntryTime,FinalExitTime,EntryPrice,NetExitPrice,RiskPoints,RealizedPoints,RealizedReturnPct,Leg1_Price,Leg1_Reason,Leg2_Price,Leg2_Reason");

                foreach (var t in trades)
                {
                    var leg1 = t.ExecutedLegs.FirstOrDefault();
                    var leg2 = t.ExecutedLegs.Skip(1).FirstOrDefault();

                    string leg1Info = leg1 != null ? $"{leg1.ExitPrice:F2},{leg1.ExitReason}" : ",";
                    string leg2Info = leg2 != null ? $"{leg2.ExitPrice:F2},{leg2.ExitReason}" : ",";

                    sbTrades.AppendLine($"{t.TradeId},{t.EntryTime:yyyy-MM-dd HH:mm},{t.FinalExitTime:yyyy-MM-dd HH:mm},{t.EntryPrice:F2},{t.NetExitPrice:F2},{t.RiskPoints:F2},{t.RealizedPoints:F2},{t.RealizedReturnPct:F2}%,{leg1Info},{leg2Info}");
                }
                File.WriteAllText(tradesPath, sbTrades.ToString());

                // Export Detailed Execution Logs CSV
                string logsPath = Path.Combine(outputFolder, "Execution_Logs.csv");
                var sbLogs = new StringBuilder();
                sbLogs.AppendLine("TimeStamp,EventType,Price,EMA5,DEMA5,RSI,NearestPivot,Reason");

                foreach (var l in logs)
                {
                    sbLogs.AppendLine($"{l.Time:yyyy-MM-dd HH:mm},{l.EventType},{l.Price:F2},{l.Ema5:F2},{l.Dema5:F2},{l.Rsi:F2},{l.NearestPivot:F2},\"{l.Reason}\"");
                }
                File.WriteAllText(logsPath, sbLogs.ToString());

                Console.WriteLine($"Results exported successfully to:\n - {tradesPath}\n - {logsPath}");
            }
        }
    }
}