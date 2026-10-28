using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GradualRepair
{
    // Only mod-owned display text lives here. IDs, native localization, saves and
    // interaction ownership never depend on translated text in the catalog.
    internal static class RepairText
    {
        static readonly Dictionary<string, string> English = Load("en");
        static readonly Dictionary<string, string> Japanese = Load("ja");
        static readonly Dictionary<string, Dictionary<string, string>> Overrides = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        internal static Func<string> LanguageCode = () => "en";
        static string lastLanguageCode, activeLocale = "";

        // Load once at startup: no file polling or disk access in interaction/UI updates.
        internal static void Initialize(string directory, Action<string> warn)
        {
            Overrides.Clear();
            try
            {
                Directory.CreateDirectory(directory);
                ExportIfMissing(directory, "en", English);
                ExportIfMissing(directory, "ja", Japanese);
                foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
                {
                    string locale = NormalizeLocale(Path.GetFileNameWithoutExtension(path));
                    if (locale.Length == 0) { warn("Ignored invalid translation filename: " + path); continue; }
                    if (Overrides.ContainsKey(locale)) { warn("Ignored duplicate locale: " + path); continue; }
                    try
                    {
                        var data = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                        var translations = new Dictionary<string, string>();
                        foreach (var property in data.Properties())
                        {
                            if (!English.TryGetValue(property.Name, out var original)) { warn("Unknown translation key: " + path + ": " + property.Name); continue; }
                            string value = property.Value.Type == JTokenType.String ? (string)property.Value : null;
                            if (string.IsNullOrWhiteSpace(value) || !ValidFormat(original, value)) { warn("Invalid translation; using fallback: " + path + ": " + property.Name); continue; }
                            translations.Add(property.Name, value);
                        }
                        Overrides.Add(locale, translations);
                    }
                    catch (Exception ex) { warn("Cannot read translation; using fallback: " + path + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { warn("External translations unavailable; using embedded defaults: " + ex.Message); }
        }

        static void ExportIfMissing(string directory, string locale, Dictionary<string, string> catalog)
        {
            string path = Path.Combine(directory, locale + ".json");
            if (File.Exists(path)) return; // Never overwrite a user's translation, including on mod updates.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(catalog, Formatting.Indented));
        }

        internal static string NormalizeLocale(string code)
        {
            string normalized = (code ?? "").Trim().Replace('_', '-').ToLowerInvariant();
            return Regex.IsMatch(normalized, @"^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$") ? normalized : "";
        }

        static bool ValidFormat(string original, string translated)
        {
            try
            {
                string.Format(CultureInfo.InvariantCulture, translated, new object[16]);
                Func<string, string[]> fields = text => Regex.Matches(text, @"(?<!\{)\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}").Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
                return fields(original).SequenceEqual(fields(translated));
            }
            catch (FormatException) { return false; }
        }

        static bool TryOverride(string locale, string key, out string text)
        {
            text = null;
            return Overrides.TryGetValue(locale, out var catalog) && catalog.TryGetValue(key, out text);
        }

        static Dictionary<string, string> Load(string locale)
        {
            using (var stream = typeof(RepairText).Assembly.GetManifestResourceStream("GradualRepair.Localization." + locale + ".json"))
            {
                if (stream == null) throw new InvalidDataException("Missing translation catalog: " + locale);
                using (var reader = new StreamReader(stream))
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd())
                        ?? throw new InvalidDataException("Empty translation catalog: " + locale);
            }
        }

        internal static bool IsJapanese(string code) => !string.IsNullOrEmpty(code) &&
            (code.Equals("ja", StringComparison.OrdinalIgnoreCase) || code.StartsWith("ja-", StringComparison.OrdinalIgnoreCase) || code.StartsWith("ja_", StringComparison.OrdinalIgnoreCase));

        internal static string Get(string key)
        {
            string code = LanguageCode();
            if (!string.Equals(lastLanguageCode, code, StringComparison.Ordinal)) { lastLanguageCode = code; activeLocale = NormalizeLocale(code); }
            string locale = activeLocale;
            if (TryOverride(locale, key, out var replacement)) return replacement;
            int separator = locale.IndexOf('-');
            if (separator > 0 && TryOverride(locale.Substring(0, separator), key, out replacement)) return replacement;
            var catalog = IsJapanese(locale) ? Japanese : English;
            if (catalog == English && TryOverride("en", key, out replacement)) return replacement;
            if (catalog.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text)) return text;
            return English.TryGetValue(key, out text) ? text : key;
        }
        internal static string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);
    }
}
