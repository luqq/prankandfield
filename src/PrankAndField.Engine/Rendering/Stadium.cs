using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Core.Localization;
using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Rendering;

/// <summary>
/// Draws the stadium furniture: the grandstand under its roof, the wall with advertising boards, the
/// electronic board and the flags, lanes and sectors. Everything is flat colour with hard edges, like the
/// 1983 arcade: no gradients, no glows, no translucent shadows.
/// </summary>
public static class Stadium
{
    /// <summary>Studio name and year on the advertising boards; the other two boards come from the localizer.</summary>
    private const string AdStudio = "HIRYU SOFT", AdYear = "1983";
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

    /// <summary>Points of a circular arc (screen angles in degrees, 0 = right, 90 = down) for a polyline.</summary>
    internal static Vec2[] ArcPoints(double cx, double cy, double r, double startDeg, double endDeg, int segments = 12)
    {
        var pts = new Vec2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            double a = (startDeg + (endDeg - startDeg) * i / segments) * Math.PI / 180.0;
            pts[i] = new Vec2(cx + Math.Cos(a) * r, cy + Math.Sin(a) * r);
        }
        return pts;
    }

    /// <summary>Grandstand: a roof with banks of floodlights over terraces full of spectators with flags and camera flashes, scrolling at half speed.</summary>
    public static void DrawStands(DrawList dl, double scrollPx, double time)
    {
        double top = Layout.StandsTop, bottom = Layout.StandsBottom;
        const double roofH = 5;
        // roof underside with banks of floodlights (far away: quarter speed)
        dl.FillRect(0, top, Screen.Width, roofH, Palette.RoofDark);
        dl.FillRect(0, top + roofH - 1, Screen.Width, 1, Palette.Roof);
        double lampOff = Mod(scrollPx * 0.25, 48);
        for (double x = -lampOff - 48; x < Screen.Width + 48; x += 48)
        {
            dl.FillRect(x + 1.5, top + 1.4, 9, 0.8, Palette.Grey);
            for (int k = 0; k < 3; k++) dl.FillRect(x + 2 + k * 3, top + 2.4, 2, 1.6, Palette.Floodlight);
        }
        // terraces
        double terraceTop = top + roofH;
        const int rows = 4;
        double rowH = (bottom - 2 - terraceTop) / rows;
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
                // camera flashes: a handful of photographers, each firing a white pixel for a tenth of a second now and then
                if ((hsh >> 20) % 23 == 0 && Mod(time * 0.9 + hsh % 17, 7) < 0.1)
                {
                    dl.FillRect(x + 0.1, y - 0.4, 2, 2, Palette.White);
                }
            }
            dl.FillRect(0, y + rowH - 0.6, Screen.Width, 0.6, Palette.Rail);
        }
        // front barrier
        dl.FillRect(0, bottom - 2, Screen.Width, 2, Palette.WallDark);
        dl.FillRect(0, bottom - 2, Screen.Width, 0.7, Palette.Rail);
    }

    /// <summary>
    /// The wall between the stands and the field: advertising boards scrolling with the stands, the electronic
    /// board with the event name in the middle and two flags fluttering at the ends.
    /// </summary>
    public static void DrawWall(DrawList dl, Localizer loc, string eventName, int round, double scrollPx = 0, double time = 0)
    {
        string[] ads = [AdStudio, loc["game.title"], loc["stadium.ad_cup"], AdYear];
        double top = Layout.WallTop, h = Layout.WallBottom - Layout.WallTop;
        dl.FillRect(0, top, Screen.Width, h, Palette.Wall);
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
        double cycle = panelW * ads.Length;
        double off = Mod(scrollPx * 0.5, cycle);
        for (int i = -1; i <= (int)(Screen.Width / panelW) + ads.Length; i++)
        {
            double x = i * panelW - off;
            if (x > Screen.Width || x + panelW < 0) continue;
            if (x + 2 >= bx - 1 && x + panelW - 2 <= bx + boardW + 1) continue; // fully hidden behind the board
            int idx = ((i % ads.Length) + ads.Length) % ads.Length;
            var (back, fore) = AdColors[idx];
            dl.FillRect(x + 2, top + 3, panelW - 4, 8, back);
            double adW = PixelFont.Measure(ads[idx], 0.7);
            double adL = x + panelW / 2 - adW / 2, adR = adL + adW;
            if (!Covered(adL, adR))
                PixelFont.Draw(dl, x + panelW / 2, top + 4.6, ads[idx], fore, 0.7, TextAlign.Center);
        }
        dl.FillRect(bx - 1, top + 1, boardW + 2, h - 2, Palette.WallDark);
        dl.FillRect(bx, top + 2, boardW, h - 4, Palette.Scoreboard);
        double ty = top + (h - PixelFont.GlyphH * textSize) / 2;
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

    /// <summary>Flat terracotta runway between two white lines, with a dark kerb along the grass above it.</summary>
    private static void DrawRunway(DrawList dl, double top, double w, double h)
    {
        if (w <= 0) return;
        dl.FillRect(0, top - 0.8, w, 0.8, Palette.GrassDark);
        dl.FillRect(0, top, w, h, Palette.Track);
        dl.FillRect(0, top, w, 1, Palette.LaneLine);
        dl.FillRect(0, top + h - 1, w, 1, Palette.LaneLine);
    }

    /// <summary>A running lane strip: grass, terracotta track with white lines and distance ticks, blocks, start and finish.</summary>
    public static void DrawLaneStrip(DrawList dl, double top, double cameraX, int laneNumber, double finishM, string laneLabel, Rgba labelColor)
    {
        double h = Layout.StripHeight;
        dl.PushClip(0, top, Screen.Width, h);
        DrawGrass(dl, top, h, cameraX);
        double trackTop = top + 8, trackH = 28;
        DrawRunway(dl, trackTop, Screen.Width, trackH);
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
            dl.FillRect(startX - 16, trackTop + trackH / 2 - 5, 10, 10, Palette.White);
            PixelFont.Draw(dl, startX - 11, trackTop + trackH / 2 - 3.5, laneNumber.ToString(), Palette.Black, 1, TextAlign.Center);
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
        dl.Line(bl, tl, 1, Palette.Hurdle);
        dl.Line(br, tr, 1, Palette.Hurdle);
        dl.Line(Rot(new Vec2(sx - 1.5, groundY - h * 0.45)), Rot(new Vec2(sx + 1.5, groundY - h * 0.45)), 0.8, Palette.Hurdle);
        var barL = Rot(new Vec2(sx - 2.4, groundY - h));
        var barR = Rot(new Vec2(sx + 2.4, groundY - h));
        dl.Line(barL, barR, 2.4, Palette.HurdleBar);
        dl.Line(Rot(new Vec2(sx - 2.4, groundY - h + 0.7)), Rot(new Vec2(sx + 2.4, groundY - h + 0.7)), 0.9, Palette.HurdleStripe);
        dl.FillRect(sx - 3.5, groundY - 0.5, 7, 1, Palette.Hurdle);
    }

    /// <summary>Long-jump runway and sand pit (single wide strip).</summary>
    public static void DrawLongJumpField(DrawList dl, double cameraX, double boardM, double landingMark) =>
        DrawJumpField(dl, cameraX, boardM, Rules.LongJumpPitStart, 11.0, 2, landingMark);

    /// <summary>Triple-jump runway: the same strip, with the pit much farther from the board.</summary>
    public static void DrawTripleJumpField(DrawList dl, double cameraX, double boardM, double pitStartM, IReadOnlyList<double?> landings)
    {
        DrawJumpField(dl, cameraX, boardM, pitStartM, pitStartM + 10.0, (int)Math.Round(pitStartM), landings[2] is double m ? m - boardM : 0);
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        // hop and step touch-downs: small foot marks on the runway
        for (int i = 0; i < 2; i++)
        {
            if (landings[i] is not double x) continue;
            double sx = Layout.ToScreenX(x, cameraX);
            if (sx < -4 || sx > Screen.Width + 4) continue;
            dl.FillRect(sx - 1.5, top + 44, 3, 2, Palette.TrackDark);
            dl.FillRect(sx - 0.5, top + 34, 1, 9, Palette.LaneLine);
        }
        dl.PopClip();
    }

    private static void DrawJumpField(DrawList dl, double cameraX, double boardM, double pitStartM, double pitEndM, int firstMarker, double landingMark)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        DrawGrass(dl, top, h, cameraX);
        double runTop = top + 30, runH = 30;
        double boardX = Layout.ToScreenX(boardM, cameraX);
        double pitL = Layout.ToScreenX(boardM + pitStartM, cameraX);
        double pitR = Layout.ToScreenX(boardM + pitEndM, cameraX);
        // runway up to the board (and on to the pit when the pit is farther away, as in the triple jump)
        double runEnd = Math.Max(boardX + 2, pitL);
        DrawRunway(dl, runTop, Math.Max(0, runEnd), runH);
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
        // sand pit
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
        // raked sand: light and dark grains plus the rake lines, fixed to the pit
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
                dl.FillRect(pitL + 1, ly, pitR - pitL - 2, 0.4, Palette.SandEdge);
            }
        }
        // metre markers board (white strip above the pit with numbers)
        dl.FillRect(pitL, runTop - 10, pitR - pitL, 5, Palette.White);
        for (int m = firstMarker; m <= (int)Math.Round(pitEndM); m++)
        {
            double sx = Layout.ToScreenX(boardM + m, cameraX);
            dl.FillRect(sx, runTop - 10, 0.8, 5, Palette.Black);
            PixelFont.Draw(dl, sx + 1.5, runTop - 9.5, m.ToString(), Palette.Black, 0.5);
        }
        if (landingMark > 0)
        {
            // the two footprints and the red measuring line where the jumper landed
            double sx = Layout.ToScreenX(boardM + landingMark, cameraX);
            dl.FillEllipse(sx - 1.5, runTop + runH * 0.55, 2.2, 1.2, Palette.SandEdge.Darker(0.8));
            dl.FillEllipse(sx + 1.0, runTop + runH * 0.62, 2.2, 1.2, Palette.SandEdge.Darker(0.8));
            dl.FillRect(sx - 0.6, runTop + 4, 1.2, runH - 8, Palette.HudRed);
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
            if (r > 2) dl.Polyline(0.8, Palette.White, ArcPoints(originX, originY, r, -24, 24));
            else dl.FillRect(sx, top + 6, 0.8, h - 12, Palette.White);
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
        DrawRunway(dl, runTop, Math.Max(0, lineX), runH);
        // foul arc at the end of the runway
        double cy = runTop + runH / 2;
        dl.Polyline(1.6, Palette.White, ArcPoints(lineX - 52, cy, 58, -16, 16, 10));
        // sector lines
        double sectorStart = lineX + 4;
        dl.Line(sectorStart, cy, Screen.Width + 20, top - 10, 0.8, Palette.White);
        dl.Line(sectorStart, cy, Screen.Width + 20, top + h + 10, 0.8, Palette.White);
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
        // throwing circle: concrete with a rim
        dl.FillEllipse(cx, cy, 16, 9, Palette.LightGrey);
        dl.FillEllipse(cx, cy, 14.5, 7.8, Palette.Grey);
        dl.FillRect(cx - 14, cy - 0.3, 28, 0.6, Palette.DarkGrey);
        // cage: posts, three rails and the top ring
        var cage = new List<Vec2>();
        for (int i = 0; i <= 10; i++)
        {
            double a = Math.PI * 0.55 + i / 10.0 * Math.PI * 0.9;
            cage.Add(new Vec2(cx + Math.Cos(a) * 24, cy + Math.Sin(a) * 15));
        }
        for (int k = 1; k < 4; k++)
        {
            var row = new Vec2[cage.Count];
            for (int i = 0; i < cage.Count; i++) row[i] = new Vec2(cage[i].X, cage[i].Y - k * 3.5);
            dl.Polyline(0.4, Palette.Cage, row);
        }
        dl.Polyline(1.2, Palette.Cage, cage.ToArray());
        for (int i = 0; i <= 10; i += 2)
        {
            var p = cage[i];
            dl.Line(p.X, p.Y, p.X, p.Y - 14, 1, Palette.Cage);
        }
        var topRing = new Vec2[cage.Count];
        for (int i = 0; i < cage.Count; i++) topRing[i] = new Vec2(cage[i].X, cage[i].Y - 14);
        dl.Polyline(1, Palette.Cage, topRing);
        // sector
        dl.Line(cx + 14, cy, Screen.Width + 400, top - 40, 0.8, Palette.White);
        dl.Line(cx + 14, cy, Screen.Width + 400, top + h + 40, 0.8, Palette.White);
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
        dl.FillRect(0, groundY + apronTop, apronW, apronH, Palette.Track);
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

        // mat: a padded box seen from the side, the top face lighter than the front, with the cushion seams
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
        dl.FillRect(barX - 1.2, postTop, 2, groundY - postTop, Palette.Upright);
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
            dl.FillRect(barX - 1, barY - 0.7, 46, 1.4, Palette.Bar);
            dl.FillRect(barX + 9, barY - 0.7, 6, 1.4, Palette.FlagRed);
            dl.FillRect(barX + 27, barY - 0.7, 6, 1.4, Palette.FlagRed);
            dl.FillRect(barX + 43.4, barY - 1.9, 1.4, 3.6, Palette.LightGrey);
        }
        else
        {
            // knocked off: the near end drops first, both ends settle on the mat
            double g = barFallAnim * barFallAnim;
            double nearDrop = Math.Min(1, barFallAnim * 1.6);
            double matNear = groundY - 11, matFar = groundY - 13;
            var a = new Vec2(barX + 3 + nearDrop * 4, barY + (matNear - barY) * nearDrop * nearDrop);
            var b = new Vec2(barX + 43 - g * 2, barY + (matFar - barY) * g);
            dl.Line(a, b, 1.4, Palette.Bar);
            var mid = (a + b) * 0.5;
            dl.FillCircle(mid.X, mid.Y, 1.1, Palette.FlagRed);
        }

        // height label beside the far upright, out of the jumper's way
        PixelFont.Draw(dl, barX + 48, postTop + 1, FormatMeters(barHeightM), Palette.White, 0.5, TextAlign.Left);
        dl.PopClip();
    }

    // ------------------------------------------------------------------ Hyper Sports

    /// <summary>A swimming lane: deck, blue water with drifting ripples, lane ropes, start block and touch pad.</summary>
    public static void DrawPoolStrip(DrawList dl, double top, double cameraX, int laneNumber, double finishM, string laneLabel, Rgba labelColor, double time)
    {
        double h = Layout.StripHeight;
        double camPx = cameraX * Layout.PxPerMeterX;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.PoolDeck);
        double waterTop = top + 8, waterH = 28;
        dl.FillRect(0, waterTop, Screen.Width, waterH, Palette.Water);
        // ripples drifting with the camera
        for (int row = 0; row < 4; row++)
        {
            double y = waterTop + 4 + row * 6.5;
            int first = (int)Math.Floor(camPx / 16) - 1;
            for (int k = first; k <= first + Screen.Width / 16 + 2; k++)
            {
                double x = k * 16 - camPx + (row % 2) * 8;
                double wobble = Math.Sin(time * 3 + row + k) * 0.8;
                dl.FillRect(x, y + wobble, 6, 0.9, Palette.WaterLight);
            }
        }
        // lane ropes: alternating floats
        int firstFloat = (int)Math.Floor(camPx / 6) - 1;
        for (int k = firstFloat; k <= firstFloat + Screen.Width / 6 + 2; k++)
        {
            double x = k * 6 - camPx;
            var c = k % 2 == 0 ? Palette.LaneRope : Palette.HudRed;
            dl.FillRect(x, waterTop - 0.6, 4, 1.6, c);
            dl.FillRect(x, waterTop + waterH - 1, 4, 1.6, c);
        }
        // distance marks every 10 m on the pool floor
        for (double m = Math.Floor(cameraX / 10) * 10; m <= cameraX + Screen.Width / Layout.PxPerMeterX + 10; m += 10)
        {
            double sx = Layout.ToScreenX(m, cameraX);
            if (sx < -4 || sx > Screen.Width + 4) continue;
            dl.FillRect(sx, waterTop + 2, 1, waterH - 4, Palette.WaterDark);
            if (m > 0 && m < finishM) PixelFont.Draw(dl, sx + 2, waterTop + waterH - 6, ((int)m).ToString(), Palette.WaterLight, 0.5);
        }
        // start wall with the block and the lane number; the deck runs from the wall to the edge of the screen
        double startX = Layout.ToScreenX(0, cameraX);
        if (startX > 0 && startX < Screen.Width)
        {
            dl.FillRect(0, top, startX, h, Palette.PoolDeck.Darker(0.85));
            dl.FillRect(startX - 1, waterTop, 1.5, waterH, Palette.White);
            dl.FillRect(startX - 7, waterTop - 6, 7, 6, Palette.White);
            dl.FillRect(startX - 16, waterTop + waterH / 2 - 5, 10, 10, Palette.White);
            PixelFont.Draw(dl, startX - 11, waterTop + waterH / 2 - 3.5, laneNumber.ToString(), Palette.Black, 1, TextAlign.Center);
            PixelFont.Draw(dl, startX + 14, top + 1.5, laneLabel, labelColor, 1);
        }
        // finish wall and the yellow touch pad; past the wall there is only deck
        double fx = Layout.ToScreenX(finishM, cameraX);
        if (fx > -10 && fx < Screen.Width + 10)
        {
            dl.FillRect(fx, top, Math.Max(0, Screen.Width - fx), h, Palette.PoolDeck.Darker(0.85));
            dl.FillRect(fx - 1.5, waterTop, 2, waterH, Palette.HudYellow);
            dl.FillRect(fx - 2, waterTop - 6, 1.2, 6, Palette.White);
            dl.FillRect(fx - 2, waterTop - 6, 7, 3, Palette.FlagRed);
        }
        dl.PopClip();
    }

    /// <summary>The skeet field: the two trap houses and the two fixed sights the clays fly through.</summary>
    public static void DrawSkeetRange(DrawList dl, double groundY, bool leftHot, bool rightHot)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        dl.FillRect(0, groundY - 3, Screen.Width, 1, Palette.GrassDark);
        // shooting station
        dl.FillEllipse(Screen.Width / 2, groundY + 1, 18, 4, Palette.LightGrey);
        dl.FillEllipse(Screen.Width / 2, groundY + 1, 15, 3, Palette.Grey);
        // low house on the left, high house on the right
        DrawTrapHouse(dl, Rules.SkeetLeftHouse * Layout.PxPerMeterX, groundY, 11, Rules.SkeetLaunchHeight * Layout.SkeetPxPerMeterY, faceRight: true);
        DrawTrapHouse(dl, Rules.SkeetRightHouse * Layout.PxPerMeterX, groundY, 11, Rules.SkeetLaunchHeight * Layout.SkeetPxPerMeterY + 8, faceRight: false);
        // sights
        DrawSight(dl, Rules.SkeetLeftSight * Layout.PxPerMeterX, groundY - Rules.SkeetSightHeight * Layout.SkeetPxPerMeterY, leftHot);
        DrawSight(dl, Rules.SkeetRightSight * Layout.PxPerMeterX, groundY - Rules.SkeetSightHeight * Layout.SkeetPxPerMeterY, rightHot);
        dl.PopClip();
    }

    private static void DrawTrapHouse(DrawList dl, double cx, double groundY, double w, double h, bool faceRight)
    {
        dl.FillRect(cx - w / 2, groundY - h, w, h, Palette.House);
        dl.FillRect(cx - w / 2, groundY - h, w, 1.2, Palette.LightGrey);
        double doorX = faceRight ? cx + w / 2 - 2 : cx - w / 2;
        dl.FillRect(doorX, groundY - h + 2, 2, 3, Palette.Black);
    }

    private static void DrawSight(DrawList dl, double cx, double cy, bool hot)
    {
        double r = Rules.SkeetHitRadius * Layout.SkeetPxPerMeterY;
        var color = hot ? Palette.SightHot : Palette.Sight;
        var ring = new Vec2[17];
        for (int i = 0; i <= 16; i++)
        {
            double a = i / 16.0 * Math.PI * 2;
            ring[i] = new Vec2(cx + Math.Cos(a) * r, cy + Math.Sin(a) * r);
        }
        dl.Polyline(hot ? 1.4 : 0.9, color, ring);
        dl.FillRect(cx - r - 2, cy - 0.4, 4, 0.8, color);
        dl.FillRect(cx + r - 2, cy - 0.4, 4, 0.8, color);
        dl.FillRect(cx - 0.4, cy - r - 2, 0.8, 4, color);
        dl.FillRect(cx - 0.4, cy + r - 2, 0.8, 4, color);
    }

    /// <summary>A clay target in flight (golden ones pay a bonus).</summary>
    public static void DrawClay(DrawList dl, double sx, double sy, bool golden)
    {
        dl.FillEllipse(sx, sy + 0.6, 3.4, 1.4, golden ? Palette.ClayGold.Darker(0.7) : Palette.ClayDark);
        dl.FillEllipse(sx, sy, 3.2, 1.5, golden ? Palette.ClayGold : Palette.Clay);
    }

    /// <summary>Vault runway, springboard, the long horse and the landing mat. World x in metres from the start.</summary>
    public static void DrawVaultField(DrawList dl, double cameraX, double boardM, double horseStartM, double horseEndM, double horseHeightM, double groundY)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        double boardX = Layout.ToScreenX(boardM, cameraX);
        double hs = Layout.ToScreenX(horseStartM, cameraX);
        double he = Layout.ToScreenX(horseEndM, cameraX);
        // runway (a narrow carpet) up to the springboard
        dl.FillRect(0, groundY - 3, Math.Max(0, boardX - 10), 7, Palette.Track);
        dl.FillRect(0, groundY - 3, Math.Max(0, boardX - 10), 0.8, Palette.LaneLine);
        for (double m = Math.Floor(cameraX / 5) * 5; m < boardM; m += 5)
        {
            double sx = Layout.ToScreenX(m, cameraX);
            if (sx < -2 || sx > Screen.Width) continue;
            dl.FillRect(sx, groundY - 2, 0.8, 5, Palette.LaneLine);
        }
        // springboard: a wedge rising towards the horse
        dl.FillPolygon(Palette.Springboard, new Vec2(boardX - 18, groundY + 1), new Vec2(boardX + 4, groundY + 1), new Vec2(boardX + 4, groundY - 4), new Vec2(boardX - 14, groundY - 1));
        dl.FillRect(boardX - 2, groundY - 4, 1, 4, Palette.HudRed);
        // landing mat
        dl.FillRect(he + 4, groundY - 4, 120, 10, Palette.MatDark);
        dl.FillRect(he + 5, groundY - 3, 118, 7, Palette.Mat);
        // the horse: a padded body on two legs
        double horseTop = groundY - horseHeightM * Layout.PxPerMeterY;
        dl.FillRect(hs + 4, horseTop + 4, 3, groundY - horseTop - 4, Palette.HorseLeg);
        dl.FillRect(he - 7, horseTop + 4, 3, groundY - horseTop - 4, Palette.HorseLeg);
        dl.FillRect(hs + 1, groundY - 1.5, he - hs - 2, 1.5, Palette.HorseLeg);
        dl.FillRect(hs, horseTop, he - hs, 8, Palette.Horse);
        dl.FillEllipse(hs, horseTop + 4, 3, 4, Palette.Horse);
        dl.FillEllipse(he, horseTop + 4, 3, 4, Palette.Horse);
        dl.FillRect(hs, horseTop, he - hs, 2.2, Palette.HorseTop);
        dl.PopClip();
    }

    /// <summary>The archery range: shooting line on the left and the target face on its stand to the right.</summary>
    public static void DrawArcheryRange(DrawList dl, double shooterX, double targetX, double targetCy, double groundY)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        for (int i = 1; i <= 4; i++) dl.FillRect(0, groundY - 20 * i / 2.0 + 10, Screen.Width, 0.6, Palette.GrassDark);
        dl.FillRect(0, groundY, Screen.Width, 1, Palette.GrassDark);
        // shooting line and mat
        dl.FillRect(shooterX - 8, groundY - 1, 16, 3, Palette.LightGrey);
        dl.FillRect(shooterX + 2, groundY - 12, 1, 14, Palette.White);
        // stand
        dl.Line(targetX - 10, groundY, targetX, targetCy, 1.4, Palette.TargetStand);
        dl.Line(targetX + 10, groundY, targetX, targetCy, 1.4, Palette.TargetStand);
        dl.Line(targetX - 10, groundY, targetX + 10, groundY, 1.4, Palette.TargetStand);
        // face: ten rings, two per colour
        double r = Rules.ArcheryTargetRadius * Layout.ArcheryPxPerMeter;
        double ring = Rules.ArcheryRing * Layout.ArcheryPxPerMeter;
        dl.FillCircle(targetX, targetCy, r + 1, Palette.TargetStand);
        dl.FillCircle(targetX, targetCy, r, Palette.TargetWhite);
        dl.FillCircle(targetX, targetCy, ring * 8, Palette.TargetBlack);
        dl.FillCircle(targetX, targetCy, ring * 6, Palette.TargetBlue);
        dl.FillCircle(targetX, targetCy, ring * 4, Palette.TargetRed);
        dl.FillCircle(targetX, targetCy, ring * 2, Palette.TargetGold);
        dl.FillCircle(targetX, targetCy, 0.6, Palette.Black);
        dl.PopClip();
    }

    /// <summary>An arrow: shaft, point and red fletching. <paramref name="angleDeg"/> is the screen angle (0 = right, positive = down).</summary>
    public static void DrawArrow(DrawList dl, double tipX, double tipY, double angleDeg, double length)
    {
        var dir = Vec2.FromAngle(angleDeg);
        var tip = new Vec2(tipX, tipY);
        var tail = tip - dir * length;
        dl.Line(tail, tip, 0.9, Palette.Arrow);
        dl.Line(tip - dir * 1.6, tip, 1.4, Palette.Grey);
        var side = new Vec2(-dir.Y, dir.X);
        dl.FillPolygon(Palette.Fletching, tail, tail + dir * 2.4 + side * 1.4, tail + dir * 2.4 - side * 1.4);
    }

    /// <summary>The weightlifting stage: a wooden platform and the judges' light box.</summary>
    public static void DrawLiftingPlatform(DrawList dl, double groundY, int lights)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        dl.FillRect(0, groundY + 6, Screen.Width, h, Palette.GrassDark);
        double w = 150;
        dl.FillRect(Screen.Width / 2 - w / 2 - 4, groundY, w + 8, 8, Palette.PlatformEdge);
        dl.FillRect(Screen.Width / 2 - w / 2, groundY, w, 6, Palette.Platform);
        for (int i = 1; i < 6; i++) dl.FillRect(Screen.Width / 2 - w / 2 + i * w / 6, groundY, 0.6, 6, Palette.PlatformEdge);
        // judges' lights, top left of the field
        dl.FillRect(8, top + 6, 34, 12, Palette.Black);
        for (int i = 0; i < 3; i++) dl.FillCircle(15 + i * 10, top + 12, 3.2, i < lights ? Palette.LightOn : Palette.LightOff);
        dl.PopClip();
    }

    /// <summary>A barbell with its plates; the number of plates follows the weight.</summary>
    public static void DrawBarbell(DrawList dl, double cx, double cy, double angleDeg, double weightKg)
    {
        var dir = Vec2.FromAngle(angleDeg);
        var c = new Vec2(cx, cy);
        const double half = 20;
        dl.Line(c - dir * half, c + dir * half, 1.6, Palette.Barbell);
        int plates = Math.Clamp(1 + (int)Math.Floor((weightKg - 100) / 40), 1, 4);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < plates; i++)
            {
                var p = c + dir * (side * (half - 3 - i * 2.6));
                double r = 5.5 - i * 0.6;
                dl.FillEllipse(p.X, p.Y, 1.3, r, i % 2 == 0 ? Palette.Plate : Palette.PlateDark);
            }
            var collar = c + dir * (side * (half - 3 + 2.2));
            dl.FillEllipse(collar.X, collar.Y, 0.9, 2.2, Palette.Barbell);
        }
    }

    public static string FormatMeters(double m) => m.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public static void DrawCritter(DrawList dl, FieldCritter c, double sx, double groundY, double pxPerMeterY = Layout.PxPerMeterY)
    {
        if (c.Kind == "clay")
        {
            // a broken clay: shards fly apart and drop
            double cy = groundY - c.Y * pxPerMeterY;
            double t = c.Age;
            for (int k = 0; k < 6; k++)
            {
                double a = k * Math.PI / 3 + 0.4;
                double x = sx + Math.Cos(a) * t * 34;
                double y = cy + Math.Sin(a) * t * 18 + t * t * 70;
                dl.FillRect(x - 1, y - 0.7, 2, 1.4, k % 3 == 0 ? Palette.ClayDark : Palette.Clay);
            }
        }
        else if (c.Kind == "mole")
        {
            double pop = Math.Min(1, c.Age * 3) * (c.Age > c.Lifetime - 0.5 ? (c.Lifetime - c.Age) * 2 : 1);
            double hy = groundY - 7 * Math.Clamp(pop, 0, 1);
            dl.FillEllipse(sx, groundY - 0.3, 5.5, 1.6, Palette.TrackDark);
            dl.FillEllipse(sx, hy + 3, 4, 4.2, Palette.Mole);
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
            dl.Line(bx - 5, by - flap, bx, by + 1, 1.2, Palette.Bird);
            dl.Line(bx, by + 1, bx + 5, by - flap, 1.2, Palette.Bird);
            dl.FillCircle(bx, by + 1, 1.3, Palette.Bird);
            dl.FillRect(bx + 1.2, by + 0.6, 1.6, 0.9, Palette.BirdBeak);
            dl.FillRect(bx - 0.4, by + 0.3, 0.6, 0.6, Palette.Black);
        }
    }
}
