using PrankAndField.Core;
using PrankAndField.Core.Localization;
using PrankAndField.Core.Tests.Support;
using PrankAndField.Engine.Graphics;
using PrankAndField.Engine.Rendering;
using PrankAndField.Engine.Sprites;

namespace PrankAndField.Engine.Tests;

public class RendererTests
{
    /// <summary>Walks the buffer and checks every opcode is known and every coordinate finite and on a sane range.</summary>
    internal static int Validate(double[] buf)
    {
        int i = 0, commands = 0;
        void Coord(double v)
        {
            Assert.True(double.IsFinite(v), "non-finite coordinate");
            Assert.InRange(v, -4000, 4000);
        }
        while (true)
        {
            double op = buf[i];
            if (op == Op.End) return commands;
            commands++;
            switch (op)
            {
                case Op.SetColor: i += 5; break;
                case Op.FillRect: Coord(buf[i + 1]); Coord(buf[i + 2]); Coord(buf[i + 3]); Coord(buf[i + 4]); i += 5; break;
                case Op.FillPoly: { int n = (int)buf[i + 1]; Assert.True(n >= 3); for (int k = 0; k < 2 * n; k++) Coord(buf[i + 2 + k]); i += 2 + 2 * n; break; }
                case Op.FillCircle: Coord(buf[i + 1]); Coord(buf[i + 2]); Assert.True(buf[i + 3] > 0); i += 4; break;
                case Op.FillEllipse: Coord(buf[i + 1]); Coord(buf[i + 2]); i += 5; break;
                case Op.Line: for (int k = 1; k <= 5; k++) Coord(buf[i + k]); i += 6; break;
                case Op.Polyline: { int n = (int)buf[i + 2]; for (int k = 0; k < 2 * n; k++) Coord(buf[i + 3 + k]); i += 3 + 2 * n; break; }
                case Op.Text: { int n = (int)buf[i + 5]; Coord(buf[i + 1]); Coord(buf[i + 2]); i += 6 + n; break; }
                case Op.ClipPush: i += 5; break;
                case Op.ClipPop: i += 1; break;
                case Op.AudioSection: { int n = (int)buf[i + 1]; i += 2 + 3 * n; break; }
                default: Assert.Fail($"unknown opcode {op} at {i}"); return commands;
            }
        }
    }

    public static IEnumerable<object[]> Cultures => Localizer.SupportedCultures.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cultures))]
    public void EveryScreenOfAWholeGameRendersValidCommands(string culture)
    {
        var game = new Game(new Localizer(culture), seed: 21);
        var renderer = new GameRenderer();
        var bot = new Bot(0, 11);
        var statesSeen = new HashSet<GameState>();
        var eventsSeen = new HashSet<EventKind>();
        int frames = 0;
        int maxCommands = 0;
        GameState last = (GameState)(-1);
        SimulationRunner.RunUntil(game, g => g.State == GameState.Ceremony, g =>
        {
            frames++;
            bool changed = g.State != last;
            last = g.State;
            if (changed || frames % 6 == 0) // render on every state change and at 10 Hz otherwise
            {
                var buf = renderer.Render(g).ToArray();
                maxCommands = Math.Max(maxCommands, Validate(buf));
                statesSeen.Add(g.State);
                if (g.State == GameState.Event) eventsSeen.Add(g.CurrentEvent);
            }
            return (bot.Next(g), InputFrame.None);
        }, maxSeconds: 900);
        Assert.Equal(GameState.Ceremony, game.State);
        Validate(renderer.Render(game).ToArray());
        Assert.Superset(new HashSet<GameState> { GameState.Title, GameState.NameEntry, GameState.EventIntro, GameState.Event, GameState.EventOutcome }, statesSeen);
        Assert.Equal(6, eventsSeen.Count);
        Assert.True(maxCommands < 6000, $"frames should stay light, max {maxCommands} commands");
    }

    [Fact]
    public void GameOverAndRecordEntryRender()
    {
        var game = new Game(seed: 5);
        var renderer = new GameRenderer();
        var lazy = new Bot(0) { Lazy = true };
        SimulationRunner.RunUntil(game, g => g.State == GameState.GameOver, g =>
        {
            if (g.StateTime < 0.05) Validate(renderer.Render(g).ToArray());
            return (lazy.Next(g), InputFrame.None);
        }, 400);
        Assert.Equal(GameState.GameOver, game.State);
        Validate(renderer.Render(game).ToArray());
    }

    [Fact]
    public void PracticeMenuRendersInEveryLanguage()
    {
        foreach (var culture in Localizer.SupportedCultures)
        {
            var game = new Game(new Localizer(culture));
            game.Tick(new InputFrame(Konami: true), InputFrame.None);
            Assert.Equal(GameState.PracticeMenu, game.State);
            Validate(new GameRenderer().Render(game).ToArray());
        }
    }

    [Fact]
    public void FirstFrameShowsTheTitleAndIsCompact()
    {
        var game = new Game();
        var buf = new GameRenderer().Render(game).ToArray();
        int commands = Validate(buf);
        Assert.InRange(commands, 100, 6000);
        Assert.True(buf.Length < 60_000, "title frame should be well under 60k doubles");
    }
}

