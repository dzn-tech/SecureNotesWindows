using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SecureNotesWin.Data.Repository;
using SecureNotesWin.Models;
using SecureNotesWin.Security;
using SecureNotesWin.Services;

namespace SecureNotesWin.ViewModels
{
    public enum AppLockState { Locked, Unlocked, SetupRequired }
    public enum SortOrder { Updated, Created, Title, Alpha }

    public partial class MainViewModel : ObservableObject
    {
        public readonly KdbxRepository Repository;
        public readonly SettingsManager Settings;

        [ObservableProperty] private AppLockState _lockState = AppLockState.Locked;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string? _error;
        [ObservableProperty] private string _kdbxPath = string.Empty;
        [ObservableProperty] private int _autoLockSeconds = 300;
        [ObservableProperty] private string _themeMode = "INKBLACK";
        [ObservableProperty] private string _language = "ENGLISH";
        [ObservableProperty] private string _dateFormat = "MMM d, yyyy";
        [ObservableProperty] private string _timeFormat = "HH:mm";
        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string? _selectedNotebookId;
        [ObservableProperty] private string? _selectedTagId;
        [ObservableProperty] private bool _showDiariesOnly;
        [ObservableProperty] private bool _showAllNotes;
        [ObservableProperty] private SortOrder _sortOrder = SortOrder.Updated;

        public ObservableCollection<Note> Notes { get; } = new();
        public ObservableCollection<Notebook> Notebooks { get; } = new();
        public ObservableCollection<Tag> Tags { get; } = new();

        private Timer? _autoLockTimer;
        public event EventHandler? LockRequested;
        public event EventHandler? DataRefreshed;

        public MainViewModel()
        {
            Repository = new KdbxRepository();
            Settings = new SettingsManager();
            Repository.DataChanged += (_, _) => RefreshCollections();
            Initialize();
        }

        private void Initialize()
        {
            var path = Settings.GetKdbxPath();
            if (!string.IsNullOrEmpty(path) && !System.IO.File.Exists(path)) path = null;

            KdbxPath = path ?? string.Empty;
            AutoLockSeconds = Settings.GetAutoLockTimeoutSeconds();
            ThemeMode = Settings.GetThemeMode();
            Language = Settings.GetLanguage();
            DateFormat = Settings.GetDateFormat();
            TimeFormat = Settings.GetTimeFormat();

            if (Enum.TryParse<SortOrder>(Settings.GetSortOrder(), out var so)) SortOrder = so;
            LockState = Settings.IsSetup() ? AppLockState.Locked : AppLockState.SetupRequired;
        }

        // ── Auth ──
        public void Unlock(string? path = null)
        {
            if (path != null) { KdbxPath = path; Settings.SaveKdbxPath(path); }
            LockState = AppLockState.Unlocked;
            SelectedNotebookId = Settings.GetLastNotebookId();
            SelectedTagId = Settings.GetLastTagId();
            StartAutoLockTimer();
            RefreshCollections();
        }

        public void Lock()
        {
            Repository.CloseDatabase();
            LockState = AppLockState.Locked;
            StopAutoLockTimer();
            Notes.Clear(); Notebooks.Clear(); Tags.Clear();
        }

        public void SetupComplete(string path)
        {
            Settings.MarkSetup(true);
            KdbxPath = path;
            Settings.SaveKdbxPath(path);
            Unlock(path);
        }

        // ── Auto-lock ──
        public void ResetAutoLockTimer()
        {
            if (LockState != AppLockState.Unlocked) return;
            StopAutoLockTimer(); StartAutoLockTimer();
        }

        private void StartAutoLockTimer()
        {
            if (AutoLockSeconds <= 0) return;
            _autoLockTimer = new Timer(_ =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (LockState == AppLockState.Unlocked)
                        LockRequested?.Invoke(this, EventArgs.Empty);
                });
            }, null, TimeSpan.FromSeconds(AutoLockSeconds), Timeout.InfiniteTimeSpan);
        }

        private void StopAutoLockTimer()
        { _autoLockTimer?.Dispose(); _autoLockTimer = null; }

        // ── Collections ──
        public void RefreshCollections()
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                Notebooks.Clear();
                foreach (var nb in Repository.GetAllNotebooks()) Notebooks.Add(nb);
                Tags.Clear();
                foreach (var tag in Repository.GetAllTags()) Tags.Add(tag);
                RefreshNotes();
                DataRefreshed?.Invoke(this, EventArgs.Empty);
            });
        }

        public void RefreshNotes()
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                var allNotes = Repository.GetAllNotes();
                var diaryIds = Notebooks.Where(n => n.Type == NotebookType.DIARY).Select(n => n.Id).ToHashSet();

                IEnumerable<Note> filtered = allNotes;

                if (!ShowAllNotes)
                {
                    if (SelectedTagId != null)
                    {
                        var tagNoteIds = Repository.GetNoteIdsForTag(SelectedTagId).ToHashSet();
                        filtered = filtered.Where(n => tagNoteIds.Contains(n.Id));
                    }
                    else if (ShowDiariesOnly)
                        filtered = filtered.Where(n => n.IsDiary || (n.NotebookId != null && diaryIds.Contains(n.NotebookId)));
                    else if (SelectedNotebookId != null)
                        filtered = filtered.Where(n => n.NotebookId == SelectedNotebookId);
                    else
                        filtered = Enumerable.Empty<Note>();
                }

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                    filtered = filtered.Where(n =>
                        n.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        n.Body.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

                // ── Sorting ───────────────────────────────────────────────────
                // Diary notes: sort descending by the date embedded in the title
                //   (titles are set to FormatDateTime so they parse back cleanly).
                //   Fall back to CreatedAt when the title contains no recognisable date.
                // Notebook notes: sort descending by CreatedAt.
                var currentNb = Notebooks.FirstOrDefault(n => n.Id == SelectedNotebookId);
                bool isDiaryView = currentNb?.Type == NotebookType.DIARY
                    || filtered.Any(n => n.IsDiary);

                if (isDiaryView)
                {
                    filtered = filtered.OrderByDescending(n =>
                    {
                        // Try to parse a date out of the title first.
                        if (!string.IsNullOrWhiteSpace(n.Title) &&
                            DateTimeOffset.TryParse(n.Title, out var parsed))
                            return parsed.ToUnixTimeMilliseconds();
                        // Try common date-only substrings (e.g. "23 May 2026 14:30").
                        // FormatDateTime produces "MMM d, yyyy HH:mm" or similar —
                        // TryParse handles those without extra work.
                        return n.CreatedAt;
                    });
                }
                else
                {
                    filtered = filtered.OrderByDescending(n => n.CreatedAt);
                }

                Notes.Clear();
                foreach (var note in filtered) Notes.Add(note);
            });
        }

        partial void OnSearchQueryChanged(string value) => RefreshNotes();
        partial void OnSelectedNotebookIdChanged(string? value) { Settings.SaveLastNotebookId(value); RefreshNotes(); }
        partial void OnSelectedTagIdChanged(string? value) { Settings.SaveLastTagId(value); RefreshNotes(); }
        partial void OnShowDiariesOnlyChanged(bool value) => RefreshNotes();
        partial void OnShowAllNotesChanged(bool value) => RefreshNotes();
        partial void OnSortOrderChanged(SortOrder value) { Settings.SetSortOrder(value.ToString()); RefreshNotes(); }

        // ── Navigation ──
        public void SelectNotebook(string? id)
        {
            ShowDiariesOnly = false; ShowAllNotes = false;
            SelectedTagId = null; SelectedNotebookId = id;
        }

        public void SelectTag(string? id)
        {
            ShowDiariesOnly = false; ShowAllNotes = false;
            SelectedNotebookId = null; SelectedTagId = id;
        }

        public void ShowAll() { ShowAllNotes = true; ShowDiariesOnly = false; SelectedNotebookId = null; SelectedTagId = null; }

        // ── Settings ──
        public void SetAutoLockTimeout(int seconds)
        {
            AutoLockSeconds = seconds;
            Settings.SetAutoLockTimeoutSeconds(seconds);
            StopAutoLockTimer(); StartAutoLockTimer();
        }

        public void SetThemeMode(string mode)
        {
            ThemeMode = mode;
            Settings.SetThemeMode(mode);
            AppThemeService.Apply(mode);
        }
        public void SetLanguage(string lang)
        {
            Language = lang;
            Settings.SetLanguage(lang);
            AppLocalizationService.Apply(lang);   // ← live-switches all {DynamicResource Str_*} bindings
        }
        public void SetDateFormat(string fmt) { DateFormat = fmt;  Settings.SetDateFormat(fmt); }
        public void SetTimeFormat(string fmt) { TimeFormat = fmt;  Settings.SetTimeFormat(fmt); }

        // ── Format helpers (mirrors Android formatDateTime) ──
        public string FormatDateTime(long unixMs)
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            var datePart = DateFormat switch
            {
                "yyyy-MM-dd" => dt.ToString("yyyy-MM-dd"),
                "dd/MM/yyyy" => dt.ToString("dd/MM/yyyy"),
                "MM/dd/yyyy" => dt.ToString("MM/dd/yyyy"),
                "dd.MM.yyyy" => dt.ToString("dd.MM.yyyy"),
                "d MMM yyyy" => dt.ToString("d MMM yyyy"),
                _ => dt.ToString("MMM d, yyyy")
            };
            var timePart = TimeFormat switch
            {
                "HH:mm:ss" => dt.ToString("HH:mm:ss"),
                "h:mm a" => dt.ToString("h:mm tt"),
                "h:mm:ss a" => dt.ToString("h:mm:ss tt"),
                _ => dt.ToString("HH:mm")
            };
            return $"{datePart} {timePart}";
        }

        public string Languages
        {
            get => _language;
            set
            {
                _language = value;
                Settings.SetLanguage(value);
                OnPropertyChanged();
            }
        }
 

        public string FormatDiaryTitle() => FormatDateTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        public void ClearError() => Error = null;
    }
}
