using PrankAndField.Core.Events;
using PrankAndField.Core.Localization;
using PrankAndField.Core.Tests.Support;

namespace PrankAndField.Core.Tests;

public class SwimmingTests
{
    private static SwimmingSimulation New(uint seed = 1, bool twoPlayers = false) =>
        new(1, twoPlayers ? [new Athlete(0, false, "AAA"), new Athlete(1, false, "BBB")] : [new Athlete(0, false, "AAA"), new Athlete(1, true, "CPU")], new Rng(seed));

    [Fact]
    public void BreathingOnThePromptQualifiesInRoundOne()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        var mark = sim.MarkFor(0);
        Assert.True(mark is > 20 and < 32, $"time {mark}");
        Assert.True(sim.DidQualify(0));
        Assert.True(sim.Breaths(0) >= 3, $"breaths {sim.Breaths(0)}");
        Assert.Equal(0, sim.MissedBreaths(0));
        var cues = SimulationRunner.DrainCues(sim);
        Assert.Contains(AudioCue.SfxSplash, cues);
        Assert.Contains(AudioCue.SfxBreath, cues);
        Assert.DoesNotContain(AudioCue.SfxGasp, cues);
    }

    [Fact]
    public void IgnoringThePromptMakesTheSwimmerChokeAndSlower()
    {
        var good = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(good, s => (bot.NextForSimulation(s), InputFrame.None));

        var bad = New();
        var tapper = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(bad, s => (tapper.NextForSimulation(s) with { Action = false }, InputFrame.None));
        Assert.True(bad.MissedBreaths(0) >= 3, $"missed {bad.MissedBreaths(0)}");
        Assert.True(bad.MarkFor(0) > good.MarkFor(0) + 2, $"good {good.MarkFor(0)} bad {bad.MarkFor(0)}");
        Assert.Contains(AudioCue.SfxGasp, SimulationRunner.DrainCues(bad));
    }

    [Fact]
    public void TheComputerFinishesTheHundredMetres()
    {
        var sim = New();
        var lazy = new Bot(0) { Lazy = true };
        SimulationRunner.RunUntilFinished(sim, s => (lazy.NextForSimulation(s), InputFrame.None), 60);
        var cpu = sim.AthleteFor(1)!;
        Assert.True(cpu.Finished);
        Assert.InRange(cpu.FinishTime, 20, 30);
    }

    [Fact]
    public void TwoHumansSwimAtTheSameTime()
    {
        var sim = New(3, twoPlayers: true);
        var b1 = new Bot(0, 11);
        var b2 = new Bot(1, 9);
        SimulationRunner.RunUntilFinished(sim, s => (b1.NextForSimulation(s), b2.NextForSimulation(s)));
        Assert.True(sim.MarkFor(0) < sim.MarkFor(1), $"P1 {sim.MarkFor(0)} P2 {sim.MarkFor(1)}");
    }
}

public class SkeetTests
{
    private static SkeetSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void ShootingTheSightsHitsMostClays()
    {
        var sim = New();
        var bot = new Bot(0);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.Equal(Rules.SkeetTargets, sim.Launched);
        var mark = sim.MarkFor(0);
        Assert.True(mark >= 16, $"hits {mark}");
        Assert.True(sim.DidQualify(0));
        var cues = SimulationRunner.DrainCues(sim);
        Assert.Contains(AudioCue.SfxShot, cues);
        Assert.Contains(AudioCue.SfxClayBreak, cues);
        Assert.Contains(AudioCue.SfxSpring, cues);
        // a golden clay (every seventh) or a streak of five pays a bonus
        Assert.True(sim.BonusPoints[0] >= Rules.SkeetStreakBonus, $"bonus {sim.BonusPoints[0]}");
    }

    [Fact]
    public void NeverFiringScoresZeroAndStillEnds()
    {
        var sim = New();
        var lazy = new Bot(0) { Lazy = true };
        double t = SimulationRunner.RunUntilFinished(sim, s => (lazy.NextForSimulation(s), InputFrame.None), 90);
        Assert.True(sim.IsFinished, $"not finished after {t} s");
        Assert.Equal(0, sim.MarkFor(0));
        Assert.False(sim.DidQualify(0));
    }

