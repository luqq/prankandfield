using System.Globalization;
using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Core.Localization;
using PrankAndField.Engine.Graphics;
using PrankAndField.Engine.Sprites;

namespace PrankAndField.Engine.Rendering;

/// <summary>The red-framed scoreboard of the arcade and the in-field readouts (speed bar, angle box, banners).</summary>
public static class Hud
{
    /// <summary>Draws a mark the arcade way: big digits, small unit, two decimals ("9 SEC 95", "8 M 90"). Returns width.</summary>
    public static double DrawMark(DrawList dl, double x, double y, EventKind kind, double value, Localizer loc, Rgba digits, Rgba unit, double size = 1, TextAlign align = TextAlign.Left)
    {
        value = Math.Clamp(value, 0, 999.99);
        int whole = (int)Math.Floor(value + 1e-9);
        int cents = (int)Math.Round((value - whole) * 100);
        if (cents >= 100) { cents = 0; whole++; }
        string a = whole.ToString(CultureInfo.InvariantCulture);
        string u = kind.IsTimed() ? loc["hud.sec"] : loc["hud.m"];
        string b = cents.ToString("00", CultureInfo.InvariantCulture);
        double unitSize = size * 0.55;
        double w = PixelFont.Measure(a, size) + size + PixelFont.Measure(u, unitSize) + size + PixelFont.Measure(b, size);
        double startX = align switch { TextAlign.Center => x - w / 2, TextAlign.Right => x - w, _ => x };
        double cx = startX;
        PixelFont.Draw(dl, cx, y, a, digits, size);
        cx += PixelFont.Measure(a, size) + size;
        PixelFont.Draw(dl, cx, y + (PixelFont.GlyphH * size - PixelFont.GlyphH * unitSize) * 0.5, u, unit, unitSize);
        cx += PixelFont.Measure(u, unitSize) + size;
        PixelFont.Draw(dl, cx, y, b, digits, size);
        return w;
    }

    public static string Points(long n) => n.ToString(CultureInfo.InvariantCulture);

    private static void Box(DrawList dl, double x, double y, double w, double h, Rgba border, double t = 1)
    {
        dl.FillRect(x, y, w, t, border);
        dl.FillRect(x, y + h - t, w, t, border);
        dl.FillRect(x, y, t, h, border);
        dl.FillRect(x + w - t, y, t, h, border);
    }

    /// <summary>The top scoreboard: SCORE box, WORLD RECORD box, rank list, QUALIFY and the timer / tries.</summary>
    public static void DrawScoreboard(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        dl.FillRect(0, 0, Screen.Width, Layout.HudBottom, Palette.Black);
        Box(dl, 2, 2, Screen.Width - 4, Layout.HudBottom - 4, Palette.HudFrame, 2);

        // --- SCORE box ---
        Box(dl, 6, 7, 122, 31, Palette.HudCyan);
        dl.FillRect(44, 4, 44, 6, Palette.Black);
        PixelFont.Draw(dl, 66, 3.5, loc["hud.score"], Palette.White, 0.9, TextAlign.Center);
        for (int i = 0; i < game.PlayerCount; i++)
        {
            var p = game.Players[i];
            double y = 11 + i * 9;
            var color = p.IsOut ? Palette.DarkGrey : Palette.HudCyan;
            PixelFont.Draw(dl, 10, y, $"{i + 1}P", color, 1);
            PixelFont.Draw(dl, 26, y, p.Name, color, 1);
            // score right-aligned, then the chances as a mustachioed face with a count (like the arcade "face x0")
            PixelFont.Draw(dl, 100, y, Points(p.Score), Palette.White, 1, TextAlign.Right);
            DrawChanceIcon(dl, 110, y + 3.5, i == 0 ? AthleteColors.Player1 : AthleteColors.Player2);
            PixelFont.Draw(dl, 116, y, Math.Max(0, p.Chances).ToString(), Palette.HudPink, 1);
        }
        if (game.PlayerCount == 1)
        {
            PixelFont.Draw(dl, 10, 29, loc.Format("hud.round", game.Round), Palette.HudOrange, 0.75);
        }

        // --- WORLD RECORD box ---
        Box(dl, 132, 7, 118, 31, Palette.HudCyan);
        dl.FillRect(156, 4, 70, 6, Palette.Black);
        PixelFont.Draw(dl, 191, 3.5, loc["hud.world_record"], Palette.HudCyan, 0.9, TextAlign.Center);
        var records = game.Records.For(game.CurrentEvent);
        for (int i = 0; i < records.Count; i++)
        {
            double y = 12 + i * 8;
            PixelFont.Draw(dl, 136, y + 1, loc[$"hud.rank{i + 1}"], Palette.White, 0.75);
            DrawMark(dl, 160, y, game.CurrentEvent, records[i].Mark, loc, Palette.HudYellow, Palette.HudGreen, 1);
            PixelFont.Draw(dl, 246, y, records[i].Initials, Palette.HudYellow, 1, TextAlign.Right);
        }

        // --- lower rows ---
        DrawLowerRows(dl, game, time);
    }

