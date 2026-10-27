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
            case SwimmingSimulation sw: DrawSwimming(dl, game, sw, time); break;
            case HurdlesSimulation h: DrawRace(dl, game, h, time); break;
            case RaceSimulation r: DrawRace(dl, game, r, time); break;
            case LongJumpSimulation lj: DrawLongJump(dl, game, lj, time); break;
            case JavelinSimulation jv: DrawJavelin(dl, game, jv, time); break;
            case HammerSimulation hm: DrawHammer(dl, game, hm, time); break;
            case HighJumpSimulation hj: DrawHighJump(dl, game, hj, time); break;
            case SkeetSimulation sk: DrawSkeet(dl, game, sk, time); break;
            case VaultSimulation v: DrawVault(dl, game, v, time); break;
            case ArcherySimulation ar: DrawArchery(dl, game, ar, time); break;
            case TripleJumpSimulation tj: DrawTripleJump(dl, game, tj, time); break;
            case WeightliftingSimulation wl: DrawWeightlifting(dl, game, wl, time); break;
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
        double camera = Layout.CameraFor(a.X, Rules.HighJumpApproachStart - 1, 2.0 - Screen.Width / Layout.PxPerMeterX + 6);
        double ground = Layout.FieldTop + 62;
        Stadium.DrawHighJumpField(dl, camera, hj.BarHeight, hj.BarFallen ? hj.BarFallAnim : 0, ground);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        foreach (var c in hj.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        double ax = Layout.ToScreenX(a.X, camera);
        double ay = ground - a.Y * Layout.PxPerMeterY;
        if (hj.State is HighJumpState.Landed or HighJumpState.Missed) ay = ground - 9; // on the mat
        AthleteSprite.Draw(dl, ax, ay, a, time);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[hj.Kind.Key()], game.Round);
        Hud.DrawSpeedBar(dl, game, hj.SpeedReadout(a.PlayerIndex), $"{loc["hud.bar"]} {Stadium.FormatMeters(hj.BarHeight)}");
        if (hj.ShowAngle && hj.State == HighJumpState.Rising) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 13, hj.Angle);
        DrawFieldBanner(dl, game, hj);
    }

    // ================================================================== Hyper Sports

    // ------------------------------------------------------------------ swimming

    private static void DrawSwimming(DrawList dl, Game game, SwimmingSimulation sw, double time)
    {
        var loc = game.Localizer;
        var byIndex = sw.Athletes.OrderByDescending(a => a.PlayerIndex).ToList();
        double scrollForStands = 0;
        for (int i = 0; i < byIndex.Count; i++)
        {
            var a = byIndex[i];
            double top = i == 0 ? Layout.StripATop : Layout.StripBTop;
            if (byIndex.Count == 1) top = Layout.StripBTop;
            double camera = Layout.CameraFor(a.X);
            if (a.PlayerIndex == 0) scrollForStands = camera * Layout.PxPerMeterX;
            string label = a.IsCpu ? loc["hud.computer"] : loc.Format("hud.player", a.PlayerIndex + 1) + " " + a.Name;
            Stadium.DrawPoolStrip(dl, top, camera, a.PlayerIndex + 1, sw.Distance, label, Palette.HudBlue, time);
            double ground = Layout.StripGround(top);
            double waterLine = top + 21;
            dl.PushClip(0, top, Screen.Width, Layout.StripHeight);
            double ax = Layout.ToScreenX(a.X, camera);
            bool swimming = a.Pose is Pose.Swim or Pose.Breathe;
            double ay = swimming ? waterLine : ground;
            // wake behind the swimmer
            if (swimming && a.Runner.SpeedCm > 20)
            {
                for (int k = 0; k < 4; k++)
                {
                    double wx = ax - 10 - k * 6 - (time * 40) % 6;
                    dl.FillRect(wx, waterLine + 1 + Math.Sin(time * 6 + k) * 1.2, 4, 1, Palette.White.WithAlpha(170));
                }
            }
            AthleteSprite.Draw(dl, ax, ay, a, time);
            if (swimming) dl.FillRect(ax - 14, waterLine + 2.5, 28, 1.2, Palette.Water.WithAlpha(150));
            if (sw.IsChoking(a.PlayerIndex))
            {
                for (int k = 0; k < 3; k++)
                {
                    double by = waterLine - 4 - ((time * 18 + k * 5) % 12);
                    dl.FillCircle(ax + 10 + k * 3, by, 1.1, Palette.WaterLight);
                }
            }
            if (!a.IsCpu && sw.BreathPrompt(a.PlayerIndex))
                Hud.DrawPrompt(dl, ax + 6, top + 1, loc["hud.breathe"], Palette.HudYellow, sw.BreathWindowLeft(a.PlayerIndex));
            dl.PopClip();
        }
        if (byIndex.Count == 1) dl.FillRect(0, Layout.StripATop, Screen.Width, Layout.StripHeight, Palette.Grass);
        Hud.DrawRaceBand(dl, game, sw);
        Stadium.DrawStands(dl, scrollForStands, time);
        Stadium.DrawWall(dl, loc[sw.Kind.Key()], game.Round);

        var human = sw.Athletes.FirstOrDefault(a => !a.IsCpu) ?? sw.Athletes[0];
        Hud.DrawSpeedBar(dl, game, human.Runner.SpeedCm, null, Rules.SwimMaxSpeed * 1.1);

        string banner = sw.Phase switch
        {
            RacePhase.OnYourMarks => loc["banner.on_your_marks"],
            RacePhase.GetSet => loc["banner.get_set"],
            RacePhase.Running when sw.RaceTime < 0.8 => loc["banner.go"],
            _ => string.Empty,
        };
        if (sw.Banner != BannerKind.None) banner = Hud.BannerText(sw, loc);
        if (banner.Length > 0) Hud.DrawBanner(dl, banner, sw.Banner == BannerKind.FalseStart ? Palette.HudRed : Palette.HudYellow, Layout.FieldTop + 44, 1.3);

        foreach (var a in sw.Athletes.Where(x => x.Finished && sw.Phase >= RacePhase.Results))
        {
            double top = sw.Athletes.Count == 1 || a == byIndex[^1] ? Layout.StripBTop : Layout.StripATop;
            if (a.Disqualified) PixelFont.Draw(dl, Screen.Width / 2, top + 2, loc["banner.foul"], Palette.HudRed, 1, TextAlign.Center);
            else Hud.DrawTimer(dl, Screen.Width / 2, top + 2, a.FinishTime, a.IsCpu ? Palette.White : Palette.HudYellow, 1, TextAlign.Center);
        }
    }

    // ------------------------------------------------------------------ skeet

    private static void DrawSkeet(DrawList dl, Game game, SkeetSimulation sk, double time)
    {
        var loc = game.Localizer;
        var a = sk.CurrentAthlete;
        double ground = Layout.FieldTop + 82;
        Stadium.DrawSkeetRange(dl, ground, sk.TargetInSight(0), sk.TargetInSight(1));
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        foreach (var t in sk.Targets)
        {
            if (t.Hit || t.Gone) continue;
            Stadium.DrawClay(dl, t.X * Layout.PxPerMeterX, ground - t.Y * Layout.SkeetPxPerMeterY, t.Golden);
        }
        // the shooter faces the sight of the last gun fired; the gun points at that sight
        double ax = a.X * Layout.PxPerMeterX;
        var colors = AthleteColors.For(a);
        var pose = AthleteSprite.PoseFor(a.Pose, a.AnimPhase, time);
        AthleteSprite.Draw(dl, ax, ground, pose, colors, sk.Facing, 0, a.PlayerIndex + 1);
        if (sk.Phase == TurnPhase.Active || sk.Phase == TurnPhase.Result)
        {
            int gun = sk.Facing < 0 ? 0 : 1;
            var hand = AthleteSprite.NearHand(pose);
            var grip = new Vec2(ax + hand.X * sk.Facing, ground - hand.Y);
            var sight = new Vec2(SkeetSimulation.SightX(gun) * Layout.PxPerMeterX, ground - Rules.SkeetSightHeight * Layout.SkeetPxPerMeterY);
            var dir = sight - grip;
            double len = Math.Max(1, dir.Length);
            dir = dir * (1 / len);
            var muzzle = grip + dir * 14;
            dl.Line(grip - dir * 3, muzzle, 1.8, Palette.Gun);
            dl.Line(grip - dir * 3, grip, 2.4, Palette.Horse);
            if (sk.MuzzleFlash(gun))
            {
                dl.FillCircle(muzzle.X + dir.X * 2, muzzle.Y + dir.Y * 2, 2.6, Palette.Flash);
                dl.FillCircle(muzzle.X + dir.X * 3, muzzle.Y + dir.Y * 3, 1.3, Palette.White);
            }
        }
        foreach (var c in sk.Critters) Stadium.DrawCritter(dl, c, c.X * Layout.PxPerMeterX, ground, Layout.SkeetPxPerMeterY);
        dl.PopClip();
        Stadium.DrawStands(dl, 0, time);
        Stadium.DrawWall(dl, loc[sk.Kind.Key()], game.Round);
        Hud.DrawSpeedBar(dl, game, sk.SpeedReadout(a.PlayerIndex),
            loc.Format("hud.target", Math.Min(sk.Launched, Rules.SkeetTargets), Rules.SkeetTargets) + "   " + loc.Format("hud.streak", sk.Streak),
            Rules.SkeetTargets * 150, loc["hud.hits"] + "=");
        PixelFont.Draw(dl, Screen.Width - 6, Layout.FieldTop + 4, $"{sk.Hits} {loc["hud.hits"]}", Palette.White, 1, TextAlign.Right);
        DrawTurnBanner(dl, game, sk);
    }

    // ------------------------------------------------------------------ vault

    private static void DrawVault(DrawList dl, Game game, VaultSimulation v, double time)
    {
        var loc = game.Localizer;
        var a = v.CurrentAthlete;
        double camera = Layout.CameraFor(a.X, 0, v.HorseEnd + 6.5 - Screen.Width / Layout.PxPerMeterX);
        double ground = Layout.FieldTop + 62;
        Stadium.DrawVaultField(dl, camera, v.BoardX, v.HorseStart, v.HorseEnd, Rules.VaultHorseHeight, ground);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        foreach (var c in v.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        double ax = Layout.ToScreenX(a.X, camera);
        // in the handstand the sprite is upside down: lift the anchor so the hands rest on the horse top
        double ay = ground - a.Y * Layout.PxPerMeterY - (a.Pose == Pose.Handstand ? 14 : 0);
        AthleteSprite.Draw(dl, ax, ay, a, time);
        if (v.State == VaultState.OnHorse)
            Hud.DrawPrompt(dl, ax, Layout.FieldTop + 6, loc["hud.push"], Palette.HudYellow, 1 - Math.Clamp(v.HorseTime / Rules.VaultPushAuto, 0, 1));
        else if (v.StickWindowOpen)
            Hud.DrawPrompt(dl, Math.Clamp(ax, 24, Screen.Width - 24), Layout.FieldTop + 6, loc["hud.land"], Palette.HudGreen);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[v.Kind.Key()], game.Round);
        bool flying = v.State is VaultState.PreFlight or VaultState.OnHorse or VaultState.Flight or VaultState.Landed;
        string label = flying ? $"{loc["hud.twists"]} {v.Twists.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}" : string.Empty;
        Hud.DrawSpeedBar(dl, game, v.SpeedReadout(a.PlayerIndex), label.Length > 0 ? label : null, flying ? Rules.VaultMaxTwists * 250 : 1550);
        DrawFieldBanner(dl, game, v);
    }

    // ------------------------------------------------------------------ archery

    private static void DrawArchery(DrawList dl, Game game, ArcherySimulation ar, double time)
    {
        var loc = game.Localizer;
        var a = ar.CurrentAthlete;
        double ground = Layout.FieldTop + 86;
        double shooterX = 24;
        double targetX = shooterX + ar.TargetX * Layout.ArcheryPxPerMeter;
        double targetCy = ground - ar.TargetY * Layout.ArcheryPxPerMeter;
        Stadium.DrawArcheryRange(dl, shooterX, targetX, targetCy, ground);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        // arrows already in the target move with it
        foreach (var st in ar.Stuck)
            Stadium.DrawArrow(dl, targetX - 1, targetCy - st.Offset * Layout.ArcheryPxPerMeter, 0, 9);
        var pose = AthleteSprite.PoseFor(a.Pose, a.AnimPhase, time);
        AthleteSprite.Draw(dl, shooterX, ground, pose, AthleteColors.For(a), 1, 0, a.PlayerIndex + 1);
        // the bow in the near hand, tilted by the aim angle
        var hand = AthleteSprite.NearHand(pose);
        var grip = new Vec2(shooterX + hand.X, ground - hand.Y);
        double bowAngle = ar.State == ArcheryState.Aim ? -ar.Angle : 0;
        var up = Vec2.FromAngle(bowAngle - 90);
        var fwd = Vec2.FromAngle(bowAngle);
        var bow = new Vec2[9];
        for (int i = 0; i <= 8; i++)
        {
            double t = i / 8.0 * 2 - 1;
            bow[i] = grip + up * (t * 9) + fwd * (3.5 * (1 - t * t));
        }
        dl.Polyline(1.2, Palette.Bow, bow);
        dl.Line(bow[0], bow[8], 0.5, Palette.White);
        if (ar.State == ArcheryState.Aim) Stadium.DrawArrow(dl, grip.X + fwd.X * 7, grip.Y + fwd.Y * 7, bowAngle, 10);
        if (ar.ArrowVisible)
        {
            double sx = shooterX + ar.ArrowX * Layout.ArcheryPxPerMeter;
            double sy = ground - ar.ArrowY * Layout.ArcheryPxPerMeter;
            Stadium.DrawArrow(dl, sx, sy, -ar.ArrowAngle, 10);
        }
        // the points of the last arrow, by the target
        if (ar.State == ArcheryState.Scored)
        {
            string text = ar.LastPoints > 0 ? ar.LastPoints.ToString() : loc["hud.miss"];
            PixelFont.Draw(dl, targetX, Layout.FieldTop + 6, text, ar.LastPoints >= 9 ? Palette.HudYellow : ar.LastPoints > 0 ? Palette.White : Palette.HudRed, 1.3, TextAlign.Center);
        }
        dl.PopClip();
        Stadium.DrawStands(dl, 0, time);
        Stadium.DrawWall(dl, loc[ar.Kind.Key()], game.Round);
        // wind box, top left
        dl.FillRect(6, Layout.FieldTop + 4, 46, 11, Palette.Black.WithAlpha(210));
        string windArrow = ar.Wind > 0.05 ? "↑" : ar.Wind < -0.05 ? "↓" : "-";
        PixelFont.Draw(dl, 9, Layout.FieldTop + 6, loc["hud.wind"], Palette.HudCyan, 0.7);
        PixelFont.Draw(dl, 49, Layout.FieldTop + 6, windArrow + " " + Math.Abs(ar.Wind).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), Palette.White, 0.7, TextAlign.Right);
        Hud.DrawSpeedBar(dl, game, ar.SpeedReadout(a.PlayerIndex), loc.Format("hud.arrow", Math.Min(ar.ArrowIndex + 1, Rules.ArcheryArrows), Rules.ArcheryArrows), Rules.ArcheryArrows * 10 * 20, loc["hud.pts"] + "=");
        PixelFont.Draw(dl, Screen.Width - 6, Layout.FieldTop + 4, $"{ar.Points} {loc["hud.pts"]}", Palette.White, 1, TextAlign.Right);
        if (ar.ShowAngle) Hud.DrawAngleBox(dl, shooterX + 30, Layout.FieldBottom - 13, ar.Angle);
        DrawTurnBanner(dl, game, ar);
    }

    // ------------------------------------------------------------------ triple jump

    private static void DrawTripleJump(DrawList dl, Game game, TripleJumpSimulation tj, double time)
    {
        var loc = game.Localizer;
        var a = tj.CurrentAthlete;
        double camera = Layout.CameraFor(a.X, 0, tj.PitStart + 10.5 - Screen.Width / Layout.PxPerMeterX);
        double ground = Layout.FieldTop + 58;
        Stadium.DrawTripleJumpField(dl, camera, tj.BoardX, Rules.TripleJumpPitStart, tj.Landings);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        foreach (var c in tj.Critters) Stadium.DrawCritter(dl, c, Layout.ToScreenX(c.X, camera), ground);
        double ax = Layout.ToScreenX(a.X, camera);
        AthleteSprite.Draw(dl, ax, ground - a.Y * Layout.PxPerMeterY, a, time);
        if (tj.State == TripleJumpState.Contact)
            Hud.DrawPrompt(dl, Math.Clamp(ax, 24, Screen.Width - 24), Layout.FieldTop + 6, loc["hud.press"], Palette.HudYellow, tj.ContactLeft);
        dl.PopClip();
        Stadium.DrawStands(dl, camera * Layout.PxPerMeterX, time);
        Stadium.DrawWall(dl, loc[tj.Kind.Key()], game.Round);
        string phase = tj.Phase == AttemptPhase.Active && tj.State != TripleJumpState.Approach
            ? loc[tj.JumpPhase switch { 0 => "hud.hop", 1 => "hud.step", _ => "hud.jump" }]
            : string.Empty;
        Hud.DrawSpeedBar(dl, game, a.Runner.SpeedCm, phase.Length > 0 ? phase : null);
        if (tj.ShowAngle) Hud.DrawAngleBox(dl, Math.Clamp(ax, 20, Screen.Width - 20), Layout.FieldBottom - 13, tj.Angle);
        DrawFieldBanner(dl, game, tj);
    }

    // ------------------------------------------------------------------ weightlifting

    private static void DrawWeightlifting(DrawList dl, Game game, WeightliftingSimulation wl, double time)
    {
        var loc = game.Localizer;
        var a = wl.CurrentAthlete;
        double ground = Layout.FieldTop + 84;
        double ax = Screen.Width / 2;
        Stadium.DrawLiftingPlatform(dl, ground, wl.Lights);
        dl.PushClip(0, Layout.FieldTop, Screen.Width, Layout.FieldBottom - Layout.FieldTop);
        var pose = AthleteSprite.PoseFor(a.Pose, a.AnimPhase, time);
        AthleteSprite.Draw(dl, ax, ground, pose, AthleteColors.For(a), 1, a.Rotation, a.PlayerIndex + 1);
        // the bar follows the hands: on the floor, at the chest or overhead
        double barY = wl.State switch
        {
            LiftState.Ready => ground - 5.5,
            LiftState.Clean => ground - 5.5 - wl.PowerFraction * 11,
            LiftState.Jerk => ground - 16.5,
            LiftState.Hold or LiftState.Lifted => ground - 30,
            _ => ground - 5.5,
        };
        double barX = ax + (wl.State is LiftState.Hold or LiftState.Lifted ? 0 : 3);
        double tilt = wl.State == LiftState.Hold ? wl.Balance * 12 : 0;
        Stadium.DrawBarbell(dl, barX, barY, tilt, wl.Weight);
        if (wl.JerkPrompt) Hud.DrawPrompt(dl, ax, Layout.FieldTop + 10, loc["hud.press"], Palette.HudYellow, wl.JerkWindowLeft);
        if (wl.State == LiftState.Hold)
        {
            // balance meter above the lifter
            double mx = ax, my = Layout.FieldTop + 12;
            dl.FillRect(mx - 30, my, 60, 7, Palette.Black.WithAlpha(210));
            dl.FillRect(mx - 28, my + 3, 56, 1, Palette.Grey);
            dl.FillRect(mx - 0.5, my + 1, 1, 5, Palette.HudGreen);
            dl.FillRect(mx + wl.Balance * 26 - 1.5, my + 1, 3, 5, Math.Abs(wl.Balance) > 0.7 ? Palette.HudRed : Palette.HudYellow);
            PixelFont.Draw(dl, mx, my + 9, loc["hud.hold"], Palette.HudYellow, 0.7, TextAlign.Center);
        }
        else if (wl.State == LiftState.Lifted)
            PixelFont.Draw(dl, ax, Layout.FieldTop + 12, loc["hud.good_lift"], Palette.HudGreen, 1.2, TextAlign.Center);
        else if (wl.State == LiftState.Dropped)
            PixelFont.Draw(dl, ax, Layout.FieldTop + 12, loc["hud.no_lift"], Palette.HudRed, 1.2, TextAlign.Center);
        dl.PopClip();
        Stadium.DrawStands(dl, 0, time);
        Stadium.DrawWall(dl, loc[wl.Kind.Key()], game.Round);
        // weight on the bar, top right
        PixelFont.Draw(dl, Screen.Width - 6, Layout.FieldTop + 4, $"{loc["hud.weight"]} {Math.Round(wl.Weight)} {loc["hud.kg"]}", Palette.White, 0.9, TextAlign.Right);
        string? label = wl.State == LiftState.Clean ? $"{(int)Math.Ceiling(wl.CleanTimeLeft)}" : null;
        string title = wl.State is LiftState.Hold ? loc["hud.hold"] : loc["hud.power"];
        Hud.DrawSpeedBar(dl, game, wl.SpeedReadout(a.PlayerIndex), label, 1500, title + "=");
        DrawFieldBanner(dl, game, wl);
    }

    /// <summary>Banners of a one-turn event: READY before the turn, then the usual banners.</summary>
    private static void DrawTurnBanner(DrawList dl, Game game, TurnEventSimulation sim)
    {
        var loc = game.Localizer;
        if (sim.Phase == TurnPhase.Announce)
        {
            Hud.DrawBanner(dl, loc.Format("hud.player", sim.CurrentAthlete.PlayerIndex + 1) + " " + sim.CurrentAthlete.Name, Palette.HudCyan, Layout.FieldTop + 30, 1);
            Hud.DrawBanner(dl, loc["banner.ready"], Palette.HudYellow, Layout.FieldTop + 46, 1.3);
            return;
        }
        DrawFieldBanner(dl, game, sim);
    }

    private static void DrawFieldBanner(DrawList dl, Game game, EventSimulation sim)
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
