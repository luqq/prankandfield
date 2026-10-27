using PrankAndField.Core;
using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Sprites;

/// <summary>Joint angles describing a body configuration (degrees). Limb angles are measured from straight down, forward positive.</summary>
public readonly record struct BodyPose(
    double ThighNear, double KneeNear, double ThighFar, double KneeFar,
    double ArmNear, double ElbowNear, double ArmFar, double ElbowFar,
    double Lean, double Lower, double Bounce = 0)
{
    public static BodyPose Mix(BodyPose a, BodyPose b, double t) => new(
        L(a.ThighNear, b.ThighNear, t), L(a.KneeNear, b.KneeNear, t), L(a.ThighFar, b.ThighFar, t), L(a.KneeFar, b.KneeFar, t),
        L(a.ArmNear, b.ArmNear, t), L(a.ElbowNear, b.ElbowNear, t), L(a.ArmFar, b.ArmFar, t), L(a.ElbowFar, b.ElbowFar, t),
        L(a.Lean, b.Lean, t), L(a.Lower, b.Lower, t), L(a.Bounce, b.Bounce, t));

    private static double L(double a, double b, double t) => a + (b - a) * t;
}

public readonly record struct AthleteColors(Rgba Hair, Rgba Shorts, Rgba Singlet, Rgba Skin)
{
    public static AthleteColors Player1 => new(Palette.HairBrown, Palette.ShortsP1, Palette.Singlet, Palette.Skin);
    public static AthleteColors Player2 => new(Palette.HairBlack, Palette.ShortsP2, Palette.Singlet, Palette.SkinP2);
    public static AthleteColors Cpu => new(Palette.HairBlack, Palette.ShortsP2, Rgba.Hex(0xF8E0A0), Rgba.Hex(0xC89060));

    public static AthleteColors For(Athlete a) => a.IsCpu ? Cpu : (a.PlayerIndex == 0 ? Player1 : Player2);
}

/// <summary>
/// The mustachioed athlete, built from circles, polygons and thick lines. Local space has the feet at
/// (0,0) with y pointing up; <see cref="Draw"/> converts to screen space (y down) at the given anchor.
/// Height is about 24 logical pixels like the arcade sprite.
/// </summary>
public static class AthleteSprite
{
    public const double Height = 24;
    private const double HipY = 10.5;
    private const double ShoulderY = 17.5;
    private const double HeadY = 21.0;
    private const double HeadR = 3.4;
    private const double Thigh = 5.6;
    private const double Shin = 5.0;
    private const double Upper = 4.4;
    private const double Fore = 4.0;

