using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Controls;
using AlgoTrading.RuleEditor.Models;
using AlgoTrading.RuleEditor.Services;

namespace AlgoTrading.RuleEditor.Windows
{
    public partial class ValueEditorWindow : Window
    {
        private readonly EditorValue _draft;
        private readonly string _defaultTimeframe;
        private readonly int _depth;
        private ComboBox? _timeframeBox;
        private ComboBox? _fieldBox;
        private ComboBox? _functionBox;
        private ComboBox? _arithmeticBox;
        private TextBox? _numberBox;
        private TextBox? _lengthBox;
        private TextBox? _multiplierBox;
        private TextBox? _offsetBox;
        private TextBox? _decimalPlacesBox;

        public ValueEditorWindow(EditorValue value, string defaultTimeframe, int depth)
        {
            InitializeComponent();
            _draft = EditorCopyService.Copy(value);
            _defaultTimeframe = defaultTimeframe;
            _depth = depth;
            BuildFields();
        }

        public EditorValue EditedValue
        {
            get
            {
                return _draft;
            }
        }

        private void BuildFields()
        {
            FieldsPanel.Children.Clear();
            _timeframeBox = null;
            _fieldBox = null;
            _functionBox = null;
            _arithmeticBox = null;
            _numberBox = null;
            _lengthBox = null;
            _multiplierBox = null;
            _offsetBox = null;
            _decimalPlacesBox = null;
            ComboBox kindBox = EditorControls.Choices(EditorCatalog.GetValueKinds(), _draft.Kind.ToString(), "Value type");
            kindBox.SelectionChanged += OnKindChanged;
            EditorControls.AddField(FieldsPanel, "Value type", kindBox);
            switch (_draft.Kind)
            {
                case ValueKind.Number:
                    _numberBox = EditorControls.Input(_draft.Number.ToString(CultureInfo.InvariantCulture), "Number");
                    EditorControls.AddField(FieldsPanel, "Number", _numberBox);
                    break;
                case ValueKind.Candle:
                    AddTimeframe();
                    _fieldBox = EditorControls.Choices(EditorCatalog.GetFields(), _draft.Field.ToString(), "Candle field");
                    EditorControls.AddField(FieldsPanel, "Candle field", _fieldBox);
                    AddOffset();
                    break;
                case ValueKind.Indicator:
                    _functionBox = EditorControls.Choices(EditorCatalog.GetIndicators(), _draft.Function.ToString(), "Indicator");
                    _functionBox.SelectionChanged += OnFunctionChanged;
                    EditorControls.AddField(FieldsPanel, "Indicator", _functionBox);
                    AddTimeframe();
                    AddLength("Length in candles");
                    if (_draft.Function == IndicatorFunction.SuperTrend || _draft.Function == IndicatorFunction.BollingerUpperBand || _draft.Function == IndicatorFunction.BollingerLowerBand)
                    {
                        _multiplierBox = EditorControls.Input(_draft.Multiplier.ToString(CultureInfo.InvariantCulture), "Multiplier");
                        EditorControls.AddField(FieldsPanel, "Multiplier", _multiplierBox);
                    }
                    if (_draft.Function == IndicatorFunction.SuperTrend)
                    {
                        EditorControls.AddField(FieldsPanel, "Source", EditorControls.Text("SuperTrend uses the high, low and close together.", 14));
                    }
                    else
                    {
                        AddSource();
                    }
                    break;
                case ValueKind.Arithmetic:
                    EnsureBinaryInputs();
                    EditorControls.AddField(FieldsPanel, "Left value", EditorControls.ValueButton(EditorLabels.Value(Required(_draft.Left)), OnEditLeft));
                    _arithmeticBox = EditorControls.Choices(EditorCatalog.GetArithmetic(), _draft.Arithmetic.ToString(), "Calculation operator");
                    EditorControls.AddField(FieldsPanel, "Operation", _arithmeticBox);
                    EditorControls.AddField(FieldsPanel, "Right value", EditorControls.ValueButton(EditorLabels.Value(Required(_draft.Right)), OnEditRight));
                    break;
                case ValueKind.Round:
                    AddSource();
                    _decimalPlacesBox = EditorControls.Input(_draft.DecimalPlaces.ToString(CultureInfo.InvariantCulture), "Decimal places");
                    EditorControls.AddField(FieldsPanel, "Decimal places", _decimalPlacesBox);
                    break;
                case ValueKind.Previous:
                    AddTimeframe();
                    AddSource();
                    AddOffset();
                    break;
                case ValueKind.Count:
                    AddTimeframe();
                    AddLength("Count over this many candles");
                    AddOffset();
                    if (_draft.CountCondition == null)
                    {
                        _draft.CountCondition = new EditorCondition();
                        _draft.CountCondition.Children.Add(EditorExamples.NewComparison(_draft.Timeframe));
                    }
                    EditorControls.AddField(FieldsPanel, "Count candles matching this condition", EditorControls.ValueButton(EditorLabels.Condition(_draft.CountCondition, 0), OnEditCountCondition));
                    break;
            }
        }

