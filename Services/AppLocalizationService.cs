using System;
using System.Windows;
using SecureNotesWin.Localization;

namespace SecureNotesWin.Services
{
    /// <summary>
    /// Manages live language switching for the entire application.
    ///
    /// HOW IT WORKS:
    ///   All XAML text uses {loc:Loc key} which compiles to a OneWay Binding against
    ///   LocalizationManager.Instance with Path="Item[key]".
    ///
    ///   WPF re-evaluates an indexer binding when the source raises PropertyChanged
    ///   with the argument "Item[]" (square brackets are required — "Item" alone is
    ///   not enough for WPF's indexer binding machinery).
    ///
    ///   So the entire live-switch is: set LocalizationManager.CurrentLanguage, which
    ///   fires PropertyChanged("Item[]"), which forces every {loc:Loc *} binding on
    ///   every open window to call Item[key] again on the new language dictionary.
    ///   No ResourceDictionary swapping needed for text strings.
    /// </summary>
    public static class AppLocalizationService
    {
        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Must be called once from App.OnStartup to set the initial language.
        /// </summary>
        public static void Initialize(string language)
        {
            Apply(language);
        }

        /// <summary>
        /// Switches the UI language immediately on all open windows.
        /// Every {loc:Loc key} binding re-evaluates automatically.
        /// </summary>
        public static void Apply(string language)
        {
            if (!Enum.TryParse<AppLanguage>(language, ignoreCase: true, out var appLang))
                appLang = AppLanguage.ENGLISH;

            // Setting CurrentLanguage fires PropertyChanged("Item[]") inside the
            // setter, which WPF propagates to every active indexer binding.
            LocalizationManager.Instance.CurrentLanguage = appLang;
        }
    }
}