    public static BodyPose PoseFor(Pose pose, double phase, double time)
    {
        switch (pose)
        {
            case Pose.Idle:
                return new BodyPose(3, -4, -3, -4, 8, 10, -8, 10, 0, 0);
            case Pose.Crouch:
                return new BodyPose(85, -120, -35, -70, 75, 0, 70, 0, 65, 9);
            case Pose.Set:
                return new BodyPose(65, -80, -45, -40, 80, 0, 78, 0, 55, 5);
            case Pose.Run:
                return RunCycle(phase, 48, 12, 80);
            case Pose.Walk:
                return RunCycle(phase, 22, 4, 35) with { Bounce = 0 };
            case Pose.Jump:
                return new BodyPose(75, -95, -25, -15, -55, 70, 140, -20, 12, 0);
            case Pose.Fly:
                return new BodyPose(60, -80, 50, -70, 150, -30, 140, -40, 5, 0);
            case Pose.Land:
                return new BodyPose(85, -30, 80, -25, 100, 20, 95, 20, 25, 0);
            case Pose.Hurdle:
                return new BodyPose(88, -6, -20, -105, 95, 20, -45, 70, 28, 0);
            case Pose.Stumble:
                return new BodyPose(40, -60, -30, -40, 120, -40, 100, -60, 50, 3);
            case Pose.Fall:
                return new BodyPose(10, -10, 0, -5, 160, -10, 150, -20, 85, 14);
            case Pose.Throw:
                return RunCycle(phase, 30, 8, 60) with { ArmNear = -150, ElbowNear = -10, ArmFar = 70, ElbowFar = 40, Lean = -15 };
            case Pose.Release:
                return new BodyPose(70, -40, -40, -10, 130, 0, 20, -20, 25, 0);
            case Pose.Spin:
                return new BodyPose(30, -25, -30, -25, 92, 0, 88, 0, 15, 2);
            case Pose.Flop:
                {
                    // Fosbury flop; phase is the flight progress: arched back over the bar first,
                    // then the legs kick up (pike) so the feet clear it too.
                    var arch = new BodyPose(-35, -20, -30, -25, 170, -10, 165, -15, -35, 0);
                    var pike = new BodyPose(70, -30, 60, -25, 120, -20, 110, -20, 10, 0);
                    double t = Math.Clamp((phase - 0.55) / 0.35, 0, 1);
                    return BodyPose.Mix(arch, pike, t * t * (3 - 2 * t));
                }
            case Pose.Celebrate:
                {
                    double hop = Math.Max(0, Math.Sin(time * 8)) * 2;
                    return new BodyPose(5, -8, -5, -8, 175, -15, -175, 15, 0, -hop);
                }
            case Pose.Dizzy:
                {
                    double w = Math.Sin(time * 6) * 25;
                    return new BodyPose(20, -15, -20, -15, 95 + w, -20, 95 - w, -20, w * 0.6, 2);
                }
            default:
                return new BodyPose(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    /// <summary>Running cycle. Knees bend backwards (negative), elbows bend forwards (positive) like a real runner.</summary>
    private static BodyPose RunCycle(double phase, double amplitude, double lean, double elbowBend)
    {
        double f = phase * 2 * Math.PI;
        double thighNear = amplitude * Math.Sin(f);
        double thighFar = amplitude * Math.Sin(f + Math.PI);
        double kneeNear = -(18 + 70 * Math.Max(0, -Math.Sin(f)));
        double kneeFar = -(18 + 70 * Math.Max(0, Math.Sin(f)));
        double armNear = -0.9 * thighNear;
        double armFar = -0.9 * thighFar;
        double bounce = 0.7 * Math.Abs(Math.Sin(f));
        return new BodyPose(thighNear, kneeNear, thighFar, kneeFar, armNear, elbowBend, armFar, elbowBend, lean, 0, bounce);
    }

    /// <summary>Draws the athlete with feet anchored at (x, groundY) on screen.</summary>
    public static void Draw(DrawList dl, double x, double groundY, Athlete athlete, double time, double scale = 1)
    {
        var colors = AthleteColors.For(athlete);
        var pose = PoseFor(athlete.Pose, athlete.AnimPhase, time);
        Draw(dl, x, groundY, pose, colors, facing: 1, rotation: athlete.Rotation, number: athlete.PlayerIndex + 1, scale);
    }

    public static void Draw(DrawList dl, double x, double groundY, BodyPose pose, AthleteColors colors, double facing, double rotation, int number, double scale = 1)
    {
        // Local -> screen: screen = anchor + R(rotation) * (facing*lx*scale, -ly*scale)
        double rot = rotation * Math.PI / 180.0;
        double cr = Math.Cos(rot), sr = Math.Sin(rot);
        Vec2 ToScreen(double lx, double ly)
        {
            double px = facing * lx * scale;
            double py = -(ly - pose.Lower) * scale + pose.Bounce * -scale;
            // rotate about the body centre (0, 12)
            double cx = 0, cy = -12 * scale;
            double rx = cx + (px - cx) * cr - (py - cy) * sr;
            double ry = cy + (px - cx) * sr + (py - cy) * cr;
            return new Vec2(x + rx, groundY + ry);
        }

        Vec2 Dir(double deg) => new(Math.Sin(deg * Math.PI / 180.0), -Math.Cos(deg * Math.PI / 180.0));

        double lean = pose.Lean * Math.PI / 180.0;
        // Torso top position (shoulders) leaning forward from the hip
        double torsoLen = ShoulderY - HipY;
        double shX = Math.Sin(lean) * torsoLen;
        double shY = HipY + Math.Cos(lean) * torsoLen;
        double headX = shX + Math.Sin(lean) * (HeadY - ShoulderY);
        double headY = shY + Math.Cos(lean) * (HeadY - ShoulderY);

        // --- far limbs (shaded) ---
        DrawLeg(dl, ToScreen, Dir, -0.9, pose.ThighFar, pose.KneeFar, colors.Skin.Darker(0.78), Palette.Shoe.Darker(0.8), scale);
        DrawArm(dl, ToScreen, Dir, shX - 0.6, shY, pose.ArmFar, pose.ElbowFar, colors.Skin.Darker(0.78), scale);

        // --- torso / singlet ---
        var torso = new[]
        {
            ToScreen(-2.8, HipY - 0.5), ToScreen(2.8, HipY - 0.5),
            ToScreen(shX + 3.0, shY + 0.5), ToScreen(shX - 3.0, shY + 0.5),
        };
        dl.FillPolygon(colors.Singlet, torso);
        // shorts
        var shorts = new[]
        {
            ToScreen(-3.0, HipY + 0.4), ToScreen(3.0, HipY + 0.4),
            ToScreen(2.8, HipY - 2.8), ToScreen(-2.8, HipY - 2.8),
        };
        dl.FillPolygon(colors.Shorts, shorts);
        // singlet trim and number
        dl.Line(ToScreen(shX - 2.6, shY - 0.2), ToScreen(shX + 2.6, shY - 0.2), 0.7 * scale, colors.Shorts);
        if (number > 0)
        {
            var c = ToScreen(shX * 0.5 + facing * 0.3, (HipY + shY) / 2 + 0.3);
            dl.FillRect(c.X - 0.9 * scale, c.Y - 1.2 * scale, 1.8 * scale, 2.4 * scale, colors.Shorts);
        }

        // --- near leg ---
        DrawLeg(dl, ToScreen, Dir, 0.9, pose.ThighNear, pose.KneeNear, colors.Skin, Palette.Shoe, scale);

        // --- head ---
        var hc = ToScreen(headX, headY);
        double r = HeadR * scale;
        dl.FillCircle(hc.X, hc.Y, r, colors.Skin);
        // hair: cap over the top-back of the head
        var hair = new[]
        {
            ToScreen(headX - 3.6, headY + 0.2), ToScreen(headX - 3.4, headY + 2.6), ToScreen(headX - 1.4, headY + 3.8),
            ToScreen(headX + 1.6, headY + 3.8), ToScreen(headX + 3.4, headY + 2.4), ToScreen(headX + 3.4, headY + 1.6),
            ToScreen(headX + 0.8, headY + 1.9), ToScreen(headX - 1.8, headY + 1.3), ToScreen(headX - 2.6, headY + 0.2),
        };
        dl.FillPolygon(colors.Hair, hair);
        // eye
        var eye = ToScreen(headX + 1.7, headY + 0.6);
        dl.FillRect(eye.X - 0.45 * scale, eye.Y - 0.45 * scale, 0.9 * scale, 0.9 * scale, Palette.Black);
        // the mustache!
        var mustache = new[]
        {
            ToScreen(headX + 0.4, headY - 1.1), ToScreen(headX + 3.6, headY - 0.6),
            ToScreen(headX + 3.9, headY - 1.4), ToScreen(headX + 2.4, headY - 2.1), ToScreen(headX + 0.6, headY - 2.0),
        };
        dl.FillPolygon(Palette.Mustache, mustache);
        // nose
        var nose = ToScreen(headX + 3.3, headY - 0.2);
        dl.FillCircle(nose.X, nose.Y, 0.7 * scale, colors.Skin.Darker(0.88));

        // --- near arm ---
        DrawArm(dl, ToScreen, Dir, shX + 0.6, shY, pose.ArmNear, pose.ElbowNear, colors.Skin, scale);
    }

    private static void DrawLeg(DrawList dl, Func<double, double, Vec2> toScreen, Func<double, Vec2> dir, double hipOffset,
        double thigh, double knee, Rgba skin, Rgba shoe, double scale)
    {
        var hip = new Vec2(hipOffset, HipY);
        var kneeP = hip + dir(thigh) * Thigh;
        var ankle = kneeP + dir(thigh + knee) * Shin;
        var toe = ankle + new Vec2(1.6, 0).Rotate(-(thigh + knee) * 0.5);
        dl.Line(toScreen(hip.X, hip.Y), toScreen(kneeP.X, kneeP.Y), 2.2 * scale, skin);
        dl.Line(toScreen(kneeP.X, kneeP.Y), toScreen(ankle.X, ankle.Y), 1.9 * scale, skin);
        dl.Line(toScreen(ankle.X, ankle.Y), toScreen(toe.X + 0.6, toe.Y), 1.6 * scale, shoe);
    }

    private static void DrawArm(DrawList dl, Func<double, double, Vec2> toScreen, Func<double, Vec2> dir, double sx, double sy,
        double arm, double elbow, Rgba skin, double scale)
    {
        var sh = new Vec2(sx, sy);
        var el = sh + dir(arm) * Upper;
        var hand = el + dir(arm + elbow) * Fore;
        dl.Line(toScreen(sh.X, sh.Y), toScreen(el.X, el.Y), 1.7 * scale, skin);
        dl.Line(toScreen(el.X, el.Y), toScreen(hand.X, hand.Y), 1.5 * scale, skin);
        var h = toScreen(hand.X, hand.Y);
        dl.FillCircle(h.X, h.Y, 0.9 * scale, skin);
    }

    /// <summary>Local coordinates of the near hand for the given pose (to attach implements).</summary>
    public static Vec2 NearHand(BodyPose pose)
    {
        double lean = pose.Lean * Math.PI / 180.0;
        double torsoLen = ShoulderY - HipY;
        var sh = new Vec2(Math.Sin(lean) * torsoLen + 0.6, HipY + Math.Cos(lean) * torsoLen);
        Vec2 Dir(double deg) => new(Math.Sin(deg * Math.PI / 180.0), -Math.Cos(deg * Math.PI / 180.0));
        var el = sh + Dir(pose.ArmNear) * Upper;
        return el + Dir(pose.ArmNear + pose.ElbowNear) * Fore;
    }
}