    [Fact]
    public void FiringAtNothingBreaksTheStreak()
    {
        var sim = New();
        int ticks = 0;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            ticks++;
            // fire the left gun every 20 ticks, whatever is in the air
            return (new InputFrame(Run1: ticks % 20 == 0), InputFrame.None);
        });
        Assert.True(sim.Shots > 20, $"shots {sim.Shots}");
        Assert.True(sim.Hits < Rules.SkeetTargets);
    }

    [Fact]
    public void TwoPlayersTakeOneTurnEach()
    {
        var sim = new SkeetSimulation(1, [new Athlete(0, false, "AAA"), new Athlete(1, false, "BBB")], new Rng(5));
        var b1 = new Bot(0);
        var b2 = new Bot(1);
        var turns = new List<int>();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            if (turns.Count == 0 || turns[^1] != sim.CurrentAthlete.PlayerIndex) turns.Add(sim.CurrentAthlete.PlayerIndex);
            return (b1.NextForSimulation(s), b2.NextForSimulation(s));
        }, 200);
        Assert.Equal([0, 1], turns);
        Assert.NotNull(sim.MarkFor(0));
        Assert.NotNull(sim.MarkFor(1));
    }
}

public class VaultTests
{
    private static VaultSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void AGoodVaultScoresNearTheTop()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        var mark = sim.MarkFor(0);
        Assert.True(mark is >= 8.5 and <= 10.0, $"score {mark}");
        Assert.True(sim.DidQualify(0));
        var cues = SimulationRunner.DrainCues(sim);
        Assert.Contains(AudioCue.SfxSpring, cues);
        Assert.Contains(AudioCue.SfxLand, cues);
    }

    [Fact]
    public void FewerTwistsScoreLess()
    {
        var full = New();
        var twister = new Bot(0, 11) { TwistTaps = 8 };
        SimulationRunner.RunUntilFinished(full, s => (twister.NextForSimulation(s), InputFrame.None));
        var plain = New();
        var still = new Bot(0, 11) { TwistTaps = 0.001 };
        SimulationRunner.RunUntilFinished(plain, s => (still.NextForSimulation(s), InputFrame.None));
        Assert.True(full.MarkFor(0) > plain.MarkFor(0), $"twists {full.MarkFor(0)} plain {plain.MarkFor(0)}");
    }

    [Fact]
    public void JumpingLongBeforeTheBoardIsAFoul()
    {
        var sim = New();
        int ticks = 0;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            ticks++;
            // run, then press far too early
            var a = s.AthleteFor(0)!;
            return (new InputFrame(Run1: ticks % 6 == 0, Run2: ticks % 6 == 3, Action: a.X > 10 && a.X < Rules.VaultBoard - 3), InputFrame.None);
        });
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Null(sim.MarkFor(0));
        Assert.Contains(AudioCue.SfxWhistle, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void RunningIntoTheHorseIsAFoul()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s) with { Action = false }, InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }

    [Fact]
    public void ScoreFormulaRewardsEverything()
    {
        Assert.Equal(10.0, Rules.VaultScore(1, 1, 4, true));
        Assert.True(Rules.VaultScore(1, 1, 2, true) > Rules.VaultScore(1, 1, 1, true));
        Assert.True(Rules.VaultScore(1, 1, 1.5, true) < Rules.VaultScore(1, 1, 1, true), "a half twist is a messy landing");
        Assert.True(Rules.VaultScore(0, 0, 0, false) >= 5);
    }
}

public class ArcheryTests
{
    private static ArcherySimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void SixAimedArrowsQualify()
    {
        var sim = New();
        var bot = new Bot(0);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.Equal(Rules.ArcheryArrows, sim.ArrowIndex);
        var mark = sim.MarkFor(0);
        Assert.True(mark >= Rules.QualifyingMark(EventKind.Archery, 1), $"points {mark}");
        Assert.True(sim.Stuck.Count >= 5, $"stuck {sim.Stuck.Count}");
        var cues = SimulationRunner.DrainCues(sim);
        Assert.Contains(AudioCue.SfxBow, cues);
        Assert.Contains(AudioCue.SfxArrowHit, cues);
    }

