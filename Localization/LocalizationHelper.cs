using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SecureNotesWin.Localization
{
    public enum AppLanguage
    {
        ENGLISH,
        SPANISH,
        FRENCH,
        GERMAN,
        ITALIAN,
        PORTUGUESE,
        RUSSIAN,
        BENGALI,
        HINDI,
        CHINESE_MANDARIN
    }

    public static class LocalizationHelper
    {
        private static AppLanguage _currentLanguage = AppLanguage.ENGLISH;

        public static event EventHandler? LanguageChanged;

        public static AppLanguage CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LanguageChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public static string GetLanguageCode(AppLanguage lang) => lang switch
        {
            AppLanguage.ENGLISH => "en",
            AppLanguage.SPANISH => "es",
            AppLanguage.FRENCH => "fr",
            AppLanguage.GERMAN => "de",
            AppLanguage.ITALIAN => "it",
            AppLanguage.PORTUGUESE => "pt",
            AppLanguage.RUSSIAN => "ru",
            AppLanguage.BENGALI => "bn",
            AppLanguage.HINDI => "hi",
            AppLanguage.CHINESE_MANDARIN => "zh",
            _ => "en"
        };

        public static string GetLanguageLabel(AppLanguage lang) => lang switch
        {
            AppLanguage.ENGLISH => "English",
            AppLanguage.SPANISH => "Español",
            AppLanguage.FRENCH => "Français",
            AppLanguage.GERMAN => "Deutsch",
            AppLanguage.ITALIAN => "Italiano",
            AppLanguage.PORTUGUESE => "Português",
            AppLanguage.RUSSIAN => "Русский",
            AppLanguage.BENGALI => "বাংলা",
            AppLanguage.HINDI => "हिन्दी",
            AppLanguage.CHINESE_MANDARIN => "中文 (简体)",
            _ => "English"
        };

        private static readonly Dictionary<AppLanguage, Dictionary<string, string>> _translations = new();

        static LocalizationHelper()
        {
            InitializeTranslations();
        }

        private static void InitializeTranslations()
        {
            // English
            _translations[AppLanguage.ENGLISH] = new Dictionary<string, string>
            {
                { "settings", "Settings" },
                { "appearance", "Appearance" },
                { "theme", "Theme" },
                { "follow_system", "Follow System" },
                { "light_mode", "Light Mode" },
                { "dark_mode", "Dark Mode" },
                { "security", "Security" },
                { "auto_lock", "Auto-Lock" },
                { "change_password", "Change Master Password" },
                { "update_vault_password", "Update your vault password" },
                { "import_export", "Import & Export" },
                { "import_zip", "Import from ZIP" },
                { "export_md", "Export as Markdown" },
                { "import_journey", "Import from Journey" },
                { "import_dayone", "Import from DayOne" },
                { "about", "About" },
                { "version", "Version" },
                { "language", "Language" },
                { "select_language", "Select Language" },
                { "cancel", "Cancel" },
                { "save", "Save" },
                { "done", "Done" },
                { "update", "Update" },
                { "close", "Close" },
                { "search_notes", "Search notes..." },
                { "no_notes", "No notes yet\nCreate a book then add your notes" },
                { "unlock_vault", "Unlock Vault" },
                { "master_password", "Master Password" },
                { "create_vault", "Create Vault" },
                { "secure_notes", "Secure Notes" },
                { "enter_password", "Enter your master password to unlock" },
                { "incorrect_password", "Incorrect password or wrong vault file" },
                { "vault_file", "Vault File" },
                { "browse", "Browse" },
                { "new_note", "New Note" },
                { "new_diary", "New Diary" },
                { "move_to_notebook", "Move to Notebook" },
                { "move_to_diary", "Move to Diary" },
                { "delete", "Delete" },
                { "add_tags", "Add Tags" },
                { "tags", "Tags" },
                { "new_tag", "New tag..." },
                { "lock_after", "Lock after" },
                { "minutes", "Minutes" },
                { "seconds", "Seconds" },
                { "date_format", "Date Format" },
                { "time_format", "Time Format" },
                { "general", "General" },
                { "saving", "Saving..." },
                { "saved", "Saved" },
                { "unlock", "Unlock" },
                { "create", "Create" },
                { "set_master_password", "Set Master Password" },
                { "confirm_password", "Confirm Password" },
                { "passwords_do_not_match", "Passwords do not match" },
                { "loading", "Loading..." },
                { "password_too_short", "Password must be at least 6 characters" },
                { "notes_count", "notes" },
                { "rename", "Rename" },
                { "lock_vault", "Lock Vault" },
                { "books", "BOOKS" },
                { "new_notebook", "New Notebook" },
                { "rename_book", "Rename Book" },
                { "notebook_name", "Notebook name" },
                { "diary_name", "Diary name" },
                { "type", "Type" },
                { "notebook", "Notebook" },
                { "diary", "Diary" },
                { "icon", "Icon" },
                { "name_empty_error", "Name cannot be empty" },
                { "new_tag_title", "New Tag" },
                { "tag_name", "Tag name" },
                { "color", "Color" },
                { "delete_book_title", "Delete {0}" },
                { "delete_book_msg", "Are you sure you want to delete {0}? This will also delete all notes within it." },
                { "yes_delete", "Yes, Delete" },
                { "no", "No" },
                { "delete_note_title", "Delete Note?" },
                { "delete_note_msg", "Are you sure you want to delete '{0}'?" },
                { "untitled", "Untitled" },
                { "ink_black", "Ink Black" },
                { "paper_white", "Paper White" },
                { "current_password", "Current Password" },
                { "new_password", "New Password" },
                { "confirm_new_password", "Confirm New Password" },
                { "password_updated_success", "Password updated successfully!" },
                { "export_success", "Exported successfully" },
                { "export_failed", "Export failed" },
                { "import_success", "Imported {0} notes" },
                { "import_failed", "Import failed: {0}" },
                { "open_vault", "Open Vault" },
                { "create_new_vault", "Create New Vault" },
                { "open_existing_vault", "Open Existing Vault" },
                { "choose_vault_access", "Choose how to access your encrypted notes" },
                { "password_weak", "Weak" },
                { "password_good", "Good" },
                { "password_strong", "Strong" },
                { "select_theme", "Select Theme" },
                { "auto_lock_timeout", "Auto-Lock Timeout" },
                { "select_date_format", "Select Date Format" },
                { "select_time_format", "Select Time Format" }
            };

            // Spanish
            _translations[AppLanguage.SPANISH] = new Dictionary<string, string>
            {
                { "settings", "Ajustes" },
                { "appearance", "Apariencia" },
                { "theme", "Tema" },
                { "follow_system", "Seguir sistema" },
                { "light_mode", "Modo claro" },
                { "dark_mode", "Modo oscuro" },
                { "security", "Seguridad" },
                { "auto_lock", "Bloqueo automático" },
                { "change_password", "Cambiar contraseña maestra" },
                { "import_export", "Importar y Exportar" },
                { "import_zip", "Importar desde ZIP" },
                { "export_md", "Exportar como Markdown" },
                { "about", "Acerca de" },
                { "version", "Versión" },
                { "language", "Idioma" },
                { "select_language", "Seleccionar idioma" },
                { "cancel", "Cancelar" },
                { "save", "Guardar" },
                { "done", "Listo" },
                { "close", "Cerrar" },
                { "search_notes", "Buscar notas..." },
                { "no_notes", "No hay notas aún\nCrea un libro y luego agrega tus notas" },
                { "unlock_vault", "Desbloquear Bóveda" },
                { "master_password", "Contraseña Maestra" },
                { "create_vault", "Crear Bóveda" },
                { "enter_password", "Ingrese su contraseña maestra para desbloquear" },
                { "new_note", "Nueva Nota" },
                { "new_diary", "Nuevo Diario" },
                { "delete", "Eliminar" },
                { "add_tags", "Añadir Etiquetas" },
                { "tags", "Etiquetas" },
                { "books", "LIBROS" },
                { "ink_black", "Negro Tinta" },
                { "paper_white", "Blanco Papel" }
            };

            // Add more language translations following the same pattern...
            // French, German, Italian, Portuguese, Russian, Bengali, Hindi, Chinese
        }

        public static string GetString(string key, params object[] args)
        {
            if (_translations.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var value))
            {
                return args.Length > 0 ? string.Format(value, args) : value;
            }

            // Fallback to English
            if (_translations[AppLanguage.ENGLISH].TryGetValue(key, out var fallback))
            {
                return args.Length > 0 ? string.Format(fallback, args) : fallback;
            }

            return key;
        }

        public static string GetThemeLabel(string themeMode) => themeMode?.ToUpperInvariant() switch
        {
            "LIGHT" => GetString("light_mode"),
            "DARK" => GetString("dark_mode"),
            "PAPER_WHITE" => GetString("paper_white"),
            _ => GetString("ink_black")
        };
    }
}