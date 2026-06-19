using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using SecureNotesWin.Localization;
using SecureNotesWin.Models;
using SecureNotesWin.ViewModels;

namespace SecureNotesWin.Views.Dialogs
{
    public partial class SettingsDialog : Window
    {
        private readonly MainViewModel _vm;

        // Add this property to the SettingsDialog class to expose the Repository via MainViewModel

        // Readable labels for settings
        private static string AutoLockLabel(int seconds)
        {
            var m = seconds / 60;
            var s = seconds % 60;
            if (m > 0 && s > 0) return $"{m}m {s}s";
            if (m > 0) return $"{m} minute{(m == 1 ? "" : "s")}";
            return $"{s} seconds";
        }

        private static string ThemeLabel(string mode) => mode switch
        {
            "LIGHT" => "Light Mode",
            "DARK" => "Dark Mode",
            "PAPER_WHITE" => "Paper White",
            _ => "Ink Black"
        };

        private static string LanguageLabel(string lang) => lang switch
        {
            "SPANISH" => "Español",
            "FRENCH" => "Français",
            "GERMAN" => "Deutsch",
            "ITALIAN" => "Italiano",
            "PORTUGUESE" => "Português",
            "RUSSIAN" => "Русский",
            "BENGALI" => "বাংলা",
            "HINDI" => "हिन्दी",
            "CHINESE_MANDARIN" => "中文 (简体)",
            _ => "English"
        };

        public SettingsDialog(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            RefreshSubtitles();
        }

        private void RefreshSubtitles()
        {
            ThemeSubtitle.Text = ThemeLabel(_vm.ThemeMode);
            LanguageSubtitle.Text = LanguageLabel(_vm.Language);
            DateFormatSubtitle.Text = _vm.DateFormat;
            TimeFormatSubtitle.Text = _vm.TimeFormat;
            AutoLockSubtitle.Text = AutoLockLabel(_vm.AutoLockSeconds);
        }

        // ── Theme ──

