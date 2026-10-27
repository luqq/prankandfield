using PrankAndField.Core;
using PrankAndField.Engine.Graphics;
using PrankAndField.Engine.Sprites;

namespace PrankAndField.Engine.Rendering;

/// <summary>Non-event screens: attract title, name entry, event card, results, record entry, podium, game over.</summary>
public static class Scenes
{
    private static readonly Athlete DemoP1 = new(0, false, "AAA");
    private static readonly Athlete DemoP2 = new(1, true, "CPU");
    private static readonly Athlete DemoP2Human = new(1, false, "AAA");

    /// <summary>Text colour of each player, matching the shorts of their athlete.</summary>
    private static Rgba PlayerColor(int seat) => seat == 0 ? Rgba.Hex(0x6888F8) : Palette.HudRed;

    private static uint Hash(int a, int b)
    {
        uint h = (uint)(a * 374761393) ^ (uint)(b * 668265263);
        h ^= h >> 13;
        h *= 1274126177;
        h ^= h >> 16;
        return h;
    }

    /// <summary>Evening sky with stars and a low sun, filling the area above the stands.</summary>
    private static void DrawSky(DrawList dl, double bottom, double time)
    {
        double split = bottom * 0.6;
        dl.FillGradientRect(0, 0, Screen.Width, split, Palette.SkyTop, Palette.SkyMid);
        dl.FillGradientRect(0, split, Screen.Width, bottom - split, Palette.SkyMid, Palette.SkyHorizon);
        for (int i = 0; i < 46; i++)
        {
            uint hsh = Hash(i, 3);
            double x = hsh % 256;
            double y = (hsh >> 8) % (uint)Math.Max(1, (int)(split * 0.9));
            double twinkle = 0.5 + 0.5 * Math.Sin(time * 2.5 + i * 1.7);
            dl.FillRect(x, y, 0.9, 0.9, Palette.Star.WithAlpha((byte)(90 + 140 * twinkle)));
        }
        dl.Glow(200, bottom - 6, 34, Palette.SkyGlow.WithAlpha(160));
        dl.FillCircle(200, bottom - 6, 9, Palette.Sun);
        dl.FillCircle(200, bottom - 6, 7, Palette.Sun.Lighter(0.4));
    }

    /// <summary>Floodlight mast rising above the stands, with its glowing lamp head.</summary>
    private static void DrawMast(DrawList dl, double x, double headY)
    {
        dl.Line(x, Layout.StandsTop, x, headY + 2, 1.4, Palette.RoofDark);
        dl.Line(x - 2.5, Layout.StandsTop, x, headY + 10, 0.6, Palette.RoofDark);
        dl.Line(x + 2.5, Layout.StandsTop, x, headY + 10, 0.6, Palette.RoofDark);
        dl.Glow(x, headY + 1.5, 16, Palette.FloodGlow.WithAlpha(110));
        dl.FillRect(x - 7, headY, 14, 3.5, Palette.Roof);
        for (int k = 0; k < 4; k++) dl.FillRect(x - 6 + k * 3.4, headY + 0.8, 2.4, 2, Palette.Floodlight);
    }

    /// <summary>Dark translucent card with a thin frame, used by every menu and result screen.</summary>
    private static void DrawCard(DrawList dl, double x, double y, double w, double h, Rgba border)
    {
        dl.FillRect(x + 2, y + 2, w, h, Palette.Black.WithAlpha(120));
        dl.FillGradientRect(x, y, w, h, Palette.HudPanel.WithAlpha(236), Palette.HudPanelDark.WithAlpha(240));
        dl.FillRect(x, y, w, 1, border);
        dl.FillRect(x, y + h - 1, w, 1, border);
        dl.FillRect(x, y, 1, h, border);
        dl.FillRect(x + w - 1, y, 1, h, border);
        dl.FillRect(x + 1, y + 1, w - 2, 0.8, border.WithAlpha(60));
    }

