using System.Windows;
using SecureNotesWin.Localization;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class ChangePasswordDialog : Window
    {
        public string OldPassword => OldPwdBox.Password;
        public string NewPassword => NewPwdBox.Password;

        public ChangePasswordDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => OldPwdBox.Focus();

            var loc = LocalizationManager.Instance;
            Title = loc.GetString("change_password");
            ChangeMasterPassword.Text = loc.GetString("change_password");
            CurrentPassword.Text = loc.GetString("current_password");
            NewPassword2.Text = loc.GetString("new_password");
            ConfirmNewPassword.Text = loc.GetString("confirm_new_password");

            BtnChange.Content = loc.GetString("change");
            BtnCancel.Content = loc.GetString("cancel");
        }

        private void BtnChange_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationManager.Instance;

            if (string.IsNullOrEmpty(OldPassword))
            {
                Show(loc.GetString("enter_current_password_error"));
                return;
            }

            if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < 6)
            {
                Show(loc.GetString("new_password_too_short"));
                return;
            }

            if (NewPassword != ConfirmPwdBox.Password)
            {
                Show(loc.GetString("passwords_do_not_match"));
                return;
            }

            DialogResult = true;
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Show(string msg)
        {
            ErrorLabel.Text = msg;
            ErrorLabel.Visibility = Visibility.Visible;
        }
    }
}
