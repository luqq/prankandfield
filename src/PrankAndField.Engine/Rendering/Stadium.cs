using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Rendering;

/// <summary>Draws the stadium furniture: grandstand crowd, wall with flags and event name, lanes and sectors.</summary>
public static class Stadium
{
    private static uint Hash(int a, int b)
    {
        uint h = (uint)(a * 374761393) ^ (uint)(b * 668265263);
        h ^= h >> 13;
        h *= 1274126177;
        h ^= h >> 16;
        return h;
    }

    /// <summary>Grandstand: grey terraces full of coloured spectators, scrolling at half speed.</summary>
    public static void DrawStands(DrawList dl, double scrollPx, double time)
    {
        double top = Layout.StandsTop, h = Layout.StandsBottom - Layout.StandsTop;
        dl.FillRect(0, top, Screen.Width, h, Palette.StandRow);
        const int rows = 5;
        double rowH = h / rows;
        double offset = (scrollPx * 0.5) % 4.0;
        if (offset < 0) offset += 4.0;
        int baseCol = (int)Math.Floor(scrollPx * 0.5 / 4.0);
        for (int r = 0; r < rows; r++)
        {
            double y = top + r * rowH;
            dl.FillRect(0, y, Screen.Width, rowH, r % 2 == 0 ? Palette.StandRow : Palette.StandRowAlt);
            for (int c = -1; c <= Screen.Width / 4 + 1; c++)
            {
                int worldCol = baseCol + c;
                uint hsh = Hash(worldCol, r);
                var color = Palette.Crowd[hsh % (uint)Palette.Crowd.Length];
                // a few spectators wave: bob up and down over time
                double bob = ((hsh >> 8) % 7 == 0) ? Math.Abs(Math.Sin(time * 5 + hsh % 13)) * 1.2 : 0;
                double x = c * 4 - offset + ((hsh >> 4) % 2);
                dl.FillRect(x, y + 1.2 - bob, 2.2, 2.4, color);
                dl.FillRect(x + 0.4, y + 0.2 - bob, 1.4, 1.2, Palette.Skin);
            }
            dl.FillRect(0, y + rowH - 0.6, Screen.Width, 0.6, Palette.Rail);
        }
    }

    public static void DrawWall(DrawList dl, string eventName, int round)
    {
        double top = Layout.WallTop, h = Layout.WallBottom - Layout.WallTop;
        dl.FillRect(0, top, Screen.Width, h, Palette.Wall);
        dl.FillRect(0, top, Screen.Width, 1.2, Palette.WallDark);
        dl.FillRect(0, top + h - 1.5, Screen.Width, 1.5, Palette.WallDark);
        DrawFlag(dl, 10, top + 3, round);
        DrawFlag(dl, Screen.Width - 22, top + 3, round + 1);
        PixelFont.Draw(dl, Screen.Width / 2, top + 3.5, eventName, Palette.Black, 1, TextAlign.Center);
    }

    private static void DrawFlag(DrawList dl, double x, double y, int variant)
    {
        dl.FillRect(x, y, 12, 8, Palette.White);
        switch (variant % 3)
        {
            case 0: // red disc
                dl.FillCircle(x + 6, y + 4, 2.4, Palette.FlagRed);
                break;
            case 1: // tricolour bands
                dl.FillRect(x, y, 12, 2.7, Palette.FlagRed);
                dl.FillRect(x, y + 2.7, 12, 2.7, Palette.FlagYellow);
                dl.FillRect(x, y + 5.4, 12, 2.6, Palette.FlagRed);
                break;
            default: // stripes and canton
                for (int i = 0; i < 4; i++) dl.FillRect(x, y + i * 2, 12, 1, Palette.FlagRed);
                dl.FillRect(x, y, 5, 4, Palette.FlagBlue);
                break;
        }
    }

