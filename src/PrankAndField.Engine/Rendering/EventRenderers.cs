using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Engine.Graphics;
using PrankAndField.Engine.Sprites;

namespace PrankAndField.Engine.Rendering;

/// <summary>Draws the playfield of each event: scenery, athletes, implements, critters and in-field readouts.</summary>
public static class EventRenderers
{
    public static void Draw(DrawList dl, Game game, EventSimulation sim, double time)
    {
        switch (sim)
        {
            case HurdlesSimulation h: DrawRace(dl, game, h, time); break;
            case RaceSimulation r: DrawRace(dl, game, r, time); break;
            case LongJumpSimulation lj: DrawLongJump(dl, game, lj, time); break;
            case JavelinSimulation jv: DrawJavelin(dl, game, jv, time); break;
            case HammerSimulation hm: DrawHammer(dl, game, hm, time); break;
            case HighJumpSimulation hj: DrawHighJump(dl, game, hj, time); break;
        }
    }

    // ------------------------------------------------------------------ races

    private static void DrawRace(DrawList dl, Game game, RaceSimulation race, double time)
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
            AthleteSprite.Draw(dl, ax, ground - a.Y * Layout.PxPerMeterY, a, time);
            foreach (var c in race.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
            dl.PopClip();
        }
        if (byIndex.Count == 1) dl.FillRect(0, Layout.StripATop, Screen.Width, Layout.StripHeight, Palette.Grass);
        Hud.DrawRaceBand(dl, game, race);
        Stadium.DrawStands(dl, scrollForStands, time);
        Stadium.DrawWall(dl, loc[race.Kind.Key()], game.Round);

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
            if (a.Disqualified) PixelFont.Draw(dl, Screen.Width / 2, top + 2, loc["banner.foul"], Palette.HudRed, 1, TextAlign.Center);
            else Hud.DrawTimer(dl, Screen.Width / 2, top + 2, a.FinishTime, a.IsCpu ? Palette.White : Palette.HudYellow, 1, TextAlign.Center);
        }
    }

    // ------------------------------------------------------------------ long jump

    private static void DrawLongJump(DrawList dl, Game game, LongJumpSimulation lj, double time)
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
        AthleteSprite.Draw(dl, ax, ground - a.Y * Layout.PxPerMeterY, a, time);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[lj.Kind.Key()], game.Round);
        Hud.DrawSpeedBar(dl, game, a.Runner.SpeedCm);
        if (lj.ShowAngle) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 13, lj.Angle);
        DrawFieldBanner(dl, game, lj);
    }

    // ------------------------------------------------------------------ javelin

    private static void DrawJavelin(DrawList dl, Game game, JavelinSimulation jv, double time)
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
            DrawJavelin(dl, jx, jy, -jv.JavelinAngle, 16);
        }
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[jv.Kind.Key()], game.Round);
        Hud.DrawSpeedBar(dl, game, a.Runner.SpeedCm);
        if (jv.ShowAngle && jv.State == ThrowState.WindUp) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 13, jv.Angle);
        if (jv.State == ThrowState.Flight)
        {
            PixelFont.Draw(dl, Screen.Width - 6, Layout.FieldTop + 4, Stadium.FormatMeters(jv.Distance), Palette.White, 1, TextAlign.Right);
        }
        DrawFieldBanner(dl, game, jv);
    }

    private static void DrawJavelin(DrawList dl, double cx, double cy, double angleDeg, double length)
    {
        var dir = Vec2.FromAngle(angleDeg);
        var tail = new Vec2(cx, cy) - dir * (length * 0.45);
        var tip = new Vec2(cx, cy) + dir * (length * 0.55);
        dl.Line(tail, tip, 1.1, Palette.Javelin);
        dl.Line(tip - dir * 3, tip, 1.6, Palette.JavelinTip);
        var grip = new Vec2(cx, cy);
        dl.Line(grip - dir * 1.5, grip + dir * 1.5, 1.8, Palette.HudRed);
    }

    // ------------------------------------------------------------------ hammer

    private static void DrawHammer(DrawList dl, Game game, HammerSimulation hm, double time)
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
            // heading 0 = right, 90 = towards the camera. Flip the sprite with the spin.
            double rad = hm.HammerHeading * Math.PI / 180.0;
            double facing = hm.State == HammerState.Spinning ? (Math.Cos(rad) >= 0 ? 1 : -1) : 1;
            var colors = AthleteColors.For(a);
            var pose = AthleteSprite.PoseFor(a.Pose, a.AnimPhase, time);
            double hx = ax + Math.Cos(rad) * 22;
            double hy = ground - 8 + Math.Sin(rad) * 7;
            bool behind = Math.Sin(rad) < 0;
            var ballColor = hm.State != HammerState.Spinning ? Palette.Hammer
                : hm.InReleaseArc ? Palette.HammerHot
                : hm.InPressZone && !hm.Holding ? Palette.HudBlue
                : Palette.Hammer;
            if (behind) DrawHammerBall(dl, ax, ground - 10, hx, hy, ballColor);
            AthleteSprite.Draw(dl, ax, ground, pose, colors, facing, 0, a.PlayerIndex + 1);
            if (!behind) DrawHammerBall(dl, ax, ground - 10, hx, hy, ballColor);
        }
        else
        {
            AthleteSprite.Draw(dl, ax, ground, a, time);
            double hx = Layout.ToScreenX(hm.HammerX, camera);
            double hy = ground - hm.HammerY * Layout.PxPerMeterY * Layout.FlightSquash;
            dl.FillCircle(hx, hy, 2.6, Palette.Hammer);
            dl.Line(hx, hy, hx - 4, hy - 3, 0.8, Palette.Wire);
        }
        foreach (var c in hm.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[hm.Kind.Key()], game.Round);
        Hud.DrawSpeedBar(dl, game, hm.SpeedReadout(a.PlayerIndex), $"{loc["hud.spins"]} {hm.SpinsCompleted}");
        if (hm.ShowAngle && hm.State == HammerState.Spinning) Hud.DrawAngleBox(dl, ax + 40, Layout.FieldBottom - 13, hm.Angle);
        if (hm.State == HammerState.Flight)
            PixelFont.Draw(dl, Screen.Width - 6, Layout.FieldTop + 4, Stadium.FormatMeters(hm.Distance), Palette.White, 1, TextAlign.Right);
        DrawFieldBanner(dl, game, hm);
    }

    private static void DrawHammerBall(DrawList dl, double handX, double handY, double hx, double hy, Rgba color)
    {
        dl.Line(handX, handY, hx, hy, 0.8, Palette.Wire);
        dl.FillCircle(hx, hy, 2.8, color);
    }

    // ------------------------------------------------------------------ high jump

    private static void DrawHighJump(DrawList dl, Game game, HighJumpSimulation hj, double time)
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
        double ay = ground - a.Y * Layout.PxPerMeterY - matLift;
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
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[hj.Kind.Key()], game.Round);
        Hud.DrawSpeedBar(dl, game, a.Runner.SpeedCm, $"{loc["hud.bar"]} {Stadium.FormatMeters(hj.BarHeight)}");
        if (hj.ShowAngle && hj.State == HighJumpState.Rising) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 13, hj.Angle);
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
            double w = 70, h = 14;
            dl.FillRect(Screen.Width / 2 - w / 2, y, w, h, Palette.Black.WithAlpha(210));
            Hud.DrawMark(dl, Screen.Width / 2, y + 3, sim.Kind, sim.BannerValue, loc, Palette.HudYellow, Palette.HudGreen, 1.2, TextAlign.Center);
            return;
        }
        var color = sim.Banner is BannerKind.Foul or BannerKind.WrongDirection or BannerKind.Dizzy ? Palette.HudRed : Palette.HudYellow;
        Hud.DrawBanner(dl, Hud.BannerText(sim, loc), color, Layout.FieldTop + 12, 1.3);
    }
}
