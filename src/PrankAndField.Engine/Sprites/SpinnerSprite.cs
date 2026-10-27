using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Sprites;

/// <summary>
/// The hammer thrower turning in the circle. The usual stick athlete is modelled in three dimensions
/// (forward, left, up) and projected with the same oblique view as the field (the hammer orbit is an
/// ellipse 22 wide and 7 tall), so the body, the hair, the face and the outstretched arms turn
/// continuously with the ball instead of flipping between a left and a right facing.
/// </summary>
public static class SpinnerSprite
{
    /// <summary>Screen pixels down per unit of depth towards the camera (7 / 22, the hammer orbit).</summary>
    public const double Tilt = 7.0 / 22.0;

    private const double HipY = 10.5;
    private const double TorsoLen = 7.0;
    private const double HeadR = 3.4;
    private const double Thigh = 5.6;
    private const double Shin = 5.0;
    private const double Upper = 4.4;
    private const double Fore = 4.0;
    private const double Lean = -8;        // degrees: the thrower leans back against the pull of the ball
    private const double Crouch = 1.6;     // hips lowered, knees bent

    /// <summary>Radius of the ball's orbit on the ground plane (pixels).</summary>
    public const double OrbitRadius = 22;

    /// <summary>
    /// Height of the ball above the ground along its orbit (pixels). Like a real hammer the orbit is inclined:
    /// low in front of the thrower, where it is released, and high behind, where it swings over the head
    /// instead of disappearing behind the body.
    /// </summary>
    public static double OrbitHeight(double headingDeg) => 11 - 8 * Math.Sin(headingDeg * Math.PI / 180.0);

    private static readonly P3 Grip = new(6.2, 0, 12.2);

    private readonly record struct P3(double F, double S, double U)
    {
        public static P3 operator +(P3 a, P3 b) => new(a.F + b.F, a.S + b.S, a.U + b.U);
        public static P3 operator -(P3 a, P3 b) => new(a.F - b.F, a.S - b.S, a.U - b.U);
        public static P3 operator *(P3 a, double k) => new(a.F * k, a.S * k, a.U * k);
        public double Dot(P3 o) => F * o.F + S * o.S + U * o.U;
        public double Length => Math.Sqrt(Dot(this));
        public P3 Unit() => this * (1.0 / Math.Max(1e-9, Length));
        public P3 Mirror() => new(F, -S, U);
    }

    /// <summary>
    /// Draws the thrower with the feet at (x, groundY) facing <paramref name="headingDeg"/> (0 = right, towards
    /// the sector; 90 = towards the camera). Returns the screen position of the hands, where the wire starts.
    /// </summary>
    public static Vec2 Draw(DrawList dl, double x, double groundY, double headingDeg, AthleteColors colors, int number)
    {
        double h = headingDeg * Math.PI / 180.0;
        double ch = Math.Cos(h), sh = Math.Sin(h);

        // Body space: f forward, s to the athlete's left, u up. The ground plane is seen slightly from above.
        Vec2 Pt(P3 p) => new(x + p.F * ch + p.S * sh, groundY - p.U + (p.F * sh - p.S * ch) * Tilt);
        double Depth(P3 p) => p.F * sh - p.S * ch;                    // positive = towards the camera
        var view = new P3(sh, -ch, Tilt).Unit();                        // direction towards the viewer

        double hipU = HipY - Crouch;
        double lean = Lean * Math.PI / 180.0;
        var hipC = new P3(0, 0, hipU);
        var shoulderC = new P3(Math.Sin(lean) * TorsoLen, 0, hipU + Math.Cos(lean) * TorsoLen);
        var headC = shoulderC + new P3(Math.Sin(lean) * (HeadR + 0.2), 0, Math.Cos(lean) * (HeadR + 0.2));
        var grip = Grip;

        // --- limbs, ordered by depth ---
        var legL = Leg(hipU, +1.5, 24);
        var legR = Leg(hipU, -1.5, -18);
        double dL = Depth(legL.Hip), dR = Depth(legR.Hip);
        double shadeL = Shade(dL - dR), shadeR = Shade(dR - dL);
        var armL = Arm(shoulderC, +1, grip);
        var armR = Arm(shoulderC, -1, grip);
        double armShadeL = Shade(Depth(armL.Shoulder) - Depth(armR.Shoulder));
        double armShadeR = Shade(Depth(armR.Shoulder) - Depth(armL.Shoulder));

        // far leg and the arm segments that pass behind the torso
        if (dL <= dR) DrawLeg(dl, Pt, legL, colors, shadeL); else DrawLeg(dl, Pt, legR, colors, shadeR);
        DrawArm(dl, Pt, Depth, armL, colors.Skin.Darker(armShadeL), behind: true);
        DrawArm(dl, Pt, Depth, armR, colors.Skin.Darker(armShadeR), behind: true);

        // --- torso: shorts and singlet are boxes, drawn as the hull of their projected corners ---
        dl.FillPolygon(colors.Shorts, Hull(Pt, Box(-2.6, 2.6, 2.5, hipU - 2.8, hipU + 0.4, 0)));
        dl.FillPolygon(colors.Singlet, Hull(Pt, Box(-2.6, 2.6, 2.7, hipU - 0.5, shoulderC.U + 0.5, shoulderC.F, 3.2)));
        // top of the singlet (trim) and the number on the chest
        var topF = shoulderC.F;
        dl.FillPolygon(colors.Shorts,
            Pt(new P3(topF - 2.6, 3.1, shoulderC.U + 0.3)), Pt(new P3(topF + 2.6, 3.1, shoulderC.U + 0.3)),
            Pt(new P3(topF + 2.6, -3.1, shoulderC.U + 0.3)), Pt(new P3(topF - 2.6, -3.1, shoulderC.U + 0.3)));
        if (number > 0 && sh > -0.15)
        {
            double uc = (hipU + shoulderC.U) / 2 + 0.3;
            double fc = 2.1 + (shoulderC.F / 2);
            dl.FillPolygon(colors.Shorts,
                Pt(new P3(fc, 0.9, uc + 1.2)), Pt(new P3(fc, -0.9, uc + 1.2)), Pt(new P3(fc, -0.9, uc - 1.2)), Pt(new P3(fc, 0.9, uc - 1.2)));
        }

        // near leg
        if (dL > dR) DrawLeg(dl, Pt, legL, colors, shadeL); else DrawLeg(dl, Pt, legR, colors, shadeR);

        // --- head ---
        DrawHead(dl, Pt, view, headC, colors);

        // arm segments in front of the torso
        DrawArm(dl, Pt, Depth, armL, colors.Skin.Darker(armShadeL), behind: false);
        DrawArm(dl, Pt, Depth, armR, colors.Skin.Darker(armShadeR), behind: false);

        return Pt(grip);
    }

