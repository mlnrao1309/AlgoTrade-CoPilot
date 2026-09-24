using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;
using AlgoTrading.RuleEditor.Services;
using AlgoTrading.RuleEditor.Windows;

namespace AlgoTrading.RuleEditor.Controls
{
    public sealed class ConditionEditorControl : UserControl
    {
        private readonly EditorCondition _condition;
        private readonly List<EditorCondition>? _siblings;
        private readonly string _defaultTimeframe;
        private readonly int _depth;
        public event EventHandler? Changing;
        public event EventHandler? Changed;

        public ConditionEditorControl(EditorCondition condition, List<EditorCondition>? siblings, string defaultTimeframe, int depth)
        {
            _condition = condition;
            _siblings = siblings;
            _defaultTimeframe = defaultTimeframe;
            _depth = depth;
            Build();
        }

        private void Build()
        {
            Border border = new Border();
            border.BorderBrush = (Brush)Application.Current.FindResource("LineBrush");
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(8);
            border.Background = (Brush)Application.Current.FindResource("PanelBrush");
            border.Padding = new Thickness(14);
            border.Margin = new Thickness(0, 0, 0, 10);
            StackPanel panel = new StackPanel();
            border.Child = panel;
            Content = border;
            DockPanel header = new DockPanel();
            header.Margin = new Thickness(0, 0, 0, 10);
            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            DockPanel.SetDock(actions, Dock.Right);
            if (_siblings != null)
            {
                Button up = EditorControls.Button("↑", OnMoveUp);
                up.ToolTip = "Move up";
                up.IsEnabled = _siblings.IndexOf(_condition) > 0;
                actions.Children.Add(up);
                Button down = EditorControls.Button("↓", OnMoveDown);
                down.ToolTip = "Move down";
                down.IsEnabled = _siblings.IndexOf(_condition) < _siblings.Count - 1;
                actions.Children.Add(down);
                actions.Children.Add(EditorControls.Button("Duplicate", OnDuplicate));
                actions.Children.Add(EditorControls.Button("Remove", OnRemove));
            }
            actions.Children.Add(EditorControls.Button("Comment", OnEditComment));
            header.Children.Add(actions);
            CheckBox enabled = new CheckBox();
            enabled.Content = "Enabled";
            enabled.IsChecked = _condition.Enabled;
            enabled.IsEnabled = _siblings != null;
            enabled.Click += OnEnabled;
            enabled.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(enabled);
            panel.Children.Add(header);
            if (_condition.Kind == ConditionKind.Comparison)
            {
                BuildComparison(panel);
            }
            else
            {
                BuildGroup(panel);
            }
            if (!string.IsNullOrWhiteSpace(_condition.Comment))
            {
                TextBlock comment = EditorControls.Text(_condition.Comment, 12);
                comment.Foreground = (Brush)Application.Current.FindResource("MutedBrush");
                comment.Margin = new Thickness(0, 10, 0, 0);
                panel.Children.Add(comment);
            }
            if (!_condition.Enabled)
            {
                border.Opacity = 0.55;
            }
        }

        private void BuildComparison(StackPanel panel)
        {
            Grid expression = new Grid();
            ColumnDefinition left = new ColumnDefinition();
            left.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition operation = new ColumnDefinition();
            operation.Width = new GridLength(180);
            ColumnDefinition right = new ColumnDefinition();
            right.Width = new GridLength(1, GridUnitType.Star);
            expression.ColumnDefinitions.Add(left);
            expression.ColumnDefinitions.Add(operation);
            expression.ColumnDefinitions.Add(right);
            Button leftButton = EditorControls.ValueButton(EditorLabels.Value(_condition.Left), OnEditLeft);
            expression.Children.Add(leftButton);
            ComboBox comparison = EditorControls.Choices(EditorCatalog.GetComparisons(), _condition.Operator.ToString(), "Comparison operator");
            comparison.Margin = new Thickness(4, 0, 10, 0);
            comparison.VerticalAlignment = VerticalAlignment.Center;
            comparison.SelectionChanged += OnOperatorChanged;
            Grid.SetColumn(comparison, 1);
            expression.Children.Add(comparison);
            Button rightButton = EditorControls.ValueButton(EditorLabels.Value(_condition.Right), OnEditRight);
            Grid.SetColumn(rightButton, 2);
            expression.Children.Add(rightButton);
            panel.Children.Add(expression);
        }

