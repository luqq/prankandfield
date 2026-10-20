using System.Reflection;
using System.Text.Json;

namespace PrankAndField.Core.Localization;

/// <summary>
/// Tiny dictionary-based localizer. Strings live in embedded JSON files, one per culture.
/// English is the reference language and the fallback for any missing key.
/// </summary>
public sealed class Localizer
{
    public const string DefaultCulture = "en";

    /// <summary>Supported culture tags, in menu order.</summary>
    public static readonly IReadOnlyList<string> SupportedCultures = ["en", "es-ES", "ca-ES-valencia", "ja"];

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();

    private IReadOnlyDictionary<string, string> _strings;
    private readonly IReadOnlyDictionary<string, string> _fallback;

    public Localizer(string? culture = null)
    {
        _fallback = Load(DefaultCulture);
        _strings = _fallback;
        Culture = DefaultCulture;
        SetCulture(culture ?? DefaultCulture);
    }

    public string Culture { get; private set; }

    public string this[string key] => Get(key);

    /// <summary>Switches language. Unknown tags fall back to the closest supported one, then English.</summary>
    public void SetCulture(string culture)
    {
        string resolved = Resolve(culture);
        Culture = resolved;
        _strings = Load(resolved);
    }

    public static string Resolve(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return DefaultCulture;
        foreach (var c in SupportedCultures)
            if (string.Equals(c, culture, StringComparison.OrdinalIgnoreCase)) return c;
        string prefix = culture.Split('-')[0];
        foreach (var c in SupportedCultures)
            if (c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return c;
        return DefaultCulture;
    }

    public bool HasKey(string key) => _strings.ContainsKey(key) || _fallback.ContainsKey(key);

    public string Get(string key)
    {
        if (_strings.TryGetValue(key, out var s)) return s;
        if (_fallback.TryGetValue(key, out var f)) return f;
        return "[" + key + "]";
    }

    public string Format(string key, params object[] args)
    {
        string template = Get(key);
        try
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>All keys of a culture (used by tests to verify translation completeness).</summary>
    public static IReadOnlyCollection<string> KeysOf(string culture) => Load(culture).Keys.ToArray();

    public static IReadOnlyDictionary<string, string> Load(string culture)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(culture, out var cached)) return cached;
            var asm = typeof(Localizer).GetTypeInfo().Assembly;
            string name = "PrankAndField.Core.Localization." + culture + ".json";
            using var stream = asm.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException("Missing localization resource " + name);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException("Empty localization resource " + name);
            var result = new Dictionary<string, string>(dict, StringComparer.Ordinal);
            Cache[culture] = result;
            return result;
        }
    }
}
