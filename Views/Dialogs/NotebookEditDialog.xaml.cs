using System.Windows;
using System.Windows.Controls;
using SecureNotesWin.Helpers;
using SecureNotesWin.Localization;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class NotebookEditDialog : Window
    {
        public string NotebookTitle => TitleBox.Text.Trim();
        public string NotebookIcon => string.IsNullOrEmpty(IconBox.Text) ? "📓" : IconBox.Text.Trim();

        public NotebookEditDialog(string title = "", string icon = "📓")
        {
            InitializeComponent();
            TitleBox.Text = title;
            IconBox.Text  = icon;
            var lm = LocalizationManager.Instance;
            Title            = lm.GetString("edit_notebook");
            HeaderLabel.Text = lm.GetString("edit_notebook");
            NameLabel.Text   = lm.GetString("name");
            IconLabel.Text   = lm.GetString("icon");
            BtnCancel.Content = lm.GetString("cancel");
            BtnSave.Content   = lm.GetString("save");
            BookIconPicker.Populate(IconPickerPanel, icon, IconButton_Click);
            Loaded += (_, _) => TitleBox.Focus();
        }

        private void IconButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string picked)
            {
                IconBox.Text = picked;
                BookIconPicker.Populate(IconPickerPanel, picked, IconButton_Click);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NotebookTitle)) return;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
