using PrankAndField.Core.Events;
using PrankAndField.Core.Tests.Support;

namespace PrankAndField.Core.Tests;

public class DashTests
{
    private static DashSimulation NewDash(int round = 1, uint seed = 7) =>
        new(round, [new Athlete(0, false, "AAA"), new Athlete(1, true, "CPU")], new Rng(seed));

    [Fact]
    public void StartSequenceGoesMarksSetGo()
    {
        var sim = NewDash();
        var phases = new List<RacePhase>();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var r = (RaceSimulation)s;
            if (phases.Count == 0 || phases[^1] != r.Phase) phases.Add(r.Phase);
            return (InputFrame.None, InputFrame.None);
        }, maxSeconds: 8);
        Assert.Equal([RacePhase.Intro, RacePhase.OnYourMarks, RacePhase.GetSet, RacePhase.Running], phases.Take(4));
        var cues = SimulationRunner.DrainCues(sim);
        Assert.Contains(AudioCue.VoiceOnYourMarks, cues);
        Assert.Contains(AudioCue.VoiceGetSet, cues);
        Assert.Contains(AudioCue.SfxGun, cues);
    }

    [Theory]
    [InlineData(8, 10.5, 15.9)]
    [InlineData(11, 8.0, 11.5)]
    public void TappingRhythmDeterminesTheTime(double hz, double min, double max)
    {
        var sim = NewDash();
        var bot = new Bot(0, hz);
        SimulationRunner.RunUntilFinished(sim, s => (BotInput(bot, s), InputFrame.None));
        Assert.True(sim.IsFinished);
        double time = sim.MarkFor(0)!.Value;
        Assert.InRange(time, min, max);
        Assert.True(sim.DidQualify(0));
    }

    [Fact]
    public void ComputerFinishesInAPlausibleTime()
    {
        var sim = NewDash();
        SimulationRunner.RunUntilFinished(sim, _ => (InputFrame.None, InputFrame.None));
        var cpu = sim.AthleteFor(1)!;
        Assert.True(cpu.Finished);
        Assert.InRange(cpu.FinishTime, 9.5, 16.0);
    }

    [Fact]
    public void StalledHumanIsEventuallyDisqualified()
    {
        var sim = NewDash();
        SimulationRunner.RunUntilFinished(sim, _ => (InputFrame.None, InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.Null(sim.MarkFor(0));
        Assert.False(sim.DidQualify(0));
    }

    [Fact]
    public void PressingBeforeTheGunIsAFlyingStart()
    {
        var sim = NewDash();
        int presses = 0;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var r = (RaceSimulation)s;
            if (r.Phase == RacePhase.GetSet && presses < 1)
            {
                presses++;
                return (new InputFrame(Run1: true), InputFrame.None);
            }
            return (InputFrame.None, InputFrame.None);
        }, maxSeconds: 4);
        var a = sim.AthleteFor(0)!;
        Assert.Equal(1, a.FalseStarts);
        Assert.False(a.Disqualified);
        Assert.Contains(AudioCue.VoiceFalseStart, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void ThreeFlyingStartsLoseTheHeat()
    {
        var sim = NewDash();
        bool pressedThisPhase = false;
        RacePhase last = RacePhase.Intro;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var r = (RaceSimulation)s;
            if (r.Phase != last) { last = r.Phase; pressedThisPhase = false; }
            if (r.Phase == RacePhase.OnYourMarks && !pressedThisPhase)
            {
                pressedThisPhase = true;
                return (new InputFrame(Run2: true), InputFrame.None);
            }
            return (InputFrame.None, InputFrame.None);
        });
        var a = sim.AthleteFor(0)!;
        Assert.True(a.Disqualified);
        Assert.Equal(Rules.FalseStartsAllowed + 1, a.FalseStarts);
        Assert.Null(sim.MarkFor(0));
    }

    [Fact]
    public void TwoHumansCanRaceTogether()
    {
        var sim = new DashSimulation(1, [new Athlete(0, false, "AAA"), new Athlete(1, false, "BBB")], new Rng(3));
        var b1 = new Bot(0, 10);
        var b2 = new Bot(1, 12);
        SimulationRunner.RunUntilFinished(sim, s => (BotInput(b1, s), BotInput(b2, s)));
        Assert.True(sim.DidQualify(0));
        Assert.True(sim.DidQualify(1));
        Assert.True(sim.MarkFor(1) < sim.MarkFor(0));
    }

    internal static InputFrame BotInput(Bot bot, EventSimulation sim) => bot.NextForSimulation(sim);
}