    /// <summary>Big title lettering: extruded shadow, glow and a light bevel.</summary>
    private static void DrawTitleText(DrawList dl, double cx, double y, string text, double size, Rgba color, Rgba shadow)
    {
        // Two shadow copies are enough for the extrusion; every copy costs ~8 rects per character.
        dl.Glow(cx, y + PixelFont.GlyphH * size / 2, PixelFont.Measure(text, size) * 0.6, color.WithAlpha(55));
        PixelFont.Draw(dl, cx + 2.2, y + 2.2, text, shadow, size, TextAlign.Center);
        PixelFont.Draw(dl, cx + 1, y + 1, text, shadow.Lighter(0.25), size, TextAlign.Center);
        PixelFont.Draw(dl, cx, y, text, color, size, TextAlign.Center);
    }

    public static void DrawTitle(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        DrawSky(dl, Layout.StandsTop, time);
        DrawMast(dl, 26, 12);
        DrawMast(dl, 230, 12);

        // stadium backdrop at the bottom with two demo runners
        double scroll = time * 120;
        Stadium.DrawStands(dl, scroll, time);
        Stadium.DrawWall(dl, loc["game.subtitle"], 0, scroll, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, 60, scroll / Layout.PxPerMeterX);
        dl.FillGradientRect(0, Layout.FieldTop + 12, Screen.Width, 30, Palette.TrackDark, Palette.Track);
        dl.FillGradientRect(0, Layout.FieldTop + 35, Screen.Width, 7, Palette.Track, Palette.TrackLight);
        dl.FillRect(0, Layout.FieldTop + 12, Screen.Width, 1, Palette.LaneLine);
        dl.FillRect(0, Layout.FieldTop + 41, Screen.Width, 1, Palette.LaneLine);
        for (int i = -1; i < 14; i++)
        {
            double x = (i * 22 - scroll % 22);
            dl.FillRect(x, Layout.FieldTop + 13, 1, 3, Palette.LaneLine);
        }
        DemoP1.Pose = Pose.Run; DemoP1.AnimPhase = (time * 2.2) % 1;
        DemoP2.Pose = Pose.Run; DemoP2.AnimPhase = (time * 2.0 + 0.4) % 1;
        double ground = Layout.FieldTop + 40;
        double x2 = 150 + Math.Sin(time * 0.7) * 30, x1 = 110 + Math.Sin(time * 0.9) * 40;
        AthleteSprite.DrawShadow(dl, x2, ground);
        AthleteSprite.DrawShadow(dl, x1, ground);
        AthleteSprite.Draw(dl, x2, ground, DemoP2, time);
        AthleteSprite.Draw(dl, x1, ground, DemoP1, time);

        // big title with extrusion and glow, in the HUD area
        string title = loc["game.title"];
        DrawTitleText(dl, Screen.Width / 2, 6, title, 2.2, Palette.HudYellow, Palette.HudFrameDark);
        dl.FillGradientRect(40, 27, Screen.Width / 2 - 40, 1, Palette.HudCyan.WithAlpha(0), Palette.HudCyan, horizontal: true);
        dl.FillGradientRect(Screen.Width / 2, 27, Screen.Width / 2 - 40, 1, Palette.HudCyan, Palette.HudCyan.WithAlpha(0), horizontal: true);
        PixelFont.Draw(dl, Screen.Width / 2, 30, loc["game.copyright"], Palette.HudCyan, 0.6, TextAlign.Center);

        // menu
        double my = 42;
        for (int i = 0; i < 2; i++)
        {
            bool sel = game.MenuSelection == i;
            string label = loc[i == 0 ? "title.players_1" : "title.players_2"];
            if (sel)
            {
                dl.FillGradientRect(86, my + i * 8 - 1.5, 84, 9, Palette.HudCyan.WithAlpha(70), Palette.HudCyan.WithAlpha(10), horizontal: true);
                PixelFont.Draw(dl, 92 + Math.Sin(time * 6) * 1.2, my + i * 8, "→", Palette.HudRed, 0.9);
            }
            Hud.ShadowText(dl, 102, my + i * 8, label, sel ? Palette.White : Palette.LightGrey, 0.9);
        }

        if (Math.Sin(time * 4) > -0.2)
        {
            dl.Glow(Screen.Width / 2, Layout.FieldTop + 52, 40, Palette.HudYellow.WithAlpha(40));
            Hud.ShadowText(dl, Screen.Width / 2, Layout.FieldTop + 48, loc["title.press_start"], Palette.HudYellow, 1.1, TextAlign.Center);
        }

        dl.FillRect(0, Layout.FieldTop + 60, Screen.Width, Screen.Height - Layout.FieldTop - 60, Palette.HudPanelDark.WithAlpha(150));
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 64, loc["title.controls_p1"], Palette.HudCyan, 0.7, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 72, loc["title.controls_p2"], Palette.HudPink, 0.7, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 84, loc.Format("title.language", loc["lang.name"]), Palette.White, 0.7, TextAlign.Center);

