namespace PrankAndField.Engine.Graphics;

/// <summary>8-bit RGBA colour.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static Rgba Hex(uint rgb, byte a = 255) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

    public Rgba WithAlpha(byte a) => this with { A = a };

    public Rgba Darker(double factor = 0.7) => new((byte)(R * factor), (byte)(G * factor), (byte)(B * factor), A);

    /// <summary>Moves the colour towards white: 0 keeps it, 1 is white.</summary>
    public Rgba Lighter(double amount = 0.3) => Lerp(this, new Rgba(255, 255, 255, A), amount);

    public static Rgba Lerp(Rgba a, Rgba b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Rgba(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}

/// <summary>2D vector in logical screen units.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public Vec2 Rotate(double degrees)
    {
        double r = degrees * Math.PI / 180.0;
        double c = Math.Cos(r), s = Math.Sin(r);
        return new Vec2(X * c - Y * s, X * s + Y * c);
    }

    public static Vec2 FromAngle(double degrees, double length = 1) => new Vec2(length, 0).Rotate(degrees);
}

/// <summary>Text anchoring for backend-rendered text.</summary>
public enum TextAlign
{
    Left = 0,
    Center = 1,
    Right = 2,
}

/// <summary>Opcodes of the serialized draw list. Every command is a run of doubles starting with its opcode.</summary>
public static class Op
{
    public const double SetColor = 1;   // r g b a (0..255)
    public const double FillRect = 2;   // x y w h
    public const double FillPoly = 3;   // n x1 y1 ... xn yn
    public const double FillCircle = 4; // cx cy r
    public const double Line = 5;       // x1 y1 x2 y2 width
    public const double Text = 6;       // x y size align n  c1..cn  (UTF-16 code units)
    public const double ClipPush = 7;   // x y w h
    public const double ClipPop = 8;    //
    public const double FillEllipse = 9;// cx cy rx ry
    public const double Polyline = 10;  // width n x1 y1 ... xn yn (stroked, open)
    public const double GradientRect = 11; // x y w h  r1 g1 b1 a1  r2 g2 b2 a2  horizontal(0/1): linear gradient fill
    public const double Glow = 12;      // cx cy r  r g b a: radial gradient from the colour at the centre to transparent
    public const double Arc = 13;       // cx cy r startDeg endDeg width: stroked circular arc (clockwise, screen angles)
    public const double AudioSection = 100; // marks the start of audio events: (kind, a, b) triplets
    public const double End = 0;
}
