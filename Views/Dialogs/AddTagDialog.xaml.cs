using System.Windows;
using SecureNotesWin.Localization;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class AddTagDialog : Window
    {
        public string TagName => TagNameBox.Text.Trim();

        public AddTagDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => TagNameBox.Focus();
            BtnCancel.Content = LocalizationManager.Instance.GetString("cancel");
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TagName)) return;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
