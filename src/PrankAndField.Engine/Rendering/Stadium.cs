using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Rendering;

/// <summary>Draws the stadium furniture: floodlit grandstand, wall with boards and flags, lanes and sectors.</summary>
public static class Stadium
{
    private static readonly string[] AdTexts = ["HIRYU SOFT", "PRANK & FIELD", "VECTOR CUP", "1983"];
    private static readonly (Rgba Back, Rgba Fore)[] AdColors =
    [
        (Rgba.Hex(0x1A2A7A), Palette.HudYellow), (Palette.FlagRed, Palette.White),
        (Rgba.Hex(0x1C7A3A), Palette.White), (Palette.HudYellow, Palette.Black),
    ];

    private static uint Hash(int a, int b)
    {
        uint h = (uint)(a * 374761393) ^ (uint)(b * 668265263);
        h ^= h >> 13;
        h *= 1274126177;
        h ^= h >> 16;
        return h;
    }

    private static double Mod(double v, double m)
    {
        double r = v % m;
        return r < 0 ? r + m : r;
    }

    /// <summary>Grandstand: a floodlit roof over terraces full of spectators with flags and camera flashes, scrolling at half speed.</summary>
    public static void DrawStands(DrawList dl, double scrollPx, double time)
    {
        double top = Layout.StandsTop, bottom = Layout.StandsBottom;
        const double roofH = 5;
        // roof underside with banks of floodlights (far away: quarter speed)
        dl.FillGradientRect(0, top, Screen.Width, roofH, Palette.RoofDark, Palette.Roof);
        double lampOff = Mod(scrollPx * 0.25, 48);
        for (double x = -lampOff - 48; x < Screen.Width + 48; x += 48)
        {
            dl.Glow(x + 6, top + 4.5, 7, Palette.FloodGlow.WithAlpha(120));
            dl.FillRect(x + 1.5, top + 1.4, 9, 0.8, Palette.Grey);
            for (int k = 0; k < 3; k++) dl.FillRect(x + 2 + k * 3, top + 2.4, 2, 1.6, Palette.Floodlight);
        }
        // terraces
        double terraceTop = top + roofH;
        const int rows = 4;
        double rowH = (bottom - 2 - terraceTop) / rows;
        dl.FillGradientRect(0, terraceTop, Screen.Width, bottom - terraceTop, Palette.TerraceBack, Palette.StandRow);
        double offset = Mod(scrollPx * 0.5, 4.0);
        int baseCol = (int)Math.Floor(scrollPx * 0.5 / 4.0);
        for (int r = 0; r < rows; r++)
        {
            double y = terraceTop + r * rowH;
            dl.FillRect(0, y, Screen.Width, rowH, r % 2 == 0 ? Palette.StandRow : Palette.StandRowAlt);
            for (int c = -1; c <= Screen.Width / 4 + 1; c++)
            {
                int worldCol = baseCol + c;
                uint hsh = Hash(worldCol, r);
                var shirt = Palette.Crowd[hsh % (uint)Palette.Crowd.Length];
                var skin = Palette.CrowdSkin[(hsh >> 6) % (uint)Palette.CrowdSkin.Length];
                // a few spectators wave: bob up and down over time
                bool waver = (hsh >> 8) % 7 == 0;
                double bob = waver ? Math.Abs(Math.Sin(time * 5 + hsh % 13)) * 1.2 : 0;
                double x = c * 4 - offset + ((hsh >> 4) % 2);
                dl.FillRect(x, y + 1.3 - bob, 2.2, 2.6, shirt);
                dl.FillRect(x + 0.4, y + 0.2 - bob, 1.4, 1.2, skin);
                if (waver)
                {
                    // raised arms
                    dl.FillRect(x - 0.3, y + 0.4 - bob, 0.5, 1.4, skin);
                    dl.FillRect(x + 2.0, y + 0.4 - bob, 0.5, 1.4, skin);
                }
                else if ((hsh >> 12) % 9 == 0)
                {
                    // a small flag on a stick, fluttering
                    var flag = Palette.Crowd[(hsh >> 16) % (uint)Palette.Crowd.Length];
                    double wave = Math.Sin(time * 8 + hsh % 11) * 0.5;
                    dl.FillRect(x + 2.2, y - 1.4, 0.4, 2.8, Palette.LightGrey);
                    dl.FillPolygon(flag, new Vec2(x + 2.6, y - 1.4), new Vec2(x + 5.0, y - 0.6 + wave), new Vec2(x + 2.6, y + 0.4));
                }
                // camera flashes: a handful of photographers, each firing for a tenth of a second now and then
                if ((hsh >> 20) % 23 == 0 && Mod(time * 0.9 + hsh % 17, 7) < 0.1)
                {
                    dl.Glow(x + 1.1, y + 0.6, 3.5, Palette.Flash.WithAlpha(240));
                }
            }
            dl.FillRect(0, y + rowH - 0.6, Screen.Width, 0.6, Palette.Rail.WithAlpha(160));
        }
        // front barrier
        dl.FillRect(0, bottom - 2, Screen.Width, 2, Palette.WallDark);
        dl.FillRect(0, bottom - 2, Screen.Width, 0.7, Palette.Rail);
    }

