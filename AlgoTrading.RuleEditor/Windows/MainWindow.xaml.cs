using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private readonly StrategyDocumentService _documents = new StrategyDocumentService();
        private readonly StrategyHistory _history = new StrategyHistory();
        private StrategyDocument _document;
        private bool _rendering;
        private bool _dirty;
        private bool _readOnly;
        private string? _filePath;
        private DateTime _fileWriteTimeUtc;
        private BacktestResult? _lastBacktest;

        public IReadOnlyList<string> QuantityBases { get; } = new[] { "Original", "Remaining" };

        public MainWindow()
        {
            _document = new StrategyDocument();
            InitializeComponent();
            DataContext = this;
            ((DataGridComboBoxColumn)PartialExitGrid.Columns[3]).ItemsSource = QuantityBases;
            Height = Math.Min(940, SystemParameters.WorkArea.Height - 30);
            Width = Math.Min(1500, SystemParameters.WorkArea.Width - 30);
            _document = CreateExample();
            RenderDocument();
        }

        private StrategyDocument CreateExample()
        {
            EditorDocument entry = EditorExamples.MovingAverageBreakout();
            StrategyDocument document = _documents.CreateBlank();
            document.Name = entry.Name;
            document.Entry = entry.Root;
            document.PartialExits.Add(new PartialExitStage { Label = "Target A", QuantityPercent = 50m, QuantityBasis = "Original", Target = "1R" });
            document.PartialExits.Add(new PartialExitStage { Label = "Target B", QuantityPercent = 80m, QuantityBasis = "Remaining", Target = "2R" });
            return document;
        }

        private void RenderDocument()
        {
            _rendering = true;
            NameBox.Text = _document.Name;
            DescriptionBox.Text = _document.Description;
            InstrumentBox.Text = _document.Instrument;
            BindingBox.Text = _document.LevelBinding;
            RetestWindowBox.Text = _document.RetestWindowCandles.ToString(CultureInfo.InvariantCulture);
            StopValueBox.Text = _document.Protection.Value.ToString(CultureInfo.InvariantCulture);
            StopReferenceBox.Text = _document.Protection.Reference;
            RetestBox.IsChecked = _document.RetestRequired;
            InvalidateBox.IsChecked = _document.InvalidateOnOppositeCross;
            ProtectionEnabledBox.IsChecked = _document.Protection.Enabled;
            Select(ExchangeBox, _document.Exchange);
            Select(SegmentBox, _document.Segment);
            Select(SessionBox, _document.Session);
            Select(DirectionBox, _document.Direction);
            Select(LevelBox, _document.LevelSelection);
            Select(StopModeBox, _document.Protection.Mode);
            Select(PriorityBox, _document.Execution.StopTargetPriority);
            Select(ActionBox, _document.Execution.SameCandleActions);
            Select(ReEntryBox, _document.Execution.ReEntry);

            ComboBox timeframe = EditorControls.Choices(EditorCatalog.GetTimeframes(), _document.EvaluationTimeframe, "Evaluation timeframe");
            timeframe.SelectionChanged += OnTimeframeChanged;
            TimeframeHost.Content = timeframe;

            EntryHost.Content = CreateConditionEditor(_document.Entry);
            FullExitHost.Content = CreateConditionEditor(_document.FullExit);
            PartialExitGrid.ItemsSource = null;
            PartialExitGrid.ItemsSource = _document.PartialExits;
            UndoButton.IsEnabled = _history.CanUndo;
            RedoButton.IsEnabled = _history.CanRedo;
            _rendering = false;
            RefreshChecks();
            UpdateTitle();
        }

        private ConditionEditorControl CreateConditionEditor(EditorCondition condition)
        {
            ConditionEditorControl editor = new ConditionEditorControl(condition, null, _document.EvaluationTimeframe, 0);
            editor.Changing += OnConditionChanging;
            editor.Changed += OnConditionChanged;
            return editor;
        }

        private static void Select(ComboBox combo, string value)
        {
            foreach (object item in combo.Items)
            {
                if (item is ComboBoxItem option && string.Equals(option.Content?.ToString(), value, StringComparison.Ordinal))
                {
                    combo.SelectedItem = option;
                    return;
                }
            }
            combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
        }

        private static string Selected(ComboBox combo)
        {
            return combo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? string.Empty : string.Empty;
        }

        private void CommitFields()
        {
            _document.Name = NameBox.Text.Trim();
            _document.Description = DescriptionBox.Text.Trim();
            _document.Instrument = InstrumentBox.Text.Trim();
            _document.Exchange = Selected(ExchangeBox);
            _document.Segment = Selected(SegmentBox);
            _document.Session = Selected(SessionBox);
            _document.Direction = Selected(DirectionBox);
            _document.LevelSelection = Selected(LevelBox);
            _document.LevelBinding = BindingBox.Text.Trim();
            _document.RetestRequired = RetestBox.IsChecked == true;
            _document.InvalidateOnOppositeCross = InvalidateBox.IsChecked == true;
            if (int.TryParse(RetestWindowBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int window)) _document.RetestWindowCandles = window;
            _document.Protection.Enabled = ProtectionEnabledBox.IsChecked == true;
            _document.Protection.Mode = Selected(StopModeBox);
            if (decimal.TryParse(StopValueBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal stopValue)) _document.Protection.Value = stopValue;
            _document.Protection.Reference = StopReferenceBox.Text.Trim();
            _document.Execution.StopTargetPriority = Selected(PriorityBox);
            _document.Execution.SameCandleActions = Selected(ActionBox);
            _document.Execution.ReEntry = Selected(ReEntryBox);
        }

        private void RememberAndCommit()
        {
            if (_rendering) return;
            _history.Remember(_document);
            CommitFields();
            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
            StatusText.Text = "Unsaved changes";
            UndoButton.IsEnabled = _history.CanUndo;
            RedoButton.IsEnabled = _history.CanRedo;
            RefreshChecks();
            UpdateTitle();
        }

        private void RefreshChecks()
        {
            SummaryBox.Text = _documents.Summary(_document);
            IReadOnlyList<string> errors = _documents.Validate(_document);
            if (errors.Count == 0)
            {
                ValidationTitle.Text = "Ready";
                ValidationTitle.Foreground = new SolidColorBrush(Color.FromRgb(142, 225, 194));
                ValidationMessage.Text = "All required authoring checks passed.";
            }
            else
            {
                ValidationTitle.Text = errors.Count + " item" + (errors.Count == 1 ? string.Empty : "s") + " need attention";
                ValidationTitle.Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                ValidationMessage.Text = string.Join(Environment.NewLine + Environment.NewLine, errors.Take(8));
            }
            try
            {
                BoundRule entry = RuleBinder.Bind(_documents.ToEntryRule(_document));
                BoundRule exit = RuleBinder.Bind(_documents.ToFullExitRule(_document));
                RuntimeText.Text = "Entry and full-exit rules bind to the shared runtime. Required timeframes: " + string.Join(", ", entry.Plan.RequiredTimeframes.Union(exit.Plan.RequiredTimeframes)) + ". Level/retest, protection, and position-sizing settings are explicit strategy metadata for the execution coordinator.";
            }
            catch (Exception exception)
            {
                RuntimeText.Text = "Runtime binding blocked: " + exception.Message;
            }
        }

        private void UpdateTitle()
        {
            Title = (_dirty ? "*" : string.Empty) + _document.Name + " · AlgoTrading Strategy Editor" + (_readOnly ? " [Read only]" : string.Empty);
        }

        private void OnHeaderChanged(object sender, KeyboardFocusChangedEventArgs arguments) => RememberAndCommit();
        private void OnSimpleFieldChanged(object sender, KeyboardFocusChangedEventArgs arguments) => RememberAndCommit();
        private void OnSimpleSelectionChanged(object sender, SelectionChangedEventArgs arguments) => RememberAndCommit();
        private void OnSimpleCheckChanged(object sender, RoutedEventArgs arguments) => RememberAndCommit();

        private void OnTimeframeChanged(object sender, SelectionChangedEventArgs arguments)
        {
            if (_rendering) return;
            _history.Remember(_document);
            _document.EvaluationTimeframe = EditorControls.SelectedKey((ComboBox)sender);
            MarkDirty();
            RenderDocument();
        }

        private void OnConditionChanging(object? sender, EventArgs arguments) => _history.Remember(_document);

        private void OnConditionChanged(object? sender, EventArgs arguments)
        {
            MarkDirty();
            EntryHost.Content = CreateConditionEditor(_document.Entry);
            FullExitHost.Content = CreateConditionEditor(_document.FullExit);
        }

        private void OnStageEdited(object sender, DataGridCellEditEndingEventArgs arguments)
        {
            if (_rendering) return;
            _history.Remember(_document);
            Dispatcher.BeginInvoke(new Action(MarkDirty));
        }

        private void OnAddStage(object sender, RoutedEventArgs arguments)
        {
            _history.Remember(_document);
            _document.PartialExits.Add(new PartialExitStage { Label = "Stage " + (_document.PartialExits.Count + 1) });
            MarkDirty();
            RenderDocument();
        }

        private void OnRemoveStage(object sender, RoutedEventArgs arguments)
        {
            if (PartialExitGrid.SelectedItem is not PartialExitStage selected) return;
            _history.Remember(_document);
            _document.PartialExits.Remove(selected);
            MarkDirty();
            RenderDocument();
        }

        private void OnTabChanged(object sender, SelectionChangedEventArgs arguments)
        {
            if (!_rendering && SummaryBox != null && _document != null) RefreshChecks();
        }

        private bool ConfirmReplace()
        {
            CommitFields();
            if (!_dirty) return true;
            MessageBoxResult answer = MessageBox.Show(this, "Save your changes before continuing?", "Unsaved strategy", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return false;
            return answer != MessageBoxResult.Yes || SaveDocument(false);
        }

        private void ReplaceDocument(StrategyDocument document, string? path, bool readOnly)
        {
            _document = document;
            _filePath = path;
            _readOnly = readOnly;
            _fileWriteTimeUtc = path == null ? default : File.GetLastWriteTimeUtc(path);
            _dirty = false;
            _history.Clear();
            RenderDocument();
        }

        private void OnNew(object sender, RoutedEventArgs arguments)
        {
            if (ConfirmReplace()) ReplaceDocument(_documents.CreateBlank(), null, false);
        }

        private void OnOpen(object sender, RoutedEventArgs arguments)
        {
            if (!ConfirmReplace()) return;
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Strategy or legacy rule (*.strategy.json;*.json)|*.strategy.json;*.json|All files (*.*)|*.*", Title = "Open a strategy" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                StrategyDocument opened = _documents.Deserialize(File.ReadAllText(dialog.FileName), out bool importedLegacy);
                if (importedLegacy)
                {
                    ReplaceDocument(opened, null, false);
                    _dirty = true;
                    StatusText.Text = "Legacy rule imported as a new strategy. The original file was not changed.";
                }
                else
                {
                    bool readOnly = new FileInfo(dialog.FileName).IsReadOnly;
                    ReplaceDocument(opened, dialog.FileName, readOnly);
                    StatusText.Text = "Opened " + dialog.FileName + (readOnly ? " in read-only mode" : string.Empty);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message + Environment.NewLine + Environment.NewLine + "File: " + dialog.FileName, "Cannot open strategy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool SaveDocument(bool forceSaveAs)
        {
            CommitFields();
            IReadOnlyList<string> errors = _documents.Validate(_document);
            if (errors.Count > 0)
            {
                RefreshChecks();
                MessageBox.Show(this, "Fix the strategy check items before saving.", "Strategy is not valid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            string? target = forceSaveAs || _readOnly ? null : _filePath;
            if (target == null)
            {
                SaveFileDialog dialog = new SaveFileDialog { Filter = "Strategy documents (*.strategy.json)|*.strategy.json", DefaultExt = ".strategy.json", AddExtension = true, FileName = SafeName(_document.Name) + ".strategy.json", Title = "Save strategy" };
                if (dialog.ShowDialog(this) != true) return false;
                target = dialog.FileName;
            }
            try
            {
                if (target == _filePath && File.Exists(target) && File.GetLastWriteTimeUtc(target) != _fileWriteTimeUtc)
                {
                    MessageBox.Show(this, "The file changed outside the editor. Use Save As so neither copy is overwritten.", "File conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                File.WriteAllText(target, _documents.Serialize(_document));
                _filePath = target;
                _fileWriteTimeUtc = File.GetLastWriteTimeUtc(target);
                _readOnly = false;
                _dirty = false;
                StatusText.Text = "Saved " + target;
                UpdateTitle();
                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message + Environment.NewLine + Environment.NewLine + "File: " + target, "Cannot save strategy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private static string SafeName(string value)
        {
            string result = string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            return string.IsNullOrWhiteSpace(result) ? "strategy" : result;
        }

        private void OnSave(object sender, RoutedEventArgs arguments) => SaveDocument(false);
        private void OnSaveAs(object sender, RoutedEventArgs arguments) => SaveDocument(true);
        private void OnValidate(object sender, RoutedEventArgs arguments) { CommitFields(); RefreshChecks(); StatusText.Text = ValidationTitle.Text + "."; }

        private void OnChooseBacktestCsv(object sender, RoutedEventArgs arguments)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Candle CSV (*.csv)|*.csv|All files (*.*)|*.*", Title = "Choose completed-candle data" };
            if (dialog.ShowDialog(this) == true) BacktestPathBox.Text = dialog.FileName;
        }

        private void OnRunBacktest(object sender, RoutedEventArgs arguments)
        {
            CommitFields();
            if (string.IsNullOrWhiteSpace(BacktestPathBox.Text))
            {
                MessageBox.Show(this, "Choose a candle CSV first.", "Backtest", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!decimal.TryParse(BacktestQuantityBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity <= 0)
            {
                MessageBox.Show(this, "Quantity must be a positive number.", "Backtest", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                StrategyRuntimeSnapshot snapshot = new StrategyRuntimeAdapter().Load(_documents.Serialize(_document));
                _lastBacktest = new SimpleBacktestService().RunCsv(snapshot, BacktestPathBox.Text, quantity);
                BacktestResultBox.Text = _lastBacktest.ToReport();
                StatusText.Text = "Backtest complete: " + _lastBacktest.Trades.Count + " trade(s), net P&L " + _lastBacktest.NetProfit.ToString("0.00####", CultureInfo.InvariantCulture) + ".";
            }
            catch (Exception exception)
            {
                _lastBacktest = null;
                BacktestResultBox.Text = string.Empty;
                MessageBox.Show(this, exception.Message, "Backtest failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnUndo(object sender, RoutedEventArgs arguments)
        {
            if (!_history.CanUndo) return;
            _document = _history.Undo(_document);
            _dirty = true;
            RenderDocument();
            StatusText.Text = "Undo applied · Unsaved changes";
        }

        private void OnRedo(object sender, RoutedEventArgs arguments)
        {
            if (!_history.CanRedo) return;
            _document = _history.Redo(_document);
            _dirty = true;
            RenderDocument();
            StatusText.Text = "Redo applied · Unsaved changes";
        }

        private void OnClosing(object? sender, CancelEventArgs arguments)
        {
            if (!ConfirmReplace()) arguments.Cancel = true;
        }
    }
}