    [Fact]
    public void AimingWayOffMisses()
    {
        var sim = New();
        var bot = new Bot(0) { AimOffset = 3.0 };
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.MarkFor(0) < 20, $"points {sim.MarkFor(0)}");
    }

    [Fact]
    public void WithoutAPressTheArrowLeavesByItselfAfterTheTimeout()
    {
        var sim = New();
        var lazy = new Bot(0) { Lazy = true };
        double t = SimulationRunner.RunUntilFinished(sim, s => (lazy.NextForSimulation(s), InputFrame.None), 120);
        Assert.True(sim.IsFinished, $"not finished after {t} s");
        Assert.True(t < Rules.ArcheryArrows * (Rules.ArcheryTimeout + 3));
        Assert.Equal(0, sim.MarkFor(0));
    }

    [Fact]
    public void RingsScoreTenDownToOne()
    {
        Assert.Equal(10, Rules.ArcheryPoints(0));
        Assert.Equal(10, Rules.ArcheryPoints(0.10));
        Assert.Equal(9, Rules.ArcheryPoints(-0.15));
        Assert.Equal(1, Rules.ArcheryPoints(0.99));
        Assert.Equal(0, Rules.ArcheryPoints(1.2));
    }
}

public class TripleJumpTests
{
    private static TripleJumpSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void HopStepAndJumpQualify()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        var phases = new List<int>();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            if (sim.State == TripleJumpState.Flight && (phases.Count == 0 || phases[^1] != sim.JumpPhase)) phases.Add(sim.JumpPhase);
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.True(sim.IsFinished);
        Assert.Equal([0, 1, 2, 0, 1, 2, 0, 1, 2], phases);
        var mark = sim.MarkFor(0);
        Assert.True(mark is > 13 and < 19, $"mark {mark}");
        Assert.True(sim.DidQualify(0));
        Assert.Equal(0, sim.LateTakeoffs);
    }

    [Fact]
    public void MissingTheContactPressesCostsDistance()
    {
        var good = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(good, s => (bot.NextForSimulation(s), InputFrame.None));

        var late = New();
        var hopper = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(late, s =>
        {
            var f = hopper.NextForSimulation(s);
            if (late.State == TripleJumpState.Contact) f = f with { Action = false };
            return (f, InputFrame.None);
        });
        Assert.True(late.LateTakeoffs > 0);
        Assert.True(late.MarkFor(0) is null || late.MarkFor(0) < good.MarkFor(0), $"good {good.MarkFor(0)} late {late.MarkFor(0)}");
    }

    [Fact]
    public void JumpingPastTheBoardIsAFoul()
    {
        var sim = New();
        var bot = new Bot(0, 11) { TakeoffMargin = -0.4 };
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }

    [Fact]
    public void RunningThroughTheBoardIsAFoul()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s) with { Action = false }, InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }
}

public class WeightliftingTests
{
    private static WeightliftingSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void ThreeGoodLiftsRaiseTheBarEachTime()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        var weights = new List<double>();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            if (sim.Phase == AttemptPhase.Active && (weights.Count == 0 || weights[^1] != sim.Weight)) weights.Add(sim.Weight);
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.True(sim.IsFinished);
        double q = Rules.QualifyingMark(EventKind.Weightlifting, 1);
        Assert.Equal([q, q + Rules.WeightStep, q + 2 * Rules.WeightStep], weights);
        Assert.Equal(q + 2 * Rules.WeightStep, sim.MarkFor(0));
        Assert.True(sim.DidQualify(0));
        var cues = SimulationRunner.DrainCues(sim);
        Assert.Contains(AudioCue.SfxClank, cues);
        Assert.Contains(AudioCue.SfxLights, cues);
    }

    [Fact]
    public void NotPressingOnThePromptDropsTheBar()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s) with { Action = false }, InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Null(sim.MarkFor(0));
        Assert.DoesNotContain(AudioCue.SfxLights, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void TappingTooSlowlyRunsOutTheClock()
    {
        var sim = New();
        var slow = new Bot(0, 2);
        double t = SimulationRunner.RunUntilFinished(sim, s => (slow.NextForSimulation(s), InputFrame.None), 120);
        Assert.True(sim.IsFinished, $"not finished after {t} s");
        Assert.All(sim.AttemptsFor(0), a => Assert.True(a.Foul));
    }

    [Fact]
    public void LettingTheBarTiltDropsIt()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var f = bot.NextForSimulation(s);
            if (sim.State == LiftState.Hold) f = InputFrame.None; // no balance corrections
            return (f, InputFrame.None);
        });
        // with drift and no corrections most lifts drop; at least one must have dropped from the hold
        Assert.True(sim.AttemptsFor(0).Any(a => a.Foul), "no lift dropped");
    }
}

public class HyperSportsGameTests
{
    private static void Press(Game game, InputFrame f)
    {
        game.Tick(f, InputFrame.None);
        game.Tick(InputFrame.None, InputFrame.None);
    }

