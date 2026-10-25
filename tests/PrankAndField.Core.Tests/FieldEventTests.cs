using PrankAndField.Core.Events;
using PrankAndField.Core.Tests.Support;

namespace PrankAndField.Core.Tests;

public class LongJumpTests
{
    private static LongJumpSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void ThreeTriesThenFinished()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        var tries = sim.AttemptsFor(0);
        Assert.All(tries, t => Assert.True(t.Done));
        Assert.True(sim.MarkFor(0) is > 5.0 and < 12.0, $"mark {sim.MarkFor(0)}");
        Assert.True(sim.DidQualify(0));
    }

    [Fact]
    public void JumpingPastTheBoardIsAFoul()
    {
        var sim = New();
        var bot = new Bot(0, 11) { TakeoffMargin = -0.4 };
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Null(sim.MarkFor(0));
        Assert.Contains(AudioCue.VoiceFoul, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void LandingShortOfTheSandIsAFoulNotANegativeMark()
    {
        var sim = New();
        var bot = new Bot(0, 11) { TakeoffMargin = 12 }; // takes off far too early and lands on the runway
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        var cues = SimulationRunner.DrainCues(sim);
        Assert.Contains(AudioCue.VoiceFoul, cues);
        Assert.DoesNotContain(AudioCue.VoiceMark, cues);
    }

    [Fact]
    public void RunningIntoThePitIsAFoul()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var f = bot.NextForSimulation(s);
            return (f with { Action = false }, InputFrame.None);
        });
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }

    [Fact]
    public void HoldingLongerRaisesTheAngleUpToNinety()
    {
        var sim = New();
        bool jumped = false;
        double maxAngle = 0;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var lj = (LongJumpSimulation)s;
            maxAngle = Math.Max(maxAngle, lj.Angle);
            if (lj.Phase != AttemptPhase.Active) return (InputFrame.None, InputFrame.None);
            if (lj.State == JumpState.Approach)
            {
                var a = lj.CurrentAthlete;
                if (a.X >= lj.BoardX - 0.8) { jumped = true; return (new InputFrame(Action: true), InputFrame.None); }
                return ((int)(s.Elapsed * 60) % 3 == 0 ? new InputFrame(Run1: (int)(s.Elapsed * 20) % 2 == 0, Run2: (int)(s.Elapsed * 20) % 2 == 1) : InputFrame.None, InputFrame.None);
            }
            return (new InputFrame(Action: true), InputFrame.None); // hold forever
        }, maxSeconds: 60);
        Assert.True(jumped);
        Assert.Equal(90, maxAngle, 0);
    }

    [Fact]
    public void DistanceGrowsWithSpeedAndPeaksAroundFortyFiveDegrees()
    {
        Assert.True(Rules.LongJumpDistance(12, 45) > Rules.LongJumpDistance(10, 45));
        Assert.True(Rules.LongJumpDistance(11, 45) > Rules.LongJumpDistance(11, 20));
        Assert.True(Rules.LongJumpDistance(11, 45) > Rules.LongJumpDistance(11, 80));
        Assert.True(Rules.LongJumpDistance(10.3, 45) >= 6.0, "a 10.3 m/s run-up should clear the round-1 standard");
    }
}

