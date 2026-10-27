namespace PrankAndField.Engine.Graphics;

/// <summary>
/// Retained list of vector drawing commands for one frame, encoded as a flat array of doubles so
/// it crosses the .NET / JavaScript boundary in a single copy. Coordinates are logical screen
/// units (see <see cref="Screen"/>); the backend scales them to the real canvas.
/// </summary>
public sealed class DrawList
{
    private readonly List<double> _buf = new(8192);
    private readonly Stack<Vec2> _offsets = new();
    private Rgba _color = new(0, 0, 0, 0);
    private bool _colorSet;

    public int CommandCount { get; private set; }

    /// <summary>Current translation applied to every coordinate written (camera / strip origin).</summary>
    public Vec2 Offset { get; private set; }

    public void Clear()
    {
        _buf.Clear();
        _offsets.Clear();
        Offset = default;
        _colorSet = false;
        CommandCount = 0;
    }

    public void PushOffset(double dx, double dy)
    {
        _offsets.Push(Offset);
        Offset = new Vec2(Offset.X + dx, Offset.Y + dy);
    }

    public void PopOffset()
    {
        Offset = _offsets.Count > 0 ? _offsets.Pop() : default;
    }

    public void SetColor(Rgba c)
    {
        if (_colorSet && c == _color) return;
        _color = c;
        _colorSet = true;
        _buf.Add(Op.SetColor);
        _buf.Add(c.R);
        _buf.Add(c.G);
        _buf.Add(c.B);
        _buf.Add(c.A);
        CommandCount++;
    }

    public void FillRect(double x, double y, double w, double h, Rgba c)
    {
        if (w <= 0 || h <= 0) return;
        SetColor(c);
        _buf.Add(Op.FillRect);
        _buf.Add(x + Offset.X);
        _buf.Add(y + Offset.Y);
        _buf.Add(w);
        _buf.Add(h);
        CommandCount++;
    }

    /// <summary>
    /// Rectangle filled with a linear gradient from <paramref name="from"/> to <paramref name="to"/>,
    /// top to bottom (or left to right when <paramref name="horizontal"/>). The gradient carries its own
    /// colours, so it never changes the current fill colour.
    /// </summary>
    public void FillGradientRect(double x, double y, double w, double h, Rgba from, Rgba to, bool horizontal = false)
    {
        if (w <= 0 || h <= 0) return;
        _buf.Add(Op.GradientRect);
        _buf.Add(x + Offset.X);
        _buf.Add(y + Offset.Y);
        _buf.Add(w);
        _buf.Add(h);
        _buf.Add(from.R); _buf.Add(from.G); _buf.Add(from.B); _buf.Add(from.A);
        _buf.Add(to.R); _buf.Add(to.G); _buf.Add(to.B); _buf.Add(to.A);
        _buf.Add(horizontal ? 1 : 0);
        CommandCount++;
    }

    /// <summary>Soft radial light: the colour at the centre fading to transparent at radius <paramref name="r"/>.</summary>
    public void Glow(double cx, double cy, double r, Rgba c)
    {
        if (r <= 0 || c.A == 0) return;
        _buf.Add(Op.Glow);
        _buf.Add(cx + Offset.X);
        _buf.Add(cy + Offset.Y);
        _buf.Add(r);
        _buf.Add(c.R); _buf.Add(c.G); _buf.Add(c.B); _buf.Add(c.A);
        CommandCount++;
    }

    /// <summary>Stroked circular arc from <paramref name="startDeg"/> to <paramref name="endDeg"/> (0 = right, 90 = down).</summary>
    public void Arc(double cx, double cy, double r, double startDeg, double endDeg, double width, Rgba c)
    {
        if (r <= 0 || width <= 0 || startDeg == endDeg) return;
        SetColor(c);
        _buf.Add(Op.Arc);
        _buf.Add(cx + Offset.X);
        _buf.Add(cy + Offset.Y);
        _buf.Add(r);
        _buf.Add(startDeg);
        _buf.Add(endDeg);
        _buf.Add(width);
        CommandCount++;
    }