    [Fact]
    public void TitleMenuHasFourEntriesAcrossTheTwoModes()
    {
        var game = new Game();
        Assert.Equal(GameMode.TrackAndField, game.MenuMode);
        Assert.Equal(1, game.MenuPlayers);
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Assert.Equal(2, game.MenuSelection);
        Assert.Equal(GameMode.HyperSports, game.MenuMode);
        Assert.Equal(1, game.MenuPlayers);
        Press(game, new InputFrame(Down: true));
        Assert.Equal((GameMode.HyperSports, 2), (game.MenuMode, game.MenuPlayers));
        Press(game, new InputFrame(Down: true)); // wraps
        Assert.Equal(0, game.MenuSelection);
        Press(game, new InputFrame(Up: true));
        Assert.Equal(3, game.MenuSelection);
    }

    [Fact]
    public void StartingHyperSportsBeginsWithTheSwimming()
    {
        var game = new Game();
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Start: true));
        Assert.Equal(GameState.NameEntry, game.State);
        Assert.Equal(GameMode.HyperSports, game.Mode);
        Assert.Equal(1, game.PlayerCount);
        Press(game, new InputFrame(Start: true));
        Assert.Equal(GameState.EventIntro, game.State);
        Assert.Equal(EventKind.Swimming100m, game.CurrentEvent);
    }

    [Fact]
    public void AHyperSportsRoundReachesTheMedalCeremonyInOrder()
    {
        var game = new Game(seed: 42);
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        var bot = new Bot(0, 11);
        var seen = new List<EventKind>();
        double elapsed = SimulationRunner.RunUntil(game, g => g.State == GameState.Ceremony, g =>
        {
            if (g.State == GameState.Event && (seen.Count == 0 || seen[^1] != g.CurrentEvent)) seen.Add(g.CurrentEvent);
            return (bot.Next(g), InputFrame.None);
        }, maxSeconds: 1200);
        Assert.Equal(GameState.Ceremony, game.State);
        Assert.Equal(GameMode.HyperSports.Events(), seen);
        Assert.False(game.Players[0].IsOut);
        Assert.True(game.Players[0].Score > 10_000, $"score {game.Players[0].Score}");
        Assert.True(elapsed < 1200);

        SimulationRunner.RunUntil(game, g => g.State == GameState.EventIntro, g => (InputFrame.None, InputFrame.None), 20);
        Assert.Equal(2, game.Round);
        Assert.Equal(EventKind.Swimming100m, game.CurrentEvent);
        Assert.Equal(GameMode.HyperSports, game.Mode);
    }

    [Fact]
    public void TwoPlayersShareAHyperSportsCredit()
    {
        var game = new Game(seed: 11);
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Start: true));
        Assert.Equal((GameMode.HyperSports, 2), (game.Mode, game.PlayerCount));
        var b1 = new Bot(0, 11);
        var b2 = new Bot(1, 10);
        SimulationRunner.RunUntil(game, g => g.State == GameState.Event && g.CurrentEvent == EventKind.SkeetShooting, g => (b1.Next(g), b2.Next(g)), 200);
        var sk = Assert.IsType<SkeetSimulation>(game.Simulation);
        Assert.Equal(2, sk.Athletes.Count);
        SimulationRunner.RunUntil(game, g => g.State == GameState.EventOutcome, g => (b1.Next(g), b2.Next(g)), 200);
        Assert.Equal(2, game.Outcomes.Count);
        Assert.All(game.Outcomes, o => Assert.NotNull(o.Mark));
    }

    [Fact]
    public void FailingTwiceEndsAHyperSportsGameToo()
    {
        var game = new Game(seed: 9);
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        var lazy = new Bot(0) { Lazy = true };
        SimulationRunner.RunUntil(game, g => g.State == GameState.GameOver, g => (lazy.Next(g), InputFrame.None), maxSeconds: 600);
        Assert.Equal(GameState.GameOver, game.State);
        Assert.Equal(EventKind.SkeetShooting, game.CurrentEvent);
    }

    [Fact]
    public void PracticeMenuCyclesTheEventsOfTheHighlightedMode()
    {
        var game = new Game();
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Konami: true));
        Assert.Equal(GameState.PracticeMenu, game.State);
        Assert.Equal(GameMode.HyperSports, game.Mode);
        Assert.Equal(EventKind.Swimming100m, game.PracticeEvent);
        Press(game, new InputFrame(Up: true));
        Assert.Equal(EventKind.Weightlifting, game.PracticeEvent);
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Assert.Equal(EventKind.LongHorse, game.PracticeEvent);
        Press(game, new InputFrame(Action: true));
        Assert.Equal(GameState.EventIntro, game.State);
        Assert.Equal(EventKind.LongHorse, game.CurrentEvent);
        Assert.Equal(GameMode.HyperSports, game.Mode);
    }

    [Fact]
    public void JumpingToAHyperSportsEventSwitchesTheMode()
    {
        var game = new Game(seed: 2);
        game.JumpToEvent(EventKind.Archery, 2);
        Assert.Equal(GameMode.HyperSports, game.Mode);
        Assert.Equal(EventKind.Archery, game.CurrentEvent);
        Assert.Equal(2, game.Round);
        SimulationRunner.RunUntil(game, g => g.State == GameState.Event, g => (InputFrame.None, InputFrame.None), 20);
        Assert.IsType<ArcherySimulation>(game.Simulation);
    }

    [Fact]
    public void FinishingAHyperSportsEventAnnouncesItsMark()
    {
        var game = new Game(seed: 8);
        game.JumpToEvent(EventKind.Weightlifting);
        var bot = new Bot(0, 11);
        var marks = new List<MarkAnnouncement>();
        SimulationRunner.RunUntil(game, g => g.State == GameState.EventOutcome, g =>
        {
            while (g.TryDequeueMark(out var m)) marks.Add(m);
            return (bot.Next(g), InputFrame.None);
        }, 200);
        Assert.NotEmpty(marks);
        Assert.All(marks, m => Assert.Equal(EventKind.Weightlifting, m.Kind));
        Assert.Equal(0, marks[^1].Mark % Rules.WeightStep);
    }

    [Fact]
    public void EventKindsKnowTheirModeUnitAndOrder()
    {
        Assert.Equal(12, EventKindExtensions.All.Length);
        Assert.Equal(6, GameMode.HyperSports.Events().Length);
        Assert.All(GameMode.HyperSports.Events(), k => Assert.Equal(GameMode.HyperSports, k.Mode()));
        Assert.All(EventKindExtensions.InOrder, k => Assert.Equal(GameMode.TrackAndField, k.Mode()));
        Assert.Equal(EventKind.Swimming100m, EventKind.Weightlifting.Next());
        Assert.Equal(EventKind.Dash100m, EventKind.HighJump.Next());
        Assert.True(EventKind.Weightlifting.IsLastOfGame());
        Assert.False(EventKind.HighJump.IsLastOfGame() == false);
        Assert.True(EventKind.Swimming100m.IsTimed());
        Assert.True(EventKind.Swimming100m.IsSimultaneous());
        Assert.Equal(MarkUnit.Hits, EventKind.SkeetShooting.Unit());
        Assert.Equal(MarkUnit.Score, EventKind.LongHorse.Unit());
        Assert.Equal(MarkUnit.Points, EventKind.Archery.Unit());
        Assert.Equal(MarkUnit.Metres, EventKind.TripleJump.Unit());
        Assert.Equal(MarkUnit.Kilograms, EventKind.Weightlifting.Unit());
        Assert.True(EventKind.Archery.IsWholeNumber());
        Assert.False(EventKind.LongHorse.IsWholeNumber());
        Assert.Equal("instr.skeet", EventKind.SkeetShooting.InstructionKey());
        var loc = new Localizer("es-ES");
        Assert.Equal("TIRO AL PLATO", loc[EventKind.SkeetShooting.Key()]);
        Assert.Equal("HYPER SPORTS", loc[GameMode.HyperSports.Key()]);
    }

    [Fact]
    public void ModeIsPartOfTheChecksum()
    {
        var a = new Game(seed: 1);
        var b = new Game(seed: 1);
        Press(b, new InputFrame(Down: true));
        Press(b, new InputFrame(Down: true));
        Press(a, new InputFrame(Start: true));
        Press(b, new InputFrame(Start: true));
        Assert.Equal(GameState.NameEntry, a.State);
        Assert.Equal(GameState.NameEntry, b.State);
        Assert.NotEqual(a.Checksum(), b.Checksum());
    }
}

