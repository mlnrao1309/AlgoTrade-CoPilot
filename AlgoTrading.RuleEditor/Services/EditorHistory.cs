using System.Collections.Generic;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public sealed class EditorHistory
    {
        private readonly List<EditorDocument> _undo;
        private readonly List<EditorDocument> _redo;

        public EditorHistory()
        {
            _undo = new List<EditorDocument>();
            _redo = new List<EditorDocument>();
        }

        public bool CanUndo
        {
            get
            {
                return _undo.Count > 0;
            }
        }

        public bool CanRedo
        {
            get
            {
                return _redo.Count > 0;
            }
        }

        public void Remember(EditorDocument document)
        {
            _undo.Add(EditorCopyService.Copy(document));
            if (_undo.Count > 100)
            {
                _undo.RemoveAt(0);
            }
            _redo.Clear();
        }

        public EditorDocument Undo(EditorDocument current)
        {
            _redo.Add(EditorCopyService.Copy(current));
            EditorDocument previous = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            return previous;
        }

        public EditorDocument Redo(EditorDocument current)
        {
            _undo.Add(EditorCopyService.Copy(current));
            EditorDocument next = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            return next;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