        private void ThemeRow_Click(object sender, RoutedEventArgs e)
        {
            var options = new[] { "INKBLACK", "DARK", "LIGHT", "PAPER_WHITE" };
            var labels = new[] { "Ink Black", "Dark Mode", "Light Mode", "Paper White" };
            var dlg = new OptionPickerDialog(LocalizationManager.Instance.GetString("select_theme"), labels, Array.IndexOf(options, _vm.ThemeMode))
            { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _vm.SetThemeMode(options[dlg.SelectedIndex]);
            ThemeSubtitle.Text = ThemeLabel(_vm.ThemeMode);
        }

        // ── Language ──

        private void LanguageRow_Click(object sender, RoutedEventArgs e)
        {
            var codes = new[] { "ENGLISH", "SPANISH", "FRENCH", "GERMAN", "ITALIAN", "PORTUGUESE", "RUSSIAN", "BENGALI", "HINDI", "CHINESE_MANDARIN" };
            var labels = codes.Select(LanguageLabel).ToArray();
            var dlg = new OptionPickerDialog(LocalizationManager.Instance.GetString("select_language"), labels, Array.IndexOf(codes, _vm.Language))
            { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _vm.SetLanguage(codes[dlg.SelectedIndex]);
            LanguageSubtitle.Text = LanguageLabel(_vm.Language);
        }

        // ── Date format ──

        private void DateFormatRow_Click(object sender, RoutedEventArgs e)
        {
            var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd.MM.yyyy", "MMM d, yyyy", "d MMM yyyy" };
            var dlg = new OptionPickerDialog(LocalizationManager.Instance.GetString("select_date_format"), formats, Array.IndexOf(formats, _vm.DateFormat))
            { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _vm.SetDateFormat(formats[dlg.SelectedIndex]);
            DateFormatSubtitle.Text = _vm.DateFormat;
        }

        // ── Time format ──

        private void TimeFormatRow_Click(object sender, RoutedEventArgs e)
        {
            var formats = new[] { "HH:mm", "HH:mm:ss", "h:mm a", "h:mm:ss a" };
            var dlg = new OptionPickerDialog(LocalizationManager.Instance.GetString("select_time_format"), formats, Array.IndexOf(formats, _vm.TimeFormat))
            { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _vm.SetTimeFormat(formats[dlg.SelectedIndex]);
            TimeFormatSubtitle.Text = _vm.TimeFormat;
        }

        // ── Auto-lock ──

        private void AutoLockRow_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AutoLockDialog(_vm.AutoLockSeconds) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _vm.SetAutoLockTimeout(dlg.TotalSeconds);
            AutoLockSubtitle.Text = AutoLockLabel(_vm.AutoLockSeconds);
        }

        // ── Change password ──

        private async void ChangePasswordRow_Click(object sender, RoutedEventArgs e)
            => await DoChangePassword();

        private async void BtnChangePassword_Click(object sender, RoutedEventArgs e)
            => await DoChangePassword();

        private async Task DoChangePassword()
        {
            var dlg = new ChangePasswordDialog { Owner = this };
            if (dlg.ShowDialog() != true) return;

            var oldPwd = _vm.Settings.GetVaultPassword();
            if (oldPwd != dlg.OldPassword)
            {
                MessageBox.Show("Current password is incorrect.", "Change Password",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ok = await _vm.Repository.SaveDatabaseWithNewPasswordAsync(_vm.KdbxPath, dlg.NewPassword);
            if (ok)
            {
                _vm.Settings.SaveVaultPassword(dlg.NewPassword);
                MessageBox.Show("Password changed successfully.", "Change Password",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to change password.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Backup & Restore ──

        private void BackupToZipRow_Click(object sender, RoutedEventArgs e)
            => _ = DoBackupToZipAsync();

        private void RestoreFromZipRow_Click(object sender, RoutedEventArgs e)
            => _ = DoRestoreFromZipAsync();

        private async Task DoBackupToZipAsync()
        {
            var bookDlg = new BookSelectorDialog(_vm.Notebooks.ToList(),
                LocalizationManager.Instance.GetString("backup_book_selector_title"),
                showAllOption: true)
            { Owner = this };
            string? notebookId = null;
            if (bookDlg.ShowDialog() == true) notebookId = bookDlg.SelectedNotebookId;

            var dlg = new SaveFileDialog
            {
                Filter   = "ZIP archive (*.zip)|*.zip",
                FileName = $"SecureNotes_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };
            if (dlg.ShowDialog() != true) return;

            await Task.Run(() => ExportToMarkdownZip(dlg.FileName, notebookId));
            MessageBox.Show(
                LocalizationManager.Instance.GetString("backup_success"),
                LocalizationManager.Instance.GetString("backup_to_zip"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task DoRestoreFromZipAsync()
        {
            var dlg = new OpenFileDialog { Filter = "ZIP archive (*.zip)|*.zip|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;

            var bookDlg = new BookSelectorDialog(_vm.Notebooks.ToList(),
                LocalizationManager.Instance.GetString("restore_book_selector_title"))
            { Owner = this };
            string? notebookId = null;
            if (bookDlg.ShowDialog() == true) notebookId = bookDlg.SelectedNotebookId;

            int count = await Task.Run(() => ImportFromMarkdownZip(dlg.FileName, notebookId));
            _vm.RefreshCollections();
            MessageBox.Show(
                string.Format(LocalizationManager.Instance.GetString("restore_success"), count),
                LocalizationManager.Instance.GetString("restore_from_zip"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Import ──

        private void ImportJourneyRow_Click(object sender, RoutedEventArgs e)
            => _ = ImportJourneyOrDayOneAsync(isDayOne: false);

        private void ImportDayOneRow_Click(object sender, RoutedEventArgs e)
            => _ = ImportJourneyOrDayOneAsync(isDayOne: true);

        private void ImportJoplinHtmlRow_Click(object sender, RoutedEventArgs e)
            => _ = ImportJoplinHtmlAsync();

        private async Task ImportJoplinHtmlAsync()
        {
            var label = LocalizationManager.Instance.GetString("import_joplin_html");

            // Joplin HTML export is a *directory*.
            // Use the Windows Shell IFileOpenDialog (COM) to pick a folder —
            // no System.Windows.Forms dependency needed.
            var folder = PickFolder(label);
            if (folder == null) return;

            var bookDlg = new BookSelectorDialog(_vm.Notebooks.ToList(),
                string.Format(LocalizationManager.Instance.GetString("import_book_selector_title"), label))
            { Owner = this };
            string? notebookId = null;
            if (bookDlg.ShowDialog() == true) notebookId = bookDlg.SelectedNotebookId;

            int count = await Task.Run(() => ImportFromJoplinHtmlDirectory(folder, notebookId));
            _vm.RefreshCollections();
            MessageBox.Show(
                string.Format(LocalizationManager.Instance.GetString("import_success"), count, label),
                label, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Imports a Joplin "HTML - HTML Document (Directory)" export.
        ///
        /// Joplin's export layout:
        ///   &lt;ExportDir&gt;/
        ///     NoteTitle.html          – one file per note
        ///     NoteTitle.html          – …
        ///     resources/
        ///       image1.png            – embedded attachments referenced from HTML
        ///       …
        ///
        /// Each HTML file may contain a Joplin metadata block at the bottom:
        ///   &lt;!-- joplin-source:
        ///   id: &lt;guid&gt;
        ///   parent_id: &lt;guid&gt;
        ///   created_time: &lt;ISO8601&gt;
        ///   updated_time: &lt;ISO8601&gt;
        ///   is_todo: 0|1
        ///   todo_due: …
        ///   todo_completed: …
        ///   source: joplin
        ///   tags: tag1, tag2
        ///   --&gt;
        ///
        /// Images are referenced as &lt;img src="resources/filename.ext"&gt; inside the HTML.
        /// We embed them as KDBX binary attachments, identical to how Journey/DayOne photos
        /// are handled, and rewrite Markdown image references accordingly.
        /// </summary>
        /// <summary>
        /// Imports a Joplin "HTML - HTML Document (Directory)" export.
        ///
        /// Actual export layout observed:
        ///   &lt;ExportDir&gt;/
        ///     _resources/                     ← shared image pool (one level up from notebooks)
        ///       abc123.png
        ///     NotebookName/                   ← one subfolder per notebook
        ///       Note Title.html               ← base file (may lack images)
        ///       Note Title (1).html           ← "(1)" copy = the full version with images
        ///       pluginAssets/                 ← Joplin CSS assets, ignored
        ///
        /// Inside each HTML the rendered content lives in:
        ///   &lt;div id="rendered-md"&gt;
        ///     &lt;hr&gt;
        ///     &lt;h2&gt;title: …&lt;br&gt;updated: …Z&lt;br&gt;created: …Z&lt;/h2&gt;  ← metadata block
        ///     &lt;h1&gt;Note Title&lt;/h1&gt;                                           ← display title
        ///     &lt;p&gt;Tags: tag1, tag2&lt;/p&gt;                                        ← optional tags line
        ///     … body …
        ///     &lt;img src="../_resources/abc123.png" alt="…"&gt;               ← images
        ///   &lt;/div&gt;
        ///
        /// Strategy:
        ///   • Prefer "(1)" files over base files (they are the complete versions).
        ///   • Notebook name = subfolder name (underscores → spaces, trailing _ stripped).
        ///   • Title = text inside &lt;h1&gt; in #rendered-md.
        ///   • Timestamps parsed from the &lt;h2&gt; metadata block.
        ///   • Tags parsed from a leading &lt;p&gt;Tags: …&lt;/p&gt; in the body.
        ///   • Images referenced as "../_resources/filename" are loaded from
        ///     the _resources folder and stored as KDBX binary attachments.
        /// </summary>
        private int ImportFromJoplinHtmlDirectory(string exportDir, string? targetNotebookId)
        {
            var importedNotes = new List<Note>();
            var noteTagTitles = new Dictionary<string, List<string>>();

            // ------------------------------------------------------------------
            // 1. Pre-load every image from the shared _resources folder.
            //    The folder lives at the top level of the export directory.
            //    HTML files reference them as "../_resources/filename".
            // ------------------------------------------------------------------
            var resourcesDir = Path.Combine(exportDir, "_resources");
            var imageLookup  = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(resourcesDir))
            {
                foreach (var file in Directory.EnumerateFiles(resourcesDir))
                {
                    var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                    if (ext is "jpg" or "jpeg" or "png" or "gif" or "webp" or "bmp")
                        imageLookup[Path.GetFileName(file)] = File.ReadAllBytes(file);
                }
            }

            // ------------------------------------------------------------------
            // 2. Discover notebook subfolders.
            //    Each direct subdirectory (except _resources and pluginAssets)
            //    represents one Joplin notebook.
            // ------------------------------------------------------------------
            var notebookCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var notebookDirs = Directory.EnumerateDirectories(exportDir)
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return !string.Equals(name, "_resources",  StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(name, "pluginAssets", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            // If no notebook sub-folders exist, treat the root itself as the single notebook.
            if (!notebookDirs.Any())
                notebookDirs.Add(exportDir);

            // ------------------------------------------------------------------
            // 3. For each notebook folder build a deduplicated set of HTML files.
            //    Joplin sometimes exports both "Note.html" and "Note (1).html".
            //    The "(N)" copy is the full version (includes images); prefer it.
            // ------------------------------------------------------------------
            foreach (var nbDir in notebookDirs)
            {
                var notebookFolderName = Path.GetFileName(nbDir);

                // Resolve or create the target notebook in the vault.
                string? nbId = targetNotebookId;
                if (nbId == null)
                {
                    // Pretty-print: "Egypt_Diary_" → "Egypt Diary"
                    var nbTitle = notebookFolderName
                        .Replace('_', ' ')
                        .Trim();

                    if (!notebookCache.TryGetValue(nbTitle, out nbId))
                    {
                        var existing = _vm.Repository.GetAllNotebooks()
                            .FirstOrDefault(n => n.Title.Equals(nbTitle, StringComparison.OrdinalIgnoreCase));
                        nbId = existing?.Id
                            ?? _vm.Repository.CreateNotebookAsync(nbTitle).GetAwaiter().GetResult().Id;
                        notebookCache[nbTitle] = nbId;
                    }
                }

                // Build a map: base-name (without " (N)" suffix) → best file path.
                // We keep the highest-numbered copy so images are included.
                var bestFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var htmlFile in Directory.EnumerateFiles(nbDir, "*.html"))
                {
                    var ext      = Path.GetExtension(htmlFile);             // ".html"
                    var nameNoExt = Path.GetFileNameWithoutExtension(htmlFile);

                    // Strip trailing " (N)" to get the canonical base name.
                    var baseName = Regex.Replace(nameNoExt, @"\s*\(\d+\)\s*$", "");

                    if (!bestFile.ContainsKey(baseName))
                    {
                        bestFile[baseName] = htmlFile;
                    }
                    else
                    {
                        // Prefer the " (N)" copy over the plain one.
                        var existing2 = Path.GetFileNameWithoutExtension(bestFile[baseName]);
                        bool existingIsNumbered = Regex.IsMatch(existing2, @"\s*\(\d+\)\s*$");
                        bool currentIsNumbered  = Regex.IsMatch(nameNoExt,  @"\s*\(\d+\)\s*$");
                        if (currentIsNumbered && !existingIsNumbered)
                            bestFile[baseName] = htmlFile;
                    }
                }

                // ── Process each deduplicated note file ─────────────────────────
                foreach (var htmlFile in bestFile.Values)
                {
                    var rawHtml = File.ReadAllText(htmlFile, Encoding.UTF8);

                    // Extract only the #rendered-md div content.
                    var renderedMatch = Regex.Match(rawHtml,
                        @"<div\s+id=""rendered-md"">(.*?)</div>\s*</div>",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    var renderedHtml = renderedMatch.Success
                        ? renderedMatch.Groups[1].Value
                        : rawHtml;   // fallback: use whole file

                    // ── Parse metadata from <h2> block ──────────────────────────
                    // Joplin renders the YAML front-matter as the first <h2>:
                    //   <h2>title: Apr 10, 2024 20:26<br>
                    //        updated: 2026-05-25 16:08:22Z<br>
                    //        created: 2024-04-10 20:26:55Z</h2>
                    var h2Match = Regex.Match(renderedHtml,
                        @"<h[12][^>]*>(.*?)</h[12]>",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    var metaRaw = h2Match.Success ? h2Match.Groups[1].Value : "";
                    var meta    = ParseJoplinInlineMetadata(metaRaw);

                    // Remove the leading <hr> and metadata <h2> from body.
                    var bodyHtml = renderedHtml;
                    bodyHtml = Regex.Replace(bodyHtml, @"^<hr\s*/?>", "", RegexOptions.IgnoreCase).Trim();
                    bodyHtml = Regex.Replace(bodyHtml,
                        @"<h[12][^>]*>.*?</h[12]>",
                        "", RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)).Trim();

                    // ── Title ───────────────────────────────────────────────────
                    // After stripping the metadata h2, the next <h1> is the note title.
                    var h1Match = Regex.Match(bodyHtml,
                        @"<h1[^>]*>(.*?)</h1>",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    var title = h1Match.Success
                        ? System.Net.WebUtility.HtmlDecode(
                            Regex.Replace(h1Match.Groups[1].Value, @"<[^>]+>", "").Trim())
                        : (meta.TryGetValue("title", out var mt) ? mt
                           : Path.GetFileNameWithoutExtension(htmlFile).Replace('_', ' ').Trim());

                    // Remove the title <h1> from the body so it is not duplicated.
                    if (h1Match.Success)
                        bodyHtml = bodyHtml.Remove(h1Match.Index, h1Match.Length).Trim();

                    // ── Timestamps ──────────────────────────────────────────────
                    long createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (meta.TryGetValue("created", out var cr)
                        && DateTimeOffset.TryParse(cr.Trim(),
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var cdt))
                        createdAt = cdt.ToUnixTimeMilliseconds();

                    long updatedAt = createdAt;
                    if (meta.TryGetValue("updated", out var up)
                        && DateTimeOffset.TryParse(up.Trim(),
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var udt))
                        updatedAt = udt.ToUnixTimeMilliseconds();

                    // ── Tags ────────────────────────────────────────────────────
                    // Joplin renders tags as the first <p> in the body:
                    //   <p>Tags: tag1, tag2</p>
                    List<string>? parsedTags = null;
                    var tagParaMatch = Regex.Match(bodyHtml,
                        @"<p[^>]*>\s*Tags:\s*([^<]+)</p>",
                        RegexOptions.IgnoreCase);
                    if (tagParaMatch.Success)
                    {
                        parsedTags = tagParaMatch.Groups[1].Value
                            .Split(',')
                            .Select(t => System.Net.WebUtility.HtmlDecode(t.Trim()))
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
                        // Strip the tags paragraph from the body.
                        bodyHtml = bodyHtml.Remove(tagParaMatch.Index, tagParaMatch.Length).Trim();
                    }

                    // ── Embed images ────────────────────────────────────────────
                    // Joplin HTML references images as: <img src="../_resources/filename.png">
                    var attachments = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

                    bodyHtml = Regex.Replace(bodyHtml,
                        @"<img\b([^>]*?)src=""\.\./_resources/([^""]+)""([^>]*)>",
                        match =>
                        {
                            var bareFile = Path.GetFileName(match.Groups[2].Value);
                            if (!imageLookup.TryGetValue(bareFile, out var bytes))
                                return match.Value;

                            var attachKey = $"{Guid.NewGuid():N}{Path.GetExtension(bareFile)}";
                            attachments[attachKey] = bytes;

                            var mime = Path.GetExtension(bareFile).TrimStart('.').ToLowerInvariant() switch
                            {
                                "jpg" or "jpeg" => "image/jpeg",
                                "gif"            => "image/gif",
                                "webp"           => "image/webp",
                                "bmp"            => "image/bmp",
                                _                => "image/png"
                            };
                            var b64 = Convert.ToBase64String(bytes);
                            return $"<img{match.Groups[1].Value}src=\"data:{mime};base64,{b64}\"{match.Groups[3].Value}>";
                        },
                        RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    // ── Convert HTML body → Markdown ────────────────────────────
                    var markdownBody = HtmlToMarkdown(bodyHtml);

                    // Rewrite inline data-URIs back to attachment key refs so the
                    // note editor loads them from the KDBX binary store.
                    foreach (var kv in attachments)
                    {
                        var ext2 = Path.GetExtension(kv.Key).TrimStart('.').ToLowerInvariant();
                        var mime2 = ext2 switch
                        {
                            "jpg" or "jpeg" => "image/jpeg",
                            "gif"           => "image/gif",
                            "webp"          => "image/webp",
                            "bmp"           => "image/bmp",
                            _               => "image/png"
                        };
                        var b64Key = $"data:{mime2};base64,{Convert.ToBase64String(kv.Value)}";
                        markdownBody = markdownBody.Replace(b64Key, kv.Key);
                    }

                    // ── Build Note ──────────────────────────────────────────────
                    var note = new Note
                    {
                        Title      = title,
                        Body       = markdownBody.Trim(),
                        NotebookId = nbId,
                        CreatedAt  = createdAt,
                        UpdatedAt  = updatedAt,
                    };

                    foreach (var kv in attachments)
                    {
                        note.Attachments[kv.Key] = kv.Value;
                        note.AttachmentNames.Add(kv.Key);
                    }

                    if (parsedTags is { Count: > 0 })
                        noteTagTitles[note.Id] = parsedTags;

                    importedNotes.Add(note);
                }
            }

            // ------------------------------------------------------------------
            // 4. Single KDBX write (notes + tags together).
            // ------------------------------------------------------------------
            _vm.Repository.BulkImportAsync(importedNotes, noteTagTitles: noteTagTitles)
                .GetAwaiter().GetResult();

            return importedNotes.Count;
        }

        /// <summary>
        /// Parses the metadata block that Joplin renders as the first &lt;h2&gt; in exported HTML.
        /// The raw inner HTML looks like:
        ///   "title: Apr 10, 2024 20:26&lt;br&gt;\nupdated: 2026-05-25 16:08:22Z&lt;br&gt;\ncreated: 2024-04-10 20:26:55Z"
        /// Returns a case-insensitive dictionary with keys "title", "updated", "created".
        /// </summary>
        private static Dictionary<string, string> ParseJoplinInlineMetadata(string h2InnerHtml)
        {
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Normalise: replace <br> variants and HTML entities, then split by newline.
            var text = Regex.Replace(h2InnerHtml, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", "");   // strip any remaining tags
            text = System.Net.WebUtility.HtmlDecode(text);

            foreach (var line in text.Split('\n'))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    meta[parts[0].Trim()] = parts[1].Trim();
            }
            return meta;
        }


        private async Task ImportJourneyOrDayOneAsync(bool isDayOne)
        {
            var label = isDayOne
                ? LocalizationManager.Instance.GetString("import_dayone")
                : LocalizationManager.Instance.GetString("import_journey");
            var dlg = new OpenFileDialog { Filter = "ZIP archive (*.zip)|*.zip", Title = label };
            if (dlg.ShowDialog() != true) return;

            var bookDlg = new BookSelectorDialog(_vm.Notebooks.ToList(),
                string.Format(LocalizationManager.Instance.GetString("import_book_selector_title"), label))
            { Owner = this };
            string? notebookId = null;
            if (bookDlg.ShowDialog() == true) notebookId = bookDlg.SelectedNotebookId;

            int count = await Task.Run(() => ImportFromJourneyOrDayOne(dlg.FileName, notebookId, isDayOne));
            _vm.RefreshCollections();
            MessageBox.Show(
                string.Format(LocalizationManager.Instance.GetString("import_success"), count, label),
                label, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private int ImportFromMarkdownZip(string zipPath, string? targetNotebookId)
        {
            int count = 0;
            var importedNotes = new List<Note>();
            // noteId → tag title strings parsed from front-matter
            var noteTagTitles = new Dictionary<string, List<string>>();
            using var zip = ZipFile.OpenRead(zipPath);

            // Build a lookup of every non-markdown file by bare filename so image
            // refs like ![alt](photo.jpg) can be resolved to bytes in one pass.
            var imageLookup = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var ze in zip.Entries)
            {
                if (string.IsNullOrEmpty(ze.Name)) continue;
                var ext = Path.GetExtension(ze.Name).TrimStart('.').ToLowerInvariant();
                if (ext is "jpg" or "jpeg" or "png" or "gif" or "webp" or "bmp")
                {
                    using var s = ze.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    imageLookup[ze.Name] = ms.ToArray();
                }
            }

            foreach (var entry in zip.Entries)
            {
                if (!entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                    !entry.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) continue;

                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                var content = reader.ReadToEnd();
                var (meta, rawBody) = ParseFrontMatter(content);
                var body = IsHtmlContent(rawBody) ? HtmlToMarkdown(rawBody) : rawBody;

                // Collect attachments: for every image ref in the body, store the
                // bytes as a KDBX binary and rewrite the ref to use the stored name.
                var attachments = new Dictionary<string, byte[]>();

                body = Regex.Replace(body, @"!\[([^\]]*)\]\(([^)]+)\)", match =>
                {
                    var alt = match.Groups[1].Value;
                    var src = match.Groups[2].Value;

                    if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        return match.Value;

                    if (src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return match.Value;

                    var bareFileName = Path.GetFileName(src);
                    if (!imageLookup.TryGetValue(bareFileName, out var bytes))
                        return match.Value;

                    var attachName = $"{Guid.NewGuid():N}{Path.GetExtension(bareFileName)}";
                    attachments[attachName] = bytes;

                    return $"![{alt}]({attachName})";
                });

                var title = meta.TryGetValue("title", out var t) ? t
                    : Path.GetFileNameWithoutExtension(entry.Name);
                var notebookName = Path.GetDirectoryName(entry.FullName)?.Replace('/', '\\') ?? "Imported";

                string? nbId = targetNotebookId;
                if (nbId == null && !string.IsNullOrEmpty(notebookName) && notebookName != "Unfiled")
                {
                    var existing = _vm.Repository.GetAllNotebooks()
                        .FirstOrDefault(n => n.Title.Equals(notebookName, StringComparison.OrdinalIgnoreCase));
                    nbId = existing?.Id
                        ?? _vm.Repository.CreateNotebookAsync(notebookName).GetAwaiter().GetResult().Id;
                }

                long createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (meta.TryGetValue("created", out var cr) && DateTimeOffset.TryParse(cr, out var cdt))
                    createdAt = cdt.ToUnixTimeMilliseconds();

                long updatedAt = createdAt;
                if (meta.TryGetValue("updated", out var up) && DateTimeOffset.TryParse(up, out var udt))
                    updatedAt = udt.ToUnixTimeMilliseconds();

                var note = new Note
                {
                    Title      = title,
                    Body       = body,
                    NotebookId = nbId,
                    CreatedAt  = createdAt,
                    UpdatedAt  = updatedAt,
                    IsDiary    = meta.TryGetValue("diary", out var d) && d == "true",
                    IsFavorite = meta.TryGetValue("favorite", out var f) && f == "true",
                };

                foreach (var kv in attachments)
                {
                    note.Attachments[kv.Key] = kv.Value;
                    note.AttachmentNames.Add(kv.Key);
                }

                // Parse tags from front-matter: "tags: Work, Personal, Ideas"
                if (meta.TryGetValue("tags", out var tagsRaw) && !string.IsNullOrWhiteSpace(tagsRaw))
                {
                    var tags = tagsRaw
                        .Split(',')
                        .Select(t2 => t2.Trim())
                        .Where(t2 => !string.IsNullOrEmpty(t2))
                        .ToList();
                    if (tags.Count > 0)
                        noteTagTitles[note.Id] = tags;
                }

                importedNotes.Add(note);
                count++;
            }

            // Single KDBX write for notes + tags together.
            _vm.Repository.BulkImportAsync(importedNotes, noteTagTitles: noteTagTitles)
                .GetAwaiter().GetResult();
            return count;
        }

        private int ImportFromJourneyOrDayOne(string zipPath, string? targetNotebookId, bool isDayOne)
        {
            int count = 0;
            var importedNotes = new List<Note>();
            using var zip = ZipFile.OpenRead(zipPath);

            // Build in-memory image lookup keyed by BOTH bare filename AND full
            // relative path (case-insensitive) so we match regardless of how the
            // photo filename is referenced in the JSON.
            var imageLookup = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var ze in zip.Entries)
            {
                if (string.IsNullOrEmpty(ze.Name)) continue;
                var ext = Path.GetExtension(ze.Name).TrimStart('.').ToLowerInvariant();
                if (ext is "jpg" or "jpeg" or "png" or "gif" or "webp" or "bmp")
                {
                    using var s = ze.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    var bytes = ms.ToArray();
                    // Index by bare name AND full path so any reference style matches.
                    imageLookup[ze.Name] = bytes;
                    if (ze.FullName != ze.Name)
                        imageLookup[ze.FullName] = bytes;
                }
            }

            // Read every JSON file into memory while the ZipArchive is open.
            // Journey exports ONE .json file per entry (a single object, not an array).
            // DayOne exports a single JSON with {"entries": [...]} wrapping all entries.
            var jsonFiles = new List<string>();
            foreach (var ze in zip.Entries)
            {
                if (string.IsNullOrEmpty(ze.Name)) continue;
                if (!ze.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                using var sr = new StreamReader(ze.Open(), Encoding.UTF8);
                jsonFiles.Add(sr.ReadToEnd());
            }

            // Find or create the target notebook.
            string? nbId = targetNotebookId;
            if (nbId == null)
            {
                var diaryNb = _vm.Repository.GetAllNotebooks()
                    .FirstOrDefault(n => n.Type == NotebookType.DIARY);
                nbId = diaryNb?.Id
                    ?? _vm.Repository.CreateNotebookAsync(
                        LocalizationManager.Instance.GetString("diary"), "📖", NotebookType.DIARY)
                        .GetAwaiter().GetResult().Id;
            }

            foreach (var json in jsonFiles)
            {
                try
                {
                    var root = JToken.Parse(json);

                    // Normalise to a flat list of entry tokens regardless of format:
                    //   Journey : each file is a bare JObject  → wrap in list
                    //   DayOne  : top-level has "entries" array → use that
                    //   Fallback: top-level is already a JArray  → use directly
                    IEnumerable<JToken> entries;
                    if (root is JObject rootObj)
                    {
                        var entriesArray = rootObj["entries"] as JArray;
                        entries = entriesArray != null
                            ? (IEnumerable<JToken>)entriesArray
                            : new[] { rootObj };
                    }
                    else if (root is JArray rootArray)
                    {
                        entries = rootArray;
                    }
                    else continue;

                    foreach (JObject entry in entries.OfType<JObject>())
                    {
                        // ── Body text ──────────────────────────────────────────────
                        var textRaw = entry["text"]?.ToString() ?? "";
                        var body = IsHtmlContent(textRaw) ? HtmlToMarkdown(textRaw) : textRaw;

                        // ── Photos ─────────────────────────────────────────────────
                        // Journey photo object: { "id": "abc123", ... }
                        //   → file in ZIP: photos/abc123.jpg  (or .jpeg/.png)
                        //   → NO "filename" field; must try id + common extensions.
                        //
                        // DayOne photo object: { "md5": "abc", "type": "jpeg", ... }
                        //   → file in ZIP: photos/<md5>.<type>
                        var attachments = new Dictionary<string, byte[]>();
                        if (entry["photos"] is JArray photos)
                        {
                            //foreach (JObject photo in photos.OfType<JObject>())
                            //{
                            //    byte[]? bytes = null;
                            //    string? sourceFileName = null;

                            //    if (isDayOne)
                            //    {
                            //        // DayOne: md5 + type gives the filename.
                            //        var md5 = photo["md5"]?.ToString();
                            //        var type = photo["type"]?.ToString() ?? "jpeg";
                            //        if (!string.IsNullOrEmpty(md5))
                            //        {
                            //            sourceFileName = $"{md5}.{type}";
                            //            imageLookup.TryGetValue(sourceFileName, out bytes);
                            //        }
                            //    }
                            //    else
                            //    {
                            //        // Journey: try "filename" first (some versions include it),
                            //        // then fall back to constructing from "id" + image extensions.
                            //        var explicitFilename = photo["filename"]?.ToString();
                            //        if (!string.IsNullOrEmpty(explicitFilename))
                            //        {
                            //            imageLookup.TryGetValue(explicitFilename, out bytes);
                            //            sourceFileName = explicitFilename;
                            //        }

                            //        if (bytes == null)
                            //        {
                            //            var photoId = photo["id"]?.ToString();
                            //            if (!string.IsNullOrEmpty(photoId))
                            //            {
                            //                // Try each common extension that Journey uses.
                            //                foreach (var ext in new[] { "jpg", "jpeg", "png", "gif", "webp" })
                            //                {
                            //                    var candidate = $"{photoId}.{ext}";
                            //                    if (imageLookup.TryGetValue(candidate, out bytes))
                            //                    {
                            //                        sourceFileName = candidate;
                            //                        break;
                            //                    }
                            //                }
                            //            }
                            //        }
                            //    }

                            //    if (bytes == null || string.IsNullOrEmpty(sourceFileName)) continue;

                            //    // Store as a KDBX attachment with a unique name.
                            //    var attachName = $"{Guid.NewGuid():N}{Path.GetExtension(sourceFileName)}";
                            //    attachments[attachName] = bytes;

                            //    // Append markdown image ref to the body.
                            //    var sep = body.Length > 0 && !body.EndsWith('\n') ? "\n" : "";
                            //    body += $"{sep}![image]({attachName})\n";
                            //}

                            // AFTER (fixed): handles BOTH formats
                            //   - string element:  "abc123.jpg"          (Journey flat export)
                            //   - object element:  { "id": "abc123", ... } (Journey older format)
                            foreach (var photoToken in photos)
                            {
                                byte[]? bytes = null;
                                string? sourceFileName = null;

                                if (!isDayOne)
                                {
                                    // Journey "flat" format: photo is just a filename string
                                    if (photoToken.Type == JTokenType.String)
                                    {
                                        sourceFileName = photoToken.ToString();
                                        imageLookup.TryGetValue(sourceFileName, out bytes);

                                        // Also try bare name in case the lookup is keyed differently
                                        if (bytes == null)
                                        {
                                            var bare = Path.GetFileName(sourceFileName);
                                            imageLookup.TryGetValue(bare, out bytes);
                                            if (bytes != null) sourceFileName = bare;
                                        }
                                    }
                                    else if (photoToken is JObject photo)
                                    {
                                        // Journey older object format: { "filename": "...", "id": "..." }
                                        var explicitFilename = photo["filename"]?.ToString();
                                        if (!string.IsNullOrEmpty(explicitFilename))
                                        {
                                            imageLookup.TryGetValue(explicitFilename, out bytes);
                                            sourceFileName = explicitFilename;
                                        }

                                        if (bytes == null)
                                        {
                                            var photoId = photo["id"]?.ToString();
                                            if (!string.IsNullOrEmpty(photoId))
                                            {
                                                foreach (var ext in new[] { "jpg", "jpeg", "png", "gif", "webp" })
                                                {
                                                    var candidate = $"{photoId}.{ext}";
                                                    if (imageLookup.TryGetValue(candidate, out bytes))
                                                    {
                                                        sourceFileName = candidate;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (photoToken is JObject dayOnePhoto)
                                {
                                    // DayOne: md5 + type
                                    var md5 = dayOnePhoto["md5"]?.ToString();
                                    var type = dayOnePhoto["type"]?.ToString() ?? "jpeg";
                                    if (!string.IsNullOrEmpty(md5))
                                    {
                                        sourceFileName = $"{md5}.{type}";
                                        imageLookup.TryGetValue(sourceFileName, out bytes);
                                    }
                                }

                                if (bytes == null || string.IsNullOrEmpty(sourceFileName)) continue;

                                var attachName = $"{Guid.NewGuid():N}{Path.GetExtension(sourceFileName)}";
                                attachments[attachName] = bytes;

                                var sep = body.Length > 0 && !body.EndsWith('\n') ? "\n" : "";
                                body += $"{sep}![image]({attachName})\n";
                            }

                        }

                        // ── Timestamp ──────────────────────────────────────────────
                        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (isDayOne)
                        {
                            if (DateTimeOffset.TryParse(entry["creationDate"]?.ToString(), out var d))
                                ts = d.ToUnixTimeMilliseconds();
                        }
                        else
                        {
                            // Journey date_journal is Unix ms.
                            if (long.TryParse(entry["date_journal"]?.ToString(), out var ms))
                                ts = ms;
                        }

                        // ── Title ──────────────────────────────────────────────────
                        // Use the explicit title field if present, otherwise format
                        // the timestamp (same behaviour as the Android app).
                        var title = entry["title"]?.ToString();
                        if (string.IsNullOrWhiteSpace(title))
                            title = _vm.FormatDateTime(ts);

                        // ── Save ───────────────────────────────────────────────────
                        // Build note in memory — collect for a single bulk write at the end.
                        var note = new Note
                        {
                            Title      = title,
                            Body       = body,
                            NotebookId = nbId,
                            IsDiary    = true,
                            CreatedAt  = ts,
                            UpdatedAt  = ts,
                        };

                        foreach (var kv in attachments)
                        {
                            note.Attachments[kv.Key] = kv.Value;
                            note.AttachmentNames.Add(kv.Key);
                        }

                        importedNotes.Add(note);
                        count++;
                    }
                }
                catch { /* skip malformed JSON files */ }
            }

            // Single KDBX write for the entire batch (avoids O(n) debounced saves).
            _vm.Repository.BulkImportAsync(importedNotes).GetAwaiter().GetResult();
            return count;
        }

        private void ExportToMarkdownZip(string outputPath, string? notebookId)
        {
            var notes = _vm.Repository.GetAllNotes();
            var notebooks = _vm.Repository.GetAllNotebooks().ToDictionary(n => n.Id, n => n.Title);
            if (notebookId != null) notes = notes.Where(n => n.NotebookId == notebookId).ToList();

            using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
            foreach (var note in notes)
            {
                var folder = note.NotebookId != null && notebooks.TryGetValue(note.NotebookId, out var nbName)
                    ? Sanitize(nbName) : "Unfiled";

                // ── Write attachment image files first ────────────────────────────
                var writtenAttachments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in note.Attachments)
                {
                    if (kv.Value == null || kv.Value.Length == 0) continue;
                    var attachEntryName = $"{folder}/{kv.Key}";
                    if (writtenAttachments.Contains(attachEntryName)) continue;
                    writtenAttachments.Add(attachEntryName);

                    var attachZipEntry = zip.CreateEntry(attachEntryName);
                    using var attachStream = attachZipEntry.Open();
                    attachStream.Write(kv.Value, 0, kv.Value.Length);
                }

                // ── Write the markdown file ───────────────────────────────────────
                var mdEntry = zip.CreateEntry($"{folder}/{Sanitize(note.Title)}.md");
                using var w = new StreamWriter(mdEntry.Open(), Encoding.UTF8);
                w.WriteLine("---");
                w.WriteLine($"title: {note.Title}");
                w.WriteLine($"created: {DateTimeOffset.FromUnixTimeMilliseconds(note.CreatedAt):O}");
                w.WriteLine($"updated: {DateTimeOffset.FromUnixTimeMilliseconds(note.UpdatedAt):O}");
                if (note.IsDiary) w.WriteLine("diary: true");
                if (note.IsFavorite) w.WriteLine("favorite: true");

                // ── Tags ──────────────────────────────────────────────────────────
                var noteTags = _vm.Repository.GetTagsForNote(note.Id);
                if (noteTags.Count > 0)
                    w.WriteLine($"tags: {string.Join(", ", noteTags.Select(t => t.Title))}");

                w.WriteLine("---");
                w.WriteLine();
                w.Write(note.Body);
            }
        }

        // ── Close ──
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Utilities ──
        private static (Dictionary<string, string> meta, string body) ParseFrontMatter(string content)
        {
            var meta = new Dictionary<string, string>();
            if (!content.StartsWith("---")) return (meta, content);
            var end = content.IndexOf("---", 3);
            if (end < 0) return (meta, content);
            foreach (var line in content.Substring(3, end - 3).Split('\n'))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2) meta[parts[0].Trim()] = parts[1].Trim();
            }
            return (meta, content.Substring(end + 3).TrimStart('\r', '\n'));
        }

        private static bool IsHtmlContent(string t)
            => t.Contains("<p") || t.Contains("<br") || t.Contains("<div") || t.Contains("<html");

        private static string HtmlToMarkdown(string html)
        {
            var md = html;
            // Normalize all line endings to \n first so ^ anchors in subsequent
            // patterns work regardless of the source platform.
            md = md.Replace("\r\n", "\n").Replace("\r", "\n");
            md = Regex.Replace(md, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            // Headings: strip any inner tags before emitting the # prefix so the
            // resulting line is clean markdown that LoadMarkdown can parse.
            md = Regex.Replace(md, @"<h([1-6])[^>]*>(.*?)</h\1>",
                m => new string('#', int.Parse(m.Groups[1].Value)) + " "
                     + Regex.Replace(m.Groups[2].Value, @"<[^>]+>", "").Trim() + "\n\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<p[^>]*>(.*?)</p>", m => m.Groups[1].Value.Trim() + "\n\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<li[^>]*>(.*?)</li>",
                m => "- " + Regex.Replace(m.Groups[1].Value, @"<[^>]+>", "").Trim() + "\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<(ul|ol)[^>]*>|</(ul|ol)>", "\n", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<strong[^>]*>(.*?)</strong>", "**$1**",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<em[^>]*>(.*?)</em>", "*$1*",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<[^>]+>", "", RegexOptions.IgnoreCase);
            md = md.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                   .Replace("&quot;", "\"").Replace("&#39;", "'");
            // Collapse 3+ consecutive blank lines to 2 for cleaner output.
            md = Regex.Replace(md, @"\n{3,}", "\n\n");
            return md.Trim();
        }

        private static string Sanitize(string s)
            => string.Join("_", s.Split(Path.GetInvalidFileNameChars())).Trim('.');

        // ── Shell folder picker (IFileOpenDialog via COM) ──────────────────────
        // Picks a folder using the native Windows Vista+ Shell dialog.
        // No System.Windows.Forms dependency.
        //
        // GUIDs (from Windows SDK shobjidl_core.h):
        //   CLSID_FileOpenDialog = DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7
        //   IID_IFileOpenDialog  = 42F85136-DB7E-439C-85F1-E4075D135FC8  ← correct IID
        //   IID_IShellItem       = 43826D1E-E718-42EE-BC55-A1E261C37BFE

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]   // IID_IFileOpenDialog
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [System.Runtime.InteropServices.PreserveSig]
            int Show(IntPtr hwndParent);                                          // IModalWindow::Show
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IntPtr psi);
            void SetFolder(IntPtr psi);
            void GetFolder(out IntPtr ppsi);
            void GetCurrentSelection(out IntPtr ppsi);
            void SetFileName([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszName);
            void GetFileName([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IntPtr ppsi);
            void AddPlace(IntPtr psi, int fdap);
            void SetDefaultExtension([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]   // IID_IShellItem
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        /// <summary>
        /// Opens a native "Select Folder" dialog. Returns the chosen path or null on cancel.
        /// Uses Type.GetTypeFromCLSID + Activator.CreateInstance so the runtime performs
        /// QueryInterface for the correct IID automatically — avoids InvalidCastException.
        /// </summary>
        private string? PickFolder(string title)
        {
            const uint FOS_PICKFOLDERS     = 0x00000020;
            const uint FOS_FORCEFILESYSTEM = 0x00000040;
            const uint SIGDN_FILESYSPATH   = 0x80058000;

            // GetTypeFromCLSID wraps CoCreateInstance and asks for IUnknown.
            // Casting to IFileOpenDialog then triggers a QI for IID_IFileOpenDialog
            // (taken from the [Guid] attribute on the interface) — no manual IID needed.
            var clsid  = new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"); // CLSID_FileOpenDialog
            object? raw = null;
            try
            {
                var comType = System.Type.GetTypeFromCLSID(clsid, throwOnError: true)!;
                raw         = System.Activator.CreateInstance(comType)!;
                var dialog  = (IFileOpenDialog)raw;   // QI → IID 42F85136... from [Guid] attribute

                dialog.GetOptions(out uint opts);
                dialog.SetOptions(opts | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
                dialog.SetTitle(title);

                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (dialog.Show(hwnd) != 0) return null;   // 0 = S_OK; anything else = cancel/error

                dialog.GetResult(out IntPtr ppsi);
                var item = (IShellItem)System.Runtime.InteropServices.Marshal
                    .GetTypedObjectForIUnknown(ppsi, typeof(IShellItem));
                item.GetDisplayName(SIGDN_FILESYSPATH, out string path);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(item);
                return path;
            }
            catch { return null; }
            finally
            {
                if (raw != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(raw);
            }
        }
    }
}