public class HurdlesTests
{
    private static HurdlesSimulation New(uint seed = 5) =>
        new(1, [new Athlete(0, false, "AAA"), new Athlete(1, true, "CPU")], new Rng(seed));

    [Fact]
    public void TenHurdlesAreSpacedLikeTheRealEvent()
    {
        var sim = New();
        var h = sim.HurdlesFor(0);
        Assert.Equal(10, h.Count);
        Assert.Equal(13.72, h[0].X, 2);
        Assert.Equal(9.14, h[1].X - h[0].X, 2);
        Assert.True(h[^1].X < Rules.HurdlesDistance);
    }

    [Fact]
    public void WellTimedJumpsClearEveryHurdle()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.DoesNotContain(sim.HurdlesFor(0), h => h.Fallen);
        Assert.True(sim.DidQualify(0), $"time {sim.MarkFor(0)}");
    }

    [Fact]
    public void NotJumpingKnocksHurdlesAndCostsTime()
    {
        var clean = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(clean, s => (bot.NextForSimulation(s), InputFrame.None));

        var crash = New();
        var runner = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(crash, s =>
        {
            var f = runner.NextForSimulation(s);
            return (f with { Action = false }, InputFrame.None);
        });
        Assert.Equal(10, crash.HurdlesFor(0).Count(h => h.Fallen));
        Assert.True(crash.MarkFor(0) > clean.MarkFor(0) + 2, $"crash {crash.MarkFor(0)} clean {clean.MarkFor(0)}");
        Assert.Contains(AudioCue.SfxHurdleHit, SimulationRunner.DrainCues(crash));
    }

    [Fact]
    public void ComputerClearsItsHurdles()
    {
        var sim = New();
        SimulationRunner.RunUntilFinished(sim, _ => (InputFrame.None, InputFrame.None));
        Assert.True(sim.AthleteFor(1)!.Finished);
        Assert.True(sim.HurdlesFor(1).Count(h => h.Fallen) <= 1);
    }

    [Theory]
    [InlineData(8, 11.8, 14.0)]  // a casual rhythm clears round 1 (14.50) with room for a clipped hurdle
    [InlineData(9, 10.6, 12.5)]  // ...and the round-4 standard (12.50)
    [InlineData(11, 9.4, 11.4)]
    public void TappingRhythmDeterminesTheTimeOverTheHurdles(double hz, double min, double max)
    {
        var sim = New();
        var bot = new Bot(0, hz);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.DoesNotContain(sim.HurdlesFor(0), h => h.Fallen);
        Assert.InRange(sim.MarkFor(0)!.Value, min, max);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    public void TenHurdlesCostLittleMoreThanTheTenExtraMetres(double hz)
    {
        // Same rhythm over 100 m flat and over 110 m with ten barriers: the hurdler may lose a second, not several.
        var dash = new DashSimulation(1, [new Athlete(0, false, "AAA"), new Athlete(1, true, "CPU")], new Rng(5));
        var dashBot = new Bot(0, hz);
        SimulationRunner.RunUntilFinished(dash, s => (dashBot.NextForSimulation(s), InputFrame.None));
        var hurdles = New();
        var hurdlesBot = new Bot(0, hz);
        SimulationRunner.RunUntilFinished(hurdles, s => (hurdlesBot.NextForSimulation(s), InputFrame.None));
        double gap = hurdles.MarkFor(0)!.Value - dash.MarkFor(0)!.Value;
        Assert.InRange(gap, -0.5, 1.5);
    }

    [Fact]
    public void TakeoffCostsSpeedAndTheLandingGivesPartOfItBack()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        double beforeTakeoff = 0;
        double? airborne = null, landed = null;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var a = sim.AthleteFor(0)!;
            if (sim.IsAirborne(0)) airborne ??= a.Runner.SpeedCm;
            else if (airborne is null) beforeTakeoff = a.Runner.SpeedCm;
            else landed ??= a.Runner.SpeedCm;
            return (landed is null ? bot.NextForSimulation(s) : InputFrame.None, InputFrame.None);
        }, maxSeconds: 10);
        Assert.NotNull(airborne);
        Assert.NotNull(landed);
        Assert.True(beforeTakeoff > Rules.HurdleTakeoffPenalty, $"took off at {beforeTakeoff} cm/s");
        // the press of the take-off tick may still add one impulse before the penalty
        double impulse = Runner.ImpulseAlternate * Rules.HurdleImpulseScale;
        Assert.InRange(airborne.Value, beforeTakeoff - Rules.HurdleTakeoffPenalty, beforeTakeoff - Rules.HurdleTakeoffPenalty + impulse);
        // speed is frozen in the air, then the landing gives the boost back minus one tick of drag
        Assert.InRange(landed.Value, airborne.Value + Rules.HurdleLandingBoost - 30, airborne.Value + Rules.HurdleLandingBoost);
    }

    [Fact]
    public void HoppingFromAStandstillGivesNoFreeSpeed()
    {
        var sim = New();
        bool hopped = false;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            if (sim.Phase == RacePhase.Running && !hopped && sim.RaceTime > 0.1)
            {
                hopped = true;
                return (new InputFrame(Action: true), InputFrame.None);
            }
            return (InputFrame.None, InputFrame.None);
        }, maxSeconds: 8);
        Assert.True(hopped);
        Assert.False(sim.IsAirborne(0));
        Assert.Equal(0, sim.AthleteFor(0)!.Runner.SpeedCm);
        Assert.Equal(0, sim.AthleteFor(0)!.X, 3);
    }

    [Fact]
    public void RunPressesAreWorthMoreBetweenHurdlesThanInTheDash()
    {
        var sim = New();
        Assert.All(sim.Athletes, a => Assert.Equal(Rules.HurdleImpulseScale, a.Runner.ImpulseScale));
        Assert.True(Rules.HurdleImpulseScale > 1);
        var dash = new DashSimulation(1, [new Athlete(0, false, "AAA"), new Athlete(1, true, "CPU")], new Rng(5));
        Assert.All(dash.Athletes, a => Assert.Equal(1.0, a.Runner.ImpulseScale));
    }

    [Fact]
    public void KnockedHurdlesChangeTheChecksum()
    {
        var clean = New();
        var cleanBot = new Bot(0, 11);
        var crash = New();
        var crashBot = new Bot(0, 11);
        // tick both until the first hurdles are behind the runner (the gun fires about five seconds in), the crasher never jumping
        SimulationRunner.RunUntilFinished(clean, s => (cleanBot.NextForSimulation(s), InputFrame.None), maxSeconds: 9);
        SimulationRunner.RunUntilFinished(crash, s => (crashBot.NextForSimulation(s) with { Action = false }, InputFrame.None), maxSeconds: 9);
        Assert.Equal(0, clean.FallenCount(0));
        Assert.True(crash.FallenCount(0) >= 1);
        var h1 = new StateHash();
        clean.AddChecksum(ref h1);
        var h2 = new StateHash();
        crash.AddChecksum(ref h2);
        Assert.NotEqual(h1.Value, h2.Value);
    }
}
