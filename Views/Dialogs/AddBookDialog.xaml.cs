using System.Windows;
using System.Windows.Controls;
using SecureNotesWin.Helpers;
using SecureNotesWin.Localization;
using SecureNotesWin.Models;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class AddBookDialog : Window
    {
        public string BookName => TitleBox.Text.Trim();
        public string BookIcon => string.IsNullOrEmpty(IconBox.Text) ? DefaultIcon() : IconBox.Text.Trim();
        public NotebookType BookType => RadioDiary.IsChecked == true ? NotebookType.DIARY : NotebookType.NOTEBOOK;

        public AddBookDialog()
        {
            InitializeComponent();
            IconBox.Text = "📓";
            IconBox.IsEnabled = false;
            var lm = LocalizationManager.Instance;
            Title                 = lm.GetString("add_book");
            HeaderLabel.Text      = lm.GetString("add_book");
            TypeLabel.Text        = lm.GetString("type");
            NameLabel.Text        = lm.GetString("name");
            IconLabel.Text        = lm.GetString("icon");
            RadioDiary.Content    = "📖  " + lm.GetString("diary");
            RadioNotebook.Content = "📓  " + lm.GetString("notebook");
            BtnCancel.Content     = lm.GetString("cancel");
            BtnSave.Content       = lm.GetString("save");
            BookIconPicker.Populate(IconPickerPanel, IconBox.Text, IconButton_Click);
            Loaded += (_, _) => TitleBox.Focus();

        }

        private void IconButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string icon)
            {
                IconBox.Text = icon;
                BookIconPicker.Populate(IconPickerPanel, icon, IconButton_Click);
            }
        }

        private void BookType_Changed(object sender, RoutedEventArgs e)
        {
            if (IconBox == null) return;
            var current = IconBox.Text.Trim();
            if (current == "📓" || current == "📖")
            {
                IconBox.Text = DefaultIcon();
                BookIconPicker.Populate(IconPickerPanel, IconBox.Text, IconButton_Click);
            }
        }

        private string DefaultIcon() => RadioDiary.IsChecked == true ? "📖" : "📓";

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(BookName)) return;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
