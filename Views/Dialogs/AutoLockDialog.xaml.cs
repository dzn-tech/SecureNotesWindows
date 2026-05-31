using System;
using System.Windows;
using SecureNotesWin.Localization;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class AutoLockDialog : Window
    {
        private int _minutes;
        private int _seconds;
        public int TotalSeconds => _minutes * 60 + _seconds;

        public AutoLockDialog(int currentSeconds)
        {
            InitializeComponent();
            Title = LocalizationManager.Instance.GetString("auto_lock_timeout");
            _minutes = currentSeconds / 60;
            _seconds = currentSeconds % 60;
            BtnCancel.Content = LocalizationManager.Instance.GetString("cancel");
            BtnSave.Content = LocalizationManager.Instance.GetString("save");
            AutoLockTimeout.Text = LocalizationManager.Instance.GetString("auto_lock_timeout");
            LockAfterInactivity.Text = LocalizationManager.Instance.GetString("lock_after_inactivity");
            Minutes.Text = LocalizationManager.Instance.GetString("minutes");
            Seconds.Text = LocalizationManager.Instance.GetString("seconds");
            Refresh();
        }

        private void Refresh()
        {
            MinLabel.Text = _minutes.ToString().PadLeft(2, '0');
            SecLabel.Text = _seconds.ToString().PadLeft(2, '0');
        }

        private void MinUp_Click(object sender, RoutedEventArgs e)   { if (_minutes < 60) _minutes++; Refresh(); }
        private void MinDown_Click(object sender, RoutedEventArgs e)  { if (_minutes > 0)  _minutes--; Refresh(); }
        private void SecUp_Click(object sender, RoutedEventArgs e)    { if (_seconds < 59) _seconds++; Refresh(); }
        private void SecDown_Click(object sender, RoutedEventArgs e)  { if (_seconds > 0)  _seconds--; Refresh(); }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var total = Math.Max(5, TotalSeconds); // minimum 5 seconds like Android
            _minutes = total / 60; _seconds = total % 60;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