        // records ticker
        var kind = (EventKind)((int)(time / 3) % 6);
        var rec = game.Records.Best(kind);
        PixelFont.Draw(dl, 8, Screen.Height - 16, loc["hud.world_record"] + " " + loc[kind.Key()], Palette.HudGreen, 0.7);
        Hud.DrawMark(dl, 8, Screen.Height - 9, kind, rec.Mark, loc, Palette.HudYellow, Palette.HudGreen, 0.8);
        PixelFont.Draw(dl, 70, Screen.Height - 9, rec.Initials, Palette.HudYellow, 0.8);
    }

    public static void DrawTextEntry(DrawList dl, Game game, double time, bool record)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 20, time);
        Stadium.DrawWall(dl, record ? loc["banner.world_record"] : loc.Format("name.enter", game.EntryPlayer + 1), game.Round, time * 20, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, Screen.Height - Layout.FieldTop, time * 20 / Layout.PxPerMeterX);
        DrawCard(dl, 12, Layout.FieldTop + 18, Screen.Width - 24, 64, record ? Palette.HudCyan : PlayerColor(game.EntryPlayer));
        string heading = record ? loc["record.enter"] : loc.Format("name.enter", game.EntryPlayer + 1);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 26, heading, record ? Palette.HudCyan : PlayerColor(game.EntryPlayer), 0.9, TextAlign.Center);
        if (record)
        {
            PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 36, loc.Format("record.rank", game.EntryRecordRank), Palette.HudYellow, 0.9, TextAlign.Center);
            Hud.DrawMark(dl, Screen.Width / 2, Layout.FieldTop + 46, game.CurrentEvent, game.EntryMark, loc, Palette.HudYellow, Palette.HudGreen, 1.2, TextAlign.Center);
        }
        double y = Layout.FieldTop + (record ? 60 : 44);
        for (int i = 0; i < 3; i++)
        {
            double x = Screen.Width / 2 - 24 + i * 24;
            bool cur = game.EntryCursor == i;
            if (cur) dl.FillRect(x - 9, y - 3, 18, 22, Palette.HudCyan.WithAlpha(30));
            var color = cur && Math.Sin(time * 10) > 0 ? Palette.HudRed : Palette.White;
            Hud.ShadowText(dl, x, y, game.EntryLetters[i].ToString(), color, 2.2, TextAlign.Center);
            if (cur)
            {
                PixelFont.Draw(dl, x, y - 9, "↑", Palette.HudYellow, 0.9, TextAlign.Center);
                PixelFont.Draw(dl, x, y + 19, "↓", Palette.HudYellow, 0.9, TextAlign.Center);
            }
        }
        PixelFont.Draw(dl, Screen.Width / 2, Screen.Height - 14, loc["name.hint"], Palette.HudGreen, 0.6, TextAlign.Center);
        var a = game.EntryPlayer == 1 ? DemoP2Human : DemoP1;
        a.Pose = Pose.Celebrate;
        AthleteSprite.DrawShadow(dl, 30, Layout.FieldBottom - 4);
        AthleteSprite.Draw(dl, 30, Layout.FieldBottom - 4, a, time);
    }

    /// <summary>
    /// Online name entry: one column per seat, each in its player's colour, typed at the same time.
    /// <paramref name="localSeat"/> marks the column this device controls.
    /// </summary>
    public static void DrawSimultaneousNameEntry(DrawList dl, Game game, double time, int? localSeat)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 20, time);
        Stadium.DrawWall(dl, loc["name.enter_both"], game.Round, time * 20, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, Screen.Height - Layout.FieldTop, time * 20 / Layout.PxPerMeterX);
        DrawCard(dl, 12, Layout.FieldTop + 18, Screen.Width - 24, 64, Palette.HudCyan);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 24, loc["name.enter_both"], Palette.HudCyan, 0.8, TextAlign.Center);
        for (int seat = 0; seat < 2; seat++)
        {
            var slot = game.NameSlots[seat];
            var color = PlayerColor(seat);
            double cx = seat == 0 ? Screen.Width * 0.27 : Screen.Width * 0.73;
            string label = loc.Format("hud.player", seat + 1);
            if (localSeat == seat) label += " (" + loc["name.you"] + ")";
            PixelFont.Draw(dl, cx, Layout.FieldTop + 34, label, color, 0.7, TextAlign.Center);
            double y = Layout.FieldTop + 50;
            for (int i = 0; i < 3; i++)
            {
                double x = cx - 18 + i * 18;
                bool cur = !slot.Done && slot.Cursor == i;
                if (cur) dl.FillRect(x - 7, y - 2, 14, 18, color.WithAlpha(40));
                var letterColor = slot.Done ? color : cur && Math.Sin(time * 10) > 0 ? Palette.White : color;
                Hud.ShadowText(dl, x, y, slot.Letters[i].ToString(), letterColor, 1.8, TextAlign.Center);
                if (cur)
                {
                    PixelFont.Draw(dl, x, y - 8, "↑", Palette.HudYellow, 0.8, TextAlign.Center);
                    PixelFont.Draw(dl, x, y + 16, "↓", Palette.HudYellow, 0.8, TextAlign.Center);
                }
            }
            if (slot.Done) PixelFont.Draw(dl, cx, Layout.FieldTop + 70, loc["name.ready"], Palette.HudGreen, 0.7, TextAlign.Center);
            var a = seat == 0 ? DemoP1 : DemoP2Human;
            a.Pose = slot.Done ? Pose.Celebrate : Pose.Idle;
            double ax = cx + (seat == 0 ? -40 : 40);
            AthleteSprite.DrawShadow(dl, ax, Layout.FieldBottom - 4);
            AthleteSprite.Draw(dl, ax, Layout.FieldBottom - 4, a, time);
        }
        PixelFont.Draw(dl, Screen.Width / 2, Screen.Height - 14, loc["name.hint"], Palette.HudGreen, 0.6, TextAlign.Center);
    }

    /// <summary>Little pictogram of the event next to the demo athlete on the event card.</summary>
    private static void DrawEventProp(DrawList dl, EventKind kind, double x, double groundY, double time)
    {
        switch (kind)
        {
            case EventKind.Hurdles110m:
                Stadium.DrawHurdle(dl, x + 18, groundY, 0);
                break;
            case EventKind.JavelinThrow:
                {
                    var dir = Vec2.FromAngle(-38);
                    var tail = new Vec2(x + 10, groundY - 10) - dir * 9;
                    var tip = new Vec2(x + 10, groundY - 10) + dir * 9;
                    dl.Line(tail, tip, 1.1, Palette.Javelin);
                    dl.Line(tip - dir * 3, tip, 1.6, Palette.JavelinTip);
                    break;
                }
            case EventKind.HammerThrow:
                {
                    double a = time * 4;
                    double hx = x + Math.Cos(a) * 14, hy = groundY - 10 + Math.Sin(a) * 4;
                    dl.Line(x, groundY - 10, hx, hy, 0.8, Palette.Wire);
                    dl.FillCircle(hx, hy, 2.8, Palette.Hammer);
                    dl.FillCircle(hx - 0.8, hy - 0.8, 1, Palette.HammerShine);
                    break;
                }
            case EventKind.HighJump:
                dl.FillRect(x + 14, groundY - 30, 1.6, 30, Palette.Upright);
                dl.FillRect(x + 14, groundY - 22, 30, 1.4, Palette.Bar);
                dl.FillRect(x + 22, groundY - 22, 5, 1.4, Palette.FlagRed);
                dl.FillPolygon(Palette.Mat, new Vec2(x + 16, groundY - 10), new Vec2(x + 44, groundY - 10), new Vec2(x + 46, groundY), new Vec2(x + 14, groundY));
                break;
            case EventKind.LongJump:
                dl.FillPolygon(Palette.Sand, new Vec2(x + 12, groundY - 6), new Vec2(x + 46, groundY - 6), new Vec2(x + 50, groundY + 2), new Vec2(x + 8, groundY + 2));
                dl.FillRect(x + 6, groundY - 6, 2.5, 8, Palette.Board);
                break;
        }
    }

    public static void DrawEventIntro(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 30, time);
        Stadium.DrawWall(dl, loc[game.CurrentEvent.Key()], game.Round, time * 30, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, Screen.Height - Layout.FieldTop, time * 30 / Layout.PxPerMeterX);
        DrawCard(dl, 16, Layout.FieldTop + 8, Screen.Width - 32, 94, Palette.HudYellow);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 16, loc.Format("hud.round", game.Round), Palette.HudOrange, 0.9, TextAlign.Center);
        DrawTitleText(dl, Screen.Width / 2, Layout.FieldTop + 28, loc[game.CurrentEvent.Key()], 1.4, Palette.HudYellow, Palette.HudFrameDark);
        PixelFont.Draw(dl, Screen.Width / 2 - 36, Layout.FieldTop + 46, loc["hud.qualify"], Palette.HudGreen, 1, TextAlign.Right);
        Hud.DrawMark(dl, Screen.Width / 2 - 28, Layout.FieldTop + 46, game.CurrentEvent, Rules.QualifyingMark(game.CurrentEvent, game.Round), loc, Palette.HudCyan, Palette.HudGreen, 1);
        dl.FillRect(28, Layout.FieldTop + 57, Screen.Width - 56, 0.5, Palette.HudYellow.WithAlpha(80));
        // instructions, wrapped on two lines if long
        string instr = loc["instr." + game.CurrentEvent.Key()["event.".Length..]];
        var lines = Wrap(instr, 52);
        for (int i = 0; i < lines.Count && i < 3; i++)
            PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 62 + i * 8, lines[i], Palette.White, 0.7, TextAlign.Center);
        var a = DemoP1;
        a.Pose = game.CurrentEvent switch
        {
            EventKind.LongJump => Pose.Fly,
            EventKind.JavelinThrow => Pose.Throw,
            EventKind.Hurdles110m => Pose.Hurdle,
            EventKind.HammerThrow => Pose.Spin,
            EventKind.HighJump => Pose.Flop,
            _ => Pose.Run,
        };
        a.AnimPhase = (time * 2) % 1;
        double ground = Layout.FieldTop + 96;
        DrawEventProp(dl, game.CurrentEvent, 40, ground, time);
        AthleteSprite.DrawShadow(dl, 36, ground);
        AthleteSprite.Draw(dl, 36, ground, a, time);
    }

    public static void DrawOutcome(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 10, time);
        Stadium.DrawWall(dl, loc[game.CurrentEvent.Key()], game.Round, time * 10, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, Screen.Height - Layout.FieldTop, time * 10 / Layout.PxPerMeterX);
        bool anyQualified = game.Outcomes.Any(o => o.Qualified);
        DrawCard(dl, 16, Layout.FieldTop + 8, Screen.Width - 32, 100, anyQualified ? Palette.HudGreen : Palette.HudRed);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 13, loc["outcome.title"], Palette.HudCyan, 1, TextAlign.Center);
        dl.FillRect(28, Layout.FieldTop + 23, Screen.Width - 56, 0.5, Palette.HudCyan.WithAlpha(80));
        double y = Layout.FieldTop + 28;
        foreach (var o in game.Outcomes)
        {
            var p = game.Players[o.PlayerIndex];
            PixelFont.Draw(dl, 24, y, $"{o.PlayerIndex + 1}P {p.Name}", Palette.HudCyan, 1);
            if (o.Mark is double m) Hud.DrawMark(dl, 100, y, game.CurrentEvent, m, loc, Palette.HudYellow, Palette.HudGreen, 1);
            else PixelFont.Draw(dl, 100, y, loc["banner.foul"], Palette.HudRed, 1);
            string verdict = o.Qualified ? loc["banner.qualified"] : loc["banner.not_qualified"];
            PixelFont.Draw(dl, 232, y, verdict, o.Qualified ? Palette.HudGreen : Palette.HudRed, 0.8, TextAlign.Right);
            y += 9;
            PixelFont.Draw(dl, 40, y, loc.Format("outcome.points", Hud.Points(o.Points + o.Bonus)), Palette.White, 0.75);
            if (o.RecordRank == 1) PixelFont.Draw(dl, 232, y, loc["banner.world_record"], Palette.HudYellow, 0.75, TextAlign.Right);
            else if (o.RecordRank > 1) PixelFont.Draw(dl, 232, y, loc.Format("record.rank", o.RecordRank), Palette.HudYellow, 0.75, TextAlign.Right);
            y += 7;
            if (o.ExtraChance) { PixelFont.Draw(dl, 40, y, loc["banner.extra_chance"], Palette.HudPink, 0.75); y += 7; }
            if (o.Eliminated) { PixelFont.Draw(dl, 40, y, loc.Format("banner.player_out", o.PlayerIndex + 1), Palette.HudRed, 0.75); y += 7; }
            y += 4;
        }
        var a = DemoP1;
        a.Pose = anyQualified ? Pose.Celebrate : Pose.Fall;
        AthleteSprite.DrawShadow(dl, Screen.Width - 36, Layout.FieldTop + 96);
        AthleteSprite.Draw(dl, Screen.Width - 36, Layout.FieldTop + 96, a, time);
    }

    public static void DrawCeremony(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 6, time);
        Stadium.DrawWall(dl, loc["ceremony.title"], game.Round, time * 6, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, Screen.Height - Layout.FieldTop, time * 6 / Layout.PxPerMeterX);
        double baseY = Layout.FieldTop + 92;
        // spotlights sweeping from the top corners onto the podium
        double sweep = Math.Sin(time * 0.8) * 10;
        dl.FillPolygon(Palette.White.WithAlpha(26), new Vec2(0, Layout.FieldTop), new Vec2(24, Layout.FieldTop), new Vec2(140 + sweep, baseY), new Vec2(100 + sweep, baseY));
        dl.FillPolygon(Palette.White.WithAlpha(26), new Vec2(Screen.Width, Layout.FieldTop), new Vec2(Screen.Width - 24, Layout.FieldTop), new Vec2(116 - sweep, baseY), new Vec2(156 - sweep, baseY));
        // podium: 2nd (left), 1st (centre, tallest), 3rd (right)
        dl.FillEllipse(128, baseY + 2, 62, 3, Palette.Shadow);
        dl.FillGradientRect(72, baseY - 14, 36, 14, Palette.LightGrey, Palette.Grey);
        dl.FillGradientRect(108, baseY - 24, 40, 24, Palette.White, Palette.LightGrey);
        dl.FillGradientRect(148, baseY - 9, 36, 9, Palette.LightGrey, Palette.Grey);
        dl.FillRect(72, baseY - 14, 36, 1, Palette.White);
        dl.FillRect(108, baseY - 24, 40, 1, Palette.White);
        dl.FillRect(148, baseY - 9, 36, 1, Palette.White);
        PixelFont.Draw(dl, 90, baseY - 12, "2", Palette.Black, 1, TextAlign.Center);
        PixelFont.Draw(dl, 128, baseY - 22, "1", Palette.Black, 1.2, TextAlign.Center);
        PixelFont.Draw(dl, 166, baseY - 8, "3", Palette.Black, 1, TextAlign.Center);
        var leader = game.Leader;
        DemoP1.Pose = Pose.Celebrate;
        DemoP1.Name = leader.Name;
        var champ = new Athlete(leader.Index, false, leader.Name) { Pose = Pose.Celebrate };
        AthleteSprite.DrawShadow(dl, 128, baseY - 24);
        AthleteSprite.Draw(dl, 128, baseY - 24, champ, time);
        // the gold medal glinting on the champion's chest
        double glint = 0.5 + 0.5 * Math.Sin(time * 5);
        dl.Glow(128.6, baseY - 24 - 13, 4, Palette.FlagGold.WithAlpha((byte)(80 + 100 * glint)));
        dl.FillCircle(128.6, baseY - 24 - 13, 1.3, Palette.FlagGold);
        if (game.PlayerCount == 2)
        {
            var other = game.Players[1 - leader.Index];
            var second = new Athlete(other.Index, false, other.Name) { Pose = Pose.Idle };
            AthleteSprite.DrawShadow(dl, 90, baseY - 14);
            AthleteSprite.Draw(dl, 90, baseY - 14, second, time);
        }
        else
        {
            DemoP2.Pose = Pose.Idle;
            AthleteSprite.DrawShadow(dl, 90, baseY - 14);
            AthleteSprite.Draw(dl, 90, baseY - 14, DemoP2, time);
        }
        dl.FillCircle(90.6, baseY - 14 - 13, 1.3, Palette.FlagSilver);
        // confetti
        for (int i = 0; i < 40; i++)
        {
            double seed = i * 37.1;
            double x = (seed * 7 + time * (20 + i % 5 * 6)) % Screen.Width;
            double y = Layout.FieldTop + ((seed * 3 + time * (18 + i % 3 * 9)) % 90);
            double spin = Math.Sin(time * 9 + i);
            dl.FillRect(x, y, 1.6, 1.0 + Math.Abs(spin) * 1.2, Palette.Crowd[i % Palette.Crowd.Length]);
        }
        // fireworks in the sky above the stands, deterministic from the clock
        for (int k = 0; k < 2; k++)
        {
            double period = 1.7 + k * 0.6;
            int shot = (int)Math.Floor((time + k * 0.9) / period);
            double age = ((time + k * 0.9) % period) / period;
            uint hsh = Hash(shot, k);
            double fxX = 30 + hsh % 196, fxY = Layout.FieldTop + 6 + (hsh >> 8) % 30;
            var color = Palette.Crowd[(hsh >> 16) % (uint)Palette.Crowd.Length];
            if (age < 0.75)
            {
                double r = 4 + age * 26;
                byte alpha = (byte)(230 * (1 - age / 0.75));
                dl.Glow(fxX, fxY, r * 0.7, color.WithAlpha((byte)(alpha / 3)));
                for (int s = 0; s < 12; s++)
                {
                    double ang = s * Math.PI / 6 + shot;
                    double drop = age * age * 18;
                    double px = fxX + Math.Cos(ang) * r, py = fxY + Math.Sin(ang) * r * 0.8 + drop;
                    dl.FillRect(px - 0.7, py - 0.7, 1.4, 1.4, (s % 3 == 0 ? Palette.Spark : color).WithAlpha(alpha));
                }
            }
        }
        Hud.ShadowText(dl, Screen.Width / 2, Layout.FieldTop + 6, loc["ceremony.champion"] + " " + leader.Name, Palette.HudYellow, 1.2, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, Screen.Height - 14, loc["ceremony.next_round"], Palette.HudCyan, 0.7, TextAlign.Center);
        // the flag of the champion rising
        double rise = Math.Min(1, time / 4);
        double fx = 214, fy = Layout.FieldTop + 70 - rise * 50;
        dl.FillRect(fx, Layout.FieldTop + 12, 1.2, 80, Palette.White);
        dl.FillRect(fx + 1, fy, 16, 10, Palette.White);
        dl.FillRect(fx + 1, fy + 3.3, 16, 3.4, Palette.FlagRed);
    }

    public static void DrawGameOver(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, 0, time);
        Stadium.DrawWall(dl, loc["gameover.title"], game.Round, 0, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, Screen.Height - Layout.FieldTop, 0);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Black.WithAlpha(90));
        DrawCard(dl, 16, Layout.FieldTop + 14, Screen.Width - 32, 70, Palette.HudRed);
        DrawTitleText(dl, Screen.Width / 2, Layout.FieldTop + 22, loc["gameover.title"], 1.8, Palette.HudRed, Palette.HudFrameDark);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 44, loc["gameover.final_score"], Palette.HudCyan, 0.9, TextAlign.Center);
        double y = Layout.FieldTop + 54;
        for (int i = 0; i < game.PlayerCount; i++)
        {
            var p = game.Players[i];
            PixelFont.Draw(dl, Screen.Width / 2, y, $"{i + 1}P {p.Name}  {Hud.Points(p.Score)}", Palette.HudYellow, 1, TextAlign.Center);
            y += 9;
        }
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 76, loc["gameover.thanks"], Palette.White, 0.7, TextAlign.Center);
        DemoP1.Pose = Pose.Fall;
        AthleteSprite.DrawShadow(dl, 40, Layout.FieldBottom - 2);
        AthleteSprite.Draw(dl, 40, Layout.FieldBottom - 2, DemoP1, time);
    }

    /// <summary>Hidden practice menu: pick an event and a round, then play it directly.</summary>
    public static void DrawPracticeMenu(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        DrawSky(dl, Layout.StandsTop, time);
        Stadium.DrawStands(dl, time * 15, time);
        Stadium.DrawWall(dl, loc["practice.title"], game.Round, time * 15, time);
        Stadium.DrawGrass(dl, Layout.FieldTop, Screen.Height - Layout.FieldTop, time * 15 / Layout.PxPerMeterX);
        DrawTitleText(dl, Screen.Width / 2, 10, loc["practice.title"], 1.6, Palette.HudYellow, Palette.HudFrameDark);
        Hud.ShadowText(dl, Screen.Width / 2, 30, loc.Format("practice.round", game.PracticeRound) + "  ←→", Palette.HudCyan, 1, TextAlign.Center);
        Hud.ShadowText(dl, Screen.Width / 2, 44, loc[game.MenuSelection == 0 ? "title.players_1" : "title.players_2"], Palette.White, 0.8, TextAlign.Center);
        DrawCard(dl, 16, Layout.FieldTop + 6, Screen.Width - 32, 76, Palette.HudCyan);
        for (int i = 0; i < EventKindExtensions.InOrder.Length; i++)
        {
            var kind = EventKindExtensions.InOrder[i];
            double y = Layout.FieldTop + 12 + i * 11;
            bool sel = kind == game.PracticeEvent;
            if (sel)
            {
                dl.FillGradientRect(22, y - 2, Screen.Width - 44, 11, Palette.HudCyan.WithAlpha(60), Palette.HudCyan.WithAlpha(8), horizontal: true);
                PixelFont.Draw(dl, 30 + Math.Sin(time * 6) * 1.2, y, "→", Palette.HudRed, 1);
            }
            PixelFont.Draw(dl, 42, y, loc[kind.Key()], sel ? Palette.HudYellow : Palette.White, 1);
            Hud.DrawMark(dl, 236, y, kind, Rules.QualifyingMark(kind, game.PracticeRound), loc, sel ? Palette.HudCyan : Palette.Grey, Palette.HudGreen, 0.9, TextAlign.Right);
        }
        PixelFont.Draw(dl, Screen.Width / 2, Screen.Height - 14, loc["practice.hint"], Palette.HudGreen, 0.6, TextAlign.Center);
        DemoP1.Pose = Pose.Run;
        DemoP1.AnimPhase = (time * 2) % 1;
        double ax = 22 + (time * 30) % 20;
        AthleteSprite.DrawShadow(dl, ax, Layout.FieldBottom + 10);
        AthleteSprite.Draw(dl, ax, Layout.FieldBottom + 10, DemoP1, time);
    }

    private static List<string> Wrap(string text, int maxChars)
    {
        var lines = new List<string>();
        if (!PixelFont.CanDraw(text)) { lines.Add(text); return lines; }
        var words = text.Split(' ');
        string cur = string.Empty;
        foreach (var w in words)
        {
            if (cur.Length + w.Length + 1 > maxChars && cur.Length > 0)
            {
                lines.Add(cur);
                cur = w;
            }
            else cur = cur.Length == 0 ? w : cur + " " + w;
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }
}
