using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SecureNotesWin.Helpers;
using SecureNotesWin.Models;
using SecureNotesWin.ViewModels;

namespace SecureNotesWin.Views
{
    public partial class NoteEditorView : UserControl
    {
        private Note?          _currentNote;
        private MainViewModel? _vm;
        private bool           _loading;       // suppresses change-handlers during load
        private bool           _isDiaryBook;
        private CancellationTokenSource? _saveCts;

        // ── Layout constants ──────────────────────────────────────────────────────

        private static readonly GridLength StarHeight    = new GridLength(1, GridUnitType.Star);
        private static readonly GridLength ZeroHeight    = new GridLength(0);
        private static readonly GridLength SplitterThick = new GridLength(5);
        private const double MinImagePanelHeight = 80;

        // ── Constructor ───────────────────────────────────────────────────────────

        public NoteEditorView()
        {
            InitializeComponent();
            var lm = SecureNotesWin.Localization.LocalizationManager.Instance;
            SelectOrCreateLabel.Text    = lm.GetString("select_or_create");
            TagsButton.ToolTip          = lm.GetString("manage_tags");
            MoveButton.ToolTip          = lm.GetString("move_to_book");
        }

        // ── Clear (show empty state) ──────────────────────────────────────────────

        public void ClearNote()
        {
            _currentNote           = null;
            _vm                    = null;
            EmptyState.Visibility  = Visibility.Visible;
            EditorPanel.Visibility = Visibility.Collapsed;
        }

        // ── Load ──────────────────────────────────────────────────────────────────

        public void LoadNote(Note note, MainViewModel vm, bool isNewNote = false)
        {
            _vm          = vm;
            _currentNote = note;
            _loading     = true;

            var nb = vm.Notebooks.FirstOrDefault(n => n.Id == note.NotebookId);
            _isDiaryBook = note.IsDiary || nb?.Type == NotebookType.DIARY;

            EmptyState.Visibility  = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Visible;

            TitleBox.Text = note.Title;
            if (_isDiaryBook && isNewNote)
                TitleBox.Text = vm.FormatDiaryTitle();

            // Populate the WYSIWYG editor from stored markdown
            LoadBodyIntoRichEditor(NoteBodyHelper.StripImageRefsFromBody(note.Body));

            RefreshImagePanel();
            RefreshTagChips();
            UpdateWordCount();
            SaveIndicator.Visibility = Visibility.Collapsed;

            if (nb != null)
            {
                //NotebookChipLabel.Text  = $"{nb.Icon} {nb.Title}";
                NotebookChip.Visibility = Visibility.Visible;
            }
            else NotebookChip.Visibility = Visibility.Collapsed;

           // NoteDateLabel.Text = vm.FormatDateTime(note.UpdatedAt);
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(note.UpdatedAt).LocalDateTime;
            StatusLabel.Text = $"Modified {dt:MMM d, yyyy h:mm tt}";

            _loading = false;

            if (isNewNote || (!_isDiaryBook && string.IsNullOrEmpty(note.Title)))
                RichEditor.Focus();
        }

        // ── RichTextBox ↔ Markdown bridge ─────────────────────────────────────────

        /// <summary>
        /// Parses stored markdown and populates the RichTextBox FlowDocument.
        /// Runs inside the _loading guard so TextChanged doesn't fire a save.
        /// </summary>
        private void LoadBodyIntoRichEditor(string markdown)
        {
            var doc = RichEditor.Document;
            var fg  = RichEditor.Foreground ?? Brushes.White;
            MarkdownRtbHelper.LoadMarkdown(doc, markdown, fg);
        }

        /// <summary>
        /// Walks the current FlowDocument and returns it as markdown.
        /// Called every time the document changes to keep _currentNote.Body in sync.
        /// </summary>
        private string ReadRichEditorAsMarkdown()
            => MarkdownRtbHelper.SaveMarkdown(RichEditor.Document);

        // ── Event handlers ────────────────────────────────────────────────────────

