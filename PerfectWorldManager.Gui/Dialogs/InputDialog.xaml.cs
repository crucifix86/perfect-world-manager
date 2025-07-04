using System.Windows;

namespace PerfectWorldManager.Gui.Dialogs
{
    public partial class InputDialog : Window
    {
        public string ResponseText { get; private set; } = string.Empty;

        public InputDialog(string prompt, string title, string defaultValue = "", string description = "")
        {
            InitializeComponent();
            
            Title = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = defaultValue;
            
            if (!string.IsNullOrEmpty(description))
            {
                DescriptionTextBlock.Text = description;
                DescriptionTextBlock.Visibility = Visibility.Visible;
            }
            
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = InputTextBox.Text;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}