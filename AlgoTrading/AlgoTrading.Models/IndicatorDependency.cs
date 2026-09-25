using System;
using AlgoTrading.Models.Rules;

namespace AlgoTrading.Models
{
    public sealed class IndicatorDependency : IEquatable<IndicatorDependency>
    {
        private readonly IndicatorFunction function;
        private readonly string timeframe;
        private readonly int length;
        private readonly double multiplier;
        private readonly ValueDefinition? source;

        internal IndicatorDependency(IndicatorValue indicator)
        {
            this.function = indicator.Function;
            this.timeframe = indicator.Timeframe;
            this.length = indicator.Length;
            this.multiplier = indicator.Multiplier;
            this.source = indicator.Source;
        }

        public IndicatorFunction Function
        {
            get
            {
                return this.function;
            }
        }
        public string Timeframe
        {
            get
            {
                return this.timeframe;
            }
        }
        public int Length
        {
            get
            {
                return this.length;
            }
        }
        public double Multiplier
        {
            get
            {
                return this.multiplier;
            }
        }
        public ValueDefinition? Source
        {
            get
            {
                return this.source;
            }
        }

        public bool Equals(IndicatorDependency? other)
        {
            if (other == null)
            {
                return false;
            }
            return this.function == other.function && this.timeframe == other.timeframe && this.length == other.length
                && this.multiplier == other.multiplier && Equals(this.source, other.source);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as IndicatorDependency);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(this.function, this.timeframe, this.length, this.multiplier, this.source);
        }
        public static bool operator ==(IndicatorDependency? left, IndicatorDependency? right)
        {
            return object.Equals(left, right);
        }
        public static bool operator !=(IndicatorDependency? left, IndicatorDependency? right)
        {
            return !object.Equals(left, right);
        }
    }
}