    /// <summary>Screen position of the hands (where the wire starts) without drawing anything.</summary>
    public static Vec2 HandsAt(double x, double groundY, double headingDeg)
    {
        double h = headingDeg * Math.PI / 180.0;
        double ch = Math.Cos(h), sh = Math.Sin(h);
        var grip = Grip;
        return new Vec2(x + grip.F * ch + grip.S * sh, groundY - grip.U + (grip.F * sh - grip.S * ch) * Tilt);
    }

    /// <summary>Screen position of the ball for a given heading and height above the ground (pixels).</summary>
    public static Vec2 BallAt(double x, double groundY, double headingDeg, double height, double radius = OrbitRadius)
    {
        double h = headingDeg * Math.PI / 180.0;
        return new Vec2(x + Math.Cos(h) * radius, groundY - height + Math.Sin(h) * radius * Tilt);
    }

    /// <summary>Darkening factor for a limb given how much nearer the camera it is than its pair: 1 (near) to 0.78 (far), blended so the swap never pops.</summary>
    private static double Shade(double depthLead)
    {
        double t = Math.Clamp((depthLead + 1.5) / 3.0, 0, 1);
        return 0.78 + 0.22 * t;
    }

    private readonly record struct LegPose(P3 Hip, P3 Knee, P3 Ankle, P3 Toe);

    /// <summary>A bent leg whose foot rests on the ground: the shin angle is solved from the hip height.</summary>
    private static LegPose Leg(double hipU, double side, double thighDeg)
    {
        double t = thighDeg * Math.PI / 180.0;
        var hip = new P3(0, side, hipU);
        var knee = hip + new P3(Math.Sin(t) * Thigh, 0, -Math.Cos(t) * Thigh);
        double c = Math.Clamp((knee.U) / Shin, -1, 1);
        double shinAngle = -Math.Acos(c);            // the shin leans back so the knee bends the right way
        var ankle = knee + new P3(Math.Sin(shinAngle) * Shin, 0, -Math.Cos(shinAngle) * Shin);
        var toe = ankle + new P3(2.0, 0, 0);
        return new LegPose(hip, knee, ankle, toe);
    }

    private static void DrawLeg(DrawList dl, Func<P3, Vec2> pt, LegPose leg, AthleteColors colors, double shade)
    {
        var skin = colors.Skin.Darker(shade);
        var shoe = Palette.Shoe.Darker(0.8 + 0.2 * (shade - 0.78) / 0.22);
        dl.Line(pt(leg.Hip), pt(leg.Knee), 2.2, skin);
        dl.Line(pt(leg.Knee), pt(leg.Ankle), 1.9, skin);
        dl.Line(pt(leg.Ankle), pt(leg.Toe), 1.6, shoe);
    }

    private readonly record struct ArmPose(P3 Shoulder, P3 Elbow, P3 Hand);

    /// <summary>Both arms reach for the grip in front; the elbows bow a little outwards.</summary>
    private static ArmPose Arm(P3 shoulderC, double side, P3 grip)
    {
        var shoulder = shoulderC + new P3(0, 3.0 * side, -0.3);
        var toGrip = (grip - shoulder).Unit();
        var elbow = shoulder + toGrip * Upper + new P3(0, 0.9 * side, 0.5);
        var hand = elbow + (grip + new P3(0, 0.5 * side, 0) - elbow).Unit() * Fore;
        return new ArmPose(shoulder, elbow, hand);
    }

