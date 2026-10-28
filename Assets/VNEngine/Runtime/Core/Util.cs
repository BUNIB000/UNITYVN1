using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VNEngine
{
    public static class Util
    {
        public static bool TryFloat(string s, out float value)
        {
            if (s != null && s.EndsWith("s", StringComparison.OrdinalIgnoreCase) && s.Length > 1 && char.IsDigit(s[s.Length - 2]))
                s = s.Substring(0, s.Length - 1); // "0.5s"
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string Key(string s) => s?.Trim().ToLowerInvariant();

        /// <summary>
        /// Splits a command line into words. Quoted strings stay together (quotes removed),
        /// and key=value pairs are returned separately.
        /// </summary>
        public static List<string> Tokenize(string line, Dictionary<string, string> options = null)
        {
            var words = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false, hadQuote = false;
            for (int i = 0; i <= line.Length; i++)
            {
                char c = i < line.Length ? line[i] : ' ';
                if (c == '"' || (quoted && c == '”') || (!quoted && c == '“'))
                {
                    quoted = !quoted;
                    hadQuote = true;
                    continue;
                }
                if (!quoted && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 || hadQuote) Flush(words, sb.ToString(), hadQuote, options);
                    sb.Clear();
                    hadQuote = false;
                    continue;
                }
                sb.Append(c);
            }
            return words;
        }

        static void Flush(List<string> words, string word, bool wasQuoted, Dictionary<string, string> options)
        {
            if (options != null)
            {
                int eq = word.IndexOf('=');
                if (eq > 0 && eq < word.Length - 1 && !word.Contains("==") && IsIdentifier(word.Substring(0, eq)))
                {
                    options[word.Substring(0, eq).ToLowerInvariant()] = word.Substring(eq + 1).Trim('"');
                    return;
                }
            }
            words.Add(word);
        }

        static bool IsIdentifier(string s)
        {
            if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return false;
            foreach (char c in s) if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.')) return false;
            return true;
        }

        /// <summary>Turns "INT. COFFEE SHOP - DAY" into "int_coffee_shop_day".</summary>
        public static string Slug(string s)
        {
            var sb = new StringBuilder();
            bool lastUnderscore = true;
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) { sb.Append(c); lastUnderscore = false; }
                else if (!lastUnderscore) { sb.Append('_'); lastUnderscore = true; }
            }
            return sb.ToString().Trim('_');
        }

        public static string FormatNumber(double d)
        {
            if (Math.Abs(d - Math.Round(d)) < 1e-9) return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            return d.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>A script error with the line it happened on.</summary>
    public sealed class StoryError
    {
        public int Line;
        public string Message;
        public string Source;
        public bool IsWarning;

        public override string ToString() => $"{(IsWarning ? "Warning" : "Error")} on line {Line}: {Message}" + (Source != null ? $"\n    {Source.Trim()}" : "");
    }

    public sealed class StoryException : Exception
    {
        public readonly int Line;
        public StoryException(int line, string message) : base(message) { Line = line; }
    }
}
