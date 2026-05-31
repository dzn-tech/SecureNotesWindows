using System.Collections.Generic;
using System.Windows;
using SecureNotesWin.Localization;
using SecureNotesWin.Models;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class NotebookSelectionDialog : Window
    {
        public string? SelectedNotebookId => (BookList.SelectedItem as Notebook)?.Id;

        public NotebookSelectionDialog(IEnumerable<Notebook> notebooks, string? currentId)
        {
            InitializeComponent();
            BookList.ItemsSource = notebooks;
            var lm = LocalizationManager.Instance;
            Title               = lm.GetString("move_to_book");
            WhichBookLabel.Text = lm.GetString("which_book");
            BtnMove.Content     = lm.GetString("move_btn");
            BtnCancel.Content   = lm.GetString("cancel");
            // Pre-select current
            foreach (Notebook nb in BookList.Items)
                if (nb.Id == currentId) { BookList.SelectedItem = nb; break; }
        }

        private void BtnMove_Click(object sender, RoutedEventArgs e)
        {
            if (BookList.SelectedItem == null) return;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
