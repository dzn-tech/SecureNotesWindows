using SecureNotesWin.Services;

// Add the following using directive if Settings is in a different namespace
// using <NamespaceWhereSettingsIsDefined>;

namespace SecureNotesWin.Services
{
    public static class Settings
    {
        public static void SetLanguage(string lang)
        {
            // Implement language setting logic here
        }
    }
}

public class MainViewModel
{
    // ... other members ...

    public string Language { get; set; } // Add this property to the MainViewModel class to fix CS0103

    // Add a reference to the Settings class if it is a static class in SecureNotesWin.Services
    // If Settings is not static or is in a different namespace, please provide more information.

    public void SetLanguage(string lang)
    {
        Language = lang;
        SecureNotesWin.Services.Settings.SetLanguage(lang); // Fully qualify Settings to fix CS0103
        AppLocalizationService.Apply(lang);   // ← live-switches all {DynamicResource Str_*} bindings
    }

    // ... other members ...
}
