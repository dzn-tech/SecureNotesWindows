using System.Windows;
using System.Windows.Input;
using SecureNotesWin.Localization;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class PasswordPromptDialog : Window
    {
        public string Password => PwdBox.Password;

        public PasswordPromptDialog(string message)
        {
            InitializeComponent();
            BtnCancel.Content = LocalizationManager.Instance.GetString("cancel");
            MessageLabel.Text = message;
            Loaded += (_, _) => PwdBox.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PwdBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) DialogResult = true;
            if (e.Key == Key.Escape) DialogResult = false;
        }
    }
}