    public void FillPolygon(Rgba c, ReadOnlySpan<Vec2> points)
    {
        if (points.Length < 3) return;
        SetColor(c);
        _buf.Add(Op.FillPoly);
        _buf.Add(points.Length);
        foreach (var p in points)
        {
            _buf.Add(p.X + Offset.X);
            _buf.Add(p.Y + Offset.Y);
        }
        CommandCount++;
    }

    public void FillPolygon(Rgba c, params Vec2[] points) => FillPolygon(c, points.AsSpan());

    public void FillCircle(double cx, double cy, double r, Rgba c)
    {
        if (r <= 0) return;
        SetColor(c);
        _buf.Add(Op.FillCircle);
        _buf.Add(cx + Offset.X);
        _buf.Add(cy + Offset.Y);
        _buf.Add(r);
        CommandCount++;
    }

    public void FillEllipse(double cx, double cy, double rx, double ry, Rgba c)
    {
        if (rx <= 0 || ry <= 0) return;
        SetColor(c);
        _buf.Add(Op.FillEllipse);
        _buf.Add(cx + Offset.X);
        _buf.Add(cy + Offset.Y);
        _buf.Add(rx);
        _buf.Add(ry);
        CommandCount++;
    }

    public void Line(double x1, double y1, double x2, double y2, double width, Rgba c)
    {
        SetColor(c);
        _buf.Add(Op.Line);
        _buf.Add(x1 + Offset.X);
        _buf.Add(y1 + Offset.Y);
        _buf.Add(x2 + Offset.X);
        _buf.Add(y2 + Offset.Y);
        _buf.Add(width);
        CommandCount++;
    }

    public void Line(Vec2 a, Vec2 b, double width, Rgba c) => Line(a.X, a.Y, b.X, b.Y, width, c);

    public void Polyline(double width, Rgba c, ReadOnlySpan<Vec2> points)
    {
        if (points.Length < 2) return;
        SetColor(c);
        _buf.Add(Op.Polyline);
        _buf.Add(width);
        _buf.Add(points.Length);
        foreach (var p in points)
        {
            _buf.Add(p.X + Offset.X);
            _buf.Add(p.Y + Offset.Y);
        }
        CommandCount++;
    }

    /// <summary>Stroked outline of a closed shape (the first point is repeated at the end).</summary>
    public void Outline(double width, Rgba c, ReadOnlySpan<Vec2> points)
    {
        if (points.Length < 2) return;
        Span<Vec2> closed = stackalloc Vec2[points.Length + 1];
        points.CopyTo(closed);
        closed[^1] = points[0];
        Polyline(width, c, closed);
    }

    /// <summary>Backend text (system font). Used for scripts the built-in pixel font cannot draw.</summary>
    public void Text(double x, double y, double size, TextAlign align, string text, Rgba c)
    {
        if (string.IsNullOrEmpty(text)) return;
        SetColor(c);
        _buf.Add(Op.Text);
        _buf.Add(x + Offset.X);
        _buf.Add(y + Offset.Y);
        _buf.Add(size);
        _buf.Add((int)align);
        _buf.Add(text.Length);
        foreach (char ch in text) _buf.Add(ch);
        CommandCount++;
    }

    public void PushClip(double x, double y, double w, double h)
    {
        _buf.Add(Op.ClipPush);
        _buf.Add(x + Offset.X);
        _buf.Add(y + Offset.Y);
        _buf.Add(w);
        _buf.Add(h);
        CommandCount++;
    }

    public void PopClip()
    {
        _buf.Add(Op.ClipPop);
        CommandCount++;
    }

    /// <summary>Appends an audio section: triplets (kind, a, b) consumed by the audio backend.</summary>
    public void AppendAudio(ReadOnlySpan<(double Kind, double A, double B)> events)
    {
        if (events.Length == 0) return;
        _buf.Add(Op.AudioSection);
        _buf.Add(events.Length);
        foreach (var e in events)
        {
            _buf.Add(e.Kind);
            _buf.Add(e.A);
            _buf.Add(e.B);
        }
    }

    public int Length => _buf.Count;

    public double[] ToArray()
    {
        var arr = new double[_buf.Count + 1];
        _buf.CopyTo(arr);
        arr[^1] = Op.End;
        return arr;
    }