    /// <summary>
    /// The wall between the stands and the field: advertising boards scrolling with the stands, the electronic
    /// board with the event name in the middle and two flags fluttering at the ends.
    /// </summary>
    public static void DrawWall(DrawList dl, string eventName, int round, double scrollPx = 0, double time = 0)
    {
        double top = Layout.WallTop, h = Layout.WallBottom - Layout.WallTop;
        dl.FillGradientRect(0, top, Screen.Width, h, Palette.WallLight, Palette.Wall);
        dl.FillRect(0, top, Screen.Width, 1.2, Palette.WallDark);
        dl.FillRect(0, top + h - 1.5, Screen.Width, 1.5, Palette.WallDark);
        // the electronic board with the event name: capped so it never reaches the flags, shrinking the text to fit
        const double boardMaxW = Screen.Width - 60;
        double textW = PixelFont.CanDraw(eventName) ? PixelFont.Measure(eventName, 1) : eventName.Length * PixelFont.CellW;
        double boardW = Math.Clamp(textW + 18, 96, boardMaxW);
        double textSize = Math.Min(1, (boardW - 10) / Math.Max(textW, 1));
        double bx = Screen.Width / 2 - boardW / 2;
        // advertising boards (their text is dropped where the central board or a flag would cut it)
        const double flagL = 7, flagR = 21, flag2L = Screen.Width - 23, flag2R = Screen.Width - 9;
        bool Covered(double l, double r) => (r >= bx - 1 && l <= bx + boardW + 1) || (r >= flagL && l <= flagR) || (r >= flag2L && l <= flag2R);
        const double panelW = 64;
        double cycle = panelW * AdTexts.Length;
        double off = Mod(scrollPx * 0.5, cycle);
        for (int i = -1; i <= (int)(Screen.Width / panelW) + AdTexts.Length; i++)
        {
            double x = i * panelW - off;
            if (x > Screen.Width || x + panelW < 0) continue;
            if (x + 2 >= bx - 1 && x + panelW - 2 <= bx + boardW + 1) continue; // fully hidden behind the board
            int idx = ((i % AdTexts.Length) + AdTexts.Length) % AdTexts.Length;
            var (back, fore) = AdColors[idx];
            dl.FillRect(x + 2, top + 3, panelW - 4, 8, back);
            dl.FillRect(x + 2, top + 3, panelW - 4, 0.6, back.Lighter(0.4));
            double adW = PixelFont.Measure(AdTexts[idx], 0.7);
            double adL = x + panelW / 2 - adW / 2, adR = adL + adW;
            if (!Covered(adL, adR))
                PixelFont.Draw(dl, x + panelW / 2, top + 4.6, AdTexts[idx], fore, 0.7, TextAlign.Center);
        }
        dl.FillRect(bx - 1, top + 1, boardW + 2, h - 2, Palette.WallDark);
        dl.FillRect(bx, top + 2, boardW, h - 4, Palette.Scoreboard);
        dl.FillRect(bx + 1, top + 3, boardW - 2, 0.5, Palette.HudFrame.WithAlpha(150));
        double ty = top + (h - PixelFont.GlyphH * textSize) / 2;
        PixelFont.Draw(dl, Screen.Width / 2 + 0.6, ty + 0.6, eventName, Palette.ScoreboardText.Darker(0.45), textSize, TextAlign.Center);
        PixelFont.Draw(dl, Screen.Width / 2, ty, eventName, Palette.ScoreboardText, textSize, TextAlign.Center);
        DrawFlag(dl, 8, top + 2.5, round, time);
        DrawFlag(dl, Screen.Width - 22, top + 2.5, round + 1, time + 1.3);
    }

