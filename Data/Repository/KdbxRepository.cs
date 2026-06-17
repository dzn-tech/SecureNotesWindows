using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Newtonsoft.Json;
using SecureNotesWin.Helpers;
using SecureNotesWin.Models;

namespace SecureNotesWin.Data.Repository
{
    /// <summary>
    /// Mirrors the Android KdbxRepository exactly:
    /// - Notes stored as KeePass entries in a "SecureNotes" group
    /// - Metadata (notebooks, tags, note-tag relations) stored as entries in a "SecureMeta" group
    /// - All custom fields prefixed with sn_ to match the Android app
    /// - Compatible with .kdbx files created by the Android app
    /// </summary>
    public class KdbxRepository
    {
        // Group names - must match Android app exactly
        private const string NOTES_GROUP = "Secure Notes";
        private const string META_GROUP = "SecureMeta";

        // Custom field keys - must match Android app exactly
        private const string F_ID = "sn_id";
        private const string F_NOTEBOOK_ID = "sn_notebookId";
        private const string F_CREATED = "sn_createdAt";
        private const string F_UPDATED = "sn_updatedAt";
        private const string F_IS_DIARY = "sn_isDiary";
        private const string F_IS_FAVORITE = "sn_favorite";
        private const string F_IS_TODO = "sn_isTodo";
        private const string F_IS_DONE = "sn_todoDone";
        private const string F_TAGS = "sn_tags";
        private const string F_NOTEBOOKS = "sn_notebooks";
        private const string F_TAGS_META = "sn_tagsMeta";

        private PwDatabase? _db;
        private string? _dbPath;
        private string _password = string.Empty;

        private List<Note> _notes = new();
        private List<Notebook> _notebooks = new();
        private List<Tag> _tags = new();
        private Dictionary<string, List<string>> _noteTags = new();

        // Debounce save
        private CancellationTokenSource? _saveCts;
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        public event EventHandler? DataChanged;

        public bool IsOpen => _db != null;
        public string? CurrentPath => _dbPath;

        // ──────────────────────────────────────────────────────
        // Open / Create / Close
        // ──────────────────────────────────────────────────────

