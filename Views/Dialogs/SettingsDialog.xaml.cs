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

        // ── Import / Export ──

        private void ImportZipRow_Click(object sender, RoutedEventArgs e)
            => _ = DoImportZipAsync();

        private void ImportJourneyRow_Click(object sender, RoutedEventArgs e)
            => _ = ImportJourneyOrDayOneAsync(isDayOne: false);

        private void ImportDayOneRow_Click(object sender, RoutedEventArgs e)
            => _ = ImportJourneyOrDayOneAsync(isDayOne: true);

        private void ExportMarkdownRow_Click(object sender, RoutedEventArgs e)
            => _ = DoExportMarkdownAsync();

        private async void BtnImportZip_Click(object sender, RoutedEventArgs e)
            => await DoImportZipAsync();

        private async Task DoImportZipAsync()
        {
            var dlg = new OpenFileDialog { Filter = "ZIP archive (*.zip)|*.zip|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;

            // Book selector
            var bookDlg = new BookSelectorDialog(_vm.Notebooks.ToList(), "Import to which book?") { Owner = this };
            string? notebookId = null;
            if (bookDlg.ShowDialog() == true) notebookId = bookDlg.SelectedNotebookId;

            int count = await Task.Run(() => ImportFromMarkdownZip(dlg.FileName, notebookId));
            _vm.RefreshCollections();
            MessageBox.Show($"Imported {count} note(s).", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ImportJourneyOrDayOneAsync(bool isDayOne)
        {
            var label = isDayOne ? "DayOne" : "Journey";
            var dlg = new OpenFileDialog { Filter = "ZIP archive (*.zip)|*.zip", Title = $"Import from {label}" };
            if (dlg.ShowDialog() != true) return;

            var bookDlg = new BookSelectorDialog(_vm.Notebooks.ToList(), $"Import {label} to which book?") { Owner = this };
            string? notebookId = null;
            if (bookDlg.ShowDialog() == true) notebookId = bookDlg.SelectedNotebookId;

            int count = await Task.Run(() => ImportFromJourneyOrDayOne(dlg.FileName, notebookId, isDayOne));
            _vm.RefreshCollections();
            MessageBox.Show($"Imported {count} note(s) from {label}.", "Import",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnExportMarkdown_Click(object sender, RoutedEventArgs e)
            => await DoExportMarkdownAsync();

        private async Task DoExportMarkdownAsync()
        {
            var bookDlg = new BookSelectorDialog(_vm.Notebooks.ToList(), "Export which book?",
                showAllOption: true)
            { Owner = this };
            string? notebookId = null;
            bool all = true;
            if (bookDlg.ShowDialog() == true) { notebookId = bookDlg.SelectedNotebookId; all = notebookId == null; }

            var dlg = new SaveFileDialog
            {
                Filter = "ZIP archive (*.zip)|*.zip",
                FileName = $"SecureNotes_Export_{DateTime.Now:yyyyMMdd}.zip"
            };
            if (dlg.ShowDialog() != true) return;

            await Task.Run(() => ExportToMarkdownZip(dlg.FileName, notebookId));
            MessageBox.Show("Export complete.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Import logic (mirrors Android ImportExportService) ──

        private int ImportFromMarkdownZip(string zipPath, string? targetNotebookId)
        {
            int count = 0;
            var importedNotes = new List<Note>();
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

                    // Already a data-URI from a previous export – strip it; we can't
                    // recover the filename so skip silently.
                    if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        return match.Value;

                    // Remote URL – leave untouched.
                    if (src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return match.Value;

                    // Local filename reference – look it up in the ZIP image table.
                    var bareFileName = Path.GetFileName(src);
                    if (!imageLookup.TryGetValue(bareFileName, out var bytes))
                        return match.Value;   // image not found in ZIP, leave ref as-is

                    // Give the attachment a stable, unique name so multiple notes that
                    // reference the same source file don't collide in the KDBX.
                    var attachName = $"{Guid.NewGuid():N}{Path.GetExtension(bareFileName)}";
                    attachments[attachName] = bytes;

                    // Rewrite the markdown ref to point at the KDBX attachment name.
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

                // Build note in memory — collect for a single bulk write at the end.
                var note = new Note
                {
                    Title      = title,
                    Body       = body,
                    NotebookId = nbId,
                    CreatedAt  = createdAt,
                    UpdatedAt  = createdAt,
                };

                foreach (var kv in attachments)
                {
                    note.Attachments[kv.Key] = kv.Value;
                    note.AttachmentNames.Add(kv.Key);
                }

                importedNotes.Add(note);
                count++;
            }

            // Single KDBX write for the entire batch (avoids O(n) debounced saves).
            _vm.Repository.BulkImportAsync(importedNotes).GetAwaiter().GetResult();
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
                // Keep track of which attachment names we've already written so that
                // two notes sharing the same binary name don't collide in the ZIP.
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
                // The body already contains refs like ![alt](filename.jpg) which
                // point at the attachment files we just wrote — no rewriting needed.
                var mdEntry = zip.CreateEntry($"{folder}/{Sanitize(note.Title)}.md");
                using var w = new StreamWriter(mdEntry.Open(), Encoding.UTF8);
                w.WriteLine("---");
                w.WriteLine($"title: {note.Title}");
                w.WriteLine($"created: {DateTimeOffset.FromUnixTimeMilliseconds(note.CreatedAt):O}");
                w.WriteLine($"updated: {DateTimeOffset.FromUnixTimeMilliseconds(note.UpdatedAt):O}");
                if (note.IsDiary) w.WriteLine("diary: true");
                if (note.IsFavorite) w.WriteLine("favorite: true");
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
    }
}