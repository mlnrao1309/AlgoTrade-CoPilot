using System;
using System.Collections.Generic;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public static class EditorCatalog
    {
        public static List<EditorChoice> GetTimeframes()
        {
            List<EditorChoice> choices = new List<EditorChoice>();
            choices.Add(new EditorChoice("minute", "1 minute"));
            choices.Add(new EditorChoice("3minute", "3 minutes"));
            choices.Add(new EditorChoice("5minute", "5 minutes"));
            choices.Add(new EditorChoice("10minute", "10 minutes"));
            choices.Add(new EditorChoice("15minute", "15 minutes"));
            choices.Add(new EditorChoice("30minute", "30 minutes"));
            choices.Add(new EditorChoice("hour", "1 hour"));
            choices.Add(new EditorChoice("2hour", "2 hours"));
            choices.Add(new EditorChoice("day", "Daily"));
            choices.Add(new EditorChoice("week", "Weekly"));
            return choices;
        }

        public static string TimeframeLabel(string key)
        {
            foreach (EditorChoice choice in GetTimeframes())
            {
                if (choice.Key == key)
                {
                    return choice.Label;
                }
            }
            return key;
        }

        public static List<EditorChoice> GetValueKinds()
        {
            List<EditorChoice> choices = new List<EditorChoice>();
            choices.Add(new EditorChoice("Candle", "Candle field"));
            choices.Add(new EditorChoice("Indicator", "Indicator"));
            choices.Add(new EditorChoice("Number", "Number"));
            choices.Add(new EditorChoice("Arithmetic", "Calculation"));
            choices.Add(new EditorChoice("Round", "Round a value"));
            choices.Add(new EditorChoice("Previous", "Previous value"));
            choices.Add(new EditorChoice("Count", "Count matching candles"));
            return choices;
        }

        public static List<EditorChoice> GetComparisons()
        {
            List<EditorChoice> choices = new List<EditorChoice>();
            foreach (ComparisonOperator operation in Enum.GetValues<ComparisonOperator>())
            {
                choices.Add(new EditorChoice(operation.ToString(), RuleLanguage.ComparisonLabel(operation)));
            }
            return choices;
        }

        public static List<EditorChoice> GetIndicators()
        {
            List<EditorChoice> choices = new List<EditorChoice>();
            foreach (IndicatorFunction function in Enum.GetValues<IndicatorFunction>())
            {
                choices.Add(new EditorChoice(function.ToString(), RuleLanguage.IndicatorLabel(function)));
            }
            return choices;
        }

        public static List<EditorChoice> GetFields()
        {
            List<EditorChoice> choices = new List<EditorChoice>();
            foreach (CandleField field in Enum.GetValues<CandleField>())
            {
                choices.Add(new EditorChoice(field.ToString(), field.ToString()));
            }
            return choices;
        }

        public static List<EditorChoice> GetArithmetic()
        {
            List<EditorChoice> choices = new List<EditorChoice>();
            choices.Add(new EditorChoice("Add", "Add (+)"));
            choices.Add(new EditorChoice("Subtract", "Subtract (−)"));
            choices.Add(new EditorChoice("Multiply", "Multiply (×)"));
            choices.Add(new EditorChoice("Divide", "Divide (÷)"));
            return choices;
        }

        public static List<EditorChoice> GetGroups()
        {
            List<EditorChoice> choices = new List<EditorChoice>();
            choices.Add(new EditorChoice("All", "All conditions"));
            choices.Add(new EditorChoice("Any", "Any condition"));
            choices.Add(new EditorChoice("None", "None of these conditions"));
            return choices;
        }
    }
}
