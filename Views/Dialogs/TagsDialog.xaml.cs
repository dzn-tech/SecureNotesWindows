using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SecureNotesWin.Localization;
using SecureNotesWin.Models;
using SecureNotesWin.ViewModels;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class TagsDialog : Window
    {
        private readonly Note?          _note;   // null in global mode
        private readonly MainViewModel  _vm;
        private string _selectedColor;

        private static readonly string[] TagColors =
        {
            "#FFB347", "#3D7BD4", "#4CAF50", "#E05252",
            "#9C6FD6", "#FF7043", "#26C6DA", "#EC407A"
        };

        // ── Constructors ──────────────────────────────────────────────────────────

        /// <summary>
        /// Note-specific mode: existing tags are shown with checkboxes so the user
        /// can attach/detach them from this note.
        /// </summary>
        public TagsDialog(Note note, MainViewModel vm) : this(vm)
        {
            _note = note;
            LoadTags();
        }

        /// <summary>
        /// Global mode (opened from the sidebar + button): no note context.
        /// Tags are listed without checkboxes; the user can add new tags or delete
        /// existing ones.  New tags appear in the sidebar immediately after closing.
        /// </summary>
        public TagsDialog(MainViewModel vm)
        {
            InitializeComponent();
            _vm            = vm;
            _note          = null;
            _selectedColor = TagColors[0];
            var lm = LocalizationManager.Instance;
            Title            = lm.GetString("manage_tags");
            HeaderLabel.Text = lm.GetString("manage_tags");
            BtnAddTag.Content = lm.GetString("add");

            BuildColorPalette();
            LoadTags();
        }

        // ── Color palette ─────────────────────────────────────────────────────────

        private void BuildColorPalette()
        {
            ColorPalette.Children.Clear();
            foreach (var hex in TagColors)
            {
                var swatch = new Border
                {
                    Width           = 22,
                    Height          = 22,
                    CornerRadius    = new CornerRadius(11),
                    Margin          = new Thickness(0, 0, 6, 0),
                    Cursor          = Cursors.Hand,
                    Background      = ParseBrush(hex),
                    Tag             = hex,
                    ToolTip         = hex,
                    BorderThickness = new Thickness(hex == _selectedColor ? 2.5 : 0),
                    BorderBrush     = Brushes.White
                };
                swatch.MouseLeftButtonUp += Swatch_Click;
                ColorPalette.Children.Add(swatch);
            }
        }

        private void Swatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not string hex) return;
            _selectedColor = hex;
            foreach (Border swatch in ColorPalette.Children)
                swatch.BorderThickness = new Thickness((string)swatch.Tag == _selectedColor ? 2.5 : 0);
        }

        // ── Tag list ──────────────────────────────────────────────────────────────

        private void LoadTags()
        {
            TagsPanel.Children.Clear();

            // In note-specific mode, show which tags are already attached
            var noteTags = _note != null
                ? _vm.Repository.GetTagsForNote(_note.Id).Select(t => t.Id).ToHashSet()
                : new HashSet<string>();

            foreach (var tag in _vm.Repository.GetAllTags())
            {
                var tagBrush = ParseBrush(tag.Color);

                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = System.Windows.GridLength.Auto });

                // Color dot + label
                var dot = new Ellipse
                {
                    Width  = 10, Height = 10,
                    Margin = new Thickness(0, 0, 8, 0),
                    Fill   = tagBrush
                };
                var label = new TextBlock
                {
                    Text              = tag.Title,
                    Foreground        = tagBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(dot);
                sp.Children.Add(label);

                UIElement leftElement;
                if (_note != null)
                {
                    // Note mode: checkbox
                    var cb = new CheckBox
                    {
                        IsChecked = noteTags.Contains(tag.Id),
                        Content   = sp,
                        Margin    = new Thickness(0, 2, 0, 2),
                        Tag       = tag
                    };
                    cb.Checked   += async (s, e) => await _vm.Repository.AddTagToNoteAsync(_note.Id, tag.Id);
                    cb.Unchecked += async (s, e) => await _vm.Repository.RemoveTagFromNoteAsync(_note.Id, tag.Id);
                    leftElement = cb;
                }
                else
                {
                    // Global mode: plain row, no checkbox
                    leftElement = sp;
                }

                var deleteBtn = new Button
                {
                    Content         = "✕",
                    Background      = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor          = Cursors.Hand,
                    Padding         = new Thickness(4, 0, 0, 0),
                    Foreground      = new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD)),
                    Tag             = tag
                };
                deleteBtn.Click += async (s, e) =>
                {
                    await _vm.Repository.DeleteTagAsync(tag);
                    LoadTags();
                };

                Grid.SetColumn(leftElement, 0);
                Grid.SetColumn(deleteBtn, 1);
                row.Children.Add(leftElement);
                row.Children.Add(deleteBtn);
                TagsPanel.Children.Add(row);
            }
        }

        // ── Add / Done ────────────────────────────────────────────────────────────

        private async void BtnAddTag_Click(object sender, RoutedEventArgs e)
        {
            var title = NewTagBox.Text.Trim();
            if (string.IsNullOrEmpty(title)) return;

            var tag = await _vm.Repository.CreateTagAsync(title, _selectedColor);

            // In note mode also attach the new tag to the current note
            if (_note != null)
                await _vm.Repository.AddTagToNoteAsync(_note.Id, tag.Id);

            NewTagBox.Clear();
            LoadTags();
        }

        private void NewTagBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnAddTag_Click(sender, new RoutedEventArgs());
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e) => Close();

        // ── Helper ────────────────────────────────────────────────────────────────

        private static SolidColorBrush ParseBrush(string colorStr)
        {
            try   { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorStr)); }
            catch { return new SolidColorBrush(Colors.Orange); }
        }
    }
}