    /// <summary>
    /// Walks an encoded buffer and yields the opcode and the index of every command, up to the end marker.
    /// Lets hosts and tests inspect a frame without decoding it by hand.
    /// </summary>
    public static IEnumerable<(double Op, int Index)> Walk(double[] a)
    {
        int i = 0;
        while (i < a.Length)
        {
            double op = a[i];
            if (op == Op.End) yield break;
            yield return (op, i);
            switch (op)
            {
                case Op.SetColor: i += 5; break;
                case Op.FillRect: i += 5; break;
                case Op.FillPoly: i += 2 + 2 * (int)a[i + 1]; break;
                case Op.FillCircle: i += 4; break;
                case Op.FillEllipse: i += 5; break;
                case Op.Line: i += 6; break;
                case Op.Polyline: i += 3 + 2 * (int)a[i + 2]; break;
                case Op.GradientRect: i += 14; break;
                case Op.Glow: i += 8; break;
                case Op.Arc: i += 7; break;
                case Op.Text: i += 6 + (int)a[i + 5]; break;
                case Op.ClipPush: i += 5; break;
                case Op.ClipPop: i += 1; break;
                case Op.AudioSection: i += 2 + 3 * (int)a[i + 1]; break;
                default: yield break;
            }
        }
    }

    /// <summary>Decodes the buffer into readable commands (for tests and debugging).</summary>
    public IEnumerable<string> Describe()
    {
        var a = ToArray();
        int i = 0;
        while (i < a.Length)
        {
            double op = a[i];
            if (op == Op.End) yield break;
            switch (op)
            {
                case Op.SetColor: yield return $"color {a[i + 1]},{a[i + 2]},{a[i + 3]},{a[i + 4]}"; i += 5; break;
                case Op.FillRect: yield return $"rect {a[i + 1]:0.##},{a[i + 2]:0.##} {a[i + 3]:0.##}x{a[i + 4]:0.##}"; i += 5; break;
                case Op.FillPoly: { int n = (int)a[i + 1]; yield return $"poly n={n}"; i += 2 + 2 * n; break; }
                case Op.FillCircle: yield return $"circle {a[i + 1]:0.##},{a[i + 2]:0.##} r={a[i + 3]:0.##}"; i += 4; break;
                case Op.FillEllipse: yield return $"ellipse {a[i + 1]:0.##},{a[i + 2]:0.##}"; i += 5; break;
                case Op.Line: yield return $"line {a[i + 1]:0.##},{a[i + 2]:0.##}-{a[i + 3]:0.##},{a[i + 4]:0.##}"; i += 6; break;
                case Op.Polyline: { int n = (int)a[i + 2]; yield return $"polyline n={n}"; i += 3 + 2 * n; break; }
                case Op.GradientRect: yield return $"gradient {a[i + 1]:0.##},{a[i + 2]:0.##} {a[i + 3]:0.##}x{a[i + 4]:0.##} {(a[i + 13] == 1 ? "h" : "v")}"; i += 14; break;
                case Op.Glow: yield return $"glow {a[i + 1]:0.##},{a[i + 2]:0.##} r={a[i + 3]:0.##}"; i += 8; break;
                case Op.Arc: yield return $"arc {a[i + 1]:0.##},{a[i + 2]:0.##} r={a[i + 3]:0.##} {a[i + 4]:0.#}-{a[i + 5]:0.#}"; i += 7; break;
                case Op.Text:
                    {
                        int n = (int)a[i + 5];
                        var chars = new char[n];
                        for (int k = 0; k < n; k++) chars[k] = (char)a[i + 6 + k];
                        yield return $"text \"{new string(chars)}\"";
                        i += 6 + n;
                        break;
                    }
                case Op.ClipPush: yield return "clip push"; i += 5; break;
                case Op.ClipPop: yield return "clip pop"; i += 1; break;
                case Op.AudioSection: { int n = (int)a[i + 1]; yield return $"audio n={n}"; i += 2 + 3 * n; break; }
                default: yield return $"?? {op}"; yield break;
            }
        }
    }
}

/// <summary>Logical screen: the arcade ran at 256x224. We keep the same aspect and grid.</summary>
public static class Screen
{
    public const double Width = 256;
    public const double Height = 224;
}
