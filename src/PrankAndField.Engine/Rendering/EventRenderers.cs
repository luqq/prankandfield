using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Engine.Graphics;
using PrankAndField.Engine.Sprites;

namespace PrankAndField.Engine.Rendering;

/// <summary>Draws the playfield of each event: scenery, athletes, implements, critters, particles and in-field readouts.</summary>
public static class EventRenderers
{
    public static void Draw(DrawList dl, Game game, EventSimulation sim, double time, Fx fx)
    {
        switch (sim)
        {
            case HurdlesSimulation h: DrawRace(dl, game, h, time, fx); break;
            case RaceSimulation r: DrawRace(dl, game, r, time, fx); break;
            case LongJumpSimulation lj: DrawLongJump(dl, game, lj, time, fx); break;
            case JavelinSimulation jv: DrawJavelin(dl, game, jv, time, fx); break;
            case HammerSimulation hm: DrawHammer(dl, game, hm, time, fx); break;
            case HighJumpSimulation hj: DrawHighJump(dl, game, hj, time, fx); break;
        }
        fx.DrawScreen(dl);
    }

    /// <summary>Faint streaks behind a sprinter at full tilt.</summary>
    private static void DrawSpeedLines(DrawList dl, double ax, double groundY, double speedCm, double time)
    {
        if (speedCm < 1000) return;
        double k = Math.Clamp((speedCm - 1000) / 450.0, 0, 1);
        for (int i = 0; i < 3; i++)
        {
            double y = groundY - 7 - i * 5.5;
            double len = (6 + i * 2) * k * (0.7 + 0.3 * Math.Sin(time * 25 + i * 2));
            dl.Line(ax - 7, y, ax - 7 - len, y, 0.8, Palette.White.WithAlpha((byte)(110 * k)));
        }
    }

    // ------------------------------------------------------------------ races