        private static EditorValue Required(EditorValue? value)
        {
            if (value == null)
            {
                throw new InvalidOperationException("Select an input value.");
            }
            return value;
        }

        private void AddTimeframe()
        {
            _timeframeBox = EditorControls.Choices(EditorCatalog.GetTimeframes(), _draft.Timeframe, "Value timeframe");
            EditorControls.AddField(FieldsPanel, "Timeframe", _timeframeBox);
        }

        private void AddLength(string label)
        {
            _lengthBox = EditorControls.Input(_draft.Length.ToString(CultureInfo.InvariantCulture), label);
            EditorControls.AddField(FieldsPanel, label, _lengthBox);
        }

        private void AddOffset()
        {
            _offsetBox = EditorControls.Input(_draft.CandlesAgo.ToString(CultureInfo.InvariantCulture), "Candles ago");
            EditorControls.AddField(FieldsPanel, "Candles ago · 0 is the current completed candle", _offsetBox);
        }

        private void AddSource()
        {
            if (_draft.Source == null)
            {
                _draft.Source = EditorExamples.Close(_draft.Timeframe);
            }
            EditorControls.AddField(FieldsPanel, "Source · select to nest another indicator or calculation", EditorControls.ValueButton(EditorLabels.Value(_draft.Source), OnEditSource));
        }

        private void EnsureBinaryInputs()
        {
            if (_draft.Left == null)
            {
                _draft.Left = EditorExamples.Close(_defaultTimeframe);
            }
            if (_draft.Right == null)
            {
                _draft.Right = new EditorValue();
            }
        }

        private void ReadFields()
        {
            if (_timeframeBox != null)
            {
                string timeframe = EditorControls.SelectedKey(_timeframeBox);
                if (_draft.Kind == ValueKind.Indicator && _draft.Source != null && _draft.Source.Kind == ValueKind.Candle && _draft.Source.Timeframe == _draft.Timeframe)
                {
                    _draft.Source.Timeframe = timeframe;
                }
                _draft.Timeframe = timeframe;
            }
            if (_fieldBox != null)
            {
                _draft.Field = Enum.Parse<CandleField>(EditorControls.SelectedKey(_fieldBox));
            }
            if (_functionBox != null)
            {
                _draft.Function = Enum.Parse<IndicatorFunction>(EditorControls.SelectedKey(_functionBox));
            }
            if (_arithmeticBox != null)
            {
                _draft.Arithmetic = Enum.Parse<ArithmeticOperator>(EditorControls.SelectedKey(_arithmeticBox));
            }
            if (_numberBox != null)
            {
                _draft.Number = ReadNumber(_numberBox, "Number");
            }
            if (_lengthBox != null)
            {
                _draft.Length = ReadInteger(_lengthBox, "Length", 1, 1000000);
            }
            if (_multiplierBox != null)
            {
                _draft.Multiplier = ReadNumber(_multiplierBox, "Multiplier");
                if (_draft.Multiplier <= 0)
                {
                    throw new ArgumentException("Multiplier must be greater than zero.");
                }
            }
            if (_offsetBox != null)
            {
                _draft.CandlesAgo = ReadInteger(_offsetBox, "Candles ago", 0, 1000000);
            }
            if (_decimalPlacesBox != null)
            {
                _draft.DecimalPlaces = ReadInteger(_decimalPlacesBox, "Decimal places", 0, 15);
            }
        }

