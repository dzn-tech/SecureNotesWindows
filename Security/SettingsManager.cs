using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace SecureNotesWin.Security
{
    public class SettingsManager
    {
        private const string REG_KEY = @"Software\SecureNotesWin";

        // ── Registry key names ──
        private const string PREF_KDBX_PATH         = "kdbx_path";
        private const string PREF_IS_SETUP           = "is_setup";
        private const string PREF_AUTO_LOCK_SECONDS  = "auto_lock_seconds";
        private const string PREF_THEME_MODE         = "theme_mode";
        private const string PREF_LANGUAGE           = "language";
        private const string PREF_DATE_FORMAT        = "date_format";
        private const string PREF_TIME_FORMAT        = "time_format";
        private const string PREF_SORT_ORDER         = "sort_order";
        private const string PREF_LAST_NOTEBOOK_ID   = "last_notebook_id";
        private const string PREF_LAST_TAG_ID        = "last_tag_id";
        private const string PREF_VAULT_PWD_ENC      = "vault_pwd_enc";

        // ── Setup ──
        public bool IsSetup()
            => bool.TryParse(ReadReg(PREF_IS_SETUP), out var v) && v;
        public void MarkSetup(bool value = true)
            => WriteReg(PREF_IS_SETUP, value.ToString());

        // ── KDBX path ──
        public string? GetKdbxPath() => ReadReg(PREF_KDBX_PATH);
        public void SaveKdbxPath(string path) => WriteReg(PREF_KDBX_PATH, path);

        // ── Vault password (DPAPI-protected) ──
        public void SaveVaultPassword(string password)
        {
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
            WriteReg(PREF_VAULT_PWD_ENC, Convert.ToBase64String(encrypted));
        }
        public string? GetVaultPassword()
        {
            try
            {
                var b64 = ReadReg(PREF_VAULT_PWD_ENC);
                if (b64 == null) return null;
                var dec = ProtectedData.Unprotect(
                    Convert.FromBase64String(b64), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return null; }
        }

        // ── Auto-lock ──
        public int GetAutoLockTimeoutSeconds()
            => int.TryParse(ReadReg(PREF_AUTO_LOCK_SECONDS), out var v) ? v : 300;
        public void SetAutoLockTimeoutSeconds(int seconds)
            => WriteReg(PREF_AUTO_LOCK_SECONDS, seconds.ToString());

        // ── Theme ──
        public string GetThemeMode() => ReadReg(PREF_THEME_MODE) ?? "INKBLACK";
        public void SetThemeMode(string mode) => WriteReg(PREF_THEME_MODE, mode);

        // ── Language ──
        public string GetLanguage() => ReadReg(PREF_LANGUAGE) ?? "ENGLISH";
        public void SetLanguage(string lang) => WriteReg(PREF_LANGUAGE, lang);

        // ── Date format ──
        public string GetDateFormat() => ReadReg(PREF_DATE_FORMAT) ?? "MMM d, yyyy";
        public void SetDateFormat(string fmt) => WriteReg(PREF_DATE_FORMAT, fmt);

        // ── Time format ──
        public string GetTimeFormat() => ReadReg(PREF_TIME_FORMAT) ?? "HH:mm";
        public void SetTimeFormat(string fmt) => WriteReg(PREF_TIME_FORMAT, fmt);

        // ── Sort order ──
        public string GetSortOrder() => ReadReg(PREF_SORT_ORDER) ?? "Updated";
        public void SetSortOrder(string order) => WriteReg(PREF_SORT_ORDER, order);

        // ── Last notebook / tag ──
        public string? GetLastNotebookId() => ReadReg(PREF_LAST_NOTEBOOK_ID);
        public void SaveLastNotebookId(string? id)
        {
            if (id == null) DeleteReg(PREF_LAST_NOTEBOOK_ID);
            else WriteReg(PREF_LAST_NOTEBOOK_ID, id);
        }
        public string? GetLastTagId() => ReadReg(PREF_LAST_TAG_ID);
        public void SaveLastTagId(string? id)
        {
            if (id == null) DeleteReg(PREF_LAST_TAG_ID);
            else WriteReg(PREF_LAST_TAG_ID, id);
        }

        // ── Registry helpers ──
        private string? ReadReg(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(REG_KEY);
            return key?.GetValue(name) as string;
        }
        private void WriteReg(string name, string value)
        {
            using var key = Registry.CurrentUser.CreateSubKey(REG_KEY);
            key.SetValue(name, value);
        }
        private void DeleteReg(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(REG_KEY, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}