    private static void DrawFlag(DrawList dl, double x, double y, int variant, double time)
    {
        const int slices = 8;
        const double w = 12, h = 8;
        dl.FillRect(x - 0.8, y - 1.5, 0.8, h + 4, Palette.LightGrey);
        Vec2[] Band(double y0, double y1)
        {
            var pts = new Vec2[2 * (slices + 1)];
            for (int s = 0; s <= slices; s++)
            {
                double wave = Math.Sin(time * 6 + s * 0.8) * 0.8 * (s / (double)slices);
                pts[s] = new Vec2(x + s * w / slices, y + y0 + wave);
                pts[2 * slices + 1 - s] = new Vec2(x + s * w / slices, y + y1 + wave);
            }
            return pts;
        }
        switch (variant % 3)
        {
            case 0: // red disc
                dl.FillPolygon(Palette.White, Band(0, h));
                dl.FillCircle(x + 6, y + 4, 2.4, Palette.FlagRed);
                break;
            case 1: // tricolour bands
                dl.FillPolygon(Palette.FlagRed, Band(0, h / 3));
                dl.FillPolygon(Palette.FlagYellow, Band(h / 3, 2 * h / 3));
                dl.FillPolygon(Palette.FlagRed, Band(2 * h / 3, h));
                break;
            default: // stripes and canton
                dl.FillPolygon(Palette.White, Band(0, h));
                for (int i = 0; i < 4; i++) dl.FillPolygon(Palette.FlagRed, Band(i * 2, i * 2 + 1));
                dl.FillRect(x, y, 5, 4, Palette.FlagBlue);
                break;
        }
    }

    /// <summary>Grass with mowing stripes fixed to the world (every 4 m), so they scroll with the camera.</summary>
    public static void DrawGrass(DrawList dl, double top, double h, double cameraX)
    {
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        double period = 4 * Layout.PxPerMeterX;
        double first = -Mod(cameraX * Layout.PxPerMeterX, 2 * period);
        for (double x = first; x < Screen.Width; x += 2 * period) dl.FillRect(x, top, period, h, Palette.GrassStripe);
    }

    /// <summary>A running lane strip: grass, terracotta track with white lines and distance ticks, blocks, start and finish.</summary>
    public static void DrawLaneStrip(DrawList dl, double top, double cameraX, int laneNumber, double finishM, string laneLabel, Rgba labelColor)
    {
        double h = Layout.StripHeight;
        dl.PushClip(0, top, Screen.Width, h);
        DrawGrass(dl, top, h, cameraX);
        double trackTop = top + 8, trackH = 28;
        dl.FillGradientRect(0, trackTop, Screen.Width, trackH, Palette.TrackDark, Palette.Track);
        dl.FillGradientRect(0, trackTop + trackH - 7, Screen.Width, 7, Palette.Track, Palette.TrackLight);
        dl.FillRect(0, trackTop - 0.8, Screen.Width, 0.8, Palette.GrassDark);
        dl.FillRect(0, trackTop, Screen.Width, 1, Palette.LaneLine);
        dl.FillRect(0, trackTop + trackH - 1, Screen.Width, 1, Palette.LaneLine);
        dl.FillRect(0, trackTop + trackH / 2 - 0.4, Screen.Width, 0.8, Palette.TrackDark);

        // distance ticks every 10 m
        double firstTick = Math.Floor(cameraX / 10) * 10;
        for (double m = firstTick; m <= cameraX + Screen.Width / Layout.PxPerMeterX + 10; m += 10)
        {
            double sx = Layout.ToScreenX(m, cameraX);
            if (sx < -4 || sx > Screen.Width + 4) continue;
            dl.FillRect(sx, trackTop + 1, 1, 4, Palette.LaneLine);
            dl.FillRect(sx, trackTop + trackH - 5, 1, 4, Palette.LaneLine);
            if (m > 0 && m < finishM) PixelFont.Draw(dl, sx + 2, trackTop + 2, ((int)m).ToString(), Palette.LaneLine, 0.5);
        }
        // start line, blocks, lane number box and the runner's label
        double startX = Layout.ToScreenX(0, cameraX);
        if (startX > -40 && startX < Screen.Width)
        {
            dl.FillRect(startX - 1, trackTop, 2, trackH, Palette.LaneLine);
            // starting blocks behind the line
            dl.FillRect(startX - 7, trackTop + trackH - 2.6, 6, 1, Palette.DarkGrey);
            dl.FillPolygon(Palette.HudRed.Darker(0.8), new Vec2(startX - 6.5, trackTop + trackH - 2.6), new Vec2(startX - 4.5, trackTop + trackH - 2.6), new Vec2(startX - 4.5, trackTop + trackH - 4.6));
            dl.FillPolygon(Palette.HudRed.Darker(0.8), new Vec2(startX - 3.5, trackTop + trackH - 2.6), new Vec2(startX - 1.5, trackTop + trackH - 2.6), new Vec2(startX - 1.5, trackTop + trackH - 4.2));
            dl.FillRect(startX - 17, trackTop + trackH / 2 - 6, 12, 12, Palette.Outline.WithAlpha(120));
            dl.FillRect(startX - 16, trackTop + trackH / 2 - 5, 10, 10, Palette.White);
            PixelFont.Draw(dl, startX - 11, trackTop + trackH / 2 - 3.5, laneNumber.ToString(), Palette.Black, 1, TextAlign.Center);
            PixelFont.Draw(dl, startX + 14.6, trackTop + 3.1, laneLabel, Palette.Outline.WithAlpha(160), 1);
            PixelFont.Draw(dl, startX + 14, trackTop + 2.5, laneLabel, labelColor, 1);
        }
        // finish line (chequered band) with the photo-finish camera on its post
        double fx = Layout.ToScreenX(finishM, cameraX);
        if (fx > -10 && fx < Screen.Width + 10)
        {
            for (int i = 0; i < 7; i++)
            {
                dl.FillRect(fx - 1, trackTop + i * 4, 2, 2, Palette.White);
                dl.FillRect(fx + 1, trackTop + i * 4 + 2, 2, 2, Palette.White);
            }
            dl.FillRect(fx - 2, trackTop - 8, 1.2, 8, Palette.White);
            dl.FillRect(fx - 2, trackTop - 8, 7, 3, Palette.FlagRed);
            dl.FillRect(fx + 1, trackTop + trackH, 1.2, 5, Palette.White);
            dl.FillRect(fx - 0.5, trackTop + trackH + 1, 4, 2.6, Palette.DarkGrey);
            dl.FillCircle(fx + 1.5, trackTop + trackH + 2.3, 0.7, Palette.HudCyan);
        }
        dl.PopClip();
    }