        public async Task<bool> OpenDatabaseAsync(string filePath, string password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var ioConn = IOConnectionInfo.FromPath(filePath);
                    var key = new CompositeKey();
                    key.AddUserKey(new KcpPassword(password));

                    var db = new PwDatabase();
                    db.Open(ioConn, key, new NullStatusLogger());

                    _db = db;
                    _dbPath = filePath;
                    _password = password;
                    LoadFromDatabase();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OpenDatabase error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> CreateDatabaseAsync(string filePath, string password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var ioConn = IOConnectionInfo.FromPath(filePath);
                    var key = new CompositeKey();
                    key.AddUserKey(new KcpPassword(password));

                    var db = new PwDatabase();
                    db.New(ioConn, key);
                    db.Name = "SecureNotes";

                    // Create required groups
                    EnsureGroupStructure(db);

                    db.Save(new NullStatusLogger());

                    _db = db;
                    _dbPath = filePath;
                    _password = password;
                    _notes = new();
                    _notebooks = new();
                    _tags = new();
                    _noteTags = new();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CreateDatabase error: {ex.Message}");
                    return false;
                }
            });
        }

        public void CloseDatabase()
        {
            _db?.Close();
            _db = null;
            _dbPath = null;
            _password = string.Empty;
            _notes = new();
            _notebooks = new();
            _tags = new();
            _noteTags = new();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        // ──────────────────────────────────────────────────────
        // Queries (return copies to avoid threading issues)
        // ──────────────────────────────────────────────────────

        public IReadOnlyList<Note> GetAllNotes() => _notes.ToList();
        public IReadOnlyList<Notebook> GetAllNotebooks() => _notebooks.ToList();
        public IReadOnlyList<Tag> GetAllTags() => _tags.ToList();

        public Note? GetNoteById(string id) => _notes.FirstOrDefault(n => n.Id == id);

        public IReadOnlyList<Note> GetNotesByNotebook(string notebookId)
            => _notes.Where(n => n.NotebookId == notebookId).ToList();

        public IReadOnlyList<Note> SearchNotes(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return GetAllNotes();
            return _notes.Where(n =>
                n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                n.Body.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public IReadOnlyList<Tag> GetTagsForNote(string noteId)
        {
            var ids = _noteTags.GetValueOrDefault(noteId) ?? new List<string>();
            return _tags.Where(t => ids.Contains(t.Id)).ToList();
        }

        public IReadOnlyList<Note> GetNotesForTag(string tagId)
        {
            var noteIds = _noteTags.Where(kv => kv.Value.Contains(tagId)).Select(kv => kv.Key).ToHashSet();
            return _notes.Where(n => noteIds.Contains(n.Id)).ToList();
        }

        public List<string> GetNoteIdsForTag(string tagId)
            => _noteTags.Where(kv => kv.Value.Contains(tagId)).Select(kv => kv.Key).ToList();

        public int GetTotalNoteCount() => _notes.Count;

        // ──────────────────────────────────────────────────────
        // Note CRUD
        // ──────────────────────────────────────────────────────

        public async Task<Note> CreateNoteAsync(string title, string body, string? notebookId = null,
            bool isTodo = false, bool isDiary = false)
        {
            var note = new Note
            {
                Title = title, Body = body, NotebookId = notebookId,
                IsTodo = isTodo, IsDiary = isDiary
            };
            _notes.Add(note);
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
            return note;
        }

        public async Task<Note> UpdateNoteAsync(Note note)
        {
            note.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var idx = _notes.FindIndex(n => n.Id == note.Id);
            if (idx >= 0) _notes[idx] = note;
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
            return note;
        }

        public async Task DeleteNoteAsync(string id)
        {
            _notes.RemoveAll(n => n.Id == id);
            _noteTags.Remove(id);
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
        }

        // ──────────────────────────────────────────────────────
        // Notebook CRUD
        // ──────────────────────────────────────────────────────

        public async Task<Notebook> CreateNotebookAsync(string title, string icon = "📓",
            NotebookType type = NotebookType.NOTEBOOK)
        {
            if (_notebooks.Any(n => n.Title.Equals(title, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A book with this name already exists");

            var nb = new Notebook { Title = title, Icon = icon, Type = type };
            _notebooks.Add(nb);
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
            return nb;
        }

        public async Task<Notebook> UpdateNotebookAsync(Notebook notebook)
        {
            if (_notebooks.Any(n => n.Id != notebook.Id &&
                n.Title.Equals(notebook.Title, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A book with this name already exists");

            var idx = _notebooks.FindIndex(n => n.Id == notebook.Id);
            if (idx >= 0) _notebooks[idx] = notebook;
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
            return notebook;
        }

        public async Task DeleteNotebookAsync(Notebook notebook)
        {
            _notebooks.RemoveAll(n => n.Id == notebook.Id);
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
        }

        // ──────────────────────────────────────────────────────
        // Tag CRUD
        // ──────────────────────────────────────────────────────

        public async Task<Tag> CreateTagAsync(string title, string color = "#FFB347")
        {
            var tag = new Tag { Title = title, Color = color };
            _tags.Add(tag);
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
            return tag;
        }

        public async Task DeleteTagAsync(Tag tag)
        {
            _tags.RemoveAll(t => t.Id == tag.Id);
            foreach (var kv in _noteTags.Keys.ToList())
                _noteTags[kv] = _noteTags[kv].Where(id => id != tag.Id).ToList();
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
        }

        public async Task AddTagToNoteAsync(string noteId, string tagId)
        {
            if (!_noteTags.ContainsKey(noteId)) _noteTags[noteId] = new List<string>();
            if (!_noteTags[noteId].Contains(tagId))
            {
                _noteTags[noteId].Add(tagId);
                DataChanged?.Invoke(this, EventArgs.Empty);
                await ScheduleSaveAsync();
            }
        }

        public async Task RemoveTagFromNoteAsync(string noteId, string tagId)
        {
            if (_noteTags.TryGetValue(noteId, out var list))
            {
                list.Remove(tagId);
                DataChanged?.Invoke(this, EventArgs.Empty);
                await ScheduleSaveAsync();
            }
        }

        // ──────────────────────────────────────────────────────
        // Import helpers
        // ──────────────────────────────────────────────────────

        public async Task ImportNotesAsync(List<Note> notes)
        {
            _notes.AddRange(notes);
            DataChanged?.Invoke(this, EventArgs.Empty);
            await ScheduleSaveAsync();
        }

        /// <summary>
        /// Imports a batch of notes (and optionally their notebooks) in one shot,
        /// writing the KDBX database only once at the end instead of once per note.
        /// Use this for all bulk import operations to avoid O(n) database writes.
        /// </summary>
        public async Task BulkImportAsync(
            IEnumerable<Note> notes,
            IEnumerable<Notebook>? newNotebooks = null,
            Dictionary<string, List<string>>? noteTagTitles = null)
        {
            if (newNotebooks != null)
                _notebooks.AddRange(newNotebooks);

            _notes.AddRange(notes);

            // Wire up tags in-memory so they are persisted in the single save below.
            // noteTagTitles maps noteId -> list of tag title strings from front-matter.
            if (noteTagTitles != null && noteTagTitles.Count > 0)
            {
                foreach (var kvp in noteTagTitles)
                {
                    var noteId = kvp.Key;
                    foreach (var title in kvp.Value)
                    {
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        // Reuse existing tag or create a new one in _tags.
                        var tag = _tags.FirstOrDefault(t =>
                            t.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
                        if (tag == null)
                        {
                            tag = new Tag { Title = title };
                            _tags.Add(tag);
                        }

                        if (!_noteTags.ContainsKey(noteId))
                            _noteTags[noteId] = new List<string>();
                        if (!_noteTags[noteId].Contains(tag.Id))
                            _noteTags[noteId].Add(tag.Id);
                    }
                }
            }

            DataChanged?.Invoke(this, EventArgs.Empty);

            // Cancel any pending debounce and write once immediately.
            _saveCts?.Cancel();
            _saveCts = null;
            await SaveDatabaseAsync();
        }

        /// <summary>
        /// Deletes all notes with the given IDs in one shot,
        /// writing the KDBX database only once at the end instead of once per note.
        /// Use this for bulk-delete operations (e.g. deleting an entire notebook)
        /// to avoid O(n) database writes.
        /// </summary>
        public async Task BulkDeleteNotesAsync(IEnumerable<string> noteIds)
        {
            var idSet = noteIds.ToHashSet();
            _notes.RemoveAll(n => idSet.Contains(n.Id));
            foreach (var id in idSet)
                _noteTags.Remove(id);

            DataChanged?.Invoke(this, EventArgs.Empty);

            // Cancel any pending debounce and write once immediately.
            _saveCts?.Cancel();
            _saveCts = null;
            await SaveDatabaseAsync();
        }

        // ──────────────────────────────────────────────────────
        // KDBX persistence — mirrors Android KdbxRepository.saveDatabase() exactly
        // ──────────────────────────────────────────────────────

        private async Task ScheduleSaveAsync()
        {
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;
            try
            {
                await Task.Delay(1000, token); // 1-second debounce matching Android
                if (!token.IsCancellationRequested)
                    await SaveDatabaseAsync();
            }
            catch (TaskCanceledException) { }
        }

        public async Task<bool> SaveDatabaseWithNewPasswordAsync(string filePath, string newPassword)
        {
            if (_db == null) return false;
            return await Task.Run(() =>
            {
                try
                {
                    var newKey = new CompositeKey();
                    newKey.AddUserKey(new KcpPassword(newPassword));
                    _db.MasterKey = newKey;
                    _password = newPassword;
                    _db.Save(new NullStatusLogger());
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ChangePassword error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task SaveDatabaseAsync()
        {
            if (_db == null || _dbPath == null) return;
            await _saveLock.WaitAsync();
            try
            {
                await Task.Run(() => SaveDatabaseInternal());
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private void SaveDatabaseInternal()
        {
            if (_db == null || _dbPath == null) return;
            try
            {
                var root = _db.RootGroup;

                // ── Ensure group structure ──
                EnsureGroupStructure(_db);

                var notesGroup = FindGroup(root, NOTES_GROUP)!;
                var metaGroup = FindGroup(root, META_GROUP)!;

                // ── Clear existing entries ──
                notesGroup.Entries.Clear();
                metaGroup.Entries.Clear();

                // ── Write notes ──
                foreach (var note in _notes)
                    notesGroup.AddEntry(NoteToEntry(note), true);

                // ── Write metadata entries (same as Android's EntryBuilder(F_NOTEBOOKS).notes(...)) ──
                metaGroup.AddEntry(MakeMetaEntry(F_NOTEBOOKS,
                    JsonConvert.SerializeObject(_notebooks.Select(NotebookJson.From))), true);
                metaGroup.AddEntry(MakeMetaEntry(F_TAGS_META,
                    JsonConvert.SerializeObject(_tags.Select(TagJson.From))), true);
                metaGroup.AddEntry(MakeMetaEntry(F_TAGS,
                    JsonConvert.SerializeObject(_noteTags)), true);

                _db.Save(new NullStatusLogger());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveDatabase error: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────

        private void LoadFromDatabase()
        {
            if (_db == null) return;

            var root = _db.RootGroup;

            // Metadata
            var notebooksJson = GetMetaField(root, F_NOTEBOOKS) ?? "[]";
            var tagsJson = GetMetaField(root, F_TAGS_META) ?? "[]";
            var noteTagsJson = GetMetaField(root, F_TAGS) ?? "{}";

            try { _notebooks = JsonConvert.DeserializeObject<List<NotebookJson>>(notebooksJson)?.Select(x => x.ToNotebook()).ToList() ?? new(); }
            catch { _notebooks = new(); }

            try { _tags = JsonConvert.DeserializeObject<List<TagJson>>(tagsJson)?.Select(x => x.ToTag()).ToList() ?? new(); }
            catch { _tags = new(); }

            try { _noteTags = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(noteTagsJson) ?? new(); }
            catch { _noteTags = new(); }

            // Notes
            var notesGroup = FindGroup(root, NOTES_GROUP);
            _notes = new();
            if (notesGroup != null)
            {
                foreach (var entry in notesGroup.Entries)
                {
                    var note = EntryToNote(entry);
                    if (note != null) _notes.Add(note);
                }
            }
        }

        private Note? EntryToNote(PwEntry entry)
        {
            var id = GetCustomField(entry, F_ID) ?? entry.Strings.ReadSafe(PwDefs.UserNameField);
            if (string.IsNullOrEmpty(id)) return null;
            var title = entry.Strings.ReadSafe(PwDefs.TitleField);
            if (string.IsNullOrEmpty(title)) return null;

            var attachments = new Dictionary<string, byte[]>();
            foreach (var bin in entry.Binaries)
                attachments[bin.Key] = bin.Value.ReadData();

            var rawBody = NoteBodyHelper.NormalizeStorageBody(
                entry.Strings.ReadSafe(PwDefs.NotesField));

            return new Note
            {
                Id = id,
                Title = title,
                Body = rawBody,
                NotebookId = GetCustomField(entry, F_NOTEBOOK_ID)?.TakeIfNotEmpty(),
                CreatedAt = long.TryParse(GetCustomField(entry, F_CREATED), out var ca) ? ca : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UpdatedAt = long.TryParse(GetCustomField(entry, F_UPDATED), out var ua) ? ua : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsDiary = GetCustomField(entry, F_IS_DIARY) == "true",
                IsFavorite = GetCustomField(entry, F_IS_FAVORITE) == "true",
                IsTodo = GetCustomField(entry, F_IS_TODO) == "true",
                IsDone = GetCustomField(entry, F_IS_DONE) == "true",
                AttachmentNames = attachments.Keys.ToList(),
                Attachments = attachments
            };
        }

        private PwEntry NoteToEntry(Note note)
        {
            var entry = new PwEntry(true, true);
            var bodyForStorage = NoteBodyHelper.StripDataUrisFromBody(
                NoteBodyHelper.NormalizeStorageBody(note.Body));

            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, note.Title));
            entry.Strings.Set(PwDefs.NotesField, new ProtectedString(false, bodyForStorage));
            entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, note.Id));

            // Custom fields — mirrors Android Property(key, value, false)
            entry.Strings.Set(F_ID, new ProtectedString(false, note.Id));
            entry.Strings.Set(F_NOTEBOOK_ID, new ProtectedString(false, note.NotebookId ?? ""));
            entry.Strings.Set(F_CREATED, new ProtectedString(false, note.CreatedAt.ToString()));
            entry.Strings.Set(F_UPDATED, new ProtectedString(false, note.UpdatedAt.ToString()));
            entry.Strings.Set(F_IS_DIARY, new ProtectedString(false, note.IsDiary.ToString().ToLower()));
            entry.Strings.Set(F_IS_FAVORITE, new ProtectedString(false, note.IsFavorite.ToString().ToLower()));
            entry.Strings.Set(F_IS_TODO, new ProtectedString(false, note.IsTodo.ToString().ToLower()));
            entry.Strings.Set(F_IS_DONE, new ProtectedString(false, note.IsDone.ToString().ToLower()));

            foreach (var attachment in note.Attachments)
            {
                if (attachment.Value is { Length: > 0 })
                    entry.Binaries.Set(attachment.Key, new ProtectedBinary(false, attachment.Value));
            }

            return entry;
        }

        private PwEntry MakeMetaEntry(string title, string notesJson)
        {
            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));
            entry.Strings.Set(PwDefs.NotesField, new ProtectedString(false, notesJson));
            return entry;
        }

        private string? GetMetaField(PwGroup root, string key)
        {
            var metaGroup = FindGroup(root, META_GROUP);
            if (metaGroup == null) return null;
            var entry = metaGroup.Entries.FirstOrDefault(e => e.Strings.ReadSafe(PwDefs.TitleField) == key);
            return entry?.Strings.ReadSafe(PwDefs.NotesField);
        }

        private string? GetCustomField(PwEntry entry, string key)
        {
            var s = entry.Strings.Get(key);
            return s == null ? null : s.ReadString();
        }

        private PwGroup? FindGroup(PwGroup parent, string name)
        {
            if (parent.Name == name) return parent;
            foreach (var sub in parent.Groups)
            {
                var found = FindGroup(sub, name);
                if (found != null) return found;
            }
            return null;
        }

        private void EnsureGroupStructure(PwDatabase db)
        {
            var root = db.RootGroup;
            if (FindGroup(root, NOTES_GROUP) == null)
            {
                var g = new PwGroup(true, true, NOTES_GROUP, PwIcon.Folder);
                root.AddGroup(g, true);
            }
            if (FindGroup(root, META_GROUP) == null)
            {
                var g = new PwGroup(true, true, META_GROUP, PwIcon.Folder);
                root.AddGroup(g, true);
            }
        }
    }

    internal static class StringExtensions
    {
        public static string? TakeIfNotEmpty(this string? s)
            => string.IsNullOrEmpty(s) ? null : s;
    }
}