    /// <summary>A running lane strip: grass, terracotta track with white lines and distance ticks, start and finish.</summary>
    public static void DrawLaneStrip(DrawList dl, double top, double cameraX, int laneNumber, double finishM, string laneLabel, Rgba labelColor)
    {
        double h = Layout.StripHeight;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        double trackTop = top + 8, trackH = 28;
        dl.FillRect(0, trackTop, Screen.Width, trackH, Palette.Track);
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
        // start line + lane number box
        double startX = Layout.ToScreenX(0, cameraX);
        if (startX > -40 && startX < Screen.Width)
        {
            dl.FillRect(startX - 1, trackTop, 2, trackH, Palette.LaneLine);
            dl.FillRect(startX - 16, trackTop + trackH / 2 - 5, 10, 10, Palette.White);
            PixelFont.Draw(dl, startX - 11, trackTop + trackH / 2 - 3.5, laneNumber.ToString(), Palette.Black, 1, TextAlign.Center);
            PixelFont.Draw(dl, startX + 14, trackTop + 2.5, laneLabel, labelColor, 1);
        }
        // finish line (chequered band)
        double fx = Layout.ToScreenX(finishM, cameraX);
        if (fx > -10 && fx < Screen.Width + 10)
        {
            for (int i = 0; i < 7; i++)
            {
                dl.FillRect(fx - 1, trackTop + i * 4, 2, 2, Palette.White);
                dl.FillRect(fx + 1, trackTop + i * 4 + 2, 2, 2, Palette.White);
            }
            dl.FillRect(fx - 2, trackTop - 6, 1.2, 6, Palette.White);
            dl.FillRect(fx - 2, trackTop - 6, 7, 3, Palette.FlagRed);
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
    public static void DrawLongJumpField(DrawList dl, double cameraX, double boardM, double landingMark)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        double runTop = top + 30, runH = 30;
        double boardX = Layout.ToScreenX(boardM, cameraX);
        // runway up to the board
        dl.FillRect(0, runTop, Math.Max(0, boardX + 2), runH, Palette.Track);
        dl.FillRect(0, runTop, Math.Max(0, boardX + 2), 1, Palette.LaneLine);
        dl.FillRect(0, runTop + runH - 1, Math.Max(0, boardX + 2), 1, Palette.LaneLine);
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
        // metre markers board (white strip above the pit with numbers)
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
            dl.FillRect(sx - 0.6, runTop + 4, 1.2, runH - 8, Palette.SandEdge.Darker(0.8));
        }
        dl.PopClip();
    }

