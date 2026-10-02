using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation
{
    /// <summary>
    /// The game's languages. The game is written in Russian and the Russian text is the key: a translation file
    /// (UI/Localization/&lt;code&gt;.json in a <see cref="LanguageTable"/>, made from the extracted keys) maps each
    /// text to the language.
    /// <see cref="T"/> translates what the screen shows: an exact text, line by line, a template such as
    /// "Построить: {0}" (whose filled parts are translated too), and at last the known names inside a composed
    /// line ("2 Руда + 1 Уголь → 2 Слиток"). Untranslated text stays Russian. Presentation only.
    /// </summary>
    public static class Localization
    {
        public const string Source = "ru";

        public static readonly List<(string Code, string Name)> Languages = new()
        {
            ("ru", "Русский"), ("en", "English"), ("de", "Deutsch"), ("fr", "Français"), ("es", "Español"),
            ("it", "Italiano"), ("pt", "Português (Brasil)"), ("pl", "Polski"), ("cs", "Čeština"),
            ("uk", "Українська"), ("tr", "Türkçe"), ("kk", "Қазақша"), ("ja", "日本語"), ("ko", "한국어"),
            ("zh", "简体中文"), ("hi", "हिन्दी")
        };

        private sealed class Template
        {
            public Regex Pattern;
            public string Translation;
            public int[] Holes;
        }

        private sealed class Original
        {
            public string Text;
        }

        private static readonly Regex Cyrillic = new("[А-Яа-яЁё]", RegexOptions.Compiled);
        private static readonly Regex NameRun = new("[А-Яа-яЁё][А-Яа-яЁё\\-]*(?: [А-Яа-яЁё][А-Яа-яЁё\\-]*)*", RegexOptions.Compiled);
        private static readonly ConditionalWeakTable<TextElement, Original> Originals = new();
        private static readonly List<WeakReference<VisualElement>> Trees = new();
        private static Dictionary<string, string> s_exact = new(StringComparer.Ordinal);
        // the same texts without their edge spaces and punctuation, for the words inside a composed line
        private static Dictionary<string, string> s_words = new(StringComparer.Ordinal);
        private static List<Template> s_templates = new();
        private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
        private static string s_loaded;
        private static LanguageTable s_table;

        /// <summary>The translation files to read; the bootstrap passes the scene's table.</summary>
        public static void Use(LanguageTable table)
        {
            s_table = table;
            s_loaded = null;
        }

        /// <summary>The language in use: the player's choice, else the system's when the game has it, else English.</summary>
        public static string Current
        {
            get
            {
                string chosen = GameSettings.Language;
                if (!string.IsNullOrEmpty(chosen) && Languages.Exists(l => l.Code == chosen)) return chosen;
                return SystemCode();
            }
        }

        public static event Action Changed;

        /// <summary>The player picks a language: every text on screen follows at once.</summary>
        public static void Select(string code)
        {
            GameSettings.SetLanguage(code);
            Load();
            Retranslate();
            Changed?.Invoke();
        }

        /// <summary>Back to the source texts until the next <see cref="Load"/>.</summary>
        public static void Unload()
        {
            s_loaded = null;
            s_exact = new Dictionary<string, string>(StringComparer.Ordinal);
            s_words = new Dictionary<string, string>(StringComparer.Ordinal);
            s_templates = new List<Template>();
            Cache.Clear();
        }

        /// <summary>The language that is loaded now; null before the game loads one.</summary>
        public static string Loaded => s_loaded;

        /// <summary>Reads the current language's file once; called at start and on a change.</summary>
        public static void Load()
        {
            string code = Current;
            if (s_loaded == code) return;
            s_loaded = code;
            s_exact = new Dictionary<string, string>(StringComparer.Ordinal);
            s_words = new Dictionary<string, string>(StringComparer.Ordinal);
            s_templates = new List<Template>();
            Cache.Clear();
            if (code == Source) return;
            var asset = s_table != null ? s_table.Get(code) : null;
            if (asset == null) return;
            var file = JsonUtility.FromJson<File>(asset.text);
            if (file?.entries == null) return;
            var templates = new List<(int Literal, Template Template)>();
            foreach (var entry in file.entries)
            {
                if (string.IsNullOrEmpty(entry.k) || string.IsNullOrEmpty(entry.v)) continue;
                s_exact[entry.k] = entry.v;
                string word = entry.k.Trim(' ', ',', '.', ':', ';', '!', '?');
                if (word.Length > 0 && !s_words.ContainsKey(word)) s_words[word] = entry.v.Trim(' ', ',', '.', ':', ';', '!', '?');
                var template = Compile(entry.k, entry.v, out int literal);
                // a template with no words of its own ("{0} {1}") would match anything
                if (template != null && literal >= 3) templates.Add((literal, template));
            }
            templates.Sort((a, b) => b.Literal.CompareTo(a.Literal));
            foreach (var (_, template) in templates) s_templates.Add(template);
        }

        /// <summary>The text in the current language; a text with no Russian in it comes back as it is.</summary>
        public static string T(string text)
        {
            // nothing is translated until the game loads a language (tests and tools see the source texts)
            if (string.IsNullOrEmpty(text) || s_loaded == null || s_loaded == Source) return text;
            if (s_exact.Count == 0 || !Cyrillic.IsMatch(text)) return text;
            if (Cache.TryGetValue(text, out var cached)) return cached;
            string result = Translate(text, 0);
            if (Cache.Count > 20000) Cache.Clear();
            Cache[text] = result;
            return result;
        }

        /// <summary>Sets an element's text from its Russian source, remembering the source for a language change.</summary>
        public static void Apply(TextElement element, string source)
        {
            if (element == null) return;
            source ??= string.Empty;
            if (Originals.TryGetValue(element, out var original)) original.Text = source;
            else Originals.Add(element, new Original { Text = source });
            string shown = T(source);
            if (element.text != shown) element.text = shown;
        }

        /// <summary>
        /// Translates the static texts of a document (its UXML labels and buttons) and keeps the tree for a
        /// language change. Texts set later through <see cref="Apply"/> keep their own source.
        /// </summary>
        public static void TranslateTree(VisualElement root)
        {
            if (root == null) return;
            Trees.RemoveAll(reference => !reference.TryGetTarget(out _));
            if (!Trees.Exists(reference => reference.TryGetTarget(out var known) && known == root))
                Trees.Add(new WeakReference<VisualElement>(root));
            root.Query<TextElement>().ForEach(element =>
            {
                if (!Originals.TryGetValue(element, out var original))
                {
                    original = new Original { Text = element.text };
                    Originals.Add(element, original);
                }
                string shown = T(original.Text);
                if (element.text != shown) element.text = shown;
            });
            root.EnableInClassList("lang-complex", s_loaded == "hi");
        }

        private static void Retranslate()
        {
            foreach (var reference in Trees.ToArray())
                if (reference.TryGetTarget(out var root)) TranslateTree(root);
        }

        private static string Translate(string text, int depth)
        {
            if (s_exact.TryGetValue(text, out var exact)) return exact;
            if (depth > 3) return text;
            // a name lower-cased in a sentence ("продано: руда") is its catalog name ("Руда")
            string capital = char.ToUpperInvariant(text[0]) + text.Substring(1);
            if (capital != text && s_exact.TryGetValue(capital, out var named))
                return named.Length > 0 ? char.ToLowerInvariant(named[0]) + named.Substring(1) : named;
            if (text.IndexOf('\n') >= 0)
            {
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Length > 0 && Cyrillic.IsMatch(lines[i])) lines[i] = Translate(lines[i], depth + 1);
                return string.Join("\n", lines);
            }
            foreach (var template in s_templates)
            {
                var match = template.Pattern.Match(text);
                if (!match.Success) continue;
                var builder = new StringBuilder(template.Translation);
                for (int i = 0; i < template.Holes.Length; i++)
                {
                    string part = match.Groups["h" + template.Holes[i]].Value;
                    if (Cyrillic.IsMatch(part)) part = Translate(part, depth + 1);
                    builder.Replace("{" + template.Holes[i] + "}", part);
                }
                return builder.ToString();
            }
            // a line composed of names and numbers: translate the names it holds
            return NameRun.Replace(text, run =>
            {
                string name = run.Value;
                if (s_exact.TryGetValue(name, out var translated) || s_words.TryGetValue(name, out translated))
                    return translated;
                string upper = char.ToUpperInvariant(name[0]) + name.Substring(1);
                if ((s_exact.TryGetValue(upper, out translated) || s_words.TryGetValue(upper, out translated)) &&
                    translated.Length > 0)
                    return char.ToLowerInvariant(translated[0]) + translated.Substring(1);
                return name;
            });
        }

        // "Построить: {0}" -> ^Построить: (?<h0>.+?)$ ; literal is the count of characters outside the holes
        private static Template Compile(string key, string value, out int literal)
        {
            literal = 0;
            var holes = new List<int>();
            var pattern = new StringBuilder("^");
            int i = 0;
            var hole = new Regex("\\{(\\d+)\\}");
            foreach (Match m in hole.Matches(key))
            {
                string text = key.Substring(i, m.Index - i);
                literal += text.Trim().Length;
                pattern.Append(Regex.Escape(text));
                int number = int.Parse(m.Groups[1].Value);
                if (holes.Contains(number)) pattern.Append("\\k<h" + number + ">");
                else
                {
                    pattern.Append("(?<h" + number + ">.+?)");
                    holes.Add(number);
                }
                i = m.Index + m.Length;
            }
            if (holes.Count == 0) return null;
            string tail = key.Substring(i);
            literal += tail.Trim().Length;
            pattern.Append(Regex.Escape(tail)).Append('$');
            return new Template
            {
                Pattern = new Regex(pattern.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant),
                Translation = value,
                Holes = holes.ToArray()
            };
        }

        private static string SystemCode()
        {
            switch (UnityEngine.Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Belarusian: return "ru";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Czech: return "cs";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return "zh";
                default: return "en";
            }
        }

        [Serializable]
        private sealed class File
        {
            public Entry[] entries;
        }

        [Serializable]
        private sealed class Entry
        {
            public string k;
            public string v;
        }
    }
}
