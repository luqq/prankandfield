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
}
