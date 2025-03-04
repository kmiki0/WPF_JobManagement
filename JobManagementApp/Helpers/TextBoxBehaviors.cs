using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JobManagementApp.Helpers
{
    public static class TextBoxBehaviors
    {
        public static readonly DependencyProperty LostFocusCommandProperty =
            DependencyProperty.RegisterAttached(
                "LostFocusCommand",
                typeof(ICommand),
                typeof(TextBoxBehaviors),
                new PropertyMetadata(null, OnLostFocusCommandChanged));

        public static ICommand GetLostFocusCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(LostFocusCommandProperty);
        }

        public static void SetLostFocusCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(LostFocusCommandProperty, value);
        }

        private static void OnLostFocusCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if (e.OldValue != null)
                {
                    textBox.LostFocus -= TextBox_LostFocus;
                }
                if (e.NewValue != null)
                {
                    textBox.LostFocus += TextBox_LostFocus;
                }
            }
        }

        private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var command = GetLostFocusCommand(textBox);
            if (command != null && command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
    }
}
