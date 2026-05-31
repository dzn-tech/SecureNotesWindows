using System.Windows;
using System.Windows.Controls;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class OptionPickerDialog : Window
    {
        public int SelectedIndex => OptionsList.SelectedIndex >= 0 ? OptionsList.SelectedIndex : 0;

        public OptionPickerDialog(string title, string[] options, int selectedIndex = 0)
        {
            InitializeComponent();
            TitleLabel.Text = title;
            Title = title;
            OptionsList.ItemsSource = options;
            OptionsList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            OptionsList.SelectionChanged += (_, _) => DialogResult = true;
        }

        //private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