public class AthleteSpriteTests
{
    [Theory]
    [InlineData(Pose.Idle)]
    [InlineData(Pose.Crouch)]
    [InlineData(Pose.Set)]
    [InlineData(Pose.Run)]
    [InlineData(Pose.Walk)]
    [InlineData(Pose.Jump)]
    [InlineData(Pose.Fly)]
    [InlineData(Pose.Land)]
    [InlineData(Pose.Hurdle)]
    [InlineData(Pose.Stumble)]
    [InlineData(Pose.Fall)]
    [InlineData(Pose.Throw)]
    [InlineData(Pose.Release)]
    [InlineData(Pose.Spin)]
    [InlineData(Pose.Flop)]
    [InlineData(Pose.Celebrate)]
    [InlineData(Pose.Dizzy)]
    public void EveryPoseDrawsAMustachioedAthleteNearTheAnchor(Pose pose)
    {
        var athlete = new Athlete(0, false, "AAA") { Pose = pose, AnimPhase = 0.3 };
        var dl = new DrawList();
        AthleteSprite.Draw(dl, 100, 150, athlete, 1.0);
        var arr = dl.ToArray();
        Assert.True(dl.CommandCount >= 15);
        // mustache colour must be present
        var described = dl.Describe().ToList();
        Assert.Contains($"color {Palette.Mustache.R},{Palette.Mustache.G},{Palette.Mustache.B},255", described);
        // all coordinates within ~40 px of the anchor
        int i = 0;
        while (arr[i] != Op.End)
        {
            switch (arr[i])
            {
                case Op.SetColor: i += 5; break;
                case Op.FillRect: AssertNear(arr[i + 1], arr[i + 2]); i += 5; break;
                case Op.FillCircle: AssertNear(arr[i + 1], arr[i + 2]); i += 4; break;
                case Op.Line: AssertNear(arr[i + 1], arr[i + 2]); AssertNear(arr[i + 3], arr[i + 4]); i += 6; break;
                case Op.FillPoly: { int n = (int)arr[i + 1]; for (int k = 0; k < n; k++) AssertNear(arr[i + 2 + 2 * k], arr[i + 3 + 2 * k]); i += 2 + 2 * n; break; }
                default: Assert.Fail("unexpected op " + arr[i]); break;
            }
        }
        static void AssertNear(double x, double y)
        {
            Assert.InRange(x, 55, 145);
            Assert.InRange(y, 100, 175);
        }
    }

    [Fact]
    public void RunCycleMovesTheLegsOverThePhase()
    {
        var a = AthleteSprite.PoseFor(Pose.Run, 0.25, 0);
        var b = AthleteSprite.PoseFor(Pose.Run, 0.75, 0);
        Assert.True(a.ThighNear > 0 && b.ThighNear < 0);
        Assert.True(Math.Abs(a.ThighNear - b.ThighNear) > 40);
    }

    [Fact]
    public void RunnersBendKneesBackwardsAndElbowsForwards()
    {
        foreach (var pose in new[] { Pose.Run, Pose.Walk })
        {
            for (double phase = 0; phase < 1; phase += 0.1)
            {
                var p = AthleteSprite.PoseFor(pose, phase, 0);
                Assert.True(p.KneeNear < 0 && p.KneeFar < 0, $"{pose} knees must bend backwards");
                Assert.True(p.ElbowNear > 0 && p.ElbowFar > 0, $"{pose} elbows must bend forwards");
            }
        }
    }

    [Fact]
    public void NearHandIsAboveTheGroundForAThrow()
    {
        var pose = AthleteSprite.PoseFor(Pose.Throw, 0, 0);
        var hand = AthleteSprite.NearHand(pose);
        Assert.True(double.IsFinite(hand.X) && double.IsFinite(hand.Y));
        Assert.True(hand.Y > 10);
    }
}
