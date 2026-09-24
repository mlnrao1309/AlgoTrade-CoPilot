using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public static class EditorExamples
    {
        public static EditorValue Close(string timeframe)
        {
            EditorValue value = new EditorValue();
            value.Kind = ValueKind.Candle;
            value.Timeframe = timeframe;
            return value;
        }

        public static EditorCondition NewComparison(string timeframe)
        {
            EditorCondition condition = new EditorCondition();
            condition.Kind = ConditionKind.Comparison;
            condition.Left = Close(timeframe);
            return condition;
        }

        public static EditorDocument Blank()
        {
            EditorDocument document = new EditorDocument();
            document.Root.Children.Add(NewComparison(document.EvaluationTimeframe));
            return document;
        }

        public static EditorDocument MovingAverageBreakout()
        {
            EditorDocument document = new EditorDocument();
            document.Name = "Moving average breakout";
            EditorValue average = new EditorValue();
            average.Kind = ValueKind.Indicator;
            average.Function = IndicatorFunction.ExponentialMovingAverage;
            average.Length = 5;
            average.Source = Close(document.EvaluationTimeframe);
            EditorValue nested = EditorCopyService.Copy(average);
            nested.Source = EditorCopyService.Copy(average);
            EditorCondition crossover = NewComparison(document.EvaluationTimeframe);
            crossover.Left = EditorCopyService.Copy(average);
            crossover.Operator = ComparisonOperator.CrossedAbove;
            crossover.Right = nested;
            document.Root.Children.Add(crossover);
            EditorCondition trendCondition = NewComparison(document.EvaluationTimeframe);
            trendCondition.Right.Kind = ValueKind.Indicator;
            trendCondition.Right.Function = IndicatorFunction.SuperTrend;
            trendCondition.Right.Length = 10;
            trendCondition.Right.Multiplier = 3;
            document.Root.Children.Add(trendCondition);
            EditorCondition averageCondition = NewComparison(document.EvaluationTimeframe);
            averageCondition.Right = EditorCopyService.Copy(average);
            document.Root.Children.Add(averageCondition);
            EditorCondition previousHigh = NewComparison(document.EvaluationTimeframe);
            previousHigh.Right = Close(document.EvaluationTimeframe);
            previousHigh.Right.Field = CandleField.High;
            previousHigh.Right.CandlesAgo = 1;
            document.Root.Children.Add(previousHigh);
            return document;
        }
    }
}
