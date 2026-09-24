using System;
using System.Globalization;
using System.Text;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public static class EditorLabels
    {
        public static string Value(EditorValue value)
        {
            switch (value.Kind)
            {
                case ValueKind.Number:
                    return value.Number.ToString(CultureInfo.InvariantCulture);
                case ValueKind.Candle:
                    string offset = "Current";
                    if (value.CandlesAgo == 1)
                    {
                        offset = "Previous";
                    }
                    else if (value.CandlesAgo > 1)
                    {
                        offset = value.CandlesAgo.ToString(CultureInfo.InvariantCulture) + " candles ago";
                    }
                    return offset + " " + EditorCatalog.TimeframeLabel(value.Timeframe) + " " + value.Field.ToString().ToLowerInvariant();
                case ValueKind.Indicator:
                    string description = EditorCatalog.TimeframeLabel(value.Timeframe) + " · " + RuleLanguage.IndicatorLabel(value.Function) + " (" + value.Length.ToString(CultureInfo.InvariantCulture);
                    if (value.Function == IndicatorFunction.SuperTrend || value.Function == IndicatorFunction.BollingerUpperBand || value.Function == IndicatorFunction.BollingerLowerBand)
                    {
                        description = description + ", " + value.Multiplier.ToString(CultureInfo.InvariantCulture);
                    }
                    description = description + ")";
                    if (value.Function != IndicatorFunction.SuperTrend)
                    {
                        if (value.Source == null)
                        {
                            description = description + " of close";
                        }
                        else
                        {
                            description = description + " of [" + Value(value.Source) + "]";
                        }
                    }
                    return description;
                case ValueKind.Arithmetic:
                    return "(" + OptionalValue(value.Left) + " " + value.Arithmetic.ToString().ToLowerInvariant() + " " + OptionalValue(value.Right) + ")";
                case ValueKind.Round:
                    return "Round [" + OptionalValue(value.Source) + "] to " + value.DecimalPlaces + " decimal places";
                case ValueKind.Previous:
                    return OptionalValue(value.Source) + " · " + value.CandlesAgo + " previous " + EditorCatalog.TimeframeLabel(value.Timeframe) + " candles";
                case ValueKind.Count:
                    return "Count matches over " + value.Length + " " + EditorCatalog.TimeframeLabel(value.Timeframe) + " candles, ending " + value.CandlesAgo + " candles ago";
                default:
                    return "Choose a value";
            }
        }

        private static string OptionalValue(EditorValue? value)
        {
            if (value == null)
            {
                return "Choose an input";
            }
            return Value(value);
        }

        public static string Condition(EditorCondition condition, int depth)
        {
            string prefix = new string(' ', depth * 3);
            string disabled = string.Empty;
            if (!condition.Enabled)
            {
                disabled = "[Disabled] ";
            }
            if (condition.Kind == ConditionKind.Comparison)
            {
                return prefix + disabled + Value(condition.Left) + " " + RuleLanguage.ComparisonLabel(condition.Operator) + " " + Value(condition.Right);
            }
            StringBuilder result = new StringBuilder();
            result.Append(prefix);
            result.Append(disabled);
            result.AppendLine(condition.Kind.ToString() + " of the following:");
            foreach (EditorCondition child in condition.Children)
            {
                result.AppendLine(Condition(child, depth + 1));
            }
            return result.ToString().TrimEnd();
        }
    }
}