        private static double ReadNumber(TextBox input, string label)
        {
            double value;
            if (!double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value))
            {
                throw new ArgumentException(label + " must be a valid number. Use a dot for decimal places.");
            }
            return value;
        }

        private static int ReadInteger(TextBox input, string label, int minimum, int maximum)
        {
            int value;
            if (!int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < minimum || value > maximum)
            {
                throw new ArgumentException(label + " must be a whole number between " + minimum + " and " + maximum + ".");
            }
            return value;
        }

        private void OnKindChanged(object sender, SelectionChangedEventArgs arguments)
        {
            ComboBox combo = (ComboBox)sender;
            _draft.Kind = Enum.Parse<ValueKind>(EditorControls.SelectedKey(combo));
            ErrorText.Text = string.Empty;
            BuildFields();
        }

        private void OnFunctionChanged(object sender, SelectionChangedEventArgs arguments)
        {
            try
            {
                ReadFields();
                ErrorText.Text = string.Empty;
                BuildFields();
            }
            catch (ArgumentException exception)
            {
                ErrorText.Text = exception.Message;
            }
        }

        private bool PrepareNestedEdit()
        {
            if (_depth >= 24)
            {
                ErrorText.Text = "This calculation is already deeply nested. Use a shallower expression.";
                return false;
            }
            try
            {
                ReadFields();
                ErrorText.Text = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                ErrorText.Text = exception.Message;
                return false;
            }
        }

        private void OnEditSource(object sender, RoutedEventArgs arguments)
        {
            if (!PrepareNestedEdit())
            {
                return;
            }
            ValueEditorWindow dialog = new ValueEditorWindow(Required(_draft.Source), _draft.Timeframe, _depth + 1);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _draft.Source = dialog.EditedValue;
                BuildFields();
            }
        }

        private void OnEditLeft(object sender, RoutedEventArgs arguments)
        {
            if (!PrepareNestedEdit())
            {
                return;
            }
            ValueEditorWindow dialog = new ValueEditorWindow(Required(_draft.Left), _defaultTimeframe, _depth + 1);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _draft.Left = dialog.EditedValue;
                BuildFields();
            }
        }

        private void OnEditRight(object sender, RoutedEventArgs arguments)
        {
            if (!PrepareNestedEdit())
            {
                return;
            }
            ValueEditorWindow dialog = new ValueEditorWindow(Required(_draft.Right), _defaultTimeframe, _depth + 1);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _draft.Right = dialog.EditedValue;
                BuildFields();
            }
        }

        private void OnEditCountCondition(object sender, RoutedEventArgs arguments)
        {
            if (!PrepareNestedEdit() || _draft.CountCondition == null)
            {
                return;
            }
            ConditionEditorWindow dialog = new ConditionEditorWindow(_draft.CountCondition, _draft.Timeframe, _depth + 1);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _draft.CountCondition = dialog.EditedCondition;
                BuildFields();
            }
        }

        private void OnApply(object sender, RoutedEventArgs arguments)
        {
            try
            {
                ReadFields();
                RuleDocumentAdapter adapter = new RuleDocumentAdapter();
                adapter.ValidateValue(_draft);
                DialogResult = true;
            }
            catch (Exception exception)
            {
                ErrorText.Text = exception.Message;
            }
        }
    }
}
