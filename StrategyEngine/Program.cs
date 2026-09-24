using AlgorithmicEngine.AlgorithmicEngine;
using AlgorithmicEngine.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace AlgorithmicEngine
{
    internal partial class Program
    {
        public static string connectionString = "Server=MANCHIKANTI\\MANCHIKANTI;Database=Algo_Trading_CFCore;Trusted_Connection=True;Encrypt=false;TrustServerCertificate=True;";

        protected static long targetInstrument = 265;//60417; // Replace with your actual InstrumentToken
        protected static string timeFrame = "hour";

        static void Main(string[] args)
        {

            

            Console.WriteLine($"Fetching data for Instrument: {targetInstrument} | TimeFrame: {timeFrame}...");

            var repository = new MarketDataRepository(connectionString);
            List<ResistanceSupportData> srData = new List<ResistanceSupportData>();
            List<ExtendedCandle> candles = repository.GetMarketData(targetInstrument, timeFrame, srData: out srData);

            //BuildSnapShotData(candles, srData);
            //return;
            if (candles.Count < 20)
            {
                Console.WriteLine("Insufficient historical bars for backtesting.");
                return;
            }

            Console.WriteLine($"Loaded {candles.Count} candles. Calculating indicators...");

            decimal[] closes = candles.Select(c => c.Close).ToArray();
            decimal[] ema5 = CalculateEMA(closes, 5);
            decimal[] dema5 = CalculateDEMA(closes, 5);
            decimal[] rsi14 = CalculateRSI(closes, 14);

            var strategy = new BearishRetestStrategy(new StrategyParameters());

            for (int i = 1; i < candles.Count; i++)
            {
                strategy.ProcessBar(candles[i], candles[i - 1], rsi14[i], ema5[i], dema5[i]);
            }

            // ---------------------------------------------------------
            // STEP 2 METRICS GENERATION & LOGGING
            // ---------------------------------------------------------
            var metrics = BacktestAnalytics.CalculateMetrics(strategy.ClosedTrades);
            BacktestAnalytics.PrintSummary(metrics);

            string exportFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BacktestResults");
            BacktestAnalytics.ExportToCsv(strategy.ClosedTrades, strategy.ExecutionLogs, exportFolder);
        }

        #region Indicators Math

        private static decimal[] CalculateEMA(decimal[] prices, int period)
        {
            decimal[] ema = new decimal[prices.Length];
            if (prices.Length < period) return ema;

            decimal multiplier = 2.0m / (period + 1);
            ema[period - 1] = prices.Take(period).Average();

            for (int i = period; i < prices.Length; i++)
            {
                ema[i] = ((prices[i] - ema[i - 1]) * multiplier) + ema[i - 1];
            }
            return ema;
        }

        private static decimal[] CalculateDEMA(decimal[] prices, int period)
        {
            decimal[] ema1 = CalculateEMA(prices, period);
            decimal[] ema2 = CalculateEMA(ema1, period);
            decimal[] dema = new decimal[prices.Length];

            for (int i = 0; i < prices.Length; i++)
            {
                dema[i] = (2 * ema1[i]) - ema2[i];
            }
            return dema;
        }

        private static decimal[] CalculateRSI(decimal[] prices, int period)
        {
            decimal[] rsi = new decimal[prices.Length];
            if (prices.Length <= period) return rsi;

            decimal gains = 0m, losses = 0m;
            for (int i = 1; i <= period; i++)
            {
                decimal diff = prices[i] - prices[i - 1];
                if (diff >= 0) gains += diff;
                else losses += Math.Abs(diff);
            }

            decimal avgGain = gains / period;
            decimal avgLoss = losses / period;
            rsi[period] = avgLoss == 0m ? 100m : 100m - (100m / (1m + (avgGain / avgLoss)));

            for (int i = period + 1; i < prices.Length; i++)
            {
                decimal diff = prices[i] - prices[i - 1];
                decimal currentGain = diff > 0 ? diff : 0m;
                decimal currentLoss = diff < 0 ? Math.Abs(diff) : 0m;

                avgGain = ((avgGain * (period - 1)) + currentGain) / period;
                avgLoss = ((avgLoss * (period - 1)) + currentLoss) / period;

                if (avgLoss == 0m) rsi[i] = 100m;
                else
                {
                    decimal rs = avgGain / avgLoss;
                    rsi[i] = 100m - (100m / (1m + rs));
                }
            }
            return rsi;
        }

        #endregion
        private static void BuildSnapShotData(List<ExtendedCandle> candles,  List<ResistanceSupportData> srData)
        {
            var snapshot = new StrategySnapshot();
            List<decimal> srLevels = new List<decimal>();
            // Populate SR lines (77 reference lines)
            
            foreach (var item in srData)
            {
                srLevels.AddRange(item.LineValue);

            }
            snapshot.ReferenceLines = srLevels;
            // Populate candles from SQL Server database
            snapshot.Candles = candles;

            // 2. Export to disk
            SnapshotManager.ExportSnapshot(snapshot, "strategy_snap.json");
        }
    }

    
}
//