        private void BuildGroup(StackPanel panel)
        {
            TextBlock label = EditorControls.Text("Match", 14);
            label.Margin = new Thickness(0, 0, 0, 8);
            panel.Children.Add(label);
            ComboBox groupMode = EditorControls.Choices(EditorCatalog.GetGroups(), _condition.Kind.ToString(), "Group matching mode");
            groupMode.MaxWidth = 320;
            groupMode.HorizontalAlignment = HorizontalAlignment.Left;
            groupMode.MinWidth = 230;
            groupMode.Margin = new Thickness(0, 0, 0, 16);
            groupMode.SelectionChanged += OnGroupModeChanged;
            panel.Children.Add(groupMode);
            if (_condition.Children.Count == 0)
            {
                TextBlock empty = EditorControls.Text("Add a condition or a nested group to begin.", 14);
                empty.Margin = new Thickness(0, 8, 0, 16);
                panel.Children.Add(empty);
            }
            foreach (EditorCondition child in _condition.Children)
            {
                ConditionEditorControl editor = new ConditionEditorControl(child, _condition.Children, _defaultTimeframe, _depth + 1);
                editor.Changing += OnChildChanging;
                editor.Changed += OnChildChanged;
                panel.Children.Add(editor);
            }
            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.Children.Add(EditorControls.Button("+ Condition", OnAddCondition));
            actions.Children.Add(EditorControls.Button("+ Group", OnAddGroup));
            panel.Children.Add(actions);
        }

        private void OnChildChanging(object? sender, EventArgs arguments)
        {
            NotifyChanging();
        }

        private void OnChildChanged(object? sender, EventArgs arguments)
        {
            NotifyChanged();
        }

        private void NotifyChanging()
        {
            if (Changing != null)
            {
                Changing(this, EventArgs.Empty);
            }
        }

        private void NotifyChanged()
        {
            if (Changed != null)
            {
                Changed(this, EventArgs.Empty);
            }
        }

        private void OnEditLeft(object sender, RoutedEventArgs arguments)
        {
            ValueEditorWindow dialog = new ValueEditorWindow(_condition.Left, _defaultTimeframe, _depth + 1);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                NotifyChanging();
                _condition.Left = dialog.EditedValue;
                NotifyChanged();
            }
        }

        private void OnEditRight(object sender, RoutedEventArgs arguments)
        {
            ValueEditorWindow dialog = new ValueEditorWindow(_condition.Right, _defaultTimeframe, _depth + 1);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                NotifyChanging();
                _condition.Right = dialog.EditedValue;
                NotifyChanged();
            }
        }

        private void OnOperatorChanged(object sender, SelectionChangedEventArgs arguments)
        {
            ComboBox combo = (ComboBox)sender;
            NotifyChanging();
            _condition.Operator = Enum.Parse<ComparisonOperator>(EditorControls.SelectedKey(combo));
            NotifyChanged();
        }

        private void OnGroupModeChanged(object sender, SelectionChangedEventArgs arguments)
        {
            ComboBox combo = (ComboBox)sender;
            NotifyChanging();
            _condition.Kind = Enum.Parse<ConditionKind>(EditorControls.SelectedKey(combo));
            NotifyChanged();
        }

        private void OnEnabled(object sender, RoutedEventArgs arguments)
        {
            CheckBox checkBox = (CheckBox)sender;
            NotifyChanging();
            _condition.Enabled = checkBox.IsChecked == true;
            NotifyChanged();
        }

        private void OnEditComment(object sender, RoutedEventArgs arguments)
        {
            CommentEditorWindow dialog = new CommentEditorWindow(_condition.Comment);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                NotifyChanging();
                _condition.Comment = dialog.EditedComment;
                NotifyChanged();
            }
        }

        private void OnAddCondition(object sender, RoutedEventArgs arguments)
        {
            NotifyChanging();
            _condition.Children.Add(EditorExamples.NewComparison(_defaultTimeframe));
            NotifyChanged();
        }

        private void OnAddGroup(object sender, RoutedEventArgs arguments)
        {
            if (_depth >= 24)
            {
                MessageBox.Show("This group is already deeply nested. Use a shallower group.");
                return;
            }
            NotifyChanging();
            EditorCondition group = new EditorCondition();
            group.Children.Add(EditorExamples.NewComparison(_defaultTimeframe));
            _condition.Children.Add(group);
            NotifyChanged();
        }

        private void OnRemove(object sender, RoutedEventArgs arguments)
        {
            if (_siblings != null)
            {
                NotifyChanging();
                _siblings.Remove(_condition);
                NotifyChanged();
            }
        }

        private void OnDuplicate(object sender, RoutedEventArgs arguments)
        {
            if (_siblings != null)
            {
                NotifyChanging();
                _siblings.Insert(_siblings.IndexOf(_condition) + 1, EditorCopyService.Copy(_condition));
                NotifyChanged();
            }
        }

        private void OnMoveUp(object sender, RoutedEventArgs arguments)
        {
            Move(-1);
        }

        private void OnMoveDown(object sender, RoutedEventArgs arguments)
        {
            Move(1);
        }

        private void Move(int direction)
        {
            if (_siblings == null)
            {
                return;
            }
            int index = _siblings.IndexOf(_condition);
            int destination = index + direction;
            if (destination < 0 || destination >= _siblings.Count)
            {
                return;
            }
            NotifyChanging();
            _siblings.RemoveAt(index);
            _siblings.Insert(destination, _condition);
            NotifyChanged();
        }
    }
}

