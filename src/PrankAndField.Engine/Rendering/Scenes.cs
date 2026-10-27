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

    public static void DrawTitle(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);

        // stadium backdrop at the bottom with two demo runners
        double scroll = time * 120;
        Stadium.DrawStands(dl, scroll, time);
        Stadium.DrawWall(dl, loc["game.subtitle"], 0);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, 60, Palette.Grass);
        dl.FillRect(0, Layout.FieldTop + 12, Screen.Width, 30, Palette.Track);
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
        AthleteSprite.Draw(dl, 150 + Math.Sin(time * 0.7) * 30, ground, DemoP2, time);
        AthleteSprite.Draw(dl, 110 + Math.Sin(time * 0.9) * 40, ground, DemoP1, time);

        // big title with drop shadow, in the HUD area
        string title = loc["game.title"];
        double size = 2.2;
        PixelFont.Draw(dl, Screen.Width / 2 + 2, 6 + 2, title, Palette.HudRed.Darker(0.5), size, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, 6, title, Palette.HudYellow, size, TextAlign.Center);
        dl.FillRect(40, 27, Screen.Width - 80, 1, Palette.HudCyan);
        PixelFont.Draw(dl, Screen.Width / 2, 30, loc["game.copyright"], Palette.HudCyan, 0.6, TextAlign.Center);

        // menu
        double my = 42;
        for (int i = 0; i < 2; i++)
        {
            bool sel = game.MenuSelection == i;
            string label = loc[i == 0 ? "title.players_1" : "title.players_2"];
            if (sel) PixelFont.Draw(dl, 92, my + i * 8, "→", Palette.HudRed, 0.9);
            PixelFont.Draw(dl, 102, my + i * 8, label, sel ? Palette.White : Palette.Grey, 0.9);
        }

        if (Math.Sin(time * 4) > -0.2)
            PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 48, loc["title.press_start"], Palette.HudYellow, 1.1, TextAlign.Center);

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
        Stadium.DrawWall(dl, record ? loc["banner.world_record"] : loc.Format("name.enter", game.EntryPlayer + 1), game.Round);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Grass);
        dl.FillRect(0, Layout.FieldTop + 20, Screen.Width, 60, Palette.Black.WithAlpha(230));
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
            var color = cur && Math.Sin(time * 10) > 0 ? Palette.HudRed : Palette.White;
            PixelFont.Draw(dl, x, y, game.EntryLetters[i].ToString(), color, 2.2, TextAlign.Center);
            if (cur)
            {
                PixelFont.Draw(dl, x, y - 9, "↑", Palette.HudYellow, 0.9, TextAlign.Center);
                PixelFont.Draw(dl, x, y + 19, "↓", Palette.HudYellow, 0.9, TextAlign.Center);
            }
        }
        PixelFont.Draw(dl, Screen.Width / 2, Screen.Height - 14, loc["name.hint"], Palette.HudGreen, 0.6, TextAlign.Center);
        var a = game.EntryPlayer == 1 ? DemoP2Human : DemoP1;
        a.Pose = Pose.Celebrate;
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
        Stadium.DrawWall(dl, loc["name.enter_both"], game.Round);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Grass);
        dl.FillRect(0, Layout.FieldTop + 20, Screen.Width, 60, Palette.Black.WithAlpha(230));
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
                var letterColor = slot.Done ? color : cur && Math.Sin(time * 10) > 0 ? Palette.White : color;
                PixelFont.Draw(dl, x, y, slot.Letters[i].ToString(), letterColor, 1.8, TextAlign.Center);
                if (cur)
                {
                    PixelFont.Draw(dl, x, y - 8, "↑", Palette.HudYellow, 0.8, TextAlign.Center);
                    PixelFont.Draw(dl, x, y + 16, "↓", Palette.HudYellow, 0.8, TextAlign.Center);
                }
            }
            if (slot.Done) PixelFont.Draw(dl, cx, Layout.FieldTop + 70, loc["name.ready"], Palette.HudGreen, 0.7, TextAlign.Center);
            var a = seat == 0 ? DemoP1 : DemoP2Human;
            a.Pose = slot.Done ? Pose.Celebrate : Pose.Idle;
            AthleteSprite.Draw(dl, cx + (seat == 0 ? -40 : 40), Layout.FieldBottom - 4, a, time);
        }
        PixelFont.Draw(dl, Screen.Width / 2, Screen.Height - 14, loc["name.hint"], Palette.HudGreen, 0.6, TextAlign.Center);
    }

    public static void DrawEventIntro(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 30, time);
        Stadium.DrawWall(dl, loc[game.CurrentEvent.Key()], game.Round);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Grass);
        dl.FillRect(16, Layout.FieldTop + 10, Screen.Width - 32, 92, Palette.Black.WithAlpha(235));
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 16, loc.Format("hud.round", game.Round), Palette.HudOrange, 0.9, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 28, loc[game.CurrentEvent.Key()], Palette.HudYellow, 1.4, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2 - 36, Layout.FieldTop + 46, loc["hud.qualify"], Palette.HudGreen, 1, TextAlign.Right);
        Hud.DrawMark(dl, Screen.Width / 2 - 28, Layout.FieldTop + 46, game.CurrentEvent, Rules.QualifyingMark(game.CurrentEvent, game.Round), loc, Palette.HudCyan, Palette.HudGreen, 1);
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
        if (game.CurrentEvent == EventKind.HammerThrow)
        {
            // a live turn with the hammer, like in the event itself
            double heading = (time * 240) % 360;
            double ax = 36, ground = Layout.FieldTop + 96;
            var ball = SpinnerSprite.BallAt(ax, ground, heading, SpinnerSprite.OrbitHeight(heading), 16);
            bool behind = Math.Sin(heading * Math.PI / 180.0) < 0;
            var colors = AthleteColors.For(a);
            var hands = SpinnerSprite.HandsAt(ax, ground, heading);
            void Ball()
            {
                dl.Line(hands.X, hands.Y, ball.X, ball.Y, 0.8, Palette.Wire);
                dl.FillCircle(ball.X, ball.Y, 2.4, Palette.Hammer);
            }
            if (behind) Ball();
            SpinnerSprite.Draw(dl, ax, ground, heading, colors, a.PlayerIndex + 1);
            if (!behind) Ball();
        }
        else AthleteSprite.Draw(dl, 36, Layout.FieldTop + 96, a, time);
    }

    public static void DrawOutcome(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 10, time);
        Stadium.DrawWall(dl, loc[game.CurrentEvent.Key()], game.Round);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Grass);
        dl.FillRect(16, Layout.FieldTop + 8, Screen.Width - 32, 100, Palette.Black.WithAlpha(235));
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 13, loc["outcome.title"], Palette.HudCyan, 1, TextAlign.Center);
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
        a.Pose = game.Outcomes.Any(o => o.Qualified) ? Pose.Celebrate : Pose.Fall;
        AthleteSprite.Draw(dl, Screen.Width - 36, Layout.FieldTop + 96, a, time);
    }

    public static void DrawCeremony(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Hud.DrawScoreboard(dl, game, time);
        Stadium.DrawStands(dl, time * 6, time);
        Stadium.DrawWall(dl, loc["ceremony.title"], game.Round);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Grass);
        double baseY = Layout.FieldTop + 92;
        // podium: 2nd (left), 1st (centre, tallest), 3rd (right)
        dl.FillRect(72, baseY - 14, 36, 14, Palette.LightGrey);
        dl.FillRect(108, baseY - 24, 40, 24, Palette.White);
        dl.FillRect(148, baseY - 9, 36, 9, Palette.LightGrey);
        PixelFont.Draw(dl, 90, baseY - 12, "2", Palette.Black, 1, TextAlign.Center);
        PixelFont.Draw(dl, 128, baseY - 22, "1", Palette.Black, 1.2, TextAlign.Center);
        PixelFont.Draw(dl, 166, baseY - 8, "3", Palette.Black, 1, TextAlign.Center);
        var leader = game.Leader;
        DemoP1.Pose = Pose.Celebrate;
        DemoP1.Name = leader.Name;
        var champ = new Athlete(leader.Index, false, leader.Name) { Pose = Pose.Celebrate };
        AthleteSprite.Draw(dl, 128, baseY - 24, champ, time);
        if (game.PlayerCount == 2)
        {
            var other = game.Players[1 - leader.Index];
            var second = new Athlete(other.Index, false, other.Name) { Pose = Pose.Idle };
            AthleteSprite.Draw(dl, 90, baseY - 14, second, time);
        }
        else
        {
            DemoP2.Pose = Pose.Idle;
            AthleteSprite.Draw(dl, 90, baseY - 14, DemoP2, time);
        }
        // confetti
        for (int i = 0; i < 40; i++)
        {
            double seed = i * 37.1;
            double x = (seed * 7 + time * (20 + i % 5 * 6)) % Screen.Width;
            double y = Layout.FieldTop + ((seed * 3 + time * (18 + i % 3 * 9)) % 90);
            dl.FillRect(x, y, 1.6, 1.6, Palette.Crowd[i % Palette.Crowd.Length]);
        }
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 6, loc["ceremony.champion"] + " " + leader.Name, Palette.HudYellow, 1.2, TextAlign.Center);
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
        Stadium.DrawWall(dl, loc["gameover.title"], game.Round);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Grass);
        dl.FillRect(16, Layout.FieldTop + 14, Screen.Width - 32, 70, Palette.Black.WithAlpha(235));
        PixelFont.Draw(dl, Screen.Width / 2, Layout.FieldTop + 22, loc["gameover.title"], Palette.HudRed, 1.8, TextAlign.Center);
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
        AthleteSprite.Draw(dl, 40, Layout.FieldBottom - 2, DemoP1, time);
    }

    /// <summary>Hidden practice menu: pick an event and a round, then play it directly.</summary>
    public static void DrawPracticeMenu(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
        Stadium.DrawStands(dl, time * 15, time);
        Stadium.DrawWall(dl, loc["practice.title"], game.Round);
        dl.FillRect(0, Layout.FieldTop, Screen.Width, Screen.Height - Layout.FieldTop, Palette.Grass);
        PixelFont.Draw(dl, Screen.Width / 2, 10, loc["practice.title"], Palette.HudYellow, 1.6, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, 30, loc.Format("practice.round", game.PracticeRound) + "  ←→", Palette.HudCyan, 1, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, 44, loc[game.MenuSelection == 0 ? "title.players_1" : "title.players_2"], Palette.White, 0.8, TextAlign.Center);
        dl.FillRect(16, Layout.FieldTop + 6, Screen.Width - 32, 76, Palette.Black.WithAlpha(235));
        for (int i = 0; i < EventKindExtensions.InOrder.Length; i++)
        {
            var kind = EventKindExtensions.InOrder[i];
            double y = Layout.FieldTop + 12 + i * 11;
            bool sel = kind == game.PracticeEvent;
            if (sel) PixelFont.Draw(dl, 30, y, "→", Palette.HudRed, 1);
            PixelFont.Draw(dl, 42, y, loc[kind.Key()], sel ? Palette.HudYellow : Palette.White, 1);
            Hud.DrawMark(dl, 236, y, kind, Rules.QualifyingMark(kind, game.PracticeRound), loc, sel ? Palette.HudCyan : Palette.Grey, Palette.HudGreen, 0.9, TextAlign.Right);
        }
        PixelFont.Draw(dl, Screen.Width / 2, Screen.Height - 14, loc["practice.hint"], Palette.HudGreen, 0.6, TextAlign.Center);
        DemoP1.Pose = Pose.Run;
        DemoP1.AnimPhase = (time * 2) % 1;
        AthleteSprite.Draw(dl, 22 + (time * 30) % 20, Layout.FieldBottom + 10, DemoP1, time);
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
