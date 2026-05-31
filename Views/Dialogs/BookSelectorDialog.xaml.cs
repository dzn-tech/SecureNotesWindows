using System.Collections.Generic;
using System.Windows;
using SecureNotesWin.Localization;
using SecureNotesWin.Models;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class BookSelectorDialog : Window
    {
        public string? SelectedNotebookId { get; private set; }

        public BookSelectorDialog(List<Notebook> notebooks, string title, bool showAllOption = false)
        {
            InitializeComponent();
            TitleLabel.Text = title;
            Title = title;
            BtnCancel.Content = LocalizationManager.Instance.GetString("cancel");
            var items = new List<Notebook>(notebooks);
            if (showAllOption)
                items.Insert(0, new Notebook { Id = "", Title = "All Books", Icon = "📚" });

            BookList.ItemsSource = items;
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (BookList.SelectedItem is Notebook nb)
            {
                SelectedNotebookId = string.IsNullOrEmpty(nb.Id) ? null : nb.Id;
                DialogResult = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
