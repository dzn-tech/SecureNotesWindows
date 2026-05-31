using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SecureNotesWin.Helpers
{
    /// <summary>
    /// Converts between the app's stored markdown format and a WPF FlowDocument,
    /// enabling a WYSIWYG RichTextBox editor with no markdown syntax ever shown.
    ///
    /// Supported round-trips:
    ///   # H1   ## H2   ### H3
    ///   **bold**   *italic*   ***bold+italic***
    ///   - bullet list    1. numbered list
    ///   <big>…</big>   <small>…</small>
    ///   plain paragraphs / blank-line paragraph breaks
    /// </summary>
    public static class MarkdownRtbHelper
    {
        // ── Font size map ────────────────────────────────────────────────────────

        public const double BaseSize    = 15;
        public const double BigSize     = 19;
        public const double SmallSize   = 12;
        public const double H1Size      = 28;
        public const double H2Size      = 22;
        public const double H3Size      = 18;

        // ── Markdown → FlowDocument ──────────────────────────────────────────────

        /// <summary>
        /// Parses <paramref name="markdown"/> and populates <paramref name="doc"/>
        /// with styled WPF blocks.  All existing blocks are cleared first.
        /// Foreground is applied from <paramref name="fg"/> so theming is respected.
        /// </summary>
        public static void LoadMarkdown(FlowDocument doc, string markdown, Brush fg)
        {
            doc.Blocks.Clear();

            if (string.IsNullOrEmpty(markdown))
            {
                doc.Blocks.Add(MakeParagraph(string.Empty, fg));
                return;
            }

            var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                // ── Heading ──
                var hm = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (hm.Success)
                {
                    var level = hm.Groups[1].Length;
                    var text  = hm.Groups[2].Value;
                    var p     = MakeParagraph(string.Empty, fg);
                    p.Margin  = new Thickness(0, level == 1 ? 10 : 6, 0, 2);
                    ApplyInlineMarkup(p.Inlines, text, fg, HeadingSize(level),
                                      FontWeights.Bold, FontStyles.Normal);
                    doc.Blocks.Add(p);
                    i++;
                    continue;
                }

                // ── Unordered list item ──
                var ulm = Regex.Match(line, @"^(\s*)([-*])\s+(.*)$");
                if (ulm.Success)
                {
                    var list = new List { MarkerStyle = TextMarkerStyle.Disc,
                                         Margin = new Thickness(0), Padding = new Thickness(20, 0, 0, 0) };
                    while (i < lines.Length)
                    {
                        var lm2 = Regex.Match(lines[i], @"^(\s*)([-*])\s+(.*)$");
                        if (!lm2.Success) break;
                        var item = new ListItem();
                        var ip   = MakeParagraph(string.Empty, fg);
                        ip.Margin = new Thickness(0);
                        ApplyInlineMarkup(ip.Inlines, lm2.Groups[3].Value, fg,
                                          BaseSize, FontWeights.Normal, FontStyles.Normal);
                        item.Blocks.Add(ip);
                        list.ListItems.Add(item);
                        i++;
                    }
                    doc.Blocks.Add(list);
                    continue;
                }

                // ── Ordered list item ──
                var olm = Regex.Match(line, @"^(\s*)\d+\.\s+(.*)$");
                if (olm.Success)
                {
                    var list = new List { MarkerStyle = TextMarkerStyle.Decimal,
                                         Margin = new Thickness(0), Padding = new Thickness(20, 0, 0, 0) };
                    while (i < lines.Length)
                    {
                        var lm2 = Regex.Match(lines[i], @"^(\s*)\d+\.\s+(.*)$");
                        if (!lm2.Success) break;
                        var item = new ListItem();
                        var ip   = MakeParagraph(string.Empty, fg);
                        ip.Margin = new Thickness(0);
                        ApplyInlineMarkup(ip.Inlines, lm2.Groups[2].Value, fg,
                                          BaseSize, FontWeights.Normal, FontStyles.Normal);
                        item.Blocks.Add(ip);
                        list.ListItems.Add(item);
                        i++;
                    }
                    doc.Blocks.Add(list);
                    continue;
                }

                // ── Blank line → paragraph break (skip, already handled by paragraph per line logic) ──
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Emit an empty paragraph to preserve blank lines
                    doc.Blocks.Add(MakeParagraph(string.Empty, fg));
                    i++;
                    continue;
                }

                // ── Normal paragraph line ──
                var p2 = MakeParagraph(string.Empty, fg);
                ApplyInlineMarkup(p2.Inlines, line, fg, BaseSize,
                                  FontWeights.Normal, FontStyles.Normal);
                doc.Blocks.Add(p2);
                i++;
            }

            // Ensure at least one editable paragraph
            if (doc.Blocks.Count == 0)
                doc.Blocks.Add(MakeParagraph(string.Empty, fg));
        }

        // ── FlowDocument → Markdown ──────────────────────────────────────────────

        /// <summary>
        /// Serializes the FlowDocument back to the app's markdown storage format.
        /// </summary>
        public static string SaveMarkdown(FlowDocument doc)
        {
            var sb = new StringBuilder();
            SerializeBlocks(doc.Blocks, sb, 0);
            return sb.ToString().TrimEnd();
        }

        private static void SerializeBlocks(BlockCollection blocks, StringBuilder sb, int listDepth)
        {
            bool prevWasList = false;
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case Paragraph p:
                        if (prevWasList) sb.AppendLine();
                        sb.AppendLine(SerializeParagraph(p));
                        prevWasList = false;
                        break;

                    case List list:
                        int idx = 1;
                        foreach (ListItem item in list.ListItems)
                        {
                            var innerSb = new StringBuilder();
                            SerializeBlocks(item.Blocks, innerSb, listDepth + 1);
                            var text = innerSb.ToString().TrimEnd();

                            if (list.MarkerStyle == TextMarkerStyle.Decimal)
                                sb.AppendLine($"{idx++}. {text}");
                            else
                                sb.AppendLine($"- {text}");
                        }
                        prevWasList = true;
                        break;

                    default:
                        prevWasList = false;
                        break;
                }
            }
        }

        private static string SerializeParagraph(Paragraph p)
        {
            // Detect heading level by font size
            double fontSize = p.FontSize;
            if (double.IsNaN(fontSize)) fontSize = BaseSize;

            // Walk inlines to check for a uniform heading run
            var headingPrefix = string.Empty;
            if (Math.Abs(fontSize - H1Size) < 0.5) headingPrefix = "# ";
            else if (Math.Abs(fontSize - H2Size) < 0.5) headingPrefix = "## ";
            else if (Math.Abs(fontSize - H3Size) < 0.5) headingPrefix = "### ";
            else
            {
                // Check if the first run has a heading size
                foreach (var inline in p.Inlines)
                {
                    if (inline is Run r)
                    {
                        double rs = double.IsNaN(r.FontSize) ? BaseSize : r.FontSize;
                        if (Math.Abs(rs - H1Size) < 0.5) headingPrefix = "# ";
                        else if (Math.Abs(rs - H2Size) < 0.5) headingPrefix = "## ";
                        else if (Math.Abs(rs - H3Size) < 0.5) headingPrefix = "### ";
                    }
                    break; // only check first inline for heading
                }
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(headingPrefix)) sb.Append(headingPrefix);

            foreach (var inline in p.Inlines)
                SerializeInline(inline, sb, headingPrefix.Length > 0);

            return sb.ToString();
        }

        private static void SerializeInline(Inline inline, StringBuilder sb, bool isHeading)
        {
            switch (inline)
            {
                case Run r:
                {
                    var text = r.Text;
                    if (string.IsNullOrEmpty(text)) break;

                    double fontSize = double.IsNaN(r.FontSize) ? BaseSize : r.FontSize;
                    bool bold   = r.FontWeight == FontWeights.Bold   || r.FontWeight == FontWeights.ExtraBold;
                    bool italic = r.FontStyle  == FontStyles.Italic  || r.FontStyle  == FontStyles.Oblique;
                    bool big    = !isHeading && Math.Abs(fontSize - BigSize) < 0.5;
                    bool small  = !isHeading && Math.Abs(fontSize - SmallSize) < 0.5;

                    if (big)   sb.Append("<big>");
                    if (small) sb.Append("<small>");
                    if (bold && italic) sb.Append("***");
                    else if (bold)      sb.Append("**");
                    else if (italic)    sb.Append("*");

                    sb.Append(text);

                    if (bold && italic) sb.Append("***");
                    else if (bold)      sb.Append("**");
                    else if (italic)    sb.Append("*");
                    if (big)   sb.Append("</big>");
                    if (small) sb.Append("</small>");
                    break;
                }
                case Span span:
                    foreach (var child in span.Inlines)
                        SerializeInline(child, sb, isHeading);
                    break;

                case LineBreak:
                    sb.AppendLine();
                    break;
            }
        }

        // ── Inline markup parser ─────────────────────────────────────────────────

        /// <summary>
        /// Parses a single line of markdown inline syntax and appends styled
        /// <see cref="Run"/> objects to <paramref name="inlines"/>.
        /// </summary>
        private static void ApplyInlineMarkup(InlineCollection inlines, string text,
            Brush fg, double fontSize, FontWeight weight, FontStyle style)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Tokenise: find bold/italic/size spans
            // Pattern matches (in order of decreasing priority):
            //   ***...***  bold+italic
            //   **...**    bold
            //   *...*      italic
            //   <big>...</big>
            //   <small>...</small>
            const string pattern =
                @"(\*\*\*(.+?)\*\*\*)" +
                @"|(\*\*(.+?)\*\*)" +
                @"|(\*(.+?)\*)" +
                @"|(<big>(.*?)<\/big>)" +
                @"|(<small>(.*?)<\/small>)";

            int pos = 0;
            foreach (Match m in Regex.Matches(text, pattern, RegexOptions.Singleline))
            {
                // Emit plain text before this match
                if (m.Index > pos)
                    inlines.Add(MakeRun(text[pos..m.Index], fg, fontSize, weight, style));

                if (m.Groups[1].Success) // ***bold+italic***
                    inlines.Add(MakeRun(m.Groups[2].Value, fg, fontSize, FontWeights.Bold, FontStyles.Italic));
                else if (m.Groups[3].Success) // **bold**
                    inlines.Add(MakeRun(m.Groups[4].Value, fg, fontSize, FontWeights.Bold, style));
                else if (m.Groups[5].Success) // *italic*
                    inlines.Add(MakeRun(m.Groups[6].Value, fg, fontSize, weight, FontStyles.Italic));
                else if (m.Groups[7].Success) // <big>
                    inlines.Add(MakeRun(m.Groups[8].Value, fg, BigSize, weight, style));
                else if (m.Groups[9].Success) // <small>
                    inlines.Add(MakeRun(m.Groups[10].Value, fg, SmallSize, weight, style));

                pos = m.Index + m.Length;
            }

            // Trailing plain text
            if (pos < text.Length)
                inlines.Add(MakeRun(text[pos..], fg, fontSize, weight, style));
        }

        // ── Factory helpers ──────────────────────────────────────────────────────

        private static Paragraph MakeParagraph(string text, Brush fg)
        {
            var p = new Paragraph
            {
                Margin    = new Thickness(0, 0, 0, 4),
                FontSize  = BaseSize,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = fg
            };
            if (!string.IsNullOrEmpty(text))
                p.Inlines.Add(MakeRun(text, fg, BaseSize, FontWeights.Normal, FontStyles.Normal));
            return p;
        }

        private static Run MakeRun(string text, Brush fg, double fontSize,
                                   FontWeight weight, FontStyle style) =>
            new(text)
            {
                Foreground = fg,
                FontSize   = fontSize,
                FontWeight = weight,
                FontStyle  = style,
                FontFamily = new FontFamily("Segoe UI")
            };

        private static double HeadingSize(int level) => level switch
        {
            1 => H1Size,
            2 => H2Size,
            3 => H3Size,
            _ => H3Size   // h4–h6 all render as H3
        };

        // ── Selection helpers (used by toolbar) ─────────────────────────────────

        /// <summary>
        /// Returns true if every character in <paramref name="sel"/> has the given
        /// font weight (so the toolbar knows whether to toggle off).
        /// </summary>
        public static bool SelectionHasWeight(TextSelection sel, FontWeight weight)
        {
            var v = sel.GetPropertyValue(TextElement.FontWeightProperty);
            return v != DependencyProperty.UnsetValue && (FontWeight)v == weight;
        }

        public static bool SelectionHasStyle(TextSelection sel, FontStyle style)
        {
            var v = sel.GetPropertyValue(TextElement.FontStyleProperty);
            return v != DependencyProperty.UnsetValue && (FontStyle)v == style;
        }

        public static bool SelectionHasSize(TextSelection sel, double size)
        {
            var v = sel.GetPropertyValue(TextElement.FontSizeProperty);
            return v != DependencyProperty.UnsetValue && Math.Abs((double)v - size) < 0.5;
        }

        /// <summary>
        /// Returns the font size shared by the entire selection,
        /// or <see cref="BaseSize"/> if mixed / unset.
        /// </summary>
        public static double SelectionFontSize(TextSelection sel)
        {
            var v = sel.GetPropertyValue(TextElement.FontSizeProperty);
            return (v == DependencyProperty.UnsetValue) ? BaseSize : (double)v;
        }

        // ── Paragraph-level helpers ──────────────────────────────────────────────

        /// <summary>
        /// Applies a heading style to the paragraph(s) touched by the selection.
        /// Passing level=0 resets back to normal body text.
        /// </summary>
        public static void ApplyHeading(RichTextBox rtb, int level)
        {
            // Collect all paragraphs under the selection
            var paras = GetSelectedParagraphs(rtb);
            foreach (var p in paras)
            {
                if (level == 0)
                {
                    p.FontSize   = BaseSize;
                    p.FontWeight = FontWeights.Normal;
                    p.Margin     = new Thickness(0, 0, 0, 4);
                    // Reset inline runs too
                    foreach (var inline in p.Inlines)
                        if (inline is Run r)
                        {
                            r.FontSize   = BaseSize;
                            r.FontWeight = FontWeights.Normal;
                        }
                }
                else
                {
                    double sz = HeadingSize(level);
                    p.FontSize   = sz;
                    p.FontWeight = FontWeights.Bold;
                    p.Margin     = new Thickness(0, level == 1 ? 10 : 6, 0, 2);
                    foreach (var inline in p.Inlines)
                        if (inline is Run r)
                        {
                            r.FontSize   = sz;
                            r.FontWeight = FontWeights.Bold;
                        }
                }
            }
            rtb.Focus();
        }

        /// <summary>
        /// Wraps / unwraps selected paragraphs in a WPF <see cref="List"/>.
        /// </summary>
        public static void ApplyList(RichTextBox rtb, TextMarkerStyle markerStyle)
        {
            // Use built-in editing commands where possible;
            // fall back to manual wrapping for ordered lists.
            rtb.Focus();
            if (markerStyle == TextMarkerStyle.Disc)
            {
                EditingCommands.ToggleBullets.Execute(null, rtb);
            }
            else
            {
                EditingCommands.ToggleNumbering.Execute(null, rtb);
            }
        }

        private static IEnumerable<Paragraph> GetSelectedParagraphs(RichTextBox rtb)
        {
            var result = new List<Paragraph>();
            var start  = rtb.Selection.Start.Paragraph;
            var end    = rtb.Selection.End.Paragraph;

            if (start == null) yield break;

            // Walk from start paragraph to end paragraph
            Block? block = start;
            while (block != null)
            {
                if (block is Paragraph p)
                {
                    result.Add(p);
                    if (p == end) break;
                }
                else if (block is List list)
                {
                    foreach (ListItem item in list.ListItems)
                        foreach (var b in item.Blocks)
                            if (b is Paragraph lp)
                            {
                                result.Add(lp);
                                if (lp == end) goto done;
                            }
                }
                block = block.NextBlock;
            }
            done:
            foreach (var p in result) yield return p;
        }
    }
}
