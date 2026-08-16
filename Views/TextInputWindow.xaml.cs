using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TodayLOL.Views
{
    public partial class TextInputWindow : Window
    {
        public event EventHandler<string>? TextConfirmed;

        public TextInputWindow(System.Windows.Point position, System.Windows.Media.Brush color, double fontSize)
        {
            InitializeComponent();
            InputTextBox.Foreground = color;
            InputTextBox.FontSize = fontSize;
            InputTextBox.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var text = InputTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                TextConfirmed?.Invoke(this, text);
            }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }
    }
}