public class JavelinTests
{
    private static JavelinSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void AGoodThrowQualifies()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.True(sim.MarkFor(0) is > 50 and < 120, $"mark {sim.MarkFor(0)}");
        Assert.True(sim.DidQualify(0));
    }

    [Fact]
    public void TheThrowerStopsAsSoonAsHePressesThrow()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        double? releaseX = null;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var jv = (JavelinSimulation)s;
            if (jv.CurrentTry == 1 && jv.State is ThrowState.WindUp or ThrowState.Flight or ThrowState.Landed)
            {
                releaseX ??= jv.CurrentAthlete.X;
                Assert.Equal(releaseX.Value, jv.CurrentAthlete.X, 6);
                Assert.Equal(0, jv.CurrentAthlete.Runner.SpeedCm);
                Assert.NotEqual(Pose.Run, jv.CurrentAthlete.Pose);
            }
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.NotNull(releaseX);
    }

    [Fact]
    public void TheMarkIsMeasuredFromTheFoulLine()
    {
        var sim = New();
        var bot = new Bot(0, 11) { TakeoffMargin = 6 }; // plants 6 m before the line
        double? landedX = null, launchX = null;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var jv = (JavelinSimulation)s;
            if (jv.CurrentTry == 1 && jv.State == ThrowState.WindUp) launchX ??= jv.CurrentAthlete.X;
            if (jv.CurrentTry == 1 && jv.State == ThrowState.Landed) landedX ??= jv.JavelinX;
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.NotNull(landedX);
        Assert.True(launchX < sim.LineX - 5, $"planted at {launchX}");
        Assert.Equal(Rules.Snap(landedX!.Value - sim.LineX), sim.AttemptsFor(0)[0].Mark!.Value, 6);
    }

    [Fact]
    public void CrossingTheLineIsAFoul()
    {
        var sim = New();
        var bot = new Bot(0, 11);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var f = bot.NextForSimulation(s);
            return (f with { Action = false }, InputFrame.None);
        });
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }

    [Fact]
    public void HoldingRunDuringFlightAddsDistance()
    {
        double WithHold(bool hold)
        {
            var sim = New();
            var bot = new Bot(0, 11);
            SimulationRunner.RunUntilFinished(sim, s =>
            {
                var f = bot.NextForSimulation(s);
                var jv = (JavelinSimulation)s;
                if (jv.State == ThrowState.Flight && !hold) f = InputFrame.None;
                return (f, InputFrame.None);
            });
            return sim.AttemptsFor(0)[0].Mark!.Value;
        }
        double held = WithHold(true);
        double plain = WithHold(false);
        Assert.True(held > plain * 1.1, $"held {held} plain {plain}");
    }

    [Fact]
    public void ASteepPowerfulThrowHitsTheBird()
    {
        var sim = New();
        var bot = new Bot(0, 14);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var f = bot.NextForSimulation(s);
            var jv = (JavelinSimulation)s;
            if (jv.State == ThrowState.WindUp) f = f with { Action = jv.Angle < 85 };
            return (f, InputFrame.None);
        });
        Assert.True(sim.BonusPoints[0] >= Rules.BonusPoints, "bird bonus expected");
        Assert.Contains(AudioCue.SfxBird, SimulationRunner.DrainCues(sim));
    }
}

public class HammerTests
{
    private static HammerSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void SixSpinsAndAForwardReleaseQualify()
    {
        var sim = New();
        var bot = new Bot(0);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.True(sim.MarkFor(0) is >= 60 and <= 99.99, $"mark {sim.MarkFor(0)}");
        Assert.True(sim.DidQualify(0));
    }

    [Fact]
    public void TheThrowerStandsStillWhileTheHammerFlies()
    {
        var sim = New();
        var bot = new Bot(0);
        bool sawFlight = false;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hm = (HammerSimulation)s;
            if (hm.State is HammerState.Flight or HammerState.Landed)
            {
                sawFlight = true;
                Assert.Equal(0, hm.CurrentAthlete.Rotation);
                Assert.Equal(0, hm.CurrentAthlete.X);
                Assert.NotEqual(Pose.Spin, hm.CurrentAthlete.Pose);
            }
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.True(sawFlight);
    }

