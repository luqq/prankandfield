using PrankAndField.Core;
using PrankAndField.Core.Events;
using PrankAndField.Core.Localization;
using PrankAndField.Core.Tests.Support;
using PrankAndField.Engine.Graphics;
using PrankAndField.Engine.Rendering;

namespace PrankAndField.Engine.Tests;

/// <summary>The SPEED= bar at the bottom of the heats: one bar per human runner.</summary>
public class SpeedBarTests
{
    private const int BlocksPerBar = 24;

    /// <summary>
    /// Finds the rows of speed blocks in a frame, top to bottom: 4.5 px wide, at least 5 px tall rectangles inside
    /// the bottom band (the pixel font only draws 1 px tall runs there). Decodes the raw buffer like
    /// <see cref="RendererTests.Validate"/>, so the host culture cannot change the result. Each row reports the x of
    /// its first block, the block height and how many blocks are lit (yellow or blue rather than grey).
    /// </summary>
    private static List<(double Y, double X, double H, int Lit, int Total)> BlockRows(DrawList dl)
    {
        var rows = new SortedDictionary<double, (double X, double H, int Lit, int Total)>();
        var buf = dl.ToArray();
        bool lit = false;
        int i = 0;
        while (buf[i] != Op.End)
        {
            switch (buf[i])
            {
                case Op.SetColor:
                    lit = IsLit(buf[i + 1], buf[i + 2], buf[i + 3]);
                    i += 5;
                    break;
                case Op.FillRect:
                    {
                        double x = buf[i + 1], y = buf[i + 2], w = buf[i + 3], h = buf[i + 4];
                        if (y >= Layout.BottomTop && w == 4.5 && h >= 5)
                        {
                            var row = rows.TryGetValue(y, out var r) ? r : (X: x, H: h, Lit: 0, Total: 0);
                            rows[y] = (Math.Min(row.X, x), h, row.Lit + (lit ? 1 : 0), row.Total + 1);
                        }
                        i += 5;
                        break;
                    }
                case Op.FillPoly: i += 2 + 2 * (int)buf[i + 1]; break;
                case Op.FillCircle: i += 4; break;
                case Op.FillEllipse: i += 5; break;
                case Op.Line: i += 6; break;
                case Op.Polyline: i += 3 + 2 * (int)buf[i + 2]; break;
                case Op.Text: i += 6 + (int)buf[i + 5]; break;
                case Op.ClipPush: i += 5; break;
                case Op.ClipPop: i += 1; break;
                case Op.AudioSection: i += 2 + 3 * (int)buf[i + 1]; break;
                default: throw new InvalidOperationException($"unknown opcode {buf[i]} at {i}");
            }
        }
        return rows.Select(kv => (kv.Key, kv.Value.X, kv.Value.H, kv.Value.Lit, kv.Value.Total)).ToList();
    }

    private static bool IsLit(double r, double g, double b) =>
        (r == Palette.HudYellow.R && g == Palette.HudYellow.G && b == Palette.HudYellow.B) ||
        (r == Palette.HudBlue.R && g == Palette.HudBlue.G && b == Palette.HudBlue.B);

    private static RaceSimulation StartHeat(Game game, EventKind kind, int players)
    {
        game.JumpToEvent(kind, 1, players);
        var b1 = new Bot(0, 11);
        var b2 = new Bot(1, 10);
        SimulationRunner.RunUntil(game, g => g.Simulation is RaceSimulation r && r.Phase == RacePhase.Running,
            g => (b1.Next(g), b2.Next(g)), 30);
        var race = Assert.IsAssignableFrom<RaceSimulation>(game.Simulation);
        Assert.Equal(RacePhase.Running, race.Phase);
        return race;
    }

    [Theory]
    [InlineData(EventKind.Dash100m)]
    [InlineData(EventKind.Hurdles110m)]
    public void SinglePlayerHeatKeepsOneBarForTheHuman(EventKind kind)
    {
        var game = new Game(seed: 3);
        var race = StartHeat(game, kind, players: 1);
        race.AthleteFor(0)!.Runner.SpeedCm = 1442;
        race.AthleteFor(1)!.Runner.SpeedCm = 775; // the computer's speed (12 blocks) never gets a bar

        var rows = BlockRows(new GameRenderer().Render(game));
        var row = Assert.Single(rows);
        Assert.Equal(BlocksPerBar, row.Total);
        Assert.Equal(22, row.Lit); // 1442 / 1550 of 24 blocks
        // the bar of the arcade, exactly where it always was
        Assert.Equal(Layout.BottomTop + 4, row.Y);
        Assert.Equal(44, row.X);
        Assert.Equal(8, row.H);
    }