    public static void DrawHurdle(DrawList dl, double sx, double groundY, double fallAnim)
    {
        // A hurdle: two uprights, a top bar with blue stripe. When knocked, it tilts forward.
        // 1.07 m tall against a 1.8 m athlete: about 14 px when the sprite is 24 px.
        const double h = 14;
        double tilt = fallAnim * 80;
        var baseL = new Vec2(sx - 1.5, groundY);
        Vec2 Rot(Vec2 p) => baseL + (p - baseL).Rotate(tilt);
        var tl = Rot(new Vec2(sx - 1.5, groundY - h));
        var tr = Rot(new Vec2(sx + 1.5, groundY - h));
        var bl = baseL;
        var br = Rot(new Vec2(sx + 1.5, groundY));
        dl.FillEllipse(sx + 1, groundY + 0.6, 4.5, 1.1, Palette.Shadow);
        dl.Line(bl, tl, 1.9, Palette.Outline.WithAlpha(170));
        dl.Line(br, tr, 1.9, Palette.Outline.WithAlpha(170));
        dl.Line(bl, tl, 1, Palette.Hurdle);
        dl.Line(br, tr, 1, Palette.Hurdle);
        dl.Line(Rot(new Vec2(sx - 1.5, groundY - h * 0.45)), Rot(new Vec2(sx + 1.5, groundY - h * 0.45)), 0.8, Palette.Hurdle);
        var barL = Rot(new Vec2(sx - 2.4, groundY - h));
        var barR = Rot(new Vec2(sx + 2.4, groundY - h));
        dl.Line(barL, barR, 3.2, Palette.Outline.WithAlpha(170));
        dl.Line(barL, barR, 2.4, Palette.HurdleBar);
        dl.Line(Rot(new Vec2(sx - 2.4, groundY - h + 0.7)), Rot(new Vec2(sx + 2.4, groundY - h + 0.7)), 0.9, Palette.HurdleStripe);
        dl.FillRect(sx - 3.5, groundY - 0.5, 7, 1, Palette.Hurdle);
    }

