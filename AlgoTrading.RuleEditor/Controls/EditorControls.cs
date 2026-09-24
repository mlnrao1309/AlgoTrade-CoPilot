using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Controls
{
    internal static class EditorControls
    {
        internal static TextBlock Text(string text, double size)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = size;
            block.TextWrapping = TextWrapping.Wrap;
            return block;
        }

        internal static Button Button(string label, RoutedEventHandler handler)
        {
            Button button = new Button();
            button.Content = label;
            button.Click += handler;
            button.Margin = new Thickness(0, 0, 6, 0);
            AutomationProperties.SetName(button, label);
            return button;
        }

        internal static ComboBox Choices(List<EditorChoice> choices, string selectedKey, string accessibleName)
        {
            bool present = false;
            foreach (EditorChoice choice in choices)
            {
                if (choice.Key == selectedKey)
                {
                    present = true;
                }
            }
            if (!present)
            {
                choices.Add(new EditorChoice(selectedKey, selectedKey));
            }
            ComboBox comboBox = new ComboBox();
            comboBox.ItemsSource = choices;
            comboBox.DisplayMemberPath = "Label";
            comboBox.SelectedValuePath = "Key";
            comboBox.SelectedValue = selectedKey;
            AutomationProperties.SetName(comboBox, accessibleName);
            return comboBox;
        }

        internal static string SelectedKey(ComboBox comboBox)
        {
            EditorChoice? choice = comboBox.SelectedItem as EditorChoice;
            if (choice == null)
            {
                throw new InvalidOperationException("Choose an item from the list.");
            }
            return choice.Key;
        }

        internal static TextBox Input(string value, string accessibleName)
        {
            TextBox textBox = new TextBox();
            textBox.Text = value;
            AutomationProperties.SetName(textBox, accessibleName);
            return textBox;
        }

        internal static void AddField(Panel panel, string label, FrameworkElement control)
        {
            TextBlock heading = Text(label, 12);
            heading.Foreground = (Brush)Application.Current.FindResource("MutedBrush");
            heading.Margin = new Thickness(0, 15, 0, 6);
            panel.Children.Add(heading);
            panel.Children.Add(control);
        }

        internal static Button ValueButton(string description, RoutedEventHandler handler)
        {
            Button button = Button(description, handler);
            TextBlock text = Text(description, 14);
            button.Content = text;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(12);
            button.MinHeight = 48;
            button.ToolTip = "Select to edit this value, its timeframe or its source.";
            return button;
        }
    }
}
