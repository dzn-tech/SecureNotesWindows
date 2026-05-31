using System.Windows;
using System.Windows.Input;
using SecureNotesWin.Localization;
using SecureNotesWin.Models;
using SecureNotesWin.ViewModels;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class NewNoteDialog : Window
    {
        public string NoteTitle => TitleBox.Text.Trim();
        public string? SelectedNotebookId => (NotebookCombo.SelectedItem as Notebook)?.Id;
        public bool IsDiary => IsDiaryCheck.IsChecked == true;

        public NewNoteDialog(MainViewModel vm)
        {
            InitializeComponent();
            BtnCancel.Content = LocalizationManager.Instance.GetString("cancel");
            NotebookCombo.ItemsSource = vm.Notebooks;
            // Pre-select current notebook
            if (vm.SelectedNotebookId != null)
            {
                foreach (var nb in vm.Notebooks)
                {
                    if (nb.Id == vm.SelectedNotebookId)
                    {
                        NotebookCombo.SelectedItem = nb;
                        break;
                    }
                }
            }
            Loaded += (_, _) => TitleBox.Focus();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NoteTitle)) return;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void TitleBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(NoteTitle))
                DialogResult = true;
        }
    }
}