public class HyperSportsRecordsTests
{
    [Fact]
    public void DefaultsCoverTheTwelveEventsSortedBestFirst()
    {
        var r = new WorldRecords();
        foreach (var kind in EventKindExtensions.All)
        {
            var row = r.For(kind);
            Assert.Equal(3, row.Count);
            for (int i = 1; i < row.Count; i++)
                Assert.False(Rules.IsBetter(kind, row[i].Mark, row[i - 1].Mark), $"{kind} not sorted");
            if (kind.Mode() == GameMode.HyperSports)
                Assert.True(Rules.IsBetter(kind, row[0].Mark, Rules.QualifyingMark(kind, Rules.MaxRound)), $"{kind} record should beat the toughest standard");
        }
        Assert.Equal(20, r.Best(EventKind.SkeetShooting).Mark);
        Assert.Equal(9.90, r.Best(EventKind.LongHorse).Mark);
    }

    [Fact]
    public void AnOldSixEventBoardStillLoadsAndKeepsTheHyperSportsDefaults()
    {
        var old = new WorldRecords();
        old.Submit(EventKind.Dash100m, 9.50, "OLD");
        // serialize only the first six rows, like the boards saved before Hyper Sports existed
        string json = old.ToJson();
        var r = new WorldRecords();
        Assert.True(r.TryLoadJson(json));
        Assert.Equal("OLD", r.Best(EventKind.Dash100m).Initials);
        Assert.Equal(205, r.Best(EventKind.Weightlifting).Mark);

        // boards saved before Hyper Sports had six rows: times ascending, distances descending
        string timed = "[{\"m\":30,\"i\":\"AAA\"},{\"m\":31,\"i\":\"BBB\"},{\"m\":32,\"i\":\"CCC\"}]";
        string field = "[{\"m\":3,\"i\":\"AAA\"},{\"m\":2,\"i\":\"BBB\"},{\"m\":1,\"i\":\"CCC\"}]";
        string sixRows = "{\"v\":1,\"records\":[" + string.Join(",", EventKindExtensions.InOrder.Select(k => k.IsTimed() ? timed : field)) + "]}";
        var r2 = new WorldRecords();
        Assert.True(r2.TryLoadJson(sixRows));
        Assert.Equal("AAA", r2.Best(EventKind.HighJump).Initials);
        Assert.Equal(17.89, r2.Best(EventKind.TripleJump).Mark);
    }

