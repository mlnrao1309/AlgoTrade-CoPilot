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
            File.WriteAllText(reportPath, "Passed " + _assertions + " editor verification assertions.");
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