    private static void DrawRace(DrawList dl, Game game, RaceSimulation race, double time, Fx fx)
    {
        var loc = game.Localizer;
        // lane 2 on top (player 2 / computer), lane 1 below, each scrolling on its own.
        var byIndex = race.Athletes.OrderByDescending(a => a.PlayerIndex).ToList();
        double scrollForStands = 0;
        for (int i = 0; i < byIndex.Count; i++)
        {
            var a = byIndex[i];
            double top = i == 0 ? Layout.StripATop : Layout.StripBTop;
            if (byIndex.Count == 1) top = Layout.StripBTop;
            double camera = Layout.CameraFor(a.X);
            if (a.PlayerIndex == 0) scrollForStands = camera * Layout.PxPerMeterX;
            string label = a.IsCpu ? loc["hud.computer"] : loc.Format("hud.player", a.PlayerIndex + 1) + " " + a.Name;
            Stadium.DrawLaneStrip(dl, top, camera, a.PlayerIndex + 1, race.Distance, label, a.IsCpu ? Palette.HudBlue : Palette.HudBlue);
            double ground = Layout.StripGround(top);
            dl.PushClip(0, top, Screen.Width, Layout.StripHeight);
            if (race is HurdlesSimulation hs)
            {
                foreach (var h in hs.HurdlesFor(a.PlayerIndex))
                {
                    double sx = Layout.ToScreenX(h.X, camera);
                    if (sx < -10 || sx > Screen.Width + 10) continue;
                    Stadium.DrawHurdle(dl, sx, ground, h.FallAnim);
                }
            }
            double ax = Layout.ToScreenX(a.X, camera);
            double heightPx = a.Y * Layout.PxPerMeterY;
            AthleteSprite.DrawShadow(dl, ax, ground, heightPx);
            DrawSpeedLines(dl, ax, ground, a.Runner.SpeedCm, time);
            AthleteSprite.Draw(dl, ax, ground - heightPx, a, time);
            fx.Draw(dl, a.PlayerIndex, camera, ground);
            foreach (var c in race.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
            dl.PopClip();
        }
        if (byIndex.Count == 1) Stadium.DrawGrass(dl, Layout.StripATop, Layout.StripHeight, 0);
        Hud.DrawRaceBand(dl, game, race);
        Stadium.DrawStands(dl, scrollForStands, time);
        Stadium.DrawWall(dl, loc[race.Kind.Key()], game.Round, scrollForStands, time);

        // bottom bar: show the human speed
        var human = race.Athletes.FirstOrDefault(a => !a.IsCpu) ?? race.Athletes[0];
        Hud.DrawSpeedBar(dl, game, human.Runner.SpeedCm);

        // start-sequence banners
        string banner = race.Phase switch
        {
            RacePhase.OnYourMarks => loc["banner.on_your_marks"],
            RacePhase.GetSet => loc["banner.get_set"],
            RacePhase.Running when race.RaceTime < 0.8 => loc["banner.go"],
            _ => string.Empty,
        };
        if (race.Banner != BannerKind.None) banner = Hud.BannerText(race, loc);
        if (banner.Length > 0) Hud.DrawBanner(dl, banner, race.Banner == BannerKind.FalseStart ? Palette.HudRed : Palette.HudYellow, Layout.FieldTop + 44, 1.3);

        // finishing times above the lanes
        foreach (var a in race.Athletes.Where(x => x.Finished && race.Phase >= RacePhase.Results))
        {
            double top = race.Athletes.Count == 1 || a == byIndex[^1] ? Layout.StripBTop : Layout.StripATop;
            if (a.Disqualified) Hud.ShadowText(dl, Screen.Width / 2, top + 2, loc["banner.foul"], Palette.HudRed, 1, TextAlign.Center);
            else
            {
                dl.FillRect(Screen.Width / 2 - 20, top + 0.5, 40, 10, Palette.HudPanelDark.WithAlpha(170));
                Hud.DrawTimer(dl, Screen.Width / 2, top + 2, a.FinishTime, a.IsCpu ? Palette.White : Palette.HudYellow, 1, TextAlign.Center);
            }
        }
    }

    // ------------------------------------------------------------------ long jump

    private static void DrawLongJump(DrawList dl, Game game, LongJumpSimulation lj, double time, Fx fx)
    {
        var loc = game.Localizer;
        var a = lj.CurrentAthlete;
        double camera = Layout.CameraFor(a.X, 0, lj.BoardX + 11 - Screen.Width / Layout.PxPerMeterX + 1);
        double ground = Layout.FieldTop + 58;
        double mark = lj.State == JumpState.Landed && lj.AttemptsFor(a.PlayerIndex)[lj.CurrentTry - 1].Mark is double m ? m : 0;
        Stadium.DrawLongJumpField(dl, camera, lj.BoardX, mark);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        foreach (var c in lj.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        double ax = Layout.ToScreenX(a.X, camera);
        double heightPx = a.Y * Layout.PxPerMeterY;
        AthleteSprite.DrawShadow(dl, ax, ground, heightPx);
        if (lj.State == JumpState.Approach) DrawSpeedLines(dl, ax, ground, a.Runner.SpeedCm, time);
        AthleteSprite.Draw(dl, ax, ground - heightPx, a, time);
        fx.Draw(dl, 0, camera, ground);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[lj.Kind.Key()], game.Round, camera * Layout.PxPerMeterX, time);
        Hud.DrawSpeedBar(dl, game, a.Runner.SpeedCm);
        if (lj.ShowAngle) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 15, lj.Angle);
        DrawFieldBanner(dl, game, lj);
    }

    // ------------------------------------------------------------------ javelin

