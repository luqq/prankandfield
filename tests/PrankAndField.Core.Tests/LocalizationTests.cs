using PrankAndField.Core.Localization;

namespace PrankAndField.Core.Tests;

public class LocalizationTests
{
    public static IEnumerable<object[]> Cultures => Localizer.SupportedCultures.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cultures))]
    public void EveryCultureHasExactlyTheEnglishKeys(string culture)
    {
        var english = Localizer.KeysOf("en").ToHashSet();
        var keys = Localizer.KeysOf(culture).ToHashSet();
        var missing = english.Except(keys).OrderBy(k => k).ToList();
        var extra = keys.Except(english).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, $"{culture} is missing: {string.Join(", ", missing)}");
        Assert.True(extra.Count == 0, $"{culture} has unknown keys: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void NoEmptyStringsAndPlaceholdersMatchEnglish(string culture)
    {
        var english = Localizer.Load("en");
        foreach (var (key, value) in Localizer.Load(culture))
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"{culture}:{key} is empty");
            int placeholders = english[key].Count(c => c == '{');
            Assert.Equal(placeholders, value.Count(c => c == '{'));
        }
    }

    [Theory]
    [InlineData("es", "es-ES")]
    [InlineData("es-MX", "es-ES")]
    [InlineData("ca", "ca-ES-valencia")]
    [InlineData("ca-ES", "ca-ES-valencia")]
    [InlineData("ja-JP", "ja")]
    [InlineData("JA", "ja")]
    [InlineData("fr-FR", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void CultureResolutionFallsBackSensibly(string? requested, string expected) => Assert.Equal(expected, Localizer.Resolve(requested));

    [Fact]
    public void EventNamesAreTranslated()
    {
        var loc = new Localizer("ja");
        Assert.Equal("走り幅跳び", loc[EventKind.LongJump.Key()]);
        loc.SetCulture("ca-ES-valencia");
        Assert.Equal("SALT DE LLARGÀRIA", loc[EventKind.LongJump.Key()]);
        loc.SetCulture("es-ES");
        Assert.Equal("SALTO DE LONGITUD", loc[EventKind.LongJump.Key()]);
        loc.SetCulture("en");
        Assert.Equal("LONG JUMP", loc[EventKind.LongJump.Key()]);
    }

    [Fact]
    public void FormatAndFallbackWork()
    {
        var loc = new Localizer("es-ES");
        Assert.Equal("RONDA 3", loc.Format("hud.round", 3));
        Assert.Equal("[nope]", loc["nope"]);
        Assert.False(loc.HasKey("nope"));
        Assert.True(loc.HasKey("hud.score"));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void TextToSpeechTagsAreValid(string culture)
    {
        var tts = Localizer.Load(culture)["lang.tts"];
        Assert.Matches("^[a-z]{2}-[A-Z]{2}$", tts);
    }
}