    private static void DrawChanceIcon(DrawList dl, double cx, double cy, AthleteColors colors)
    {
        dl.FillCircle(cx, cy, 2.6, colors.Skin);
        dl.FillRect(cx - 2.4, cy - 2.6, 4.8, 1.4, colors.Hair);
        dl.FillRect(cx - 1.4, cy + 0.4, 2.8, 0.9, Palette.Mustache);
        dl.FillRect(cx - 1.2, cy - 0.9, 0.7, 0.7, Palette.Black);
        dl.FillRect(cx + 0.5, cy - 0.9, 0.7, 0.7, Palette.Black);
    }

    private static void DrawLowerRows(DrawList dl, Game game, double time)
    {
        var loc = game.Localizer;
        var sim = game.Simulation;
        // Four small rows must fit between the boxes (y 40) and the inner edge of the red frame (y 62).
        const double y0 = 40.5;
        const double rowH = 5.4;
        const double small = 0.7;

        // rank list on the left (lanes / players)
        var entries = new List<(string label, Rgba color, bool arrow)>();
        if (sim is RaceSimulation race)
        {
            var ordered = race.Athletes
                .OrderBy(a => a.Finished ? a.FinishTime : 1000 - a.X)
                .ToList();
            foreach (var a in ordered)
            {
                string label = a.IsCpu ? $"{a.PlayerIndex + 1}P {loc["hud.computer"]}" : $"{a.PlayerIndex + 1}P {a.Name}";
                entries.Add((label, a.IsCpu ? Palette.White : Palette.HudCyan, true));
            }
        }
        else
        {
            for (int i = 0; i < game.PlayerCount; i++)
            {
                var p = game.Players[i];
                if (p.IsOut) continue;
                entries.Add(($"{i + 1}P {p.Name}", Palette.HudCyan, sim is FieldEventSimulation f && f.CurrentAthlete.PlayerIndex == i));
            }
        }
        for (int i = 0; i < 4; i++)
        {
            double y = y0 + i * rowH;
            PixelFont.Draw(dl, 8, y, loc[$"hud.rank{i + 1}"], Palette.White, small);
            if (i < entries.Count)
            {
                var (label, color, arrow) = entries[i];
                if (arrow) PixelFont.Draw(dl, 26, y, "→", Palette.HudRed, small);
                PixelFont.Draw(dl, 33, y, label, color, small);
            }
        }

        // QUALIFY
        PixelFont.Draw(dl, 136, y0, loc["hud.qualify"], Palette.HudGreen, small);
        DrawMark(dl, 186, y0 - 0.8, game.CurrentEvent, Rules.QualifyingMark(game.CurrentEvent, game.Round), loc, Palette.HudCyan, Palette.HudGreen, 0.85);

        if (sim is RaceSimulation r)
        {
            // timer box, inside the frame
            Box(dl, 168, 47, 78, 14, Palette.HudCyan);
            var humans = r.Athletes.Where(a => !a.IsCpu).OrderBy(a => a.PlayerIndex).ToList();
            if (humans.Count == 1)
            {
                double t = r.Phase >= RacePhase.Running ? r.TimerFor(humans[0].PlayerIndex) : 0;
                DrawTimer(dl, 207, 50.5, t, Palette.White, 1, TextAlign.Center);
            }
            else
            {
                for (int i = 0; i < humans.Count && i < 2; i++)
                {
                    double t = r.Phase >= RacePhase.Running ? r.TimerFor(humans[i].PlayerIndex) : 0;
                    DrawTimer(dl, 188 + i * 38, 50.5, t, i == 0 ? Palette.HudCyan : Palette.HudPink, 0.9, TextAlign.Center);
                }
            }
        }
        else if (sim is FieldEventSimulation field)
        {
            var tries = field.AttemptsFor(field.CurrentAthlete.PlayerIndex);
            for (int i = 0; i < Rules.TriesPerFieldEvent; i++)
            {
                double y = y0 + (i + 1) * rowH;
                bool current = field.CurrentTry == i + 1 && field.Phase != AttemptPhase.Finished;
                var color = current ? Palette.HudYellow : Palette.White;
                PixelFont.Draw(dl, 136, y, loc[$"hud.try{i + 1}"], color, small);
                var at = tries[i];
                if (at.Foul) PixelFont.Draw(dl, 192, y, loc["banner.foul"], Palette.HudRed, small);
                else if (at.Mark is double m) DrawMark(dl, 192, y - 0.3, field.Kind, m, loc, Palette.HudCyan, Palette.HudGreen, small);
                if (current && Math.Sin(time * 8) > 0) PixelFont.Draw(dl, 246, y, "←", Palette.HudRed, small, TextAlign.Right);
            }
        }
        else
        {
            for (int i = 0; i < Rules.TriesPerFieldEvent && !game.CurrentEvent.IsTimed(); i++)
                PixelFont.Draw(dl, 136, y0 + (i + 1) * rowH, loc[$"hud.try{i + 1}"], Palette.White, small);
        }
    }