    private static void DrawJavelin(DrawList dl, Game game, JavelinSimulation jv, double time, Fx fx)
    {
        var loc = game.Localizer;
        var a = jv.CurrentAthlete;
        double follow = jv.State is ThrowState.Flight or ThrowState.Landed ? jv.JavelinX : a.X;
        double camera = Layout.CameraFor(follow);
        double ground = Layout.FieldTop + 60;
        Stadium.DrawJavelinField(dl, camera, jv.LineX);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        foreach (var c in jv.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        double ax = Layout.ToScreenX(a.X, camera);
        AthleteSprite.DrawShadow(dl, ax, ground);
        if (jv.State == ThrowState.Approach) DrawSpeedLines(dl, ax, ground, a.Runner.SpeedCm, time);
        AthleteSprite.Draw(dl, ax, ground, a, time);
        // the javelin: in hand before release, flying after.
        if (jv.State is ThrowState.Approach or ThrowState.WindUp)
        {
            var pose = AthleteSprite.PoseFor(a.Pose, a.AnimPhase, time);
            var hand = AthleteSprite.NearHand(pose);
            double hx = ax + hand.X, hy = ground - hand.Y;
            double ang = jv.State == ThrowState.WindUp ? -jv.Angle : -10;
            DrawJavelin(dl, hx, hy, ang, 16);
        }
        else if (jv.JavelinVisible)
        {
            // Flight heights are squashed so a normal throw stays on screen; only bird-high throws leave the top.
            double jx = Layout.ToScreenX(jv.JavelinX, camera);
            double jy = ground - jv.JavelinY * Layout.PxPerMeterY * Layout.FlightSquash;
            if (jv.State == ThrowState.Flight) dl.FillEllipse(jx, ground + 0.5, 4, 1, Palette.Shadow.WithAlpha(40));
            DrawJavelin(dl, jx, jy, -jv.JavelinAngle, 16);
        }
        fx.Draw(dl, 0, camera, ground);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[jv.Kind.Key()], game.Round, camera * Layout.PxPerMeterX, time);
        Hud.DrawSpeedBar(dl, game, a.Runner.SpeedCm);
        if (jv.ShowAngle && jv.State == ThrowState.WindUp) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 15, jv.Angle);
        if (jv.State == ThrowState.Flight)
        {
            Hud.ShadowText(dl, Screen.Width - 6, Layout.FieldTop + 4, Stadium.FormatMeters(jv.Distance), Palette.White, 1, TextAlign.Right);
        }
        DrawFieldBanner(dl, game, jv);
    }

    private static void DrawJavelin(DrawList dl, double cx, double cy, double angleDeg, double length)
    {
        var dir = Vec2.FromAngle(angleDeg);
        var tail = new Vec2(cx, cy) - dir * (length * 0.45);
        var tip = new Vec2(cx, cy) + dir * (length * 0.55);
        dl.Line(tail, tip, 2.0, Palette.Outline.WithAlpha(120));
        dl.Line(tail, tip, 1.1, Palette.Javelin);
        dl.Line(tip - dir * 3, tip, 1.6, Palette.JavelinTip);
        var grip = new Vec2(cx, cy);
        dl.Line(grip - dir * 1.5, grip + dir * 1.5, 1.8, Palette.HudRed);
    }

    // ------------------------------------------------------------------ hammer

    private static void DrawHammer(DrawList dl, Game game, HammerSimulation hm, double time, Fx fx)
    {
        var loc = game.Localizer;
        var a = hm.CurrentAthlete;
        double follow = hm.State is HammerState.Flight or HammerState.Landed ? hm.HammerX : 0;
        double camera = Layout.CameraFor(follow, -2.0);
        double ground = Layout.FieldTop + 60;
        Stadium.DrawHammerField(dl, camera);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        double ax = Layout.ToScreenX(0, camera);

        if (hm.State is HammerState.Ready or HammerState.Spinning or HammerState.Fouled)
        {
            // The release gate: the ball glows red while it passes over this arc in front of the circle.
            if (hm.State != HammerState.Fouled) DrawReleaseGate(dl, ax, ground, time);

            // The ball rests on the ground before the spin, rises to hand height over the first half turn and
            // drops where it was when the attempt is fouled.
            double lift = hm.State == HammerState.Spinning ? Math.Clamp((hm.HammerHeading - HammerSimulation.StartHeading) / 180.0, 0, 1) : 0;
            double ballHeight = 1 + (SpinnerSprite.OrbitHeight(hm.HammerHeading) - 1) * lift;
            var ball = SpinnerSprite.BallAt(ax, ground, hm.HammerHeading, ballHeight);
            var shadow = SpinnerSprite.BallAt(ax, ground, hm.HammerHeading, -0.5);
            bool behind = Math.Sin(hm.HammerHeading * Math.PI / 180.0) < 0;
            var ballColor = hm.State == HammerState.Spinning && hm.InReleaseArc ? Palette.HammerHot : Palette.Hammer;
            AthleteSprite.DrawShadow(dl, ax, ground);
            dl.FillEllipse(shadow.X, shadow.Y, 2.6, 1.0, Palette.Shadow.WithAlpha(50));

            if (hm.State == HammerState.Fouled)
            {
                // the athlete has fallen or is reeling; the hammer lies on the ground where it was
                DrawHammerBall(dl, ax + 4, ground - 6, ball.X, ball.Y, ballColor);
                AthleteSprite.Draw(dl, ax, ground, AthleteSprite.PoseFor(a.Pose, a.AnimPhase, time), AthleteColors.For(a), 1, 0, a.PlayerIndex + 1);
            }
            else
            {
                var colors = AthleteColors.For(a);
                var hands = SpinnerSprite.HandsAt(ax, ground, hm.HammerHeading);
                // behind the thrower the wire and the ball pass behind the body
                if (behind) DrawHammerBall(dl, hands.X, hands.Y, ball.X, ball.Y, ballColor);
                SpinnerSprite.Draw(dl, ax, ground, hm.HammerHeading, colors, a.PlayerIndex + 1);
                if (!behind) DrawHammerBall(dl, hands.X, hands.Y, ball.X, ball.Y, ballColor);
            }
        }
        else
        {
            AthleteSprite.DrawShadow(dl, ax, ground);
            AthleteSprite.Draw(dl, ax, ground, a, time);
            double hx = Layout.ToScreenX(hm.HammerX, camera);
            double hy = ground - hm.HammerY * Layout.PxPerMeterY * Layout.FlightSquash;
            if (hm.State == HammerState.Flight) dl.FillEllipse(hx, ground + 0.5, 3, 0.9, Palette.Shadow.WithAlpha(45));
            dl.FillCircle(hx, hy, 3.2, Palette.Outline.WithAlpha(150));
            dl.FillCircle(hx, hy, 2.6, Palette.Hammer);
            dl.FillCircle(hx - 0.9, hy - 0.9, 0.9, Palette.HammerShine);
            dl.Line(hx, hy, hx - 4, hy - 3, 0.8, Palette.Wire);
        }
        fx.Draw(dl, 0, camera, ground);
        foreach (var c in hm.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[hm.Kind.Key()], game.Round, camera * Layout.PxPerMeterX, time);
        Hud.DrawSpeedBar(dl, game, hm.SpeedReadout(a.PlayerIndex), $"{loc["hud.spins"]} {hm.SpinsCompleted}");
        if (hm.State == HammerState.Flight)
            Hud.ShadowText(dl, Screen.Width - 6, Layout.FieldTop + 4, Stadium.FormatMeters(hm.Distance), Palette.White, 1, TextAlign.Right);
        DrawFieldBanner(dl, game, hm);
    }

    /// <summary>Red arc on the ground in front of the circle, under the stretch of the orbit where a release is full distance.</summary>
    private static void DrawReleaseGate(DrawList dl, double ax, double ground, double time)
    {
        const int segments = 10;
        var pts = new Vec2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            double deg = -Rules.HammerReleaseArc + i * 2 * Rules.HammerReleaseArc / segments;
            pts[i] = SpinnerSprite.BallAt(ax, ground, deg, -0.5);
        }
        byte alpha = (byte)(150 + 60 * Math.Sin(time * 6));
        dl.Polyline(1.2, Palette.HammerHot.WithAlpha(alpha), pts);
    }

    private static void DrawHammerBall(DrawList dl, double handX, double handY, double hx, double hy, Rgba color)
    {
        dl.Line(handX, handY, hx, hy, 0.8, Palette.Wire);
        if (color != Palette.Hammer) dl.Glow(hx, hy, 7, color.WithAlpha(120));
        dl.FillCircle(hx, hy, 3.4, Palette.Outline.WithAlpha(150));
        dl.FillCircle(hx, hy, 2.8, color);
        dl.FillCircle(hx - 1, hy - 1, 0.9, color.Lighter(0.5));
    }

    // ------------------------------------------------------------------ high jump

    private static void DrawHighJump(DrawList dl, Game game, HighJumpSimulation hj, double time, Fx fx)
    {
        var loc = game.Localizer;
        var a = hj.CurrentAthlete;
        // The camera follows the run-up and parks with the bar at about a third of the screen, mat beside it.
        double camera = Layout.CameraFor(a.X, Rules.HighJumpApproachStart - 1.5, -HighJumpBarScreenX / Layout.PxPerMeterX);
        double ground = Layout.FieldTop + 62;
        Stadium.DrawHighJumpField(dl, camera, hj.BarHeight, hj.BarFallAnim, ground);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        foreach (var c in hj.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        double ax = Layout.ToScreenX(a.X, camera);
        // The mat is drawn in false perspective: whoever lands on it stands a few pixels "up" the mat. The lift
        // blends in over the second half of the flight so the landing does not pop.
        double matLift = 0;
        if (hj.OnMat)
        {
            double t = Math.Clamp((hj.FlightProgress - 0.5) / 0.5, 0, 1);
            matLift = Stadium.HighJumpMatLift * t * t * (3 - 2 * t);
        }
        double heightPx = a.Y * Layout.PxPerMeterY;
        double ay = ground - heightPx - matLift;
        if (!hj.OnMat) AthleteSprite.DrawShadow(dl, ax, ground, heightPx);
        if (hj.State == HighJumpState.Airborne)
        {
            // Fosbury flop: after the take-off stretch the jumper turns his back to the bar (mirrored sprite)
            // and rotates head first over it, landing on his shoulders. A few cosmetic pixels of extra lift in
            // the middle of the flight keep the draped limbs clear of the bar line on a narrow clearance.
            ay -= 3 * Math.Sin(Math.PI * hj.FlightProgress);
            var pose = AthleteSprite.PoseFor(a.Pose, a.AnimPhase, time);
            double facing = hj.FlightProgress < 0.1 ? 1 : -1;
            AthleteSprite.Draw(dl, ax, ay, pose, AthleteColors.For(a), facing, a.Rotation, a.PlayerIndex + 1);
        }
        else
        {
            AthleteSprite.Draw(dl, ax, ay, a, time);
        }
        fx.Draw(dl, 0, camera, ground);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[hj.Kind.Key()], game.Round, camera * Layout.PxPerMeterX, time);
        Hud.DrawSpeedBar(dl, game, a.Runner.SpeedCm, $"{loc["hud.bar"]} {Stadium.FormatMeters(hj.BarHeight)}");
        if (hj.ShowAngle && hj.State == HighJumpState.Rising) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 15, hj.Angle);
        DrawFieldBanner(dl, game, hj);
    }

    /// <summary>Screen x where the high-jump bar parks once the camera stops following the run-up.</summary>
    private const double HighJumpBarScreenX = 118;

    private static void DrawFieldBanner(DrawList dl, Game game, FieldEventSimulation sim)
    {
        var loc = game.Localizer;
        if (sim.Banner == BannerKind.None) return;
        if (sim.Banner == BannerKind.Mark)
        {
            double y = Layout.FieldTop + 10;
            double w = 74, h = 15;
            dl.FillRect(Screen.Width / 2 - w / 2 - 2, y - 2, w + 4, h + 4, Palette.HudYellow.WithAlpha(40));
            dl.FillGradientRect(Screen.Width / 2 - w / 2, y, w, h, Palette.HudPanel.WithAlpha(230), Palette.HudPanelDark.WithAlpha(230));
            dl.FillRect(Screen.Width / 2 - w / 2, y, w, 0.8, Palette.HudYellow);
            dl.FillRect(Screen.Width / 2 - w / 2, y + h - 0.8, w, 0.8, Palette.HudYellow);
            Hud.DrawMark(dl, Screen.Width / 2, y + 3.5, sim.Kind, sim.BannerValue, loc, Palette.HudYellow, Palette.HudGreen, 1.2, TextAlign.Center);
            return;
        }
        var color = sim.Banner is BannerKind.Foul or BannerKind.WrongDirection or BannerKind.Dizzy ? Palette.HudRed : Palette.HudYellow;
        Hud.DrawBanner(dl, Hud.BannerText(sim, loc), color, Layout.FieldTop + 12, 1.3);
    }
}
