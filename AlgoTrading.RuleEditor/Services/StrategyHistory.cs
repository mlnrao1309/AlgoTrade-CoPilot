using System.Collections.Generic;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public sealed class StrategyHistory
    {
        private const int Limit = 50;
        private readonly List<StrategyDocument> _undo = new List<StrategyDocument>();
        private readonly List<StrategyDocument> _redo = new List<StrategyDocument>();

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Remember(StrategyDocument document)
        {
            _undo.Add(EditorCopyService.Copy(document));
            if (_undo.Count > Limit) _undo.RemoveAt(0);
            _redo.Clear();
        }

        public StrategyDocument Undo(StrategyDocument current)
        {
            _redo.Add(EditorCopyService.Copy(current));
            StrategyDocument result = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            return result;
        }

        public StrategyDocument Redo(StrategyDocument current)
        {
            _undo.Add(EditorCopyService.Copy(current));
            StrategyDocument result = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            return result;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
