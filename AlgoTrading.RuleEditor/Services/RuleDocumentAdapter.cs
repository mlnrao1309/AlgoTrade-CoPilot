using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    /// <summary>Explicit boundary between mutable editor classes and the existing Models serialization contract.</summary>
    public sealed class RuleDocumentAdapter
    {
        public RuleDefinition ToDefinition(EditorDocument document)
        {
            JsonObject root = new JsonObject();
            root.Add("Name", document.Name);
            root.Add("EvaluationTimeframe", document.EvaluationTimeframe);
            root.Add("Condition", WriteCondition(document.Root, 0));
            return RuleDefinitionJson.Deserialize(root.ToJsonString());
        }

        public EditorDocument FromDefinition(RuleDefinition definition)
        {
            EditorDocument document = new EditorDocument();
            document.Name = definition.Name;
            document.EvaluationTimeframe = definition.EvaluationTimeframe;
            document.Root = ReadCondition(definition.Condition);
            return document;
        }

        public void ValidateValue(EditorValue value)
        {
            EditorDocument document = new EditorDocument();
            EditorCondition comparison = new EditorCondition();
            comparison.Kind = ConditionKind.Comparison;
            comparison.Left = value;
            comparison.Right = new EditorValue();
            document.Root = comparison;
            RuleBinder.Bind(ToDefinition(document));
        }

        private static void CheckDepth(int depth)
        {
            if (depth > 48)
            {
                throw new ArgumentException("This rule is too deeply nested. Use no more than 48 levels.");
            }
        }

        private JsonObject WriteCondition(EditorCondition condition, int depth)
        {
            CheckDepth(depth);
            JsonObject result = new JsonObject();
            if (condition.Kind == ConditionKind.Comparison)
            {
                result.Add("type", "comparison");
                result.Add("Left", WriteValue(condition.Left, depth + 1));
                result.Add("Operator", condition.Operator.ToString());
                result.Add("Right", WriteValue(condition.Right, depth + 1));
            }
            else
            {
                JsonArray children = new JsonArray();
                foreach (EditorCondition child in condition.Children)
                {
                    children.Add(WriteCondition(child, depth + 1));
                }
                if (condition.Kind == ConditionKind.None)
                {
                    JsonObject innerGroup = new JsonObject();
                    innerGroup.Add("type", "group");
                    innerGroup.Add("Operator", "Any");
                    innerGroup.Add("Conditions", children);
                    result.Add("type", "not");
                    result.Add("Condition", innerGroup);
                }
                else
                {
                    result.Add("type", "group");
                    result.Add("Operator", condition.Kind.ToString());
                    result.Add("Conditions", children);
                }
            }
            result.Add("Enabled", condition.Enabled);
            result.Add("Comment", condition.Comment);
            return result;
        }

        private JsonObject WriteValue(EditorValue value, int depth)
        {
            CheckDepth(depth);
            JsonObject result = new JsonObject();
            switch (value.Kind)
            {
                case ValueKind.Number:
                    result.Add("type", "number");
                    result.Add("Value", value.Number);
                    break;
                case ValueKind.Candle:
                    result.Add("type", "candle");
                    result.Add("Timeframe", value.Timeframe);
                    result.Add("Field", value.Field.ToString());
                    result.Add("CandlesAgo", value.CandlesAgo);
                    break;
                case ValueKind.Indicator:
                    result.Add("type", "indicator");
                    result.Add("Function", value.Function.ToString());
                    result.Add("Timeframe", value.Timeframe);
                    result.Add("Length", value.Length);
                    result.Add("Multiplier", value.Multiplier);
                    if (value.Function != IndicatorFunction.SuperTrend && value.Source != null)
                    {
                        result.Add("Source", WriteValue(value.Source, depth + 1));
                    }
                    break;
                case ValueKind.Arithmetic:
                    result.Add("type", "arithmetic");
                    result.Add("Left", WriteValue(RequireValue(value.Left), depth + 1));
                    result.Add("Operator", value.Arithmetic.ToString());
                    result.Add("Right", WriteValue(RequireValue(value.Right), depth + 1));
                    break;
                case ValueKind.Round:
                    result.Add("type", "round");
                    result.Add("Source", WriteValue(RequireValue(value.Source), depth + 1));
                    result.Add("DecimalPlaces", value.DecimalPlaces);
                    break;
                case ValueKind.Previous:
                    result.Add("type", "previous");
                    result.Add("Source", WriteValue(RequireValue(value.Source), depth + 1));
                    result.Add("Timeframe", value.Timeframe);
                    result.Add("CandlesAgo", value.CandlesAgo);
                    break;
                case ValueKind.Count:
                    if (value.CountCondition == null)
                    {
                        throw new ArgumentException("Choose the condition to count.");
                    }
                    result.Add("type", "count");
                    result.Add("Condition", WriteCondition(value.CountCondition, depth + 1));
                    result.Add("Timeframe", value.Timeframe);
                    result.Add("Length", value.Length);
                    result.Add("CandlesAgo", value.CandlesAgo);
                    break;
                default:
                    throw new ArgumentException("Choose a supported value type.");
            }
            return result;
        }

        private static EditorValue RequireValue(EditorValue? value)
        {
            if (value == null)
            {
                throw new ArgumentException("Choose an input for this calculation.");
            }
            return value;
        }

        private EditorCondition ReadCondition(ConditionDefinition condition)
        {
            EditorCondition result = new EditorCondition();
            result.Enabled = condition.Enabled;
            result.Comment = string.Empty;
            if (condition.Comment != null)
            {
                result.Comment = condition.Comment;
            }
            ComparisonCondition? comparison = condition as ComparisonCondition;
            ConditionGroup? group = condition as ConditionGroup;
            NotCondition? negation = condition as NotCondition;
            if (comparison != null)
            {
                result.Kind = ConditionKind.Comparison;
                result.Left = ReadValue(comparison.Left);
                result.Operator = comparison.Operator;
                result.Right = ReadValue(comparison.Right);
            }
            else if (group != null)
            {
                result.Kind = ConditionKind.All;
                if (group.Operator == GroupOperator.Any)
                {
                    result.Kind = ConditionKind.Any;
                }
                foreach (ConditionDefinition child in group.Conditions)
                {
                    result.Children.Add(ReadCondition(child));
                }
            }
            else if (negation != null)
            {
                result.Kind = ConditionKind.None;
                ConditionGroup? innerGroup = negation.Condition as ConditionGroup;
                if (innerGroup != null && innerGroup.Operator == GroupOperator.Any && innerGroup.Enabled && string.IsNullOrEmpty(innerGroup.Comment))
                {
                    foreach (ConditionDefinition child in innerGroup.Conditions)
                    {
                        result.Children.Add(ReadCondition(child));
                    }
                }
                else
                {
                    result.Children.Add(ReadCondition(negation.Condition));
                }
            }
            else
            {
                throw new ArgumentException("This condition is not supported by the editor.");
            }
            return result;
        }

        private EditorValue ReadValue(ValueDefinition definition)
        {
            EditorValue result = new EditorValue();
            NumberValue? number = definition as NumberValue;
            CandleValue? candle = definition as CandleValue;
            IndicatorValue? indicator = definition as IndicatorValue;
            ArithmeticValue? arithmetic = definition as ArithmeticValue;
            RoundValue? round = definition as RoundValue;
            PreviousValue? previous = definition as PreviousValue;
            CountValue? count = definition as CountValue;
            if (number != null)
            {
                result.Kind = ValueKind.Number;
                result.Number = number.Value;
            }
            else if (candle != null)
            {
                result.Kind = ValueKind.Candle;
                result.Timeframe = candle.Timeframe;
                result.Field = candle.Field;
                result.CandlesAgo = candle.CandlesAgo;
            }
            else if (indicator != null)
            {
                result.Kind = ValueKind.Indicator;
                result.Timeframe = indicator.Timeframe;
                result.Function = indicator.Function;
                result.Length = indicator.Length;
                result.Multiplier = indicator.Multiplier;
                if (indicator.Source != null)
                {
                    result.Source = ReadValue(indicator.Source);
                }
            }
            else if (arithmetic != null)
            {
                result.Kind = ValueKind.Arithmetic;
                result.Arithmetic = arithmetic.Operator;
                result.Left = ReadValue(arithmetic.Left);
                result.Right = ReadValue(arithmetic.Right);
            }
            else if (round != null)
            {
                result.Kind = ValueKind.Round;
                result.Source = ReadValue(round.Source);
                result.DecimalPlaces = round.DecimalPlaces;
            }
            else if (previous != null)
            {
                result.Kind = ValueKind.Previous;
                result.Source = ReadValue(previous.Source);
                result.Timeframe = previous.Timeframe;
                result.CandlesAgo = previous.CandlesAgo;
            }
            else if (count != null)
            {
                result.Kind = ValueKind.Count;
                result.CountCondition = ReadCondition(count.Condition);
                result.Timeframe = count.Timeframe;
                result.Length = count.Length;
                result.CandlesAgo = count.CandlesAgo;
            }
            else
            {
                throw new ArgumentException("This value is not supported by the editor.");
            }
            return result;
        }
    }
}
