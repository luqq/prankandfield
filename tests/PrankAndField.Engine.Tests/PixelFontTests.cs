using PrankAndField.Core.Localization;
using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Tests;

public class PixelFontTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("es-ES")]
    [InlineData("ca-ES-valencia")]
    public void LatinLanguagesAreFullyCoveredByThePixelFont(string culture)
    {
        foreach (var (key, value) in Localizer.Load(culture))
        {
            if (key.StartsWith("voice.") || key == "lang.tts") continue; // spoken, not drawn
            string drawn = value.Replace("{0}", "0"); // format placeholders become digits before drawing
            var missing = drawn.Where(ch => !PixelFont.CanDraw(ch)).Distinct().ToList();
            Assert.True(missing.Count == 0, $"{culture}:{key} has undrawable characters: {string.Join(" ", missing)}");
        }
    }

    [Fact]
    public void JapaneseFallsBackToBackendText()
    {
        Assert.False(PixelFont.CanDraw("走り幅跳び"));
        var dl = new DrawList();
        PixelFont.Draw(dl, 10, 10, "走り幅跳び", Palette.White);
        Assert.Contains("text \"走り幅跳び\"", dl.Describe());
    }

    [Fact]
    public void LatinTextIsDrawnAsRectanglesOnly()
    {
        var dl = new DrawList();
        double w = PixelFont.Draw(dl, 0, 0, "PRANK & FIELD", Palette.White);
        var d = dl.Describe().ToList();
        Assert.DoesNotContain(d, s => s.StartsWith("text"));
        Assert.True(d.Count(s => s.StartsWith("rect")) > 30);
        Assert.Equal(PixelFont.Measure("PRANK & FIELD"), w);
        Assert.Equal(13 * 6 - 1, w);
    }

    [Fact]
    public void AccentedCapitalsDrawTheBaseLetterPlusTheAccent()
    {
        var plain = new DrawList();
        PixelFont.Draw(plain, 0, 10, "E", Palette.White);
        var accented = new DrawList();
        PixelFont.Draw(accented, 0, 10, "É", Palette.White);
        Assert.True(accented.CommandCount > plain.CommandCount);
        // accent rows sit above the glyph box (y < 10)
        Assert.Contains(accented.Describe(), s => s.StartsWith("rect") && s.Contains(",8 ") || s.Contains(",9 "));
    }

    [Fact]
    public void LowerCaseIsUpperCased()
    {
        Assert.True(PixelFont.CanDraw("hello ñandú"));
        Assert.Equal("HELLO ÑANDÚ", PixelFont.Upper("hello ñandú"));
    }

    [Fact]
    public void AlignmentShiftsTheStart()
    {
        var left = new DrawList();
        PixelFont.Draw(left, 100, 0, "AB", Palette.White, 1, TextAlign.Left);
        var right = new DrawList();
        PixelFont.Draw(right, 100, 0, "AB", Palette.White, 1, TextAlign.Right);
        double minLeft = MinX(left), minRight = MinX(right);
        Assert.Equal(100, minLeft, 3);
        Assert.Equal(100 - 11, minRight, 3);
    }

    private static double MinX(DrawList dl)
    {
        var arr = dl.ToArray();
        double min = double.MaxValue;
        int i = 0;
        while (arr[i] != Op.End)
        {
            if (arr[i] == Op.SetColor) i += 5;
            else if (arr[i] == Op.FillRect) { min = Math.Min(min, arr[i + 1]); i += 5; }
            else throw new InvalidOperationException("unexpected op");
        }
        return min;
    }
}
