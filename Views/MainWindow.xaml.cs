using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SecureNotesWin.Localization;
using SecureNotesWin.Models;
using SecureNotesWin.ViewModels;

namespace SecureNotesWin.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private List<LanguageItem> _languages;

        public class LanguageItem
        {
            public AppLanguage Language { get; set; }
            public string Label { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
        }

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();

            _vm.LockRequested += (_, _) => ShowLockScreen();
            _vm.DataRefreshed += (_, _) => RefreshSidebar();

            LockScreenPanel.LockViewModel = _vm;
            LockScreenPanel.UnlockSuccess += LockScreen_UnlockSuccess;

            // Initialize language dropdown
            InitializeLanguageDropdown();

            // Subscribe to localization changes
            LocalizationManager.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item" || e.PropertyName == "CurrentLanguage")
                {
                    Dispatcher.Invoke(() => UpdateLocalizedTexts());
                }
            };

            if (_vm.LockState == AppLockState.SetupRequired)
            {
                LockScreenPanel.ShowSetup();
                LockScreenPanel.Visibility = Visibility.Visible;
            }
            else ShowLockScreen();

            this.PreviewMouseMove += (_, _) => _vm.ResetAutoLockTimer();
            this.PreviewKeyDown += (_, _) => _vm.ResetAutoLockTimer();
        }

        private void InitializeLanguageDropdown()
        {
            _languages = new List<LanguageItem>
            {
                new LanguageItem { Language = AppLanguage.ENGLISH, Label = "English", Code = "ENGLISH" },
                new LanguageItem { Language = AppLanguage.SPANISH, Label = "Español", Code = "SPANISH" },
                new LanguageItem { Language = AppLanguage.FRENCH, Label = "Français", Code = "FRENCH" },
                new LanguageItem { Language = AppLanguage.GERMAN, Label = "Deutsch", Code = "GERMAN" },
                new LanguageItem { Language = AppLanguage.ITALIAN, Label = "Italiano", Code = "ITALIAN" },
                new LanguageItem { Language = AppLanguage.PORTUGUESE, Label = "Português", Code = "PORTUGUESE" },
                new LanguageItem { Language = AppLanguage.RUSSIAN, Label = "Русский", Code = "RUSSIAN" },
                new LanguageItem { Language = AppLanguage.BENGALI, Label = "বাংলা", Code = "BENGALI" },
                new LanguageItem { Language = AppLanguage.HINDI, Label = "हिन्दी", Code = "HINDI" },
                new LanguageItem { Language = AppLanguage.CHINESE_MANDARIN, Label = "中文", Code = "CHINESE_MANDARIN" }
            };

            //LanguageCombo.ItemsSource = _languages;
            // DisplayMemberPath removed — LanguageCombo already has an ItemTemplate in XAML

            // Set current selection based on VM language
            //var currentLang = _vm.Language;
            //var selected = _languages.FirstOrDefault(l => l.Code == currentLang);
            //if (selected != null)
            //    LanguageCombo.SelectedItem = selected;
        }

        //private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (LanguageCombo.SelectedItem is LanguageItem selected && selected.Code != _vm.Language)
        //    {
        //        var newLang = selected.Language;
        //        _vm.SetLanguage(selected.Code);
        //        LocalizationManager.Instance.CurrentLanguage = newLang;
        //        UpdateLocalizedTexts();
        //    }
        //}

        private void UpdateLocalizedTexts()
        {
            // Update any dynamic texts that aren't automatically bound
            //if (NotesList.SelectedItem == null && _vm.Notes.Count == 0)
            //{
            //    CurrentSectionLabel.Text = LocalizationManager.Instance.GetString("select_book_to_view");
            //}

            // Refresh note count label
            var count = _vm.Notes.Count;
            NoteCountLabel.Text = count == 0 ? "" : $"{count} {LocalizationManager.Instance.GetString("notes_count")}";

            // Refresh sidebar text
            RefreshSidebar();
        }

        private void ShowLockScreen()
        {
            LockScreenPanel.Visibility = Visibility.Visible;
            MainPanel.Visibility = Visibility.Collapsed;
            LockScreenPanel.ShowUnlock();
        }

        private void LockScreen_UnlockSuccess(object? sender, EventArgs e)
        {
            LockScreenPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            VaultPathLabel.Text = System.IO.Path.GetFileName(_vm.KdbxPath);
            RefreshSidebar();
        }

        private void RefreshSidebar()
        {
            NotebookList.ItemsSource = null;
            NotebookList.ItemsSource = _vm.Notebooks;
            TagList.ItemsSource = null;
            TagList.ItemsSource = _vm.Tags;
            RefreshNoteList();
        }

        private void RefreshNoteList()
        {
            NotesList.ItemsSource = null;
            NotesList.ItemsSource = _vm.Notes;

            var count = _vm.Notes.Count;
            NoteCountLabel.Text = count == 0 ? "" : $"{count} {LocalizationManager.Instance.GetString("notes_count")}";
        }

        private void NoteCardTagsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not WrapPanel panel || panel.Tag is not Note note) return;
            BuildTagChips(panel, note);
        }

        private void BuildTagChips(WrapPanel panel, Note note)
        {
            panel.Children.Clear();
            var tags = _vm.Repository.GetTagsForNote(note.Id);
            foreach (var tag in tags)
            {
                var brush = ParseBrush(tag.Color);
                var bgBrush = new SolidColorBrush(
                    Color.FromArgb(40,
                        brush.Color.R,
                        brush.Color.G,
                        brush.Color.B));

                var chip = new Border
                {
                    Background = bgBrush,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 4, 2),
                };
                chip.Child = new TextBlock
                {
                    Text = $"# {tag.Title}",
                    FontSize = 10,
                    Foreground = brush,
                };
                panel.Children.Add(chip);
            }
        }

        private void NotebookItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Notebook nb)
            {
                _vm.SelectNotebook(nb.Id);
                RefreshNoteList();
                NotesList.SelectedItem = null;
                NoteEditor.ClearNote();
            }
        }

        private void TagItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Tag tag)
            {
                _vm.SelectTag(tag.Id);
                RefreshNoteList();
                NotesList.SelectedItem = null;
                NoteEditor.ClearNote();
            }
        }

        private void BtnAddTag_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.TagsDialog(_vm) { Owner = this };
            dlg.ShowDialog();
            _vm.RefreshCollections();
        }

        private async void TagDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Tag tag)
            {
                var result = MessageBox.Show(
                    $"Delete tag \"{tag.Title}\"?",
                    "Delete Tag", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
                await _vm.Repository.DeleteTagAsync(tag);
                if (_vm.SelectedTagId == tag.Id) _vm.SelectNotebook(null);
                _vm.RefreshCollections();
            }
        }

        private async void NotebookRename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Notebook nb)
            {
                var dlg = new Dialogs.NotebookEditDialog(nb.Title, nb.Icon) { Owner = this };
                if (dlg.ShowDialog() != true) return;
                try
                {
                    var updated = new Notebook
                    {
                        Id = nb.Id,
                        Title = dlg.NotebookTitle,
                        Icon = dlg.NotebookIcon,
                        Type = nb.Type,
                        CreatedAt = nb.CreatedAt,
                        UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ParentId = nb.ParentId,
                        SortOrder = nb.SortOrder
                    };
                    await _vm.Repository.UpdateNotebookAsync(updated);
                    _vm.RefreshCollections();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, LocalizationManager.Instance.GetString("edit"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void NotebookDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Notebook nb)
            {
                var bookType = nb.Type == NotebookType.DIARY ? "diary" : "notebook";
                var result = MessageBox.Show(
                    $"Delete {bookType} \"{nb.Title}\" and all its notes?",
                    $"Delete {bookType}", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                var noteIds = _vm.Repository.GetAllNotes()
                    .Where(n => n.NotebookId == nb.Id).Select(n => n.Id).ToList();
                await _vm.Repository.BulkDeleteNotesAsync(noteIds);
                await _vm.Repository.DeleteNotebookAsync(nb);

                if (_vm.SelectedNotebookId == nb.Id) _vm.SelectNotebook(null);
                _vm.RefreshCollections();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.SettingsDialog(_vm) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnLock_Click(object sender, RoutedEventArgs e)
        {
            _vm.Lock(); ShowLockScreen();
        }

        private async void BtnAddBook_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddBookDialog { Owner = this };
            if (dlg.ShowDialog() != true) return;
            try
            {
                await _vm.Repository.CreateNotebookAsync(dlg.BookName, dlg.BookIcon, dlg.BookType);
                _vm.RefreshCollections();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnNewNote_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedNotebookId == null)
            {
                MessageBox.Show("Select a book first.", "New Note",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var currentNb = _vm.Notebooks.FirstOrDefault(n => n.Id == _vm.SelectedNotebookId);
            var isDiary = currentNb?.Type == NotebookType.DIARY;
            var title = isDiary == true ? _vm.FormatDiaryTitle() : string.Empty;

            var note = await _vm.Repository.CreateNoteAsync(
                title, string.Empty, _vm.SelectedNotebookId, isDiary: isDiary == true);
            _vm.RefreshNotes();
            RefreshNoteList();
            NotesList.SelectedItem = note;
            NoteEditor.LoadNote(note, _vm, isNewNote: true);
        }

        private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesList.SelectedItem is Note note)
                NoteEditor.LoadNote(note, _vm);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _vm.SearchQuery = SearchBox.Text;
            RefreshNoteList();
        }

        private void NoteAddTags_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Note note)
            {
                var dlg = new Dialogs.TagsDialog(note, _vm) { Owner = this };
                dlg.ShowDialog();
                _vm.RefreshCollections();
                RefreshNoteList();
            }
        }

        private async void NoteMoveToNotebook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Note note)
                await MoveNote(note, NotebookType.NOTEBOOK);
        }

        private async void NoteMoveToDiary_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Note note)
                await MoveNote(note, NotebookType.DIARY);
        }

        private async System.Threading.Tasks.Task MoveNote(Note note, NotebookType targetType)
        {
            var candidates = _vm.Notebooks.Where(n => n.Type == targetType).ToList();
            if (!candidates.Any())
            {
                MessageBox.Show(
                    $"No {(targetType == NotebookType.DIARY ? "diary" : "notebook")} books exist. Create one first.",
                    "Move Note", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Dialogs.NotebookSelectionDialog(candidates, note.NotebookId) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            note.NotebookId = dlg.SelectedNotebookId;
            note.IsDiary = targetType == NotebookType.DIARY;
            await _vm.Repository.UpdateNoteAsync(note);
            _vm.RefreshNotes();
            RefreshNoteList();
        }

        private async void NoteDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Note note)
            {
                var result = MessageBox.Show(
                    $"Delete note \"{note.Title}\"?",
                    "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
                await _vm.Repository.DeleteNoteAsync(note.Id);
                _vm.RefreshNotes();
                RefreshNoteList();
            }
        }

        private static SolidColorBrush ParseBrush(string colorStr)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorStr)); }
            catch { return new SolidColorBrush(Colors.Orange); }
        }
    }
}