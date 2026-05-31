using System;
using System.Windows;

namespace SecureNotesWin.Services
{
    /// <summary>
    /// Manages live theme switching for the entire application.
    ///
    /// WHY THIS APPROACH:
    ///   WPF {StaticResource} bindings resolve once — when the element is first
    ///   loaded.  Simply swapping entries in Application.Resources or replacing
    ///   a MergedDictionary item has no effect on already-open windows.
    ///
    ///   The ONE mechanism WPF does propagate to live {StaticResource} bindings
    ///   is mutating the *contents* of a ResourceDictionary that is already in
    ///   the merged list.  When you call dict.MergedDictionaries.Clear() followed
    ///   by dict.MergedDictionaries.Add(newTheme) on the *same* ResourceDictionary
    ///   instance, WPF fires invalidation events that force every element that
    ///   resolved a key from that dictionary to re-resolve it — updating
    ///   immediately across all open windows.
    ///
    /// PATTERN:
    ///   We keep a single persistent "theme slot" ResourceDictionary in
    ///   Application.Resources.MergedDictionaries.  On every Apply() call we
    ///   clear its inner MergedDictionaries and add the new theme file into it.
    ///   The slot itself never leaves the merged list, so WPF tracks it.
    /// </summary>
    public static class AppThemeService
    {
        // The persistent container that lives in Application.Resources.
        // Its inner MergedDictionaries holds whichever theme is currently active.
        private static ResourceDictionary? _themeSlot;

        private static readonly string[] ThemeUris =
        {
            "Themes/InkBlackTheme.xaml",
            "Themes/DarkTheme.xaml",
            "Themes/LightTheme.xaml",
            "Themes/PaperWhiteTheme.xaml",
        };

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Initialise the theme slot and apply the given mode.
        /// Must be called once from App.OnStartup AFTER the Application object
        /// and its ResourceDictionary exist (which they do by the time
        /// OnStartup fires).
        /// </summary>
        public static void Initialize(string mode)
        {
            var app = Application.Current;

            // Create the slot and add it to the application merged list once.
            _themeSlot = new ResourceDictionary();
            app.Resources.MergedDictionaries.Add(_themeSlot);

            Apply(mode);
        }

        /// <summary>
        /// Switches to <paramref name="mode"/> immediately on all open windows.
        /// Safe to call from any thread that has access to the dispatcher.
        /// </summary>
        public static void Apply(string mode)
        {
            if (_themeSlot == null)
            {
                // Called before Initialize (e.g. from a test or early startup path).
                // Fall back to Initialize so the slot is created.
                Initialize(mode);
                return;
            }

            var uri = ModeToUri(mode);

            // Swap the inner dictionary.  WPF propagates the change to every
            // element that has a {StaticResource} or {DynamicResource} key
            // sourced from this slot — including already-open windows.
            _themeSlot.MergedDictionaries.Clear();
            _themeSlot.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        private static Uri ModeToUri(string mode) => mode?.ToUpperInvariant() switch
        {
            "DARK"        => new Uri("Themes/DarkTheme.xaml",       UriKind.Relative),
            "LIGHT"       => new Uri("Themes/LightTheme.xaml",      UriKind.Relative),
            "PAPER_WHITE" => new Uri("Themes/PaperWhiteTheme.xaml", UriKind.Relative),
            _             => new Uri("Themes/InkBlackTheme.xaml",   UriKind.Relative),
        };
    }
}
