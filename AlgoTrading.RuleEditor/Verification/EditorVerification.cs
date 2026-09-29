using System;
using System.IO;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;
using AlgoTrading.RuleEditor.Services;

namespace AlgoTrading.RuleEditor.Verification
{
    public sealed class EditorVerification
    {
        private int _assertions;

        public EditorVerification()
        {
            _assertions = 0;
        }

        public void Run(string reportPath)
        {
            RuleDocumentAdapter adapter = new RuleDocumentAdapter();
            EditorDocument example = EditorExamples.MovingAverageBreakout();
            RuleDefinition definition = adapter.ToDefinition(example);
            BoundRule bound = RuleBinder.Bind(definition);
            Assert(bound.Plan.RequiredTimeframes.Count == 1, "Example uses one timeframe.");
            Assert(bound.Plan.IndicatorCalculations.Count == 3, "Nested indicators and SuperTrend are discovered.");
            string originalJson = RuleDefinitionJson.Serialize(definition);
            string restoredJson = RuleDefinitionJson.Serialize(adapter.ToDefinition(adapter.FromDefinition(definition)));
            Assert(originalJson == restoredJson, "The example round-trips without losing metadata or inputs.");
            EditorDocument copy = EditorCopyService.Copy(example);
            copy.Root.Children[0].Left.Length = 99;
            Assert(example.Root.Children[0].Left.Length == 5, "Value drafts are independent from the document.");
            VerifyValueKinds(adapter);
            VerifyGroups(adapter);
            VerifyHistory(example);
            VerifyExecution(adapter);
            VerifyStrategyDocuments();
            VerifyBacktest();
            File.WriteAllText(reportPath, "Passed " + _assertions + " editor verification assertions.");
        }

        private void VerifyBacktest()
        {
            StrategyDocumentService documents = new StrategyDocumentService();
            StrategyDocument strategy = documents.CreateBlank();
            strategy.Name = "Backtest verification";
            strategy.Instrument = "TEST";
            strategy.Execution.ReEntry = "Disabled";
            strategy.Execution.SameCandleActions = "All eligible exits";
            strategy.Protection.Value = 10m;
            strategy.FullExit.Children[0].Right.Number = 1000;
            strategy.PartialExits.Add(new PartialExitStage { Label = "1R", QuantityPercent = 50m, QuantityBasis = "Original", Target = "1R" });
            strategy.PartialExits.Add(new PartialExitStage { Label = "2R", QuantityPercent = 100m, QuantityBasis = "Remaining", Target = "2R" });
            string csvPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(csvPath,
                    "Timeframe,InstrumentToken,OpenedAt,ClosedAt,Open,High,Low,Close,Volume\n" +
                    "15minute,1,2026-01-01T09:15:00+05:30,2026-01-01T09:30:00+05:30,10,10,10,10,100\n" +
                    "15minute,1,2026-01-01T09:30:00+05:30,2026-01-01T09:45:00+05:30,10,11.5,9.5,11,100\n" +
                    "15minute,1,2026-01-01T09:45:00+05:30,2026-01-01T10:00:00+05:30,11,12.5,10.5,12,100");
                StrategyRuntimeSnapshot snapshot = new StrategyRuntimeAdapter().Load(documents.Serialize(strategy));
                BacktestResult result = new SimpleBacktestService().RunCsv(snapshot, csvPath, 100m);
                Assert(result.Trades.Count == 1, "Partial exits are consolidated into one completed trade.");
                Assert(result.NetProfit == 150m, "Backtest applies original and remaining quantity bases deterministically.");
                Assert(result.Trades[0].AverageExitPrice == 11.5m, "Backtest reports the quantity-weighted exit price.");
                Assert(result.CandlesProcessed == 3, "Backtest processes each completed evaluation candle once.");
            }
            finally
            {
                File.Delete(csvPath);
            }
        }