    public static void DrawTimer(DrawList dl, double x, double y, double seconds, Rgba color, double size, TextAlign align)
    {
        int whole = (int)Math.Floor(seconds);
        int cents = (int)Math.Round((seconds - whole) * 100);
        if (cents >= 100) { cents = 0; whole++; }
        string s = $"{whole,2}:{cents:00}";
        PixelFont.Draw(dl, x, y, s, color, size, align);
    }

    /// <summary>The black band between the two lanes: "2UP 0000 CM/SEC   1UP 0000 CM/SEC".</summary>
    public static void DrawRaceBand(DrawList dl, Game game, RaceSimulation race)
    {
        var loc = game.Localizer;
        double top = Layout.StripATop + Layout.StripHeight;
        dl.FillRect(0, top, Screen.Width, Layout.BandHeight, Palette.Black);
        int i = 0;
        foreach (var a in race.Athletes.OrderByDescending(x => x.PlayerIndex))
        {
            double x = i == 0 ? 4 : 134;
            string label = loc.Format("hud.player", a.PlayerIndex + 1);
            PixelFont.Draw(dl, x, top + 1.5, label, a.IsCpu ? Palette.White : Palette.HudCyan, 0.9);
            string speed = ((int)Math.Round(a.Runner.SpeedCm)).ToString("0000", CultureInfo.InvariantCulture);
            PixelFont.Draw(dl, x + 30, top + 1.5, speed, Palette.White, 0.9);
            PixelFont.Draw(dl, x + 56, top + 2.5, loc["hud.cmsec"], Palette.HudYellow, 0.6);
            i++;
        }
    }

    /// <summary>Bottom bar: SPEED= blocks and the numeric cm/sec readout.</summary>
    public static void DrawSpeedBar(DrawList dl, Game game, double speedCm, string? extraLabel = null)
    {
        var loc = game.Localizer;
        double top = Layout.BottomTop;
        dl.FillRect(0, top, Screen.Width, Screen.Height - top, Palette.Black);
        PixelFont.Draw(dl, 4, top + 5, loc["hud.speed"], Palette.White, 0.9);
        int blocks = (int)Math.Round(Math.Clamp(speedCm / 1550.0, 0, 1) * 24);
        for (int i = 0; i < 24; i++)
        {
            var c = i < blocks ? (i < 16 ? Palette.HudYellow : Palette.HudBlue) : Palette.DarkGrey.Darker(0.5);
            dl.FillRect(44 + i * 5.5, top + 4, 4.5, 8, c);
        }
        string speed = ((int)Math.Round(speedCm)).ToString("0000", CultureInfo.InvariantCulture);
        PixelFont.Draw(dl, 206, top + 4.5, speed, Palette.White, 1, TextAlign.Right);
        PixelFont.Draw(dl, 208, top + 6, loc["hud.cmsec"], Palette.HudYellow, 0.6);
        if (extraLabel is not null) PixelFont.Draw(dl, 4, top + 15, extraLabel, Palette.HudOrange, 0.75);
    }

    public static void DrawAngleBox(DrawList dl, double cx, double y, double angle)
    {
        dl.FillRect(cx - 14, y, 28, 11, Palette.Black);
        Box(dl, cx - 14, y, 28, 11, Palette.HudCyan);
        PixelFont.Draw(dl, cx, y + 2, $"{(int)Math.Round(angle)}°", Palette.White, 1, TextAlign.Center);
    }

    /// <summary>Big centered banner over the field (FOUL, GO!, QUALIFIED...).</summary>
    public static void DrawBanner(DrawList dl, string text, Rgba color, double y, double size = 1.5)
    {
        double w = PixelFont.CanDraw(text) ? PixelFont.Measure(text, size) + 10 : text.Length * PixelFont.CellW * size + 10;
        double h = PixelFont.CellH * size + 4;
        dl.FillRect(Screen.Width / 2 - w / 2, y - 2, w, h, Palette.Black.WithAlpha(200));
        Box(dl, Screen.Width / 2 - w / 2, y - 2, w, h, color);
        PixelFont.Draw(dl, Screen.Width / 2, y + 1, text, color, size, TextAlign.Center);
    }

    public static string BannerText(EventSimulation sim, Localizer loc) => sim.Banner switch
    {
        BannerKind.Foul => loc["banner.foul"],
        BannerKind.FalseStart => loc["banner.false_start"],
        BannerKind.Qualified => loc["banner.qualified"],
        BannerKind.NotQualified => loc["banner.not_qualified"],
        BannerKind.Bonus => loc.Format("banner.bonus", sim.BannerValue),
        BannerKind.WorldRecord => loc["banner.world_record"],
        BannerKind.Dizzy => loc["banner.dizzy"],
        BannerKind.WrongDirection => loc["banner.wrong_direction"],
        _ => string.Empty,
    };
}