        private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _currentNote == null) return;
            _currentNote.Title = TitleBox.Text;
            ScheduleSave();
        }

        private void RichEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _currentNote == null) return;
            var textOnly = ReadRichEditorAsMarkdown();
            _currentNote.Body = NoteBodyHelper.MergeTextIntoBody(_currentNote.Body, textOnly);
            UpdateWordCount();
            ScheduleSave();
        }

        /// <summary>
        /// Updates toolbar button highlight to reflect current selection state.
        /// </summary>
        private void RichEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            UpdateToolbarState();
        }

        private void UpdateToolbarState()
        {
            var sel = RichEditor.Selection;

            // Bold
            HighlightBtn(BoldBtn,
                MarkdownRtbHelper.SelectionHasWeight(sel, FontWeights.Bold));

            // Italic
            HighlightBtn(ItalicBtn,
                MarkdownRtbHelper.SelectionHasStyle(sel, FontStyles.Italic));

            // Headings — check paragraph font size
            double sz = MarkdownRtbHelper.SelectionFontSize(sel);
            HighlightBtn(H1Btn,    Math.Abs(sz - MarkdownRtbHelper.H1Size) < 0.5);
            HighlightBtn(H2Btn,    Math.Abs(sz - MarkdownRtbHelper.H2Size) < 0.5);
            HighlightBtn(H3Btn,    Math.Abs(sz - MarkdownRtbHelper.H3Size) < 0.5);
            //HighlightBtn(BigBtn,   Math.Abs(sz - MarkdownRtbHelper.BigSize) < 0.5);
            //HighlightBtn(SmallBtn, Math.Abs(sz - MarkdownRtbHelper.SmallSize) < 0.5);
        }

        private static void HighlightBtn(Button btn, bool active)
        {
            btn.Background = active
                ? new SolidColorBrush(Color.FromArgb(60, 245, 158, 11))   // subtle amber glow
                : Brushes.Transparent;
        }

        // ── Enter key: auto-continue lists ───────────────────────────────────────

        private void RichEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return) return;

            // Check if caret is inside a List — WPF handles list continuation
            // natively, so we only need to intercept the "empty item → exit list"
            // case (same UX as before).
            var caretPos = RichEditor.CaretPosition;
            var para     = caretPos.Paragraph;
            if (para == null) return;

            // If para is inside a ListItem and is empty → remove the item and exit
            if (para.Parent is ListItem li && li.Parent is List list)
            {
                var text = new TextRange(para.ContentStart, para.ContentEnd).Text;
                if (string.IsNullOrEmpty(text))
                {
                    // Remove empty list item and insert a plain paragraph after the list
                    list.ListItems.Remove(li);
                    if (list.ListItems.Count == 0)
                        list.SiblingBlocks.Remove(list);

                    var newPara = new Paragraph(new Run(string.Empty))
                    {
                        Margin     = new Thickness(0, 0, 0, 4),
                        FontSize   = MarkdownRtbHelper.BaseSize,
                        FontFamily = new FontFamily("Segoe UI"),
                        Foreground = RichEditor.Foreground
                    };
                    RichEditor.Document.Blocks.Add(newPara);
                    RichEditor.CaretPosition = newPara.ContentEnd;
                    e.Handled = true;
                }
            }
        }

        // ── Image panel ───────────────────────────────────────────────────────────

        private void RefreshImagePanel()
        {
            ImageContainer.Children.Clear();
            var names = _currentNote?.AttachmentNames ?? new System.Collections.Generic.List<string>();

            if (names.Count == 0)
            {
                ImagePanel.Visibility    = Visibility.Collapsed;
                PanelSplitter.Visibility = Visibility.Collapsed;
                SplitterRow.Height       = ZeroHeight;
                ImageRow.Height          = ZeroHeight;
                TextRow.Height           = StarHeight;
                return;
            }

            // 50/50 split
            if (ImageRow.Height == ZeroHeight || ImageRow.Height.Value < MinImagePanelHeight)
            {
                TextRow.Height     = StarHeight;
                ImageRow.Height    = StarHeight;
                SplitterRow.Height = SplitterThick;
            }

            ImagePanel.Visibility    = Visibility.Visible;
            PanelSplitter.Visibility = Visibility.Visible;

            foreach (var fileName in names)
            {
                if (!(_currentNote?.Attachments.TryGetValue(fileName, out var bytes) == true) || bytes == null)
                    continue;

                var container = new Grid { Margin = new Thickness(0, 0, 0, 10) };

                BitmapImage? bmp = null;
                try
                {
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(bytes);
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                }
                catch { bmp = null; }

                if (bmp != null)
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Source              = bmp,
                        Stretch             = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment   = VerticalAlignment.Top,
                        Tag                 = "attachment-image"
                    };
                    var border = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        ClipToBounds = true,
                        Background   = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16)),
                        Child        = img
                    };
                    container.Children.Add(border);
                    SetImageHeightFromPanel(img);
                }

                var delBtn = new Button
                {
                    Content             = "✕",
                    Width               = 24,
                    Height              = 24,
                    FontSize            = 10,
                    Background          = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                    Foreground          = Brushes.White,
                    BorderThickness     = new Thickness(0),
                    Cursor              = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment   = VerticalAlignment.Top,
                    Margin              = new Thickness(0, 4, 4, 0),
                    Tag                 = fileName,
                    ToolTip             = "Remove image"
                };
                delBtn.Click += DeleteImage_Click;
                container.Children.Add(delBtn);

                ImageContainer.Children.Add(container);
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(SyncAllImageHeights));
        }

        private void ImagePanel_SizeChanged(object sender, SizeChangedEventArgs e)
            => SyncAllImageHeights();

        private void SyncAllImageHeights()
        {
            foreach (var img in FindImages(ImageContainer))
                SetImageHeightFromPanel(img);
        }

        private void SetImageHeightFromPanel(System.Windows.Controls.Image img)
        {
            const double HeaderHeight = 28;
            var available = ImagePanel.ActualHeight - HeaderHeight - 16;
            if (available > 0)
            {
                img.MaxHeight = available;
                img.Height    = double.NaN;
            }
        }

        private static System.Collections.Generic.IEnumerable<System.Windows.Controls.Image>
            FindImages(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Image img &&
                    img.Tag is string t && t == "attachment-image")
                    yield return img;
                foreach (var sub in FindImages(child))
                    yield return sub;
            }
        }

        private async void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null || sender is not Button btn || btn.Tag is not string fileName)
                return;

            _currentNote.Attachments.Remove(fileName);
            _currentNote.AttachmentNames.Remove(fileName);
            _currentNote.Body = NoteBodyHelper.RemoveImageRefFromBody(_currentNote.Body, fileName);

            // Re-load the editor without image refs
            _loading = true;
            LoadBodyIntoRichEditor(NoteBodyHelper.StripImageRefsFromBody(_currentNote.Body));
            _loading = false;

            RefreshImagePanel();
            await SaveNoteAsync();
        }

        // ── Tags ──────────────────────────────────────────────────────────────────

        private void RefreshTagChips()
        {
            TagsRow.Children.Clear();
            if (_currentNote == null || _vm == null) return;

            foreach (var tag in _vm.Repository.GetTagsForNote(_currentNote.Id))
            {
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding      = new Thickness(8, 3, 8, 3),
                    Margin       = new Thickness(0, 0, 6, 4),
                    Cursor       = Cursors.Hand,
                    Tag          = tag
                };
                try   { chip.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tag.Color + "30")); }
                catch { chip.Background = new SolidColorBrush(Color.FromArgb(48, 255, 179, 71)); }

                var label = new TextBlock { Text = $"# {tag.Title}", FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                try   { label.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tag.Color)); }
                catch { label.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); }

                chip.Child = label;
                TagsRow.Children.Add(chip);
            }

            var addBtn = new TextBlock
            {
                Text              = "+ tag",
                FontSize          = 11,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor            = Cursors.Hand,
                Margin            = new Thickness(0, 0, 0, 4)
            };
            addBtn.MouseLeftButtonUp += (_, _) => TagsBtn_Click(addBtn, new RoutedEventArgs());
            TagsRow.Children.Add(addBtn);
        }

        // ── Word count ────────────────────────────────────────────────────────────

        private void UpdateWordCount()
        {
            var text  = NoteBodyHelper.StripImageRefsFromBody(_currentNote?.Body ?? string.Empty);
            var words = string.IsNullOrWhiteSpace(text) ? 0
                : text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            WordCountLabel.Text = $"{words} words";
        }

        // ── Save ──────────────────────────────────────────────────────────────────

        private async void ScheduleSave()
        {
            SaveIndicator.Visibility = Visibility.Collapsed;
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;
            try
            {
                await Task.Delay(1000, token);
                if (!token.IsCancellationRequested) await SaveNoteAsync();
            }
            catch (TaskCanceledException) { }
        }

        private async Task SaveNoteAsync()
        {
            if (_currentNote == null || _vm == null) return;
            _currentNote.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _vm.Repository.UpdateNoteAsync(_currentNote);
            Dispatcher.Invoke(() =>
            {
                SaveIndicator.Visibility = Visibility.Visible;
                //NoteDateLabel.Text = _vm.FormatDateTime(_currentNote.UpdatedAt);
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(_currentNote.UpdatedAt).LocalDateTime;
                StatusLabel.Text = $"Modified {dt:MMM d, yyyy h:mm tt}";
                _vm.RefreshNotes();
            });
        }

        // ── Dialogs ───────────────────────────────────────────────────────────────

        private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null || _vm == null) return;
            var L = SecureNotesWin.Localization.LocalizationManager.Instance;
            var result = MessageBox.Show(
                string.Format(L.GetString("delete_note_msg"), _currentNote.Title),
                L.GetString("delete_note_title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            await _vm.Repository.DeleteNoteAsync(_currentNote.Id);
            _currentNote = null;
            EmptyState.Visibility  = Visibility.Visible;
            EditorPanel.Visibility = Visibility.Collapsed;
            _vm.RefreshNotes();
        }

        private void TagsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null || _vm == null) return;
            var dlg = new Dialogs.TagsDialog(_currentNote, _vm) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
            _vm.RefreshCollections();
            RefreshTagChips();
        }

        private void MoveToBookBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null || _vm == null) return;
            var dlg = new Dialogs.NotebookSelectionDialog(_vm.Notebooks, _currentNote.NotebookId)
                { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true) return;
            _currentNote.NotebookId = dlg.SelectedNotebookId;
            _ = _vm.Repository.UpdateNoteAsync(_currentNote);
            _vm.RefreshNotes();
            var nb = _vm.Notebooks.FirstOrDefault(n => n.Id == _currentNote.NotebookId);
         //   NotebookChipLabel.Text  = nb != null ? $"{nb.Icon} {nb.Title}" : "";
            NotebookChip.Visibility = nb != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NotebookChip_Click(object sender, MouseButtonEventArgs e)
            => MoveToBookBtn_Click(sender, e);

        // ── Toolbar — WYSIWYG formatting commands ─────────────────────────────────
        //
        // All formatting is applied directly to the RichTextBox selection as WPF
        // TextElement properties.  No markdown syntax is ever inserted into the
        // visible document.  The document is serialised to markdown only on save.
        //

        private void ToolbarBold_Click(object sender, RoutedEventArgs e)
        {
            RichEditor.Focus();
            var isBold = MarkdownRtbHelper.SelectionHasWeight(RichEditor.Selection, FontWeights.Bold);
            RichEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,
                isBold ? FontWeights.Normal : FontWeights.Bold);
            UpdateToolbarState();
        }

        private void ToolbarItalic_Click(object sender, RoutedEventArgs e)
        {
            RichEditor.Focus();
            var isItalic = MarkdownRtbHelper.SelectionHasStyle(RichEditor.Selection, FontStyles.Italic);
            RichEditor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty,
                isItalic ? FontStyles.Normal : FontStyles.Italic);
            UpdateToolbarState();
        }

        private void ToolbarH1_Click(object sender, RoutedEventArgs e)
        {
            var isH1 = MarkdownRtbHelper.SelectionHasSize(RichEditor.Selection, MarkdownRtbHelper.H1Size);
            MarkdownRtbHelper.ApplyHeading(RichEditor, isH1 ? 0 : 1);
            UpdateToolbarState();
        }

        private void ToolbarH2_Click(object sender, RoutedEventArgs e)
        {
            var isH2 = MarkdownRtbHelper.SelectionHasSize(RichEditor.Selection, MarkdownRtbHelper.H2Size);
            MarkdownRtbHelper.ApplyHeading(RichEditor, isH2 ? 0 : 2);
            UpdateToolbarState();
        }

        private void ToolbarH3_Click(object sender, RoutedEventArgs e)
        {
            var isH3 = MarkdownRtbHelper.SelectionHasSize(RichEditor.Selection, MarkdownRtbHelper.H3Size);
            MarkdownRtbHelper.ApplyHeading(RichEditor, isH3 ? 0 : 3);
            UpdateToolbarState();
        }

        private void ToolbarBig_Click(object sender, RoutedEventArgs e)
        {
            RichEditor.Focus();
            var isBig = MarkdownRtbHelper.SelectionHasSize(RichEditor.Selection, MarkdownRtbHelper.BigSize);
            RichEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,
                isBig ? MarkdownRtbHelper.BaseSize : MarkdownRtbHelper.BigSize);
            UpdateToolbarState();
        }

        private void ToolbarSmall_Click(object sender, RoutedEventArgs e)
        {
            RichEditor.Focus();
            var isSmall = MarkdownRtbHelper.SelectionHasSize(RichEditor.Selection, MarkdownRtbHelper.SmallSize);
            RichEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,
                isSmall ? MarkdownRtbHelper.BaseSize : MarkdownRtbHelper.SmallSize);
            UpdateToolbarState();
        }

        private void ToolbarBullet_Click(object sender, RoutedEventArgs e)
        {
            MarkdownRtbHelper.ApplyList(RichEditor, TextMarkerStyle.Disc);
            UpdateToolbarState();
        }

        private void ToolbarNumbered_Click(object sender, RoutedEventArgs e)
        {
            MarkdownRtbHelper.ApplyList(RichEditor, TextMarkerStyle.Decimal);
            UpdateToolbarState();
        }

        private async void ToolbarImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null || _vm == null) return;

            var dlg = new OpenFileDialog
            {
                Filter      = "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|All Files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            foreach (var filePath in dlg.FileNames)
            {
                var bytes = await File.ReadAllBytesAsync(filePath);
                var name  = $"{Guid.NewGuid():N}{Path.GetExtension(filePath)}";

                _currentNote.Attachments[name] = bytes;
                if (!_currentNote.AttachmentNames.Contains(name))
                    _currentNote.AttachmentNames.Add(name);

                var sep = _currentNote.Body.Length > 0 && !_currentNote.Body.EndsWith('\n') ? "\n" : "";
                _currentNote.Body += $"{sep}![image]({name})\n";
            }

            RefreshImagePanel();
            await SaveNoteAsync();
        }
    }
}