        private void VerifyStrategyDocuments()
        {
            StrategyDocumentService documents = new StrategyDocumentService();
            StrategyDocument strategy = documents.CreateBlank();
            strategy.Name = "Verification strategy";
            strategy.Instrument = "TEST";
            strategy.LevelSelection = "Critical Resistance";
            strategy.LevelBinding = "breakout-level";
            strategy.RetestRequired = true;
            strategy.PartialExits.Add(new PartialExitStage { Label = "First", QuantityPercent = 50m, QuantityBasis = "Original", Target = "1R" });
            strategy.PartialExits.Add(new PartialExitStage { Label = "Second", QuantityPercent = 80m, QuantityBasis = "Remaining", Target = "2R" });
            string json = documents.Serialize(strategy);
            StrategyDocument restored = documents.Deserialize(json, out bool importedLegacy);
            Assert(!importedLegacy, "A strategy document is not mistaken for a legacy rule.");
            Assert(documents.Serialize(restored) == json, "A complete strategy round-trips without changing its JSON meaning.");
            Assert(restored.PartialExits[0].QuantityBasis == "Original" && restored.PartialExits[1].QuantityBasis == "Remaining", "Partial-exit quantity bases remain distinct.");
            Assert(documents.Validate(restored).Count == 0, "A complete strategy passes authoring validation.");
            StrategyRuntimeSnapshot snapshot = new StrategyRuntimeAdapter().Load(json);
            restored.Name = "Changed after load";
            Assert(snapshot.Document.Name == "Verification strategy", "Runtime snapshots are independent from editor changes.");
            string legacy = RuleDefinitionJson.Serialize(new RuleDocumentAdapter().ToDefinition(EditorExamples.MovingAverageBreakout()));
            StrategyDocument imported = documents.Deserialize(legacy, out importedLegacy);
            Assert(importedLegacy && imported.Entry.Children.Count > 0, "Legacy single-rule JSON imports without changing the original.");
            bool rejected = false;
            try { documents.Deserialize("{\"SchemaVersion\":99}", out importedLegacy); }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected, "Unsupported strategy versions fail explicitly.");
            StrategyHistory history = new StrategyHistory();
            history.Remember(strategy);
            StrategyDocument changed = EditorCopyService.Copy(strategy);
            changed.PartialExits[0].QuantityPercent = 25m;
            Assert(history.Undo(changed).PartialExits[0].QuantityPercent == 50m, "Undo covers strategy-level partial exits.");
        }

        private void VerifyValueKinds(RuleDocumentAdapter adapter)
        {
            foreach (ValueKind kind in Enum.GetValues<ValueKind>())
            {
                EditorValue value = new EditorValue();
                value.Kind = kind;
                value.Source = EditorExamples.Close("15minute");
                value.Left = EditorExamples.Close("15minute");
                value.Right = new EditorValue();
                value.Right.Number = 3;
                value.CountCondition = EditorExamples.Blank().Root;
                adapter.ValidateValue(value);
                EditorDocument document = EditorExamples.Blank();
                document.Root.Children[0].Left = value;
                RuleDefinition definition = adapter.ToDefinition(document);
                EditorDocument restored = adapter.FromDefinition(definition);
                Assert(restored.Root.Children[0].Left.Kind == kind, "Supported value type round trip: " + kind);
                Assert(RuleDefinitionJson.Serialize(definition) == RuleDefinitionJson.Serialize(adapter.ToDefinition(restored)), "Supported value settings round trip: " + kind);
            }
        }

        private void VerifyGroups(RuleDocumentAdapter adapter)
        {
            EditorDocument document = EditorExamples.Blank();
            document.Root.Kind = ConditionKind.None;
            document.Root.Comment = "Do not match this condition";
            document.Root.Children[0].Comment = "A retained comment";
            RuleDefinition definition = adapter.ToDefinition(document);
            Assert(definition.Condition is NotCondition, "None exports as a logical negation.");
            EditorDocument restored = adapter.FromDefinition(definition);
            Assert(restored.Root.Kind == ConditionKind.None && restored.Root.Children.Count == 1, "None does not accumulate extra groups after opening.");
            Assert(restored.Root.Children[0].Comment == "A retained comment", "Comments survive saving.");
            document.Root.Children[0].Enabled = false;
            bool rejected = false;
            try
            {
                RuleBinder.Bind(adapter.ToDefinition(document));
            }
            catch (ArgumentException)
            {
                rejected = true;
            }
            Assert(rejected, "An entirely disabled group cannot be saved as executable.");
        }

        private void VerifyHistory(EditorDocument document)
        {
            EditorHistory history = new EditorHistory();
            history.Remember(document);
            EditorDocument changed = EditorCopyService.Copy(document);
            changed.Name = "Changed";
            EditorDocument restored = history.Undo(changed);
            Assert(restored.Name == document.Name, "Undo restores the earlier document.");
            Assert(history.Redo(restored).Name == "Changed", "Redo restores the edit.");
        }

        private void VerifyExecution(RuleDocumentAdapter adapter)
        {
            EditorDocument document = EditorExamples.Blank();
            EditorCondition comparison = document.Root.Children[0];
            comparison.Operator = ComparisonOperator.CrossedAbove;
            comparison.Right.Number = 2;
            DateTimeOffset origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            System.Collections.Generic.List<CompletedCandle> candles = new System.Collections.Generic.List<CompletedCandle>();
            candles.Add(new CompletedCandle(new AlgoTrading.Models.Candle(1, "15minute", origin.UtcDateTime, 1, 1, 1, 1, 1), origin.AddMinutes(15)));
            candles.Add(new CompletedCandle(new AlgoTrading.Models.Candle(1, "15minute", origin.UtcDateTime, 3, 3, 3, 3, 1), origin.AddMinutes(30)));
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<CompletedCandle>> series = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<CompletedCandle>>();
            series.Add("15minute", candles);
            RuleMarketData data = new RuleMarketData(1, series);
            BoundRuleExecution execution = RuleBinder.Bind(adapter.ToDefinition(document)).BindData(data);
            Assert(execution.Evaluate(origin.AddMinutes(30)).IsMatch, "A visually authored crossover executes through Models.");
            Assert(!execution.Evaluate(origin.AddMinutes(29)).IsMatch, "The generated rule does not read a forming candle.");
        }

        private void Assert(bool condition, string message)
        {
            _assertions = _assertions + 1;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
