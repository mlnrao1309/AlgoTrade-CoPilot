using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AlgoTrading.Models.Rules
{
    public sealed class IndicatorValue : ValueDefinition, IEquatable<IndicatorValue>
    {
        private IndicatorFunction function;
        private string timeframe;
        private int length;
        private ValueDefinition? source;
        private double multiplier;

        [JsonConstructor]
        public IndicatorValue(IndicatorFunction Function, string Timeframe, int Length, ValueDefinition? Source = null, double Multiplier = 2)
        {
            this.function = Function;
            this.timeframe = Timeframe;
            this.length = Length;
            this.source = Source;
            this.multiplier = Multiplier;
        }

        [JsonRequired]
        public IndicatorFunction Function
        {
            get
            {
                return this.function;
            }
            init
            {
                this.function = value;
            }
        }

        [JsonRequired]
        public string Timeframe
        {
            get
            {
                return this.timeframe;
            }
            init
            {
                this.timeframe = value;
            }
        }

        [JsonRequired]
        public int Length
        {
            get
            {
                return this.length;
            }
            init
            {
                this.length = value;
            }
        }

        public ValueDefinition? Source
        {
            get
            {
                return this.source;
            }
            init
            {
                this.source = value;
            }
        }

        public double Multiplier
        {
            get
            {
                return this.multiplier;
            }
            init
            {
                this.multiplier = value;
            }
        }

        public bool Equals(IndicatorValue? other)
        {
            if (other is null)
            {
                return false;
            }
            return EqualityComparer<IndicatorFunction>.Default.Equals(Function, other.Function)
                && EqualityComparer<string>.Default.Equals(Timeframe, other.Timeframe)
                && EqualityComparer<int>.Default.Equals(Length, other.Length)
                && EqualityComparer<ValueDefinition?>.Default.Equals(Source, other.Source)
                && EqualityComparer<double>.Default.Equals(Multiplier, other.Multiplier);
        }

        public override bool Equals(object? other)
        {
            return Equals(other as IndicatorValue);
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(typeof(IndicatorValue));
            hash.Add(Function);
            hash.Add(Timeframe);
            hash.Add(Length);
            hash.Add(Source);
            hash.Add(Multiplier);
            return hash.ToHashCode();
        }

        public void Deconstruct(out IndicatorFunction Function, out string Timeframe, out int Length, out ValueDefinition? Source, out double Multiplier)
        {
            Function = this.Function;
            Timeframe = this.Timeframe;
            Length = this.Length;
            Source = this.Source;
            Multiplier = this.Multiplier;
        }
    }
}