    /// <summary>Long-jump runway and sand pit (single wide strip).</summary>
    public static void DrawLongJumpField(DrawList dl, double cameraX, double boardM, double landingMark)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        DrawGrass(dl, top, h, cameraX);
        double runTop = top + 30, runH = 30;
        double boardX = Layout.ToScreenX(boardM, cameraX);
        // runway up to the board
        double runW = Math.Max(0, boardX + 2);
        dl.FillGradientRect(0, runTop, runW, runH, Palette.TrackDark, Palette.Track);
        dl.FillGradientRect(0, runTop + runH - 7, runW, 7, Palette.Track, Palette.TrackLight);
        dl.FillRect(0, runTop, runW, 1, Palette.LaneLine);
        dl.FillRect(0, runTop + runH - 1, runW, 1, Palette.LaneLine);
        // ticks every 5 m on the runway
        for (double m = Math.Floor(cameraX / 5) * 5; m <= boardM; m += 5)
        {
            double sx = Layout.ToScreenX(m, cameraX);
            if (sx < -2 || sx > Screen.Width) continue;
            dl.FillRect(sx, runTop + 1, 0.8, 3, Palette.LaneLine);
        }
        // the board + plasticine
        dl.FillRect(boardX - 2.5, runTop, 2.5, runH, Palette.Board);
        dl.FillRect(boardX, runTop, 1, runH, Palette.Plasticine);
        // sand pit from board+1m to board+11m
        double pitL = Layout.ToScreenX(boardM + Rules.LongJumpPitStart, cameraX);
        double pitR = Layout.ToScreenX(boardM + 11.0, cameraX);
        var pit = new[]
        {
            new Vec2(pitL, runTop - 4), new Vec2(pitR, runTop - 4), new Vec2(pitR + 6, runTop + runH + 4), new Vec2(pitL - 6, runTop + runH + 4),
        };
        dl.FillPolygon(Palette.SandEdge, pit);
        var sand = new[]
        {
            new Vec2(pitL + 1.5, runTop - 2.5), new Vec2(pitR - 1.5, runTop - 2.5), new Vec2(pitR + 4, runTop + runH + 2.5), new Vec2(pitL - 4, runTop + runH + 2.5),
        };
        dl.FillPolygon(Palette.Sand, sand);
        // raked sand: light grains and faint rake lines fixed to the pit
        if (pitR > 0 && pitL < Screen.Width)
        {
            for (int i = 0; i < 48; i++)
            {
                uint hsh = Hash(i, 91);
                double gx = pitL + 2 + (hsh % 1000) / 1000.0 * (pitR - pitL - 4);
                double gy = runTop - 1 + ((hsh >> 10) % 1000) / 1000.0 * (runH + 2);
                dl.FillRect(gx, gy, 0.9, 0.9, (hsh >> 20) % 3 == 0 ? Palette.SandEdge : Palette.SandLight);
            }
            for (int k = 1; k < 5; k++)
            {
                double ly = runTop + k * runH / 5;
                dl.Line(pitL + 1, ly, pitR - 1, ly, 0.4, Palette.SandEdge.WithAlpha(90));
            }
        }
        // metre markers board (white strip above the pit with numbers)
        dl.FillRect(pitL, runTop - 10.6, pitR - pitL, 5.6, Palette.Outline.WithAlpha(90));
        dl.FillRect(pitL, runTop - 10, pitR - pitL, 5, Palette.White);
        for (int m = 2; m <= 11; m++)
        {
            double sx = Layout.ToScreenX(boardM + m, cameraX);
            dl.FillRect(sx, runTop - 10, 0.8, 5, Palette.Black);
            PixelFont.Draw(dl, sx + 1.5, runTop - 9.5, m.ToString(), Palette.Black, 0.5);
        }
        if (landingMark > 0)
        {
            double sx = Layout.ToScreenX(boardM + landingMark, cameraX);
            dl.FillEllipse(sx - 1.5, runTop + runH * 0.55, 2.2, 1.2, Palette.SandEdge.Darker(0.8));
            dl.FillEllipse(sx + 1.0, runTop + runH * 0.62, 2.2, 1.2, Palette.SandEdge.Darker(0.8));
            dl.FillRect(sx - 0.6, runTop + 4, 1.2, runH - 8, Palette.HudRed.WithAlpha(170));
        }
        dl.PopClip();
    }

    /// <summary>Distance arcs across a throwing sector, centred on the measuring point.</summary>
    private static void DrawSectorArcs(DrawList dl, double originX, double originY, double originM, double cameraX, double top, double h)
    {
        for (int m = 10; m <= 110; m += 10)
        {
            double sx = Layout.ToScreenX(originM + m, cameraX);
            if (sx < -10 || sx > Screen.Width + 10) continue;
            double r = sx - originX;
            if (r > 2) dl.Arc(originX, originY, r, -24, 24, 0.8, Palette.White.WithAlpha(150));
            else dl.FillRect(sx, top + 6, 0.8, h - 12, Palette.White.WithAlpha(150));
            PixelFont.Draw(dl, sx + 2, top + 8, m.ToString(), Palette.White, 0.75);
        }
    }

    /// <summary>Javelin runway and landing sector with 10 m arcs.</summary>
    public static void DrawJavelinField(DrawList dl, double cameraX, double lineM)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        DrawGrass(dl, top, h, cameraX);
        double runTop = top + 34, runH = 26;
        double lineX = Layout.ToScreenX(lineM, cameraX);
        double runW = Math.Max(0, lineX);
        dl.FillGradientRect(0, runTop, runW, runH, Palette.TrackDark, Palette.Track);
        dl.FillGradientRect(0, runTop + runH - 6, runW, 6, Palette.Track, Palette.TrackLight);
        dl.FillRect(0, runTop, runW, 1, Palette.LaneLine);
        dl.FillRect(0, runTop + runH - 1, runW, 1, Palette.LaneLine);
        // foul arc at the end of the runway
        double cy = runTop + runH / 2;
        dl.Arc(lineX - 52, cy, 58, -16, 16, 1.6, Palette.White);
        // sector lines
        double sectorStart = lineX + 4;
        dl.Line(sectorStart, cy, Screen.Width + 20, top - 10, 0.8, Palette.White.WithAlpha(190));
        dl.Line(sectorStart, cy, Screen.Width + 20, top + h + 10, 0.8, Palette.White.WithAlpha(190));
        DrawSectorArcs(dl, lineX, cy, lineM, cameraX, top, h);
        dl.PopClip();
    }

    /// <summary>Hammer circle with cage on the left and the landing sector to the right.</summary>
    public static void DrawHammerField(DrawList dl, double cameraX)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        DrawGrass(dl, top, h, cameraX);
        double cx = Layout.ToScreenX(0, cameraX);
        double cy = top + 60;
        // throwing circle: concrete with a rim and a highlight
        dl.FillEllipse(cx, cy + 1, 16.5, 9.5, Palette.Outline.WithAlpha(90));
        dl.FillEllipse(cx, cy, 16, 9, Palette.LightGrey);
        dl.FillEllipse(cx, cy, 14.5, 7.8, Palette.Grey);
        dl.FillEllipse(cx - 1, cy - 1, 11, 5.5, Palette.Grey.Lighter(0.12));
        dl.FillRect(cx - 14, cy - 0.3, 28, 0.6, Palette.DarkGrey.WithAlpha(120));
        // cage: posts, net and the top ring
        var cage = new List<Vec2>();
        for (int i = 0; i <= 10; i++)
        {
            double a = Math.PI * 0.55 + i / 10.0 * Math.PI * 0.9;
            cage.Add(new Vec2(cx + Math.Cos(a) * 24, cy + Math.Sin(a) * 15));
        }
        var net = Palette.Cage.WithAlpha(110);
        for (int i = 0; i < 10; i++)
        {
            // diagonal mesh between posts
            var p = cage[i];
            var q = cage[i + 1];
            dl.Line(p.X, p.Y - 14, q.X, q.Y, 0.4, net);
            dl.Line(p.X, p.Y, q.X, q.Y - 14, 0.4, net);
        }
        for (int k = 1; k < 4; k++)
        {
            var row = new Vec2[cage.Count];
            for (int i = 0; i < cage.Count; i++) row[i] = new Vec2(cage[i].X, cage[i].Y - k * 3.5);
            dl.Polyline(0.4, net, row);
        }
        dl.Polyline(1.2, Palette.Cage, cage.ToArray());
        for (int i = 0; i <= 10; i += 2)
        {
            var p = cage[i];
            dl.Line(p.X, p.Y + 0.5, p.X, p.Y - 14, 1.4, Palette.Outline.WithAlpha(120));
            dl.Line(p.X, p.Y, p.X, p.Y - 14, 1, Palette.Cage);
        }
        var topRing = new Vec2[cage.Count];
        for (int i = 0; i < cage.Count; i++) topRing[i] = new Vec2(cage[i].X, cage[i].Y - 14);
        dl.Polyline(1, Palette.Cage, topRing);
        // sector
        dl.Line(cx + 14, cy, Screen.Width + 400, top - 40, 0.8, Palette.White.WithAlpha(190));
        dl.Line(cx + 14, cy, Screen.Width + 400, top + h + 40, 0.8, Palette.White.WithAlpha(190));
        DrawSectorArcs(dl, cx, cy, 0, cameraX, top, h);
        dl.PopClip();
    }

    /// <summary>Pixels a jumper standing on the high-jump mat is drawn above the ground line (false perspective).</summary>
    public const double HighJumpMatLift = 9;

    /// <summary>
    /// High-jump apron with the painted take-off zone, both uprights, the bar and the landing mat.
    /// The bar plane is at world x = 0; the mat is drawn to the right of it in false perspective.
    /// </summary>
    public static void DrawHighJumpField(DrawList dl, double cameraX, double barHeightM, double barFallAnim, double groundY)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        DrawGrass(dl, top, h, cameraX);
        double barX = Layout.ToScreenX(0, cameraX);
        const double apronTop = -3, apronH = 8;

        // apron (approach lane) up to the uprights
        double apronW = Math.Max(0, barX - 1);
        dl.FillGradientRect(0, groundY + apronTop, apronW, apronH, Palette.TrackDark, Palette.TrackLight);
        dl.FillRect(0, groundY + apronTop, apronW, 0.8, Palette.LaneLine);
        dl.FillRect(0, groundY + apronTop + apronH - 0.8, apronW, 0.8, Palette.LaneLine);
        for (double m = Math.Floor(cameraX / 2) * 2; m < 0; m += 2)
        {
            double sx = Layout.ToScreenX(m, cameraX);
            if (sx < -2 || sx > Screen.Width) continue;
            dl.FillRect(sx, groundY + apronTop + 1, 0.8, 2, Palette.LaneLine);
        }

        // take-off zone: a pale band with the best spot as a white line
        double zoneL = Layout.ToScreenX(Rules.HighJumpZoneFar, cameraX);
        double zoneR = Layout.ToScreenX(Rules.HighJumpZoneNear, cameraX);
        double bestX = Layout.ToScreenX(Rules.HighJumpZoneBest, cameraX);
        dl.FillRect(zoneL, groundY + apronTop + 0.8, zoneR - zoneL, apronH - 1.6, Palette.TakeoffZone);
        dl.FillRect(zoneL, groundY + apronTop + 0.8, 0.8, apronH - 1.6, Palette.LaneLine);
        dl.FillRect(zoneR - 0.8, groundY + apronTop + 0.8, 0.8, apronH - 1.6, Palette.LaneLine);
        dl.FillRect(bestX - 0.7, groundY + apronTop + 0.8, 1.4, apronH - 1.6, Palette.White);

        // mat: a padded box seen from the side with its top, front and end faces
        dl.FillEllipse(barX + 24, groundY + 9, 30, 2.5, Palette.Shadow);
        var matFront = new[]
        {
            new Vec2(barX - 3, groundY - 4), new Vec2(barX + 48, groundY - 4),
            new Vec2(barX + 50, groundY + 8), new Vec2(barX - 3, groundY + 8),
        };
        dl.FillPolygon(Palette.MatDark, matFront);
        var matTop = new[]
        {
            new Vec2(barX + 1, groundY - 14), new Vec2(barX + 46, groundY - 14),
            new Vec2(barX + 48, groundY - 4), new Vec2(barX - 3, groundY - 4),
        };
        dl.FillPolygon(Palette.Mat, matTop);
        dl.FillPolygon(Palette.MatLight, [new Vec2(barX + 1, groundY - 14), new Vec2(barX + 46, groundY - 14), new Vec2(barX + 46.5, groundY - 11.5), new Vec2(barX + 0.5, groundY - 11.5)]);
        // cushion seams
        for (int k = 1; k < 4; k++)
        {
            double sx = barX - 3 + k * 12.5;
            dl.Line(sx + 1, groundY - 14, sx, groundY + 8, 0.5, Palette.MatDark.Darker(0.8));
        }
        dl.Line(barX - 3, groundY - 4, barX + 48, groundY - 4, 0.6, Palette.MatDark.Darker(0.7));

        // uprights on both sides of the mat (the far one slightly shorter for depth), with base plates
        double barPx = barHeightM * Layout.PxPerMeterY;
        double postTop = groundY - barPx - 12;
        dl.FillRect(barX - 3, groundY - 1, 5.6, 1.4, Palette.UprightBase);
        dl.FillRect(barX - 1.4, postTop, 2.4, groundY - postTop, Palette.Outline.WithAlpha(120));
        dl.FillGradientRect(barX - 1, postTop, 1.6, groundY - postTop, Palette.LightGrey, Palette.Upright, horizontal: true);
        dl.FillRect(barX + 44.4, postTop + 2, 1.6, groundY - 14 - (postTop + 2), Palette.Upright.Darker(0.8));
        // height ticks on the near upright every 10 cm around the bar
        for (int i = -2; i <= 2; i++)
        {
            double ty = groundY - (barHeightM + i * 0.1) * Layout.PxPerMeterY;
            dl.FillRect(barX + 0.8, ty, i == 0 ? 2.2 : 1.2, 0.6, Palette.Black);
        }

        // bar
        double barY = groundY - barPx;
        if (barFallAnim <= 0)
        {
            dl.FillRect(barX - 1, barY - 1, 44, 2, Palette.Outline.WithAlpha(110));
            dl.FillRect(barX - 1, barY - 0.7, 44, 1.4, Palette.Bar);
            dl.FillRect(barX + 9, barY - 0.7, 6, 1.4, Palette.FlagRed);
            dl.FillRect(barX + 27, barY - 0.7, 6, 1.4, Palette.FlagRed);
            dl.FillRect(barX + 42, barY - 1.9, 1.4, 3.6, Palette.LightGrey);
        }
        else
        {
            // knocked off: the near end drops first, both ends settle on the mat
            double g = barFallAnim * barFallAnim;
            double nearDrop = Math.Min(1, barFallAnim * 1.6);
            double matNear = groundY - 11, matFar = groundY - 13;
            var a = new Vec2(barX + 3 + nearDrop * 4, barY + (matNear - barY) * nearDrop * nearDrop);
            var b = new Vec2(barX + 41 - g * 2, barY + (matFar - barY) * g);
            dl.Line(a, b, 2.2, Palette.Outline.WithAlpha(110));
            dl.Line(a, b, 1.4, Palette.Bar);
            var mid = (a + b) * 0.5;
            dl.FillCircle(mid.X, mid.Y, 1.1, Palette.FlagRed);
        }

        // height label beside the far upright, out of the jumper's way
        PixelFont.Draw(dl, barX + 48, postTop + 1, FormatMeters(barHeightM), Palette.White, 0.5, TextAlign.Left);
        dl.PopClip();
    }

    public static string FormatMeters(double m) => m.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public static void DrawCritter(DrawList dl, FieldCritter c, double sx, double groundY)
    {
        if (c.Kind == "mole")
        {
            double pop = Math.Min(1, c.Age * 3) * (c.Age > c.Lifetime - 0.5 ? (c.Lifetime - c.Age) * 2 : 1);
            double hy = groundY - 7 * Math.Clamp(pop, 0, 1);
            dl.FillEllipse(sx, groundY - 0.3, 5.5, 1.6, Palette.TrackDark);
            dl.FillEllipse(sx, hy + 3.2, 4.4, 4.6, Palette.Outline.WithAlpha(140));
            dl.FillEllipse(sx, hy + 3, 4, 4.2, Palette.Mole);
            dl.FillEllipse(sx + 0.6, hy + 3.6, 2.4, 2.4, Palette.Mole.Lighter(0.2));
            dl.FillCircle(sx + 3.2, hy + 1.6, 1.2, Palette.MoleNose);
            dl.FillRect(sx + 0.5, hy + 0.6, 1, 1, Palette.Black);
            dl.FillRect(sx - 3, groundY - 2, 6, 2, Palette.Mole.Darker(0.8));
        }
        else
        {
            // bird: a flapping "V" that falls once hit (age > 0.4)
            double flap = Math.Sin(c.Age * 25) * 3;
            double fall = Math.Max(0, c.Age - 0.4) * 30;
            double by = groundY - c.Y * Layout.PxPerMeterY * (c.Y > 8 ? Layout.FlightSquash : 1) + fall;
            double bx = sx + c.Age * 14;
            dl.Line(bx - 5, by - flap, bx, by + 1, 2.2, Palette.Outline.WithAlpha(120));
            dl.Line(bx, by + 1, bx + 5, by - flap, 2.2, Palette.Outline.WithAlpha(120));
            dl.Line(bx - 5, by - flap, bx, by + 1, 1.2, Palette.Bird);
            dl.Line(bx, by + 1, bx + 5, by - flap, 1.2, Palette.Bird);
            dl.FillCircle(bx, by + 1, 1.3, Palette.Bird);
            dl.FillRect(bx + 1.2, by + 0.6, 1.6, 0.9, Palette.BirdBeak);
            dl.FillRect(bx - 0.4, by + 0.3, 0.6, 0.6, Palette.Black);
        }
    }
}