    /// <summary>Javelin runway and landing sector with 10 m arcs.</summary>
    public static void DrawJavelinField(DrawList dl, double cameraX, double lineM)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        double runTop = top + 34, runH = 26;
        double lineX = Layout.ToScreenX(lineM, cameraX);
        dl.FillRect(0, runTop, Math.Max(0, lineX), runH, Palette.Track);
        dl.FillRect(0, runTop, Math.Max(0, lineX), 1, Palette.LaneLine);
        dl.FillRect(0, runTop + runH - 1, Math.Max(0, lineX), 1, Palette.LaneLine);
        // foul arc
        var arc = new List<Vec2>();
        for (int i = 0; i <= 8; i++)
        {
            double t = i / 8.0;
            double y = runTop - 4 + t * (runH + 8);
            double bulge = Math.Sin(t * Math.PI) * 6;
            arc.Add(new Vec2(lineX + bulge, y));
        }
        dl.Polyline(1.6, Palette.White, arc.ToArray());
        // sector lines
        double sectorStart = lineX + 4;
        dl.Line(sectorStart, runTop + runH / 2, Screen.Width + 20, top - 10, 0.8, Palette.White.WithAlpha(190));
        dl.Line(sectorStart, runTop + runH / 2, Screen.Width + 20, top + h + 10, 0.8, Palette.White.WithAlpha(190));
        for (int m = 10; m <= 110; m += 10)
        {
            double sx = Layout.ToScreenX(lineM + m, cameraX);
            if (sx < -10 || sx > Screen.Width + 10) continue;
            dl.FillRect(sx, top + 6, 0.8, h - 12, Palette.White.WithAlpha(150));
            PixelFont.Draw(dl, sx + 2, top + 8, m.ToString(), Palette.White, 0.75);
        }
        dl.PopClip();
    }

    /// <summary>Hammer circle with cage on the left and the landing sector to the right.</summary>
    public static void DrawHammerField(DrawList dl, double cameraX)
    {
        double top = Layout.FieldTop, h = Layout.FieldBottom - Layout.FieldTop;
        dl.PushClip(0, top, Screen.Width, h);
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        double cx = Layout.ToScreenX(0, cameraX);
        double cy = top + 60;
        // throwing circle
        dl.FillEllipse(cx, cy, 16, 9, Palette.LightGrey);
        dl.FillEllipse(cx, cy, 14.5, 7.8, Palette.Grey);
        // cage
        var cage = new List<Vec2>();
        for (int i = 0; i <= 10; i++)
        {
            double a = Math.PI * 0.55 + i / 10.0 * Math.PI * 0.9;
            cage.Add(new Vec2(cx + Math.Cos(a) * 24, cy + Math.Sin(a) * 15));
        }
        dl.Polyline(1.2, Palette.Cage, cage.ToArray());
        for (int i = 0; i <= 10; i += 2)
        {
            var p = cage[i];
            dl.Line(p.X, p.Y, p.X, p.Y - 14, 1, Palette.Cage);
        }
        dl.Line(cage[0].X, cage[0].Y - 14, cage[10].X, cage[10].Y - 14, 1, Palette.Cage);
        // sector
        dl.Line(cx + 14, cy, Screen.Width + 400, top - 40, 0.8, Palette.White.WithAlpha(190));
        dl.Line(cx + 14, cy, Screen.Width + 400, top + h + 40, 0.8, Palette.White.WithAlpha(190));
        for (int m = 10; m <= 110; m += 10)
        {
            double sx = Layout.ToScreenX(m, cameraX);
            if (sx < -10 || sx > Screen.Width + 10) continue;
            dl.FillRect(sx, top + 6, 0.8, h - 12, Palette.White.WithAlpha(150));
            PixelFont.Draw(dl, sx + 2, top + 8, m.ToString(), Palette.White, 0.75);
        }
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
        dl.FillRect(0, top, Screen.Width, h, Palette.Grass);
        double barX = Layout.ToScreenX(0, cameraX);
        const double apronTop = -3, apronH = 8;

        // apron (approach lane) up to the uprights
        dl.FillRect(0, groundY + apronTop, Math.Max(0, barX - 1), apronH, Palette.Track);
        dl.FillRect(0, groundY + apronTop, Math.Max(0, barX - 1), 0.8, Palette.LaneLine);
        dl.FillRect(0, groundY + apronTop + apronH - 0.8, Math.Max(0, barX - 1), 0.8, Palette.LaneLine);
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

        // mat
        var mat = new[]
        {
            new Vec2(barX + 1, groundY - 14), new Vec2(barX + 46, groundY - 14),
            new Vec2(barX + 50, groundY + 8), new Vec2(barX - 3, groundY + 8),
        };
        dl.FillPolygon(Palette.MatDark, mat);
        var matTop = new[]
        {
            new Vec2(barX + 2, groundY - 13), new Vec2(barX + 45, groundY - 13),
            new Vec2(barX + 48, groundY + 4), new Vec2(barX - 1, groundY + 4),
        };
        dl.FillPolygon(Palette.Mat, matTop);
        dl.FillRect(barX - 1, groundY + 4, 49, 1, Palette.Mat.Darker(0.85));

        // uprights on both sides of the mat (the far one slightly shorter for depth)
        double barPx = barHeightM * Layout.PxPerMeterY;
        double postTop = groundY - barPx - 12;
        dl.FillRect(barX - 1.2, postTop, 2, groundY - postTop, Palette.Upright);
        dl.FillRect(barX - 3, groundY - 1, 5.6, 1.4, Palette.UprightBase);
        dl.FillRect(barX + 42.4, postTop + 2, 1.6, groundY - 13 - (postTop + 2), Palette.Upright.Darker(0.8));
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
            dl.FillRect(barX - 1, barY - 0.7, 44, 1.4, Palette.Bar);
            dl.FillRect(barX + 9, barY - 0.7, 6, 1.4, Palette.FlagRed);
            dl.FillRect(barX + 27, barY - 0.7, 6, 1.4, Palette.FlagRed);
        }
        else
        {
            // knocked off: the near end drops first, both ends settle on the mat
            double g = barFallAnim * barFallAnim;
            double nearDrop = Math.Min(1, barFallAnim * 1.6);
            double matNear = groundY - 11, matFar = groundY - 13;
            var a = new Vec2(barX + 3 + nearDrop * 4, barY + (matNear - barY) * nearDrop * nearDrop);
            var b = new Vec2(barX + 41 - g * 2, barY + (matFar - barY) * g);
            dl.Line(a, b, 1.4, Palette.Bar);
            var mid = (a + b) * 0.5;
            dl.FillCircle(mid.X, mid.Y, 1.1, Palette.FlagRed);
        }

        // height label beside the far upright, out of the jumper's way
        PixelFont.Draw(dl, barX + 46, postTop + 1, FormatMeters(barHeightM), Palette.White, 0.5, TextAlign.Left);
        dl.PopClip();
    }

    public static string FormatMeters(double m) => m.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public static void DrawCritter(DrawList dl, FieldCritter c, double sx, double groundY)
    {
        if (c.Kind == "mole")
        {
            double pop = Math.Min(1, c.Age * 3) * (c.Age > c.Lifetime - 0.5 ? (c.Lifetime - c.Age) * 2 : 1);
            double hy = groundY - 7 * Math.Clamp(pop, 0, 1);
            dl.FillRect(sx - 5, groundY - 1.5, 10, 2.5, Palette.TrackDark);
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
        }
    }
}
