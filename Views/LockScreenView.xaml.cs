using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SecureNotesWin.Localization;
using SecureNotesWin.ViewModels;

namespace SecureNotesWin.Views
{
    public partial class LockScreenView : UserControl
    {
        public MainViewModel? LockViewModel { get; set; }
        public event EventHandler? UnlockSuccess;

        // Stores the path chosen in the Open Vault flow
        private string? _pendingOpenPath;

        public LockScreenView()
        {
            InitializeComponent();
            EnterPassword.Text = LocalizationManager.Instance["please_enter_password"];
            SecureNotes.Text = LocalizationManager.Instance["secure_notes"];
            UnlockVault.Content = LocalizationManager.Instance["unlock_vault"];
            CreateVault.Content = LocalizationManager.Instance["create_vault"];
        }

        // ─────────────────────────────────────────────
        // Panel helpers
        // ─────────────────────────────────────────────

        private void ShowPanel(UIElement show)
        {
            UnlockPanel.Visibility = Visibility.Collapsed;
            OpenVaultPanel.Visibility = Visibility.Collapsed;
            SetupPanel.Visibility = Visibility.Collapsed;
            show.Visibility = Visibility.Visible;
        }

        public void ShowUnlock()
        {
            ShowPanel(UnlockPanel);
            UnlockPasswordBox.Clear();
            UnlockErrorLabel.Visibility = Visibility.Collapsed;

            // Show the stored vault path if we have one
            var path = LockViewModel?.KdbxPath;

            VaultPathBorder.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(path))
            {
                VaultPathDisplay.Text = path;
            }


            Dispatcher.BeginInvoke(() => UnlockPasswordBox.Focus());
        }

        public void ShowSetup()
        {
            ShowPanel(SetupPanel);
            SetupPasswordBox.Clear();
            SetupConfirmBox.Clear();
            SetupErrorLabel.Visibility = Visibility.Collapsed;
            StrengthBar.Value = 0;
            StrengthLabel.Text = "";
        }

        // ─────────────────────────────────────────────
        // Unlock panel
        // ─────────────────────────────────────────────

        private async void BtnUnlock_Click(object sender, RoutedEventArgs e)
            => await TryUnlock();

