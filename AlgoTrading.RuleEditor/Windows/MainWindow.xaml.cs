using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Controls;
using AlgoTrading.RuleEditor.Models;
using AlgoTrading.RuleEditor.Services;

namespace AlgoTrading.RuleEditor.Windows
{
    public partial class MainWindow : Window
    {
        private EditorDocument _document;
        private readonly RuleDocumentAdapter _adapter;
        private readonly EditorHistory _history;
        private bool _isDirty;
        private bool _rendering;
        private string? _filePath;

        public MainWindow()
        {
            InitializeComponent();
            Height = Math.Min(900, SystemParameters.WorkArea.Height - 40);
            Width = Math.Min(1440, SystemParameters.WorkArea.Width - 40);
            _document = EditorExamples.MovingAverageBreakout();
            _adapter = new RuleDocumentAdapter();
            _history = new EditorHistory();
            RenderDocument();
        }

        private void RenderDocument()
        {
            _rendering = true;
            NameBox.Text = _document.Name;
            ComboBox timeframe = EditorControls.Choices(EditorCatalog.GetTimeframes(), _document.EvaluationTimeframe, "Evaluation timeframe");
            timeframe.SelectionChanged += OnTimeframeChanged;
            TimeframeHost.Content = timeframe;
            ConditionEditorControl editor = new ConditionEditorControl(_document.Root, null, _document.EvaluationTimeframe, 0);
            editor.Changing += OnEditorChanging;
            editor.Changed += OnEditorChanged;
            EditorHost.Content = editor;
            UndoButton.IsEnabled = _history.CanUndo;
            RedoButton.IsEnabled = _history.CanRedo;
            _rendering = false;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            PreviewText.Text = EditorLabels.Condition(_document.Root, 0);
            try
            {
                RuleDefinition definition = _adapter.ToDefinition(_document);
                BoundRule bound = RuleBinder.Bind(definition);
                ValidationTitle.Text = "Ready to save";
                ValidationTitle.Foreground = new SolidColorBrush(Color.FromRgb(142, 225, 194));
                ValidationMessage.Text = "The rule is valid. " + bound.Plan.IndicatorCalculations.Count + " indicator calculations will be prepared automatically.";
                StringBuilder timeframes = new StringBuilder();
                foreach (string timeframe in bound.Plan.RequiredTimeframes)
                {
                    if (timeframes.Length > 0)
                    {
                        timeframes.Append(" · ");
                    }
                    timeframes.Append(EditorCatalog.TimeframeLabel(timeframe));
                }
                TimeframesText.Text = timeframes.ToString();
            }
            catch (Exception exception)
            {
                ValidationTitle.Text = "Needs attention";
                ValidationTitle.Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                ValidationMessage.Text = exception.Message;
                TimeframesText.Text = "Choose valid conditions to see the timeframes used.";
            }
        }

        private void OnEditorChanging(object? sender, EventArgs arguments)
        {
            _history.Remember(_document);
        }

        private void OnEditorChanged(object? sender, EventArgs arguments)
        {
            _isDirty = true;
            StatusText.Text = "Unsaved changes";
            RenderDocument();
        }

        private void OnNameChanged(object sender, KeyboardFocusChangedEventArgs arguments)
        {
            if (!_rendering)
            {
                CommitName();
            }
        }

        private void CommitName()
        {
            string name = NameBox.Text.Trim();
            if (_document.Name != name)
            {
                _history.Remember(_document);
                _document.Name = name;
                _isDirty = true;
                StatusText.Text = "Unsaved changes";
                UndoButton.IsEnabled = _history.CanUndo;
                RedoButton.IsEnabled = _history.CanRedo;
                UpdatePreview();
            }
        }

        private void OnTimeframeChanged(object sender, SelectionChangedEventArgs arguments)
        {
            if (_rendering)
            {
                return;
            }
            ComboBox combo = (ComboBox)sender;
            _history.Remember(_document);
            _document.EvaluationTimeframe = EditorControls.SelectedKey(combo);
            _isDirty = true;
            StatusText.Text = "Evaluation clock changed. Each indicator retains its selected timeframe.";
            RenderDocument();
        }

        private bool ConfirmReplace()
        {
            CommitName();
            if (!_isDirty)
            {
                return true;
            }
            MessageBoxResult choice = MessageBox.Show(this, "Save your changes before continuing?", "Unsaved rule", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel)
            {
                return false;
            }
            if (choice == MessageBoxResult.Yes)
            {
                return SaveDocument();
            }
            return true;
        }

        private void ReplaceDocument(EditorDocument document, string? filePath)
        {
            _document = document;
            _filePath = filePath;
            _isDirty = false;
            _history.Clear();
            RenderDocument();
        }

        private void OnNew(object sender, RoutedEventArgs arguments)
        {
            if (ConfirmReplace())
            {
                ReplaceDocument(EditorExamples.Blank(), null);
                StatusText.Text = "New rule · Select a value to begin.";
            }
        }

        private void OnLoadExample(object sender, RoutedEventArgs arguments)
        {
            if (ConfirmReplace())
            {
                ReplaceDocument(EditorExamples.MovingAverageBreakout(), null);
                StatusText.Text = "Unsaved example · Moving average breakout";
            }
        }

        private void OnOpen(object sender, RoutedEventArgs arguments)
        {
            if (!ConfirmReplace())
            {
                return;
            }
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Rule definitions (*.json)|*.json";
            dialog.Title = "Open a strategy rule";
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            try
            {
                RuleDefinition definition = RuleDefinitionJson.Deserialize(File.ReadAllText(dialog.FileName));
                RuleBinder.Bind(definition);
                ReplaceDocument(_adapter.FromDefinition(definition), dialog.FileName);
                StatusText.Text = "Opened " + dialog.FileName;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Cannot open rule", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool SaveDocument()
        {
            try
            {
                CommitName();
                RuleDefinition definition = _adapter.ToDefinition(_document);
                RuleBinder.Bind(definition);
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "Rule definitions (*.json)|*.json";
                dialog.DefaultExt = ".json";
                dialog.AddExtension = true;
                dialog.Title = "Save a strategy rule";
                dialog.FileName = "strategy-rule.json";
                if (_filePath != null)
                {
                    dialog.FileName = _filePath;
                }
                if (dialog.ShowDialog(this) != true)
                {
                    return false;
                }
                File.WriteAllText(dialog.FileName, RuleDefinitionJson.Serialize(definition));
                _filePath = dialog.FileName;
                _isDirty = false;
                StatusText.Text = "Saved " + dialog.FileName;
                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Cannot save rule", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void OnSave(object sender, RoutedEventArgs arguments)
        {
            SaveDocument();
        }

        private void OnValidate(object sender, RoutedEventArgs arguments)
        {
            CommitName();
            UpdatePreview();
            StatusText.Text = ValidationTitle.Text + " · " + ValidationMessage.Text;
        }

        private void OnUndo(object sender, RoutedEventArgs arguments)
        {
            if (_history.CanUndo)
            {
                _document = _history.Undo(_document);
                _isDirty = true;
                RenderDocument();
                StatusText.Text = "Undo applied · Unsaved changes";
            }
        }

        private void OnRedo(object sender, RoutedEventArgs arguments)
        {
            if (_history.CanRedo)
            {
                _document = _history.Redo(_document);
                _isDirty = true;
                RenderDocument();
                StatusText.Text = "Redo applied · Unsaved changes";
            }
        }

        private void OnClosing(object? sender, CancelEventArgs arguments)
        {
            if (!ConfirmReplace())
            {
                arguments.Cancel = true;
            }
        }
    }
}