    private static void DrawArm(DrawList dl, Func<P3, Vec2> pt, Func<P3, double> depth, ArmPose arm, Rgba skin, bool behind)
    {
        double upperDepth = (depth(arm.Shoulder) + depth(arm.Elbow)) / 2;
        double foreDepth = (depth(arm.Elbow) + depth(arm.Hand)) / 2;
        if ((upperDepth < 0) == behind) dl.Line(pt(arm.Shoulder), pt(arm.Elbow), 1.7, skin);
        if ((foreDepth < 0) == behind)
        {
            dl.Line(pt(arm.Elbow), pt(arm.Hand), 1.5, skin);
            var hp = pt(arm.Hand);
            dl.FillCircle(hp.X, hp.Y, 0.9, skin);
        }
    }

    private static void DrawHead(DrawList dl, Func<P3, Vec2> pt, P3 view, P3 c, AthleteColors colors)
    {
        var hc = pt(c);
        dl.FillCircle(hc.X, hc.Y, HeadR, colors.Skin);

        // hair: a cap over the top and back of the head. Its rim is sampled in 3D; the hidden part of the rim
        // is pushed out to the silhouette, so the polygon is the visible hair from any direction.
        var axis = new P3(-0.45, 0, 0.89).Unit();
        var e1 = new P3(0.89, 0, 0.45).Unit();
        var e2 = new P3(0, 1, 0);
        const int N = 16;
        const double capDeg = 68;
        double ca = Math.Cos(capDeg * Math.PI / 180.0), sa = Math.Sin(capDeg * Math.PI / 180.0);
        var hair = new Vec2[N];
        for (int i = 0; i < N; i++)
        {
            double phi = i * 2 * Math.PI / N;
            var p = axis * ca + (e1 * Math.Cos(phi) + e2 * Math.Sin(phi)) * sa;
            var q = pt(c + p * HeadR) - hc;
            if (p.Dot(view) > 0.02)
            {
                hair[i] = hc + q * 1.04;
            }
            else
            {
                double len = Math.Max(1e-6, q.Length);
                hair[i] = hc + q * (HeadR * 1.1 / len);
            }
        }
        dl.FillPolygon(colors.Hair, hair);

        // eyes: a small square on each side of the face, only the ones turned towards the camera
        foreach (double side in new[] { 1.0, -1.0 })
        {
            var e = new P3(2.4, 1.6 * side, 0.7);
            if (e.Unit().Dot(view) <= 0.2) continue;
            var ep = pt(c + e);
            dl.FillRect(ep.X - 0.45, ep.Y - 0.45, 0.9, 0.9, Palette.Black);
        }

        // the mustache, one half per cheek, visible when that cheek faces the camera
        bool faceVisible = view.F > -0.2;
        var half = new[]
        {
            new P3(0.5, 2.7, -0.6), new P3(2.9, 1.7, -0.4), new P3(3.5, 0, -0.4),
            new P3(3.4, 0, -1.8), new P3(2.8, 1.5, -1.6), new P3(0.9, 2.6, -1.3),
        };
        foreach (double side in new[] { 1.0, -1.0 })
        {
            var normal = new P3(0.8, 0.55 * side, -0.2).Unit();
            if (normal.Dot(view) <= -0.08) continue;
            var poly = new Vec2[half.Length];
            for (int i = 0; i < half.Length; i++) poly[i] = pt(c + (side > 0 ? half[i] : half[i].Mirror()));
            dl.FillPolygon(Palette.Mustache, poly);
        }

        // nose
        if (faceVisible)
        {
            var np = pt(c + new P3(3.3, 0, -0.2));
            dl.FillCircle(np.X, np.Y, 0.7, colors.Skin.Darker(0.88));
        }
    }

    /// <summary>The eight corners of a box in body space; the top may be shifted forward and wider than the bottom.</summary>
    private static P3[] Box(double fMin, double fMax, double sHalf, double uBottom, double uTop, double topShift, double topHalf = double.NaN)
    {
        if (double.IsNaN(topHalf)) topHalf = sHalf;
        return
        [
            new(fMin, -sHalf, uBottom), new(fMax, -sHalf, uBottom), new(fMax, sHalf, uBottom), new(fMin, sHalf, uBottom),
            new(fMin + topShift, -topHalf, uTop), new(fMax + topShift, -topHalf, uTop), new(fMax + topShift, topHalf, uTop), new(fMin + topShift, topHalf, uTop),
        ];
    }

    /// <summary>Convex hull (monotone chain) of the projected points, in drawing order.</summary>
    private static Vec2[] Hull(Func<P3, Vec2> pt, P3[] corners)
    {
        var pts = new Vec2[corners.Length];
        for (int i = 0; i < corners.Length; i++) pts[i] = pt(corners[i]);
        Array.Sort(pts, (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var hull = new List<Vec2>(2 * pts.Length);
        static double Cross(Vec2 o, Vec2 a, Vec2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        foreach (var p in pts)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        int lowerCount = hull.Count + 1;
        for (int i = pts.Length - 2; i >= 0; i--)
        {
            var p = pts[i];
            while (hull.Count >= lowerCount && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull.ToArray();
    }
}