        private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await TryUnlock();
        }

        private async System.Threading.Tasks.Task TryUnlock()
        {
            if (LockViewModel == null) return;
            var password = UnlockPasswordBox.Password;
            if (string.IsNullOrEmpty(password)) return;

            var path = LockViewModel.KdbxPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                UnlockErrorLabel.Text = LocalizationManager.Instance["no_vault_found"];
                UnlockErrorLabel.Visibility = Visibility.Visible;
                return;
            }

            UnlockErrorLabel.Visibility = Visibility.Collapsed;
            var ok = await LockViewModel.Repository.OpenDatabaseAsync(path, password);
            if (ok)
            {
                LockViewModel.Settings.SaveVaultPassword(password);
                LockViewModel.Unlock(path);
                UnlockSuccess?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                UnlockErrorLabel.Text = LocalizationManager.Instance["incorrect_password_vault"];
                UnlockErrorLabel.Visibility = Visibility.Visible;
                UnlockPasswordBox.Clear();
                UnlockPasswordBox.Focus();
            }
        }

        // ─────────────────────────────────────────────
        // Open different vault — inline flow
        // ─────────────────────────────────────────────

        private void BtnOpenVault_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "KeePass Database (*.kdbx)|*.kdbx|All Files (*.*)|*.*",
                Title = LocalizationManager.Instance["open_existing_vault"]
            };
            if (dlg.ShowDialog() != true) return;

            _pendingOpenPath = dlg.FileName;
            OpenVaultPathLabel.Text = dlg.FileName;
            OpenVaultPasswordBox.Clear();
            OpenVaultErrorLabel.Visibility = Visibility.Collapsed;
            ShowPanel(OpenVaultPanel);
            Dispatcher.BeginInvoke(() => OpenVaultPasswordBox.Focus());
        }

        private async void BtnOpenVaultConfirm_Click(object sender, RoutedEventArgs e)
            => await TryOpenVault();

        private async void OpenVaultPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await TryOpenVault();
        }

        private async System.Threading.Tasks.Task TryOpenVault()
        {
            if (LockViewModel == null || _pendingOpenPath == null) return;
            var password = OpenVaultPasswordBox.Password;
            if (string.IsNullOrEmpty(password)) return;

            OpenVaultErrorLabel.Visibility = Visibility.Collapsed;
            var ok = await LockViewModel.Repository.OpenDatabaseAsync(_pendingOpenPath, password);
            if (ok)
            {
                LockViewModel.Settings.SaveVaultPassword(password);
                LockViewModel.Settings.MarkSetup(true);
                LockViewModel.Unlock(_pendingOpenPath);
                UnlockSuccess?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                OpenVaultErrorLabel.Text = LocalizationManager.Instance["incorrect_password_vault"]; 
                OpenVaultErrorLabel.Visibility = Visibility.Visible;
                OpenVaultPasswordBox.Clear();
                OpenVaultPasswordBox.Focus();
            }
        }

        private void BtnOpenVaultCancel_Click(object sender, RoutedEventArgs e)
        {
            _pendingOpenPath = null;
            ShowUnlock();
        }

        // ─────────────────────────────────────────────
        // Create new vault — setup flow
        // ─────────────────────────────────────────────

        private void BtnCreateVault_Click(object sender, RoutedEventArgs e)
            => ShowSetup();

        private void BtnSetupCancel_Click(object sender, RoutedEventArgs e)
            => ShowUnlock();

        private void SetupPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var pwd = SetupPasswordBox.Password;
            var (score, label, color) = MeasureStrength(pwd);
            StrengthBar.Value = score;
            StrengthBar.Foreground = new SolidColorBrush(color);
            StrengthLabel.Text = label;
            StrengthLabel.Foreground = new SolidColorBrush(color);
        }

        private static (int score, string label, Color color) MeasureStrength(string pwd)
        {
            if (string.IsNullOrEmpty(pwd)) return (0, "", Colors.Transparent);

            int score = 0;
            if (pwd.Length >= 8) score += 20;
            if (pwd.Length >= 12) score += 15;
            if (pwd.Length >= 16) score += 15;
            if (System.Text.RegularExpressions.Regex.IsMatch(pwd, @"[a-z]")) score += 10;
            if (System.Text.RegularExpressions.Regex.IsMatch(pwd, @"[A-Z]")) score += 10;
            if (System.Text.RegularExpressions.Regex.IsMatch(pwd, @"[0-9]")) score += 15;
            if (System.Text.RegularExpressions.Regex.IsMatch(pwd, @"[^a-zA-Z0-9]")) score += 15;

            return score switch
            {
                < 30 => (score, "Weak", Color.FromRgb(220, 53, 69)),
                < 55 => (score, "Fair", Color.FromRgb(253, 126, 20)),
                < 75 => (score, "Good", Color.FromRgb(255, 193, 7)),
                _ => (score, "Strong", Color.FromRgb(40, 167, 69))
            };
        }

        private async void BtnSetupCreate_Click(object sender, RoutedEventArgs e)
        {
            
            if (LockViewModel == null) return;
            var pwd = SetupPasswordBox.Password;
            var confirm = SetupConfirmBox.Password;

            if (string.IsNullOrEmpty(pwd))
            {
                SetupErrorLabel.Text = "Please enter a password.";
                SetupErrorLabel.Visibility = Visibility.Visible;
                return;
            }
            if (pwd != confirm)
            {
                SetupErrorLabel.Text = "Passwords do not match.";
                SetupErrorLabel.Visibility = Visibility.Visible;
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "KeePass Database (*.kdbx)|*.kdbx",
                FileName = "SecureNotes.kdbx",
                Title = "Save new vault as…"
            };
            if (dlg.ShowDialog() != true) return;

            SetupErrorLabel.Visibility = Visibility.Collapsed;
            var ok = await LockViewModel.Repository.CreateDatabaseAsync(dlg.FileName, pwd);
            if (ok)
            {
                LockViewModel.Settings.SaveVaultPassword(pwd);
                LockViewModel.SetupComplete(dlg.FileName);
                UnlockSuccess?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                SetupErrorLabel.Text = "Failed to create vault. Check permissions.";
                SetupErrorLabel.Visibility = Visibility.Visible;
            }
        }
    }
}