using System;
using System.Collections.Generic;
using AlgoTrading.Models.Rules;

namespace AlgoTrading.RuleEditor.Models
{
    public sealed class EditorDocument
    {
        private string _name;
        private string _evaluationTimeframe;
        private EditorCondition _root;

        public EditorDocument()
        {
            _name = "Untitled strategy";
            _evaluationTimeframe = "15minute";
            _root = new EditorCondition();
        }

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        public string EvaluationTimeframe
        {
            get
            {
                return _evaluationTimeframe;
            }
            set
            {
                _evaluationTimeframe = value;
            }
        }

        public EditorCondition Root
        {
            get
            {
                return _root;
            }
            set
            {
                _root = value;
            }
        }
    }
}
