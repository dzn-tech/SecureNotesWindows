using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SecureNotesWin.Helpers
{
    /// <summary>
    /// KeePass note body format shared with the Android app:
    /// plain markdown text with image filenames referencing KDBX binary attachments.
    ///
    /// Body format: prose/markdown text, then zero or more lines of the form:
    ///     ![image](filename.jpg)
    /// The Windows editor shows text and images in separate panels; the stored
    /// body keeps both so Android can read it unchanged.
    /// </summary>
    public static class NoteBodyHelper
    {
        // ── Regex helpers ─────────────────────────────────────────────────────────

        // Matches a full markdown image reference: ![anything](anything)
        private static readonly Regex ImageRefRegex =
            new(@"!\[[^\]]*\]\([^)]+\)", RegexOptions.Compiled);

        // Matches a bare image filename on its own line (legacy format)
        private static readonly Regex BareImageLineRegex =
            new(@"(?m)^[ \t]*[^\s!<\[\]()]+\.(?:jpe?g|png|gif|webp|bmp)[ \t]*(\r?\n|$)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ── Body decomposition helpers ────────────────────────────────────────────

        /// <summary>Returns the body with all image markdown refs removed.</summary>
        public static string StripImageRefsFromBody(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            var s = ImageRefRegex.Replace(body, string.Empty);
            s = BareImageLineRegex.Replace(s, string.Empty);
            // Clean up extra blank lines left behind
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.TrimEnd();
        }

        /// <summary>
        /// Removes all markdown refs to a specific file from the body.
        /// Used when the user deletes an image from the attachment panel.
        /// </summary>
        public static string RemoveImageRefFromBody(string body, string fileName)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            // Remove ![alt](fileName) and bare filename lines
            var escapedName = Regex.Escape(fileName);
            var s = Regex.Replace(body, $@"!\[[^\]]*\]\({escapedName}\)", string.Empty);
            s = Regex.Replace(s, $@"(?m)^[ \t]*{escapedName}[ \t]*(\r?\n|$)", string.Empty,
                RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.TrimEnd();
        }

        /// <summary>
        /// Merges text-only edits back into the full stored body.
        /// Image refs are extracted from the old body, and the new text is
        /// prepended so they are always at the end (cross-platform convention).
        /// </summary>
        public static string MergeTextIntoBody(string oldBody, string newTextOnly)
        {
            // Collect all existing image refs in order
            var imageRefs = new List<string>();
            foreach (Match m in ImageRefRegex.Matches(oldBody))
                if (!imageRefs.Contains(m.Value))
                    imageRefs.Add(m.Value);

            // Also collect bare filename lines
            foreach (Match m in BareImageLineRegex.Matches(oldBody))
            {
                var line = m.Value.Trim();
                if (!string.IsNullOrEmpty(line) && !imageRefs.Contains(line))
                    imageRefs.Add(line);
            }

            if (imageRefs.Count == 0)
                return newTextOnly;

            var text = newTextOnly.TrimEnd();
            var refs = string.Join("\n", imageRefs);
            return text.Length == 0 ? refs : $"{text}\n{refs}";
        }

        // ── Temp image folder (file:// URIs for MdXaml) ───────────────────────────

        private static readonly string _tempImageDir = Path.Combine(
            Path.GetTempPath(), $"SecureNotesWin_{Environment.ProcessId}");

        /// <summary>
        /// Writes attachment bytes to temp files and substitutes file:// URIs.
        /// Only used for the MdXaml text viewer (which now only shows text).
        /// </summary>
        public static string ResolveAttachmentsForDisplay(
            string body, IReadOnlyDictionary<string, byte[]> attachments)
        {
            if (attachments.Count == 0) return body ?? string.Empty;
            body ??= string.Empty;

            Directory.CreateDirectory(_tempImageDir);

            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in attachments)
            {
                var bytes = kv.Value;
                if (bytes == null || bytes.Length == 0) continue;
                var safeName = string.Concat(kv.Key.Split(Path.GetInvalidFileNameChars()));
                var tempPath = Path.Combine(_tempImageDir, safeName);
                File.WriteAllBytes(tempPath, bytes);
                lookup[kv.Key] = new Uri(tempPath).AbsoluteUri;
            }

            string Resolve(string imgRef, string altText)
            {
                if (imgRef.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                    imgRef.StartsWith("http",    StringComparison.OrdinalIgnoreCase))
                    return $"![{altText}]({imgRef})";
                if (imgRef.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                var name = Path.GetFileName(imgRef);
                return lookup.TryGetValue(name, out var uri)
                    ? $"![{altText}]({uri})"
                    : $"![{altText}]({imgRef})";
            }

            body = ImageRefRegex.Replace(body, m =>
                Resolve(m.Value.Substring(m.Value.IndexOf('(') + 1, m.Value.Length - m.Value.IndexOf('(') - 2),
                        m.Value.Substring(2, m.Value.IndexOf(']') - 2)));

            body = BareImageLineRegex.Replace(body, m =>
            {
                var imgRef = m.Groups[0].Value.Trim();
                var name   = Path.GetFileName(imgRef);
                return lookup.TryGetValue(name, out var uri) ? $"![{name}]({uri})\n" : m.Value;
            });

            return body;
        }

        // ── List-preview strip (for note list cards) ──────────────────────────────

        public static string StripForListPreview(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;
            var s = body;
            s = ImageRefRegex.Replace(s, string.Empty);
            s = BareImageLineRegex.Replace(s, string.Empty);
            s = Regex.Replace(s, @"<[^>]+>", string.Empty);
            s = Regex.Replace(s, @"(?m)^#{1,6}\s+", string.Empty);
            s = Regex.Replace(s, @"(\*\*|__)(.*?)\1", "$2");
            s = Regex.Replace(s, @"(\*|_)(.*?)\1",   "$2");
            s = Regex.Replace(s, @"(?m)^[ \t]*[-*]\s+", string.Empty);
            s = Regex.Replace(s, @"(?m)^[ \t]*\d+\.\s+", string.Empty);
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        // ── Normalisation (legacy import) ─────────────────────────────────────────

        public static string NormalizeStorageBody(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            if (body.StartsWith("<?sn-xaml?>", StringComparison.Ordinal))
                return XamlToPlainText(body);
            if (body.Contains("data:image", StringComparison.OrdinalIgnoreCase))
                return StripDataUrisFromBody(body);
            return body;
        }

        public static string StripDataUrisFromBody(string body) =>
            Regex.Replace(body,
                @"!\[([^\]]*)\]\(data:[^;]+;base64,[^)]+\)",
                m =>
                {
                    var alt    = m.Groups[1].Value;
                    var hasExt = Regex.IsMatch(alt, @"\.(jpe?g|png|gif|webp|bmp)$", RegexOptions.IgnoreCase);
                    return hasExt ? $"![]({alt})" : m.Value;
                });

        private static string XamlToPlainText(string xamlBody)
        {
            try
            {
                var xaml = xamlBody.StartsWith("<?sn-xaml?>", StringComparison.Ordinal)
                    ? xamlBody["<?sn-xaml?>".Length..]
                    : xamlBody;
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
                var doc   = new FlowDocument();
                var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                range.Load(stream, DataFormats.Xaml);
                return new TextRange(doc.ContentStart, doc.ContentEnd).Text.TrimEnd();
            }
            catch { return string.Empty; }
        }

        // ── Markdown editing helpers ──────────────────────────────────────────────

        public static void InsertMarkdown(TextBox box, string open, string close)
        {
            var start    = box.SelectionStart;
            var length   = box.SelectionLength;
            var text     = box.Text;
            var selected = length > 0 ? text.Substring(start, length) : string.Empty;

            // Toggle off: already wrapped with this exact pair
            if (start >= open.Length && length > 0 &&
                start + length + close.Length <= text.Length &&
                text.Substring(start - open.Length, open.Length)  == open &&
                text.Substring(start + length,       close.Length) == close)
            {
                box.Text = text
                    .Remove(start + length, close.Length)
                    .Remove(start - open.Length, open.Length);
                box.SelectionStart  = start - open.Length;
                box.SelectionLength = length;
                box.Focus();
                return;
            }

            // Strip conflicting size tags before applying a different one
            var sizeOpen  = new[] { "<big>",   "<small>" };
            var sizeClose = new[] { "</big>",  "</small>" };
            for (int i = 0; i < sizeOpen.Length; i++)
            {
                if (sizeOpen[i] == open) continue;
                if (start >= sizeOpen[i].Length && length > 0 &&
                    start + length + sizeClose[i].Length <= text.Length &&
                    text.Substring(start - sizeOpen[i].Length, sizeOpen[i].Length)  == sizeOpen[i] &&
                    text.Substring(start + length,              sizeClose[i].Length) == sizeClose[i])
                {
                    text   = text
                        .Remove(start + length, sizeClose[i].Length)
                        .Remove(start - sizeOpen[i].Length, sizeOpen[i].Length);
                    start -= sizeOpen[i].Length;
                    break;
                }
            }

            box.Text = text[..start] + open + selected + close + text[(start + length)..];
            box.SelectionStart  = start + open.Length;
            box.SelectionLength = selected.Length;
            box.Focus();
        }

        public static void InsertLinePrefix(TextBox box, string prefix)
        {
            var caret     = box.SelectionStart;
            var text      = box.Text;
            var lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;

            if (text.AsSpan(lineStart).StartsWith(prefix))
            {
                box.Text           = text.Remove(lineStart, prefix.Length);
                box.SelectionStart = Math.Max(lineStart, caret - prefix.Length);
            }
            else
            {
                var headingMatch = Regex.Match(text[lineStart..], @"^#{1,6} ");
                if (headingMatch.Success && prefix != headingMatch.Value)
                    text = text.Remove(lineStart, headingMatch.Length);

                box.Text           = text.Insert(lineStart, prefix);
                box.SelectionStart = caret + prefix.Length;
            }
            box.Focus();
        }

        public static void AppendImageReference(TextBox box, string fileName)
        {
            var text   = box.Text;
            var suffix = (text.Length > 0 && !text.EndsWith('\n') ? "\n" : "") + fileName + "\n";
            box.Text           = text + suffix;
            box.SelectionStart = box.Text.Length;
            box.Focus();
        }
    }
}
