using System.Windows;

namespace Bloxstrap.UI.Elements.Dialogs
{
    public partial class TextInputDialog
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public string Value => ValueTextBox.Text.Trim();

        public TextInputDialog(string initialValue = "")
        {
            InitializeComponent();
            ValueTextBox.Text = initialValue;
            Loaded += (_, _) => { ValueTextBox.Focus(); ValueTextBox.SelectAll(); };
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(Value))
                return;

            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
