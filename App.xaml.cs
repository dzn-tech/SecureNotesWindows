using System;
using System.IO;
using System.Windows;
using SecureNotesWin.Security;
using SecureNotesWin.Services;

namespace SecureNotesWin
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Clean up temp image folders left by previous runs.
            CleanOldTempImageFolders();

            var settings = new SettingsManager();

            // Initialize theme slot first (adds first MergedDictionary slot).
            AppThemeService.Initialize(settings.GetThemeMode());

            // Initialize locale slot second (adds second MergedDictionary slot).
            // Language strings in XAML use {DynamicResource Str_*} keys and
            // re-resolve immediately whenever AppLocalizationService.Apply() is called.
            AppLocalizationService.Initialize(settings.GetLanguage());
        }

        private static void CleanOldTempImageFolders()
        {
            try
            {
                var tempBase = Path.GetTempPath();
                foreach (var dir in Directory.GetDirectories(tempBase, "SecureNotesWin_*"))
                {
                    try { Directory.Delete(dir, recursive: true); }
                    catch { /* ignore — another instance may still be using it */ }
                }
            }
            catch { /* non-fatal */ }
        }
    }
}
