using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Tests;

public class DrawListTests
{
    [Fact]
    public void CommandsAreEncodedInOrderAndTerminated()
    {
        var dl = new DrawList();
        dl.FillRect(1, 2, 3, 4, Palette.White);
        dl.FillCircle(5, 6, 7, Palette.White); // same colour: no second colour command
        dl.Line(0, 0, 10, 10, 2, Palette.HudRed);
        var arr = dl.ToArray();
        Assert.Equal(Op.End, arr[^1]);
        var described = dl.Describe().ToList();
        Assert.Equal(["color 248,248,248,255", "rect 1,2 3x4", "circle 5,6 r=7", "color 248,64,64,255", "line 0,0-10,10"], described);
        Assert.Equal(5, dl.CommandCount);
    }

    [Fact]
    public void OffsetsTranslateCoordinates()
    {
        var dl = new DrawList();
        dl.PushOffset(10, 20);
        dl.PushOffset(1, 1);
        dl.FillRect(0, 0, 2, 2, Palette.Black);
        dl.PopOffset();
        dl.FillCircle(0, 0, 1, Palette.Black);
        dl.PopOffset();
        dl.FillRect(0, 0, 1, 1, Palette.Black);
        var d = dl.Describe().ToList();
        Assert.Contains("rect 11,21 2x2", d);
        Assert.Contains("circle 10,20 r=1", d);
        Assert.Contains("rect 0,0 1x1", d);
    }

    [Fact]
    public void TextIsEncodedAsCodeUnits()
    {
        var dl = new DrawList();
        dl.Text(1, 2, 8, TextAlign.Center, "走り", Palette.White);
        var arr = dl.ToArray();
        int i = 5; // after colour
        Assert.Equal(Op.Text, arr[i]);
        Assert.Equal(2, arr[i + 5]);
        Assert.Equal('走', (char)arr[i + 6]);
        Assert.Equal('り', (char)arr[i + 7]);
        Assert.Contains("text \"走り\"", dl.Describe());
    }

    [Fact]
    public void AudioSectionIsAppendedAfterDrawing()
    {
        var dl = new DrawList();
        dl.FillRect(0, 0, 1, 1, Palette.Black);
        dl.AppendAudio([(1, 2, 3), (4, 5, 6)]);
        var d = dl.Describe().ToList();
        Assert.Equal("audio n=2", d[^1]);
        var arr = dl.ToArray();
        int idx = Array.IndexOf(arr, Op.AudioSection);
        Assert.True(idx > 0);
        Assert.Equal([2, 1, 2, 3, 4, 5, 6, 0], arr[(idx + 1)..]);
    }

    [Fact]
    public void ClearResetsEverything()
    {
        var dl = new DrawList();
        dl.PushOffset(5, 5);
        dl.FillRect(0, 0, 1, 1, Palette.White);
        dl.Clear();
        Assert.Equal(0, dl.CommandCount);
        Assert.Equal(default, dl.Offset);
        Assert.Equal([Op.End], dl.ToArray());
    }

    [Fact]
    public void GradientGlowAndArcAreEncoded()
    {
        var dl = new DrawList();
        dl.PushOffset(10, 5);
        dl.FillGradientRect(0, 0, 20, 10, Palette.SkyTop, Palette.SkyHorizon);
        dl.Glow(3, 4, 6, Palette.Sun);
        dl.Arc(1, 2, 8, 180, 360, 1.5, Palette.HudCyan);
        dl.PopOffset();
        var d = dl.Describe().ToList();
        Assert.Equal(["gradient 10,5 20x10 v", "glow 13,9 r=6", "color 64,232,232,255", "arc 11,7 r=8 180-360"], d);
        var arr = dl.ToArray();
        // the gradient carries both colours and the direction flag; it never touches the current colour
        Assert.Equal(Op.GradientRect, arr[0]);
        Assert.Equal([Palette.SkyTop.R, Palette.SkyTop.G, Palette.SkyTop.B, 255], arr[5..9]);
        Assert.Equal([Palette.SkyHorizon.R, Palette.SkyHorizon.G, Palette.SkyHorizon.B, 255], arr[9..13]);
        Assert.Equal(0, arr[13]);
        Assert.Equal(Op.Glow, arr[14]);
        Assert.Equal(4, dl.CommandCount);
    }

    [Fact]
    public void OutlineClosesTheShape()
    {
        var dl = new DrawList();
        dl.Outline(1, Palette.Outline, [new Vec2(0, 0), new Vec2(4, 0), new Vec2(4, 4)]);
        Assert.Contains("polyline n=4", dl.Describe());
        var arr = dl.ToArray();
        int i = 5; // after colour
        Assert.Equal(Op.Polyline, arr[i]);
        Assert.Equal((0, 0), (arr[i + 3], arr[i + 4]));
        Assert.Equal((0, 0), (arr[i + 9], arr[i + 10]));
    }

    [Fact]
    public void DegenerateShapesAreSkipped()
    {
        var dl = new DrawList();
        dl.FillRect(0, 0, 0, 5, Palette.White);
        dl.FillCircle(0, 0, -1, Palette.White);
        dl.FillPolygon(Palette.White, new Vec2(0, 0), new Vec2(1, 1));
        dl.FillGradientRect(0, 0, 5, 0, Palette.White, Palette.Black);
        dl.Glow(0, 0, 0, Palette.White);
        dl.Glow(0, 0, 5, Palette.White.WithAlpha(0));
        dl.Arc(0, 0, 5, 10, 10, 1, Palette.White);
        // NaN never reaches the painter either (canvas gradients throw on non-finite arguments)
        dl.FillGradientRect(0, 0, double.NaN, 5, Palette.White, Palette.Black);
        dl.FillGradientRect(double.NaN, 0, 5, 5, Palette.White, Palette.Black);
        dl.Glow(0, 0, double.NaN, Palette.White);
        dl.Arc(0, 0, 5, double.NaN, 10, 1, Palette.White);
        Assert.Equal(0, dl.CommandCount);
    }

    [Fact]
    public void ArcAnglesAreSweptFromTheSmallerToTheLarger()
    {
        var dl = new DrawList();
        dl.Arc(10, 10, 5, 30, -30, 1, Palette.White);
        Assert.Contains("arc 10,10 r=5 -30-30", dl.Describe());
    }
}

public class PrimitivesTests
{
    [Fact]
    public void ColourHelpers()
    {
        var c = Rgba.Hex(0x102030);
        Assert.Equal((0x10, 0x20, 0x30, 255), (c.R, c.G, c.B, c.A));
        Assert.Equal(128, c.WithAlpha(128).A);
        Assert.Equal(8, c.Darker(0.5).R);
        Assert.Equal(255, c.Lighter(1).G);
        Assert.Equal(c.A, c.Lighter(0.5).A);
        Assert.Equal(Rgba.Hex(0x808080), Rgba.Lerp(Rgba.Hex(0x000000), Rgba.Hex(0xFFFFFF), 0.5) with { R = 0x80, G = 0x80, B = 0x80 });
    }

    [Fact]
    public void VectorRotation()
    {
        var v = new Vec2(1, 0).Rotate(90);
        Assert.Equal(0, v.X, 9);
        Assert.Equal(1, v.Y, 9);
        Assert.Equal(5, new Vec2(3, 4).Length);
    }
}
