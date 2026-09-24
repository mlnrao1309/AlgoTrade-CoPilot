using System;
using System.Collections.Generic;
using System.Globalization;

namespace AlgoTrading.Models.Rules
{
    public sealed class BoundRule
    {
        private readonly RuleDefinition definition;
        private readonly RuleBindingPlan plan;

        internal BoundRule(RuleDefinition definition, RuleBindingPlan plan)
        {
            this.definition = definition;
            this.plan = plan;
        }

        public RuleBindingPlan Plan
        {
            get
            {
                return plan;
            }
        }

        public string Description
        {
            get
            {
                return RuleLanguage.Describe(definition.Condition);
            }
        }

        public RuleCrossoverMonitor CreateCrossoverMonitor(DateTimeOffset? startAfter = null)
        {
            return new RuleCrossoverMonitor(this, definition, startAfter);
        }

        public BoundRuleExecution BindData(RuleMarketData data)
        {
            ArgumentNullException.ThrowIfNull(data);
            foreach (string timeframe in Plan.RequiredTimeframes)
            {
                if (!data.Contains(timeframe))
                {
                    throw new ArgumentException($"Supply completed candles for '{timeframe}'.", nameof(data));
                }
            }
            return new BoundRuleExecution(definition, data);
        }
    }
}