    [Fact]
    public void TwelveRowsRoundTrip()
    {
        var r = new WorldRecords();
        r.Submit(EventKind.Archery, 60, "BOW");
        r.Submit(EventKind.Weightlifting, 300, "BIG");
        var r2 = new WorldRecords();
        Assert.True(r2.TryLoadJson(r.ToJson()));
        Assert.Equal(("BOW", 60.0), (r2.Best(EventKind.Archery).Initials, r2.Best(EventKind.Archery).Mark));
        Assert.Equal(("BIG", 300.0), (r2.Best(EventKind.Weightlifting).Initials, r2.Best(EventKind.Weightlifting).Mark));
    }
}

public class HyperSportsDeterminismTests
{
    [Fact]
    public void ReplayingAHyperSportsCreditReproducesEveryTick()
    {
        var game = new Game(seed: 17, online: true);
        var bot1 = new Bot(0, 11);
        var bot2 = new Bot(1, 10);
        var masks = new List<(int P1, int P2)>();
        var checksums = new List<ulong>();

        void Step(InputFrame p1, InputFrame p2)
        {
            game.Tick(p1, p2);
            masks.Add((p1.ToMask(), p2.ToMask()));
            checksums.Add(game.Checksum());
        }

        // Hyper Sports, 2 players: three steps down the menu, then the bots play
        for (int i = 0; i < 3; i++) { Step(new InputFrame(Down: true), InputFrame.None); Step(InputFrame.None, InputFrame.None); }
        while (game.State != GameState.Ceremony && masks.Count < 60 * 900)
            Step(bot1.Next(game), bot2.Next(game));
        Assert.Equal(GameState.Ceremony, game.State);
        Assert.Equal(GameMode.HyperSports, game.Mode);

        var replay = new Game(seed: 17, online: true);
        for (int i = 0; i < masks.Count; i++)
        {
            replay.Tick(InputFrame.FromMask(masks[i].P1), InputFrame.FromMask(masks[i].P2));
            Assert.True(checksums[i] == replay.Checksum(), $"diverged at tick {i}");
        }
        Assert.Equal(GameState.Ceremony, replay.State);
    }
}