    [Fact]
    public void ReleasingBackwardsIsAFoul()
    {
        var sim = New();
        bool started = false;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hm = (HammerSimulation)s;
            if (hm.Phase != AttemptPhase.Active) { started = false; return (InputFrame.None, InputFrame.None); }
            if (hm.State == HammerState.Ready && !started) { started = true; return (new InputFrame(Run1: true), InputFrame.None); }
            if (hm.State == HammerState.Spinning && hm.SpinsCompleted >= 3)
            {
                bool hold = !hm.Holding || hm.InFrontHalf || hm.Angle < 30; // release only with the ball behind the thrower
                return (new InputFrame(Action: hold), InputFrame.None);
            }
            return (InputFrame.None, InputFrame.None);
        });
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }

    [Fact]
    public void TenSpinsMakeTheAthleteDizzy()
    {
        var sim = New();
        bool started = false;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hm = (HammerSimulation)s;
            if (hm.Phase != AttemptPhase.Active) { started = false; return (InputFrame.None, InputFrame.None); }
            if (hm.State == HammerState.Ready && !started) { started = true; return (new InputFrame(Run2: true), InputFrame.None); }
            return (InputFrame.None, InputFrame.None);
        });
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Equal(Rules.HammerMaxSpins, sim.SpinsCompleted);
    }

    [Fact]
    public void MoreSpinsThrowFarther()
    {
        Assert.True(Rules.HammerDistance(9, 45, 1) > Rules.HammerDistance(6, 45, 1));
        Assert.True(Rules.HammerDistance(6, 45, 1) * Rules.HammerDirectionFactor(30) >= 73, "six spins at 45 degrees released a bit off-centre should reach the round-1 standard");
        Assert.Equal(1, Rules.HammerDirectionFactor(0));
        Assert.True(Rules.HammerDirectionFactor(90) < Rules.HammerDirectionFactor(45));
    }

    [Fact]
    public void AnOffCentreReleaseInFrontIsLegalButShorter()
    {
        double Throw(bool centred)
        {
            var sim = New();
            bool started = false;
            SimulationRunner.RunUntilFinished(sim, s =>
            {
                var hm = (HammerSimulation)s;
                if (hm.Phase != AttemptPhase.Active) { started = false; return (InputFrame.None, InputFrame.None); }
                if (hm.State == HammerState.Ready && !started) { started = true; return (new InputFrame(Run1: true), InputFrame.None); }
                if (hm.State == HammerState.Spinning && hm.SpinsCompleted >= 6)
                {
                    double off = Math.Abs(HammerSimulation.WrapDeg(hm.HammerHeading));
                    if (!hm.Holding) return (new InputFrame(Action: hm.InPressZone), InputFrame.None);
                    bool releaseNow = centred ? off <= 12 : off is >= 60 and <= 88;
                    return (new InputFrame(Action: !releaseNow), InputFrame.None);
                }
                return (InputFrame.None, InputFrame.None);
            });
            return sim.AttemptsFor(0)[0].Mark ?? -1;
        }
        double good = Throw(true), sloppy = Throw(false);
        Assert.True(good > 0 && sloppy > 0, $"both legal: {good} / {sloppy}");
        Assert.True(good > sloppy, $"centred {good} vs sloppy {sloppy}");
    }

    [Fact]
    public void HalfATurnHeldGivesFortyFiveDegrees()
    {
        var sim = New();
        bool started = false;
        double angleAtRelease = -1;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hm = (HammerSimulation)s;
            if (hm.Phase != AttemptPhase.Active) { started = false; return (InputFrame.None, InputFrame.None); }
            if (hm.State == HammerState.Ready && !started) { started = true; return (new InputFrame(Run1: true), InputFrame.None); }
            if (hm.State == HammerState.Spinning && hm.SpinsCompleted >= 4)
            {
                double off = Math.Abs(HammerSimulation.WrapDeg(hm.HammerHeading));
                if (!hm.Holding) return (new InputFrame(Action: off >= 176), InputFrame.None);
                bool release = off <= 6;
                if (release && angleAtRelease < 0) angleAtRelease = hm.Angle;
                return (new InputFrame(Action: !release), InputFrame.None);
            }
            return (InputFrame.None, InputFrame.None);
        });
        Assert.InRange(angleAtRelease, 40, 50);
    }

    [Fact]
    public void HeadingWrapsAroundZero()
    {
        Assert.Equal(-10, HammerSimulation.WrapDeg(350));
        Assert.Equal(10, HammerSimulation.WrapDeg(370));
        Assert.Equal(180, Math.Abs(HammerSimulation.WrapDeg(180)));
    }
}

public class HighJumpTests
{
    private static HighJumpSimulation New(uint seed = 1) => new(1, [new Athlete(0, false, "AAA")], new Rng(seed));

    [Fact]
    public void AWellTimedJumpClearsTheBarAndRaisesIt()
    {
        var sim = New();
        var bot = new Bot(0);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        var tries = sim.AttemptsFor(0);
        Assert.True(tries[0].Mark.HasValue, "first jump should clear");
        Assert.Equal(2.30, tries[0].Mark!.Value);
        Assert.True(sim.BarHeightFor(0) > 2.30);
        Assert.True(sim.DidQualify(0));
    }

    [Fact]
    public void TakingOffTooEarlyMisses()
    {
        var sim = New();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hj = (HighJumpSimulation)s;
            if (hj.Phase == AttemptPhase.Active && hj.State == HighJumpState.Approach && hj.CurrentAthlete.X >= -4.0)
                return (new InputFrame(Action: true), InputFrame.None);
            return (InputFrame.None, InputFrame.None);
        });
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Contains(AudioCue.SfxBarFall, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void NeverJumpingRunsIntoTheBar()
    {
        var sim = New();
        SimulationRunner.RunUntilFinished(sim, _ => (InputFrame.None, InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }

    [Fact]
    public void TheMoleAppearsWhenYouClearOnlyTheThirdTry()
    {
        var sim = New();
        var bot = new Bot(0);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hj = (HighJumpSimulation)s;
            if (hj.CurrentTry < 3) return (InputFrame.None, InputFrame.None); // fail the first two
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.True(sim.AttemptsFor(0)[2].Mark.HasValue);
        Assert.Equal(Rules.BonusPoints, sim.BonusPoints[0]);
        Assert.Contains(AudioCue.SfxMole, SimulationRunner.DrainCues(sim));
    }
}
