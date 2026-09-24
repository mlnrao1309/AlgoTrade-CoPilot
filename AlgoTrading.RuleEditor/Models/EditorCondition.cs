using System;
using System.Collections.Generic;
using AlgoTrading.Models.Rules;

namespace AlgoTrading.RuleEditor.Models
{
    public sealed class EditorCondition
    {
        private ConditionKind _kind;
        private bool _enabled;
        private string _comment;
        private ComparisonOperator _operator;
        private EditorValue _left;
        private EditorValue _right;
        private List<EditorCondition> _children;

        public EditorCondition()
        {
            _kind = ConditionKind.All;
            _enabled = true;
            _comment = string.Empty;
            _operator = ComparisonOperator.GreaterThan;
            _left = new EditorValue();
            _right = new EditorValue();
            _children = new List<EditorCondition>();
        }

        public ConditionKind Kind
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

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            set
            {
                _enabled = value;
            }
        }

        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                _comment = value;
            }
        }

        public ComparisonOperator Operator
        {
            get
            {
                return _operator;
            }
            set
            {
                _operator = value;
            }
        }

        public EditorValue Left
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

        public EditorValue Right
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

        public List<EditorCondition> Children
        {
            get
            {
                return _children;
            }
            set
            {
                _children = value;
            }
        }
    }
}
