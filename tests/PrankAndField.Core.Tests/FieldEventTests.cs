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

    /// <summary>Starts the spin on the first active tick and presses the action button once when <paramref name="releaseWhen"/> says so.</summary>
    private static Func<EventSimulation, (InputFrame, InputFrame)> SpinThen(Func<HammerSimulation, bool> releaseWhen)
    {
        bool started = false, released = false;
        return s =>
        {
            var hm = (HammerSimulation)s;
            if (hm.Phase != AttemptPhase.Active) { started = false; released = false; return (InputFrame.None, InputFrame.None); }
            if (hm.State == HammerState.Ready && !started) { started = true; return (new InputFrame(Run1: true), InputFrame.None); }
            if (hm.State == HammerState.Spinning && !released && releaseWhen(hm)) { released = true; return (new InputFrame(Action: true), InputFrame.None); }
            return (InputFrame.None, InputFrame.None);
        };
    }

    private static double Off(HammerSimulation hm) => Math.Abs(HammerSimulation.WrapDeg(hm.HammerHeading));

    [Fact]
    public void SixSpinsAndAForwardReleaseQualify()
    {
        var sim = New();
        var bot = new Bot(0);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.True(sim.MarkFor(0) is >= 73 and <= 99.99, $"mark {sim.MarkFor(0)}");
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
    public void ASinglePressReleasesTheHammerAtOnce()
    {
        var sim = New();
        int pressTick = -1, flightTick = -1, tick = 0;
        var script = SpinThen(hm => hm.SpinsCompleted >= 4 && Off(hm) <= 10);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            tick++;
            if (((HammerSimulation)s).State == HammerState.Flight && flightTick < 0) flightTick = tick;
            var inputs = script(s);
            if (inputs.Item1.Action && pressTick < 0) pressTick = tick;
            return inputs;
        });
        Assert.True(pressTick > 0, "the script pressed");
        Assert.Equal(pressTick + 1, flightTick); // in flight right after the press: nothing to hold or release
        Assert.True(sim.AttemptsFor(0)[0].Mark > 60, $"mark {sim.AttemptsFor(0)[0].Mark}");
    }

    [Fact]
    public void ReleasingBackwardsIsAFoul()
    {
        var sim = New();
        bool sawBanner = false;
        var script = SpinThen(hm => hm.SpinsCompleted >= 3 && !hm.InFrontHalf);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            if (s.Banner == BannerKind.WrongDirection) sawBanner = true;
            return script(s);
        });
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.True(sawBanner);
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
        Assert.Equal(0, sim.CurrentAthlete.Rotation);
    }

    [Fact]
    public void MoreTurnsThrowFarther()
    {
        Assert.True(Rules.HammerDistance(9.5, 1) > Rules.HammerDistance(6.5, 1));
        Assert.True(Rules.HammerDistance(6.5, 0.97) * Rules.HammerDirectionFactor(30) >= 73, "six turns released a bit off-centre should reach the round-1 standard even with bad luck");
        Assert.True(Rules.HammerDistance(4.5, 1.03) < 73, "four turns are not enough even with good luck");
        Assert.True(Rules.HammerDistance(7.5, 0.97) >= 85, "seven turns straight ahead reach the round-4 standard");
        Assert.Equal(1, Rules.HammerDirectionFactor(0));
        Assert.True(Rules.HammerDirectionFactor(90) < Rules.HammerDirectionFactor(45));
    }

    [Fact]
    public void TheSpinAcceleratesSmoothly()
    {
        var sim = New();
        bool started = false;
        var steps = new List<double>();
        double last = double.NaN;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hm = (HammerSimulation)s;
            if (hm.CurrentTry == 1 && hm.Phase == AttemptPhase.Active && hm.State == HammerState.Spinning)
            {
                if (!double.IsNaN(last)) steps.Add(hm.HammerHeading - last);
                last = hm.HammerHeading;
            }
            else last = double.NaN;
            if (hm.Phase != AttemptPhase.Active) { started = false; return (InputFrame.None, InputFrame.None); }
            if (hm.State == HammerState.Ready && !started) { started = true; return (new InputFrame(Run1: true), InputFrame.None); }
            return (InputFrame.None, InputFrame.None);
        });
        Assert.True(steps.Count > 300, "the spin lasts several seconds");
        for (int i = 1; i < steps.Count; i++)
        {
            Assert.True(steps[i] >= steps[i - 1] - 1e-9, $"the spin never slows down (tick {i}: {steps[i - 1]} -> {steps[i]})");
            Assert.True(steps[i] - steps[i - 1] < 0.2, $"no jump in speed at a revolution (tick {i}: {steps[i - 1]} -> {steps[i]})");
        }
        Assert.True(steps[0] < steps[^1] / 2, "it starts slowly and ends up much faster");
        // one tick at 60 Hz, 0.6 rev/s is 3.6 degrees; the dizzy limit is reached at 2.1 rev/s
        Assert.InRange(steps[^1], 11, 13);
        Assert.InRange(Rules.HammerSpinSpeed(0), 0.25, 0.4);
        Assert.Equal(1.5, Rules.HammerSpinSpeed(6), 6);
    }

    [Fact]
    public void AnOffCentreReleaseInFrontIsLegalButShorter()
    {
        double Throw(bool centred)
        {
            var sim = New();
            SimulationRunner.RunUntilFinished(sim, SpinThen(hm => hm.SpinsCompleted >= 6 && (centred ? Off(hm) <= 12 : Off(hm) is >= 60 and <= 88)));
            return sim.AttemptsFor(0)[0].Mark ?? -1;
        }
        double good = Throw(true), sloppy = Throw(false);
        Assert.True(good > 0 && sloppy > 0, $"both legal: {good} / {sloppy}");
        Assert.True(good > sloppy, $"centred {good} vs sloppy {sloppy}");
    }

    [Fact]
    public void TheBallGlowsRedOnlyInTheReleaseArcAndTheReadoutGrows()
    {
        var sim = New();
        double lastReadout = -1;
        bool sawRed = false, sawDark = false;
        var script = SpinThen(hm => hm.SpinsCompleted >= 2 && Off(hm) <= 10);
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hm = (HammerSimulation)s;
            if (hm.Phase == AttemptPhase.Active && hm.State == HammerState.Spinning)
            {
                Assert.Equal(Off(hm) <= Rules.HammerReleaseArc, hm.InReleaseArc);
                Assert.Equal(Off(hm) <= Rules.HammerFoulArc, hm.InFrontHalf);
                if (hm.InReleaseArc) sawRed = true; else sawDark = true;
                Assert.True(hm.SpeedReadout(0) >= lastReadout, "the SPEED bar never drops while spinning");
                lastReadout = hm.SpeedReadout(0);
            }
            else lastReadout = -1;
            return script(s);
        });
        Assert.True(sawRed && sawDark);
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
