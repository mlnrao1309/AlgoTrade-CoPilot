using System;
using System.Windows;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Controls;
using AlgoTrading.RuleEditor.Models;
using AlgoTrading.RuleEditor.Services;

namespace AlgoTrading.RuleEditor.Windows
{
    public partial class ConditionEditorWindow : Window
    {
        private readonly EditorCondition _draft;
        private readonly string _timeframe;
        private readonly int _depth;

        public ConditionEditorWindow(EditorCondition condition, string timeframe, int depth)
        {
            InitializeComponent();
            _draft = EditorCopyService.Copy(condition);
            _timeframe = timeframe;
            _depth = depth;
            Render();
        }

        public EditorCondition EditedCondition
        {
            get
            {
                return _draft;
            }
        }

        private void Render()
        {
            ConditionEditorControl editor = new ConditionEditorControl(_draft, null, _timeframe, _depth);
            editor.Changed += OnConditionChanged;
            EditorHost.Content = editor;
        }

        private void OnConditionChanged(object? sender, EventArgs arguments)
        {
            Render();
        }

        private void OnApply(object sender, RoutedEventArgs arguments)
        {
            try
            {
                EditorDocument document = new EditorDocument();
                document.EvaluationTimeframe = _timeframe;
                document.Root = _draft;
                RuleDocumentAdapter adapter = new RuleDocumentAdapter();
                RuleBinder.Bind(adapter.ToDefinition(document));
                DialogResult = true;
            }
            catch (Exception exception)
            {
                ErrorText.Text = exception.Message;
            }
        }
    }
}
