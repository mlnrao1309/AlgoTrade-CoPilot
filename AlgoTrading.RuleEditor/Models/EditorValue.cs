using System;
using System.Collections.Generic;
using AlgoTrading.Models.Rules;

namespace AlgoTrading.RuleEditor.Models
{
    public sealed class EditorValue
    {
        private ValueKind _kind;
        private double _number;
        private string _timeframe;
        private CandleField _field;
        private int _candlesAgo;
        private IndicatorFunction _function;
        private int _length;
        private double _multiplier;
        private ArithmeticOperator _arithmetic;
        private int _decimalPlaces;
        private EditorValue? _source;
        private EditorValue? _left;
        private EditorValue? _right;
        private EditorCondition? _countCondition;

        public EditorValue()
        {
            _kind = ValueKind.Number;
            _number = 0;
            _timeframe = "15minute";
            _field = CandleField.Close;
            _candlesAgo = 0;
            _function = IndicatorFunction.SimpleMovingAverage;
            _length = 14;
            _multiplier = 2;
            _arithmetic = ArithmeticOperator.Add;
            _decimalPlaces = 0;
            _source = null;
            _left = null;
            _right = null;
            _countCondition = null;
        }

        public ValueKind Kind
        {
            get
            {
                return _kind;
            }
            set
            {
                _kind = value;
            }
        }

        public double Number
        {
            get
            {
                return _number;
            }
            set
            {
                _number = value;
            }
        }

        public string Timeframe
        {
            get
            {
                return _timeframe;
            }
            set
            {
                _timeframe = value;
            }
        }

        public CandleField Field
        {
            get
            {
                return _field;
            }
            set
            {
                _field = value;
            }
        }

        public int CandlesAgo
        {
            get
            {
                return _candlesAgo;
            }
            set
            {
                _candlesAgo = value;
            }
        }

        public IndicatorFunction Function
        {
            get
            {
                return _function;
            }
            set
            {
                _function = value;
            }
        }

        public int Length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
            }
        }

        public double Multiplier
        {
            get
            {
                return _multiplier;
            }
            set
            {
                _multiplier = value;
            }
        }

        public ArithmeticOperator Arithmetic
        {
            get
            {
                return _arithmetic;
            }
            set
            {
                _arithmetic = value;
            }
        }

        public int DecimalPlaces
        {
            get
            {
                return _decimalPlaces;
            }
            set
            {
                _decimalPlaces = value;
            }
        }

        public EditorValue? Source
        {
            get
            {
                return _source;
            }
            set
            {
                _source = value;
            }
        }

        public EditorValue? Left
        {
            get
            {
                return _left;
            }
            set
            {
                _left = value;
            }
        }

        public EditorValue? Right
        {
            get
            {
                return _right;
            }
            set
            {
                _right = value;
            }
        }

        public EditorCondition? CountCondition
        {
            get
            {
                return _countCondition;
            }
            set
            {
                _countCondition = value;
            }
        }
    }
}
