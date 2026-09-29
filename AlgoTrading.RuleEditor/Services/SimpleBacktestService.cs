using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AlgoTrading.Models;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public sealed class SimpleBacktestService
    {
        public BacktestResult RunCsv(StrategyRuntimeSnapshot strategy, string path, decimal quantity)
        {
            ArgumentNullException.ThrowIfNull(strategy);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

            BacktestData data = LoadCsv(path, strategy.Document.EvaluationTimeframe);
            foreach (string timeframe in strategy.Entry.Plan.RequiredTimeframes.Union(strategy.FullExit.Plan.RequiredTimeframes))
            {
                if (!data.Series.ContainsKey(timeframe))
                {
                    throw new InvalidDataException("CSV does not contain the required timeframe '" + timeframe + "'. Add a Timeframe column and rows for every required series.");
                }
            }
            return Run(strategy, data, quantity);
        }

        internal BacktestResult Run(StrategyRuntimeSnapshot strategy, BacktestData data, decimal quantity)
        {
            StrategyDocument document = strategy.Document;
            if (!string.Equals(document.Execution.Evaluation, "Completed candle", StringComparison.Ordinal))
            {
                throw new NotSupportedException("The backtester evaluates completed candles only.");
            }
            if (document.RetestRequired || !string.Equals(document.LevelSelection, "None", StringComparison.Ordinal))
            {
                throw new NotSupportedException("Level selection and retest sequences require an external level provider and are not part of the personal backtester.");
            }

            List<Target> targets = ParseTargets(document);
            IReadOnlyList<CompletedCandle> clock = data.Series[document.EvaluationTimeframe];
            RuleMarketData marketData = new RuleMarketData(data.InstrumentToken, data.Series.ToDictionary(item => item.Key, item => (IReadOnlyList<CompletedCandle>)item.Value, StringComparer.Ordinal));
            BoundRuleExecution entryRule = strategy.Entry.BindData(marketData);
            BoundRuleExecution exitRule = strategy.FullExit.BindData(marketData);
            List<BacktestTrade> trades = new List<BacktestTrade>();
            Position? position = null;
            bool pendingEntry = false;
            bool pendingRuleExit = false;
            bool hasTraded = false;

            for (int index = 0; index < clock.Count; index++)
            {
                CompletedCandle completed = clock[index];
                Candle candle = completed.Candle;
                bool exitedAtOpen = false;

                if (position != null && pendingRuleExit)
                {
                    Close(position, candle.Open, completed.Candle.OpenedAt, position.Remaining, "Full-exit rule", trades, document.Direction);
                    position = null;
                    pendingRuleExit = false;
                    exitedAtOpen = true;
                }
                if (position == null && pendingEntry && !exitedAtOpen)
                {
                    position = Open(document, targets, quantity, candle.Open, candle.OpenedAt);
                    pendingEntry = false;
                    hasTraded = true;
                }

                if (position != null)
                {
                    ProcessCandle(document, position, completed, trades);
                    if (position.Remaining == 0) position = null;
                }

                if (position != null && exitRule.Evaluate(completed.ClosedAt).IsMatch)
                {
                    pendingRuleExit = true;
                }
                if (position == null && !pendingEntry && !exitedAtOpen
                    && (!hasTraded || !string.Equals(document.Execution.ReEntry, "Disabled", StringComparison.Ordinal))
                    && entryRule.Evaluate(completed.ClosedAt).IsMatch
                    && index + 1 < clock.Count)
                {
                    pendingEntry = true;
                }
            }

            if (position != null)
            {
                CompletedCandle final = clock[clock.Count - 1];
                Close(position, final.Candle.Close, final.ClosedAt, position.Remaining, "End of data", trades, document.Direction);
            }
            return new BacktestResult(trades, clock.Count);
        }

        private static Position Open(StrategyDocument document, IReadOnlyList<Target> targets, decimal quantity, decimal price, DateTimeOffset time)
        {
            decimal? stop = null;
            if (document.Protection.Enabled)
            {
                if (string.Equals(document.Protection.Mode, "Fixed percent", StringComparison.Ordinal))
                {
                    decimal distance = price * document.Protection.Value / 100m;
                    stop = IsLong(document) ? price - distance : price + distance;
                }
                else if (string.Equals(document.Protection.Mode, "Fixed points", StringComparison.Ordinal))
                {
                    stop = IsLong(document) ? price - document.Protection.Value : price + document.Protection.Value;
                }
                else
                {
                    throw new NotSupportedException("Reference-level stops require an external level provider. Use Fixed percent or Fixed points for backtesting.");
                }
            }
            if (targets.Any(target => target.IsRiskMultiple) && stop == null)
            {
                throw new InvalidOperationException("R-multiple targets require an enabled fixed stop.");
            }
            return new Position(time, price, quantity, stop, targets);
        }

        private static void ProcessCandle(StrategyDocument document, Position position, CompletedCandle completed, List<BacktestTrade> trades)
        {
            Candle candle = completed.Candle;
            bool stopHit = position.Stop.HasValue && (IsLong(document) ? candle.Low <= position.Stop.Value : candle.High >= position.Stop.Value);
            List<Target> hitTargets = position.Targets.Where(target => !target.Executed && TargetPrice(position, target, document) is decimal price
                && (IsLong(document) ? candle.High >= price : candle.Low <= price)).ToList();

            bool targetsFirst = string.Equals(document.Execution.StopTargetPriority, "Target first", StringComparison.Ordinal);
            if (stopHit && !targetsFirst)
            {
                Close(position, GapAwareStopPrice(position.Stop!.Value, candle.Open, document), completed.ClosedAt, position.Remaining, "Protective stop", trades, document.Direction);
                return;
            }

            int targetLimit = string.Equals(document.Execution.SameCandleActions, "All eligible exits", StringComparison.Ordinal) ? hitTargets.Count : Math.Min(1, hitTargets.Count);
            for (int index = 0; index < targetLimit && position.Remaining > 0; index++)
            {
                Target target = hitTargets[index];
                decimal requested = string.Equals(target.QuantityBasis, "Remaining", StringComparison.Ordinal)
                    ? position.Remaining * target.QuantityPercent / 100m
                    : position.OriginalQuantity * target.QuantityPercent / 100m;
                decimal exitQuantity = Math.Min(position.Remaining, requested);
                target.Executed = true;
                Close(position, TargetPrice(position, target, document)!.Value, completed.ClosedAt, exitQuantity, target.Label, trades, document.Direction);
            }

            if (stopHit && position.Remaining > 0)
            {
                Close(position, GapAwareStopPrice(position.Stop!.Value, candle.Open, document), completed.ClosedAt, position.Remaining, "Protective stop", trades, document.Direction);
            }
        }

        private static decimal GapAwareStopPrice(decimal stop, decimal open, StrategyDocument document)
        {
            return IsLong(document) && open < stop || !IsLong(document) && open > stop ? open : stop;
        }

        private static decimal? TargetPrice(Position position, Target target, StrategyDocument document)
        {
            decimal distance = target.IsRiskMultiple
                ? Math.Abs(position.EntryPrice - position.Stop!.Value) * target.Value
                : position.EntryPrice * target.Value / 100m;
            return IsLong(document) ? position.EntryPrice + distance : position.EntryPrice - distance;
        }

        private static void Close(Position position, decimal price, DateTimeOffset time, decimal quantity, string reason, List<BacktestTrade> trades, string direction)
        {
            if (quantity <= 0) return;
            decimal signedMove = string.Equals(direction, "Short", StringComparison.Ordinal) ? position.EntryPrice - price : price - position.EntryPrice;
            position.Profit += signedMove * quantity;
            position.ExitNotional += price * quantity;
            position.ExitedQuantity += quantity;
            position.ExitTime = time;
            position.Reasons.Add(reason);
            position.Remaining -= quantity;
            if (position.Remaining == 0)
            {
                trades.Add(new BacktestTrade
                {
                    EntryTime = position.EntryTime,
                    EntryPrice = position.EntryPrice,
                    ExitTime = position.ExitTime,
                    AverageExitPrice = position.ExitNotional / position.ExitedQuantity,
                    Quantity = position.OriginalQuantity,
                    Profit = position.Profit,
                    ExitReason = string.Join(" + ", position.Reasons.Distinct(StringComparer.Ordinal))
                });
            }
        }

        private static List<Target> ParseTargets(StrategyDocument document)
        {
            List<Target> result = new List<Target>();
            foreach (PartialExitStage stage in document.PartialExits.Where(stage => stage.Enabled))
            {
                string value = stage.Target.Trim();
                bool risk = value.EndsWith("R", StringComparison.OrdinalIgnoreCase);
                bool percent = value.EndsWith("%", StringComparison.Ordinal);
                string number = risk || percent ? value.Substring(0, value.Length - 1) : value;
                if ((!risk && !percent) || !decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) || parsed <= 0)
                {
                    throw new InvalidDataException("Partial-exit target '" + stage.Target + "' must be a positive R multiple (for example 1R) or entry-price percent (for example 2%).");
                }
                result.Add(new Target(stage.Label, stage.QuantityPercent, stage.QuantityBasis, parsed, risk));
            }
            return result;
        }

        private static bool IsLong(StrategyDocument document) => !string.Equals(document.Direction, "Short", StringComparison.Ordinal);

        private static BacktestData LoadCsv(string path, string defaultTimeframe)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2) throw new InvalidDataException("CSV must contain a header and at least one candle.");
            string[] headers = Split(lines[0]);
            Dictionary<string, int> columns = headers.Select((name, index) => new { Name = name.Trim(), Index = index })
                .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);
            string[] required = { "OpenedAt", "ClosedAt", "Open", "High", "Low", "Close", "Volume" };
            foreach (string name in required) if (!columns.ContainsKey(name)) throw new InvalidDataException("CSV is missing column '" + name + "'.");

            Dictionary<string, List<CompletedCandle>> series = new Dictionary<string, List<CompletedCandle>>(StringComparer.Ordinal);
            int? token = null;
            for (int lineNumber = 2; lineNumber <= lines.Length; lineNumber++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineNumber - 1])) continue;
                string[] cells = Split(lines[lineNumber - 1]);
                string timeframe = Get(cells, columns, "Timeframe", defaultTimeframe);
                int rowToken = int.Parse(Get(cells, columns, "InstrumentToken", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture);
                token ??= rowToken;
                if (token.Value != rowToken) throw new InvalidDataException("CSV contains more than one instrument token at line " + lineNumber + ".");
                DateTimeOffset opened = DateTimeOffset.Parse(Get(cells, columns, "OpenedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                DateTimeOffset closed = DateTimeOffset.Parse(Get(cells, columns, "ClosedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (closed <= opened) throw new InvalidDataException("ClosedAt must be after OpenedAt at line " + lineNumber + ".");
                Candle candle = new Candle(rowToken, timeframe, opened.UtcDateTime,
                    Decimal(cells, columns, "Open"), Decimal(cells, columns, "High"), Decimal(cells, columns, "Low"), Decimal(cells, columns, "Close"),
                    long.Parse(Get(cells, columns, "Volume"), NumberStyles.Integer, CultureInfo.InvariantCulture));
                if (candle.High < Math.Max(candle.Open, candle.Close) || candle.Low > Math.Min(candle.Open, candle.Close) || candle.Low > candle.High)
                    throw new InvalidDataException("Invalid OHLC values at line " + lineNumber + ".");
                if (!series.TryGetValue(timeframe, out List<CompletedCandle>? list)) series.Add(timeframe, list = new List<CompletedCandle>());
                list.Add(new CompletedCandle(candle, closed));
            }
            foreach (List<CompletedCandle> list in series.Values)
            {
                list.Sort((left, right) => left.ClosedAt.CompareTo(right.ClosedAt));
                if (list.Select(item => item.ClosedAt).Distinct().Count() != list.Count) throw new InvalidDataException("CSV contains duplicate candle close times within a timeframe.");
            }
            return new BacktestData(token ?? 1, series);
        }

        private static decimal Decimal(string[] cells, Dictionary<string, int> columns, string name) => decimal.Parse(Get(cells, columns, name), NumberStyles.Number, CultureInfo.InvariantCulture);
        private static string Get(string[] cells, Dictionary<string, int> columns, string name, string? fallback = null)
        {
            if (!columns.TryGetValue(name, out int index)) return fallback ?? throw new InvalidDataException("CSV is missing column '" + name + "'.");
            if (index >= cells.Length || string.IsNullOrWhiteSpace(cells[index])) return fallback ?? throw new InvalidDataException("CSV column '" + name + "' contains an empty value.");
            return cells[index].Trim();
        }

        private static string[] Split(string line)
        {
            List<string> cells = new List<string>();
            bool quoted = false;
            System.Text.StringBuilder cell = new System.Text.StringBuilder();
            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                if (current == '"' && quoted && index + 1 < line.Length && line[index + 1] == '"') { cell.Append('"'); index++; }
                else if (current == '"') quoted = !quoted;
                else if (current == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(current);
            }
            if (quoted) throw new InvalidDataException("CSV contains an unterminated quoted field.");
            cells.Add(cell.ToString());
            return cells.ToArray();
        }

        internal sealed class BacktestData
        {
            public BacktestData(int instrumentToken, Dictionary<string, List<CompletedCandle>> series) { InstrumentToken = instrumentToken; Series = series; }
            public int InstrumentToken { get; }
            public Dictionary<string, List<CompletedCandle>> Series { get; }
        }

        private sealed class Position
        {
            public Position(DateTimeOffset time, decimal price, decimal quantity, decimal? stop, IReadOnlyList<Target> targets)
            {
                EntryTime = time; EntryPrice = price; OriginalQuantity = quantity; Remaining = quantity; Stop = stop;
                Targets = targets.Select(target => target.Copy()).ToList();
            }
            public DateTimeOffset EntryTime { get; }
            public decimal EntryPrice { get; }
            public decimal OriginalQuantity { get; }
            public decimal Remaining { get; set; }
            public decimal? Stop { get; }
            public List<Target> Targets { get; }
            public decimal Profit { get; set; }
            public decimal ExitNotional { get; set; }
            public decimal ExitedQuantity { get; set; }
            public DateTimeOffset ExitTime { get; set; }
            public List<string> Reasons { get; } = new List<string>();
        }

        private sealed class Target
        {
            public Target(string label, decimal quantityPercent, string quantityBasis, decimal value, bool isRiskMultiple)
            { Label = label; QuantityPercent = quantityPercent; QuantityBasis = quantityBasis; Value = value; IsRiskMultiple = isRiskMultiple; }
            public string Label { get; }
            public decimal QuantityPercent { get; }
            public string QuantityBasis { get; }
            public decimal Value { get; }
            public bool IsRiskMultiple { get; }
            public bool Executed { get; set; }
            public Target Copy() => new Target(Label, QuantityPercent, QuantityBasis, Value, IsRiskMultiple);
        }
    }
}