    [Theory]
    [InlineData(EventKind.Dash100m)]
    [InlineData(EventKind.Hurdles110m)]
    public void TwoPlayerHeatShowsOneBarPerRunnerWithTheirOwnSpeed(EventKind kind)
    {
        var game = new Game(seed: 3);
        var race = StartHeat(game, kind, players: 2);
        Assert.All(race.Athletes, a => Assert.False(a.IsCpu));
        var renderer = new GameRenderer();

        race.AthleteFor(0)!.Runner.SpeedCm = 1442;
        race.AthleteFor(1)!.Runner.SpeedCm = 0;
        var rows = BlockRows(renderer.Render(game));
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(BlocksPerBar, r.Total));
        Assert.Equal(22, rows[0].Lit); // 1UP on the top row
        Assert.Equal(0, rows[1].Lit);  // 2UP below

        race.AthleteFor(0)!.Runner.SpeedCm = 0;
        race.AthleteFor(1)!.Runner.SpeedCm = 775;
        rows = BlockRows(renderer.Render(game));
        Assert.Equal(0, rows[0].Lit);
        Assert.Equal(12, rows[1].Lit);
    }

    [Fact]
    public void BothRowsStayInsideTheBottomBand()
    {
        var game = new Game(seed: 3);
        var race = StartHeat(game, EventKind.Dash100m, players: 2);
        foreach (var a in race.Athletes) a.Runner.SpeedCm = 1550;
        var rows = BlockRows(new GameRenderer().Render(game));
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].Y >= Layout.BottomTop + 1);
        Assert.True(rows[1].Y > rows[0].Y + rows[0].H, "the second row must not overlap the first");
        Assert.True(rows[1].Y + rows[1].H <= Screen.Height - 1, "the second row must fit above the bottom edge");
    }

    [Fact]
    public void OnlineHeatsGiveBothSeatsABar()
    {
        var game = new Game(seed: 9, online: true);
        game.StartOnlineCredit();
        var b1 = new Bot(0, 11);
        var b2 = new Bot(1, 10);
        SimulationRunner.RunUntil(game, g => g.Simulation is RaceSimulation r && r.Phase == RacePhase.Running,
            g => (b1.Next(g), b2.Next(g)), 60);
        var race = Assert.IsAssignableFrom<RaceSimulation>(game.Simulation);
        Assert.Equal(RacePhase.Running, race.Phase);
        Assert.Equal(2, BlockRows(new GameRenderer().Render(game)).Count);
    }

    [Theory]
    [InlineData("en")] // pixel font labels
    [InlineData("ja")] // backend text for SPEED=
    public void EveryScreenOfATwoPlayerGameRendersValidCommands(string culture)
    {
        var game = new Game(new Localizer(culture), seed: 21);
        game.Tick(new InputFrame(Down: true), InputFrame.None);
        game.Tick(InputFrame.None, InputFrame.None);
        game.Tick(new InputFrame(Start: true), InputFrame.None);
        var renderer = new GameRenderer();
        var b1 = new Bot(0, 11);
        var b2 = new Bot(1, 10);
        int frames = 0, twoBarFrames = 0;
        GameState last = (GameState)(-1);
        SimulationRunner.RunUntil(game, g => g.State == GameState.Ceremony, g =>
        {
            frames++;
            bool changed = g.State != last;
            last = g.State;
            if (changed || frames % 6 == 0)
            {
                var dl = renderer.Render(g);
                RendererTests.Validate(dl.ToArray());
                if (g.State == GameState.Event && g.Simulation is RaceSimulation race)
                {
                    // both players run at once: one bar each (unless one of them is already out of chances)
                    int humans = race.Athletes.Count(a => !a.IsCpu);
                    Assert.Equal(humans >= 2 ? 2 : 1, BlockRows(dl).Count);
                    if (humans >= 2) twoBarFrames++;
                }
                else if (g.State == GameState.Event && g.Simulation is FieldEventSimulation)
                {
                    Assert.Single(BlockRows(dl)); // field events take turns: the single bar, as before
                }
            }
            return (b1.Next(g), b2.Next(g));
        }, maxSeconds: 900);
        Assert.Equal(2, game.PlayerCount);
        Assert.Equal(GameState.Ceremony, game.State);
        Assert.True(twoBarFrames > 0, "the heats of a two-player credit must show both speed bars");
    }

}
