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
    private static HighJumpSimulation New(uint seed = 1, int round = 1) => new(round, [new Athlete(0, false, "AAA")], new Rng(seed));

    /// <summary>Runs up at the bot's rhythm and presses JUMP once the athlete passes <paramref name="takeoffX"/>, holding to <paramref name="angle"/>.</summary>
    private static Bot Jumper(double tapsPerSecond = 9, double takeoffX = Rules.HighJumpZoneBest, double angle = 75) =>
        new(0, tapsPerSecond) { HighJumpTakeoffX = takeoffX, HighJumpAngle = angle };

    [Fact]
    public void AGoodJumpClearsTheBarAndRaisesItForTheNextTry()
    {
        var sim = New();
        var bot = Jumper();
        var bars = new List<double>();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            if (bars.Count == 0 || bars[^1] != sim.BarHeightFor(0)) bars.Add(sim.BarHeightFor(0));
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.True(sim.IsFinished);
        var tries = sim.AttemptsFor(0);
        Assert.Equal(2.30, tries[0].Mark);
        Assert.Equal(2.34, tries[1].Mark);
        Assert.Equal(2.38, tries[2].Mark);
        Assert.Equal([2.30, 2.34, 2.38, 2.42], bars);
        Assert.Equal(2.38, sim.MarkFor(0));
        Assert.True(sim.DidQualify(0));
        Assert.Equal(0, sim.MissesFor(0));
        var cues = SimulationRunner.DrainCues(sim);
        Assert.DoesNotContain(AudioCue.SfxBarFall, cues);
        Assert.Contains(AudioCue.VoiceMark, cues);
    }

    [Theory]
    [InlineData(-1.8)]
    [InlineData(-1.15)]
    [InlineData(-0.5)]
    public void AnywhereInsideTheZoneIsGoodEnoughForTheOpeningHeight(double takeoffX)
    {
        var sim = New();
        var bot = Jumper(takeoffX: takeoffX);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.AttemptsFor(0)[0].Mark.HasValue, $"take-off at {takeoffX} should clear 2.30");
    }

    [Fact]
    public void TheApexSitsOverTheBarWhereverHeTakesOffInsideTheZone()
    {
        foreach (double takeoffX in new[] { -1.8, -1.2, -0.6 })
        {
            var sim = New();
            var bot = Jumper(takeoffX: takeoffX);
            double? landing = null, takeoff = null;
            SimulationRunner.RunUntilFinished(sim, s =>
            {
                var hj = (HighJumpSimulation)s;
                if (hj.CurrentTry == 1 && hj.State == HighJumpState.Airborne) { landing = hj.LandingX; takeoff = hj.TakeoffX; }
                return (hj.CurrentTry == 1 ? bot.NextForSimulation(s) : InputFrame.None, InputFrame.None);
            });
            Assert.NotNull(landing);
            Assert.Equal(-takeoff!.Value, landing!.Value, 2); // symmetric arc: apex at x = 0
            Assert.True(landing > 0 && landing < 2.2, "lands on the mat");
        }
    }

    [Fact]
    public void TakingOffWellBeforeTheZoneBringsTheBarDown()
    {
        var sim = New();
        var bot = Jumper(takeoffX: -2.5);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Equal(2.30, sim.BarHeightFor(0)); // a miss never raises the bar
        Assert.Equal(3, sim.MissesFor(0));
        Assert.Contains(AudioCue.SfxBarFall, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void TakingOffFarTooEarlyLandsShortOfTheBar()
    {
        var sim = New();
        var bot = Jumper(takeoffX: -5.0);
        bool crossed = false;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            crossed |= sim.CurrentAthlete.X >= 0;
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.False(crossed, "never reached the bar plane");
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.DoesNotContain(AudioCue.SfxBarFall, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void TakingOffJustPastTheZoneStillJumpsButMeetsTheBarOnTheWayUp()
    {
        var sim = New();
        var bot = Jumper(takeoffX: -0.25);
        bool flew = false;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            flew |= ((HighJumpSimulation)s).State == HighJumpState.Airborne;
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        Assert.True(flew, "a late press is still a jump, not a crash");
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Contains(AudioCue.SfxBarFall, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void RunningThroughWithoutJumpingKnocksTheBarOff()
    {
        var sim = New();
        var runner = Jumper(takeoffX: 10); // never reaches its take-off spot: just keeps running
        SimulationRunner.RunUntilFinished(sim, s => (runner.NextForSimulation(s), InputFrame.None));
        Assert.True(sim.IsFinished);
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.True(sim.CurrentAthlete.X <= Rules.HighJumpCrashX + 1e-9);
        Assert.Equal(HighJumpState.Missed, sim.State);
        Assert.Contains(AudioCue.SfxBarFall, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void AJogIsTooSlowToClearTheOpeningHeight()
    {
        var sim = New();
        var bot = Jumper(tapsPerSecond: 5);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
        Assert.Null(sim.MarkFor(0));
    }

    [Theory]
    [InlineData(45)]  // released too soon: flat and short on lift
    [InlineData(90)]  // held to the end: straight up, the run-up is wasted
    public void AFlatOrVerticalTakeoffMisses(double angle)
    {
        var sim = New();
        var bot = Jumper(angle: angle);
        SimulationRunner.RunUntilFinished(sim, s => (bot.NextForSimulation(s), InputFrame.None));
        Assert.All(sim.AttemptsFor(0), t => Assert.True(t.Foul));
    }

    [Fact]
    public void AFasterRunUpJumpsHigher()
    {
        double PeakAt(double taps)
        {
            var sim = New();
            var bot = Jumper(tapsPerSecond: taps);
            double peak = 0;
            SimulationRunner.RunUntilFinished(sim, s =>
            {
                var hj = (HighJumpSimulation)s;
                if (hj.CurrentTry == 1 && hj.State == HighJumpState.Airborne) peak = hj.PeakHeight;
                return (hj.CurrentTry == 1 ? bot.NextForSimulation(s) : InputFrame.None, InputFrame.None);
            });
            return peak;
        }
        double slow = PeakAt(7), fast = PeakAt(11);
        Assert.True(fast > slow + 0.1, $"{fast} vs {slow}");
    }

    [Fact]
    public void TheMoleAppearsWhenYouClearOnlyTheThirdTry()
    {
        var sim = New();
        var early = Jumper(takeoffX: -2.6); // knocks the bar
        var good = Jumper();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hj = (HighJumpSimulation)s;
            var bot = hj.CurrentTry < 3 ? early : good;
            return (bot.NextForSimulation(s), InputFrame.None);
        });
        var tries = sim.AttemptsFor(0);
        Assert.True(tries[0].Foul && tries[1].Foul);
        Assert.Equal(2.30, tries[2].Mark);
        Assert.Equal(Rules.BonusPoints, sim.BonusPoints[0]);
        Assert.Contains(AudioCue.SfxMole, SimulationRunner.DrainCues(sim));
    }

    [Fact]
    public void AnIdleAttemptTimesOutAndCountsAsAMiss()
    {
        var sim = New();
        var good = Jumper();
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hj = (HighJumpSimulation)s;
            return (hj.CurrentTry < 3 ? InputFrame.None : good.NextForSimulation(s), InputFrame.None);
        }, maxSeconds: 120);
        Assert.True(sim.IsFinished);
        Assert.Equal(2, sim.MissesFor(0));
        Assert.Equal(2.30, sim.AttemptsFor(0)[2].Mark);
        Assert.Equal(Rules.BonusPoints, sim.BonusPoints[0]);
    }

    [Fact]
    public void EachPlayerHasHisOwnBar()
    {
        var sim = new HighJumpSimulation(1, [new Athlete(0, false, "AAA"), new Athlete(1, false, "BBB")], new Rng(3));
        var p1 = Jumper();
        var p2 = new Bot(1, 9) { HighJumpTakeoffX = -2.6 }; // player 2 keeps missing
        double? p2BarDuringP1Second = null;
        SimulationRunner.RunUntilFinished(sim, s =>
        {
            var hj = (HighJumpSimulation)s;
            if (hj.CurrentAthlete.PlayerIndex == 0 && hj.CurrentTry == 2) p2BarDuringP1Second = hj.BarHeightFor(1);
            return (p1.NextForSimulation(s), p2.NextForSimulation(s));
        }, maxSeconds: 240);
        Assert.True(sim.IsFinished);
        Assert.Equal(2.38, sim.MarkFor(0));
        Assert.Null(sim.MarkFor(1));
        Assert.Equal(2.30, p2BarDuringP1Second);
        Assert.Equal(2.42, sim.BarHeightFor(0));
        Assert.Equal(2.30, sim.BarHeightFor(1));
    }

    [Fact]
    public void TheBarStartsAtTheQualifyingHeightOfTheRound()
    {
        Assert.Equal(2.30, New(round: 1).BarHeight);
        Assert.Equal(2.34, New(round: 2).BarHeight);
        Assert.Equal(2.40, New(round: 4).BarHeight);
    }

    [Fact]
    public void TheTakeoffFactorPeaksOnTheLineAndCollapsesBeforeTheZone()
    {
        Assert.Equal(1.0, Rules.HighJumpTakeoffFactor(Rules.HighJumpZoneBest), 6);
        Assert.Equal(1 - Rules.HighJumpZonePenalty, Rules.HighJumpTakeoffFactor(Rules.HighJumpZoneFar), 6);
        Assert.Equal(1 - Rules.HighJumpZonePenalty, Rules.HighJumpTakeoffFactor(Rules.HighJumpZoneNear), 6);
        Assert.True(Rules.HighJumpTakeoffFactor(Rules.HighJumpZoneFar - 0.3) < 0.8);
        Assert.True(Rules.HighJumpTakeoffFactor(Rules.HighJumpZoneFar - 1.0) < 0.1);
        // monotonic on either side of the line
        for (double x = Rules.HighJumpZoneBest; x > -4; x -= 0.1)
            Assert.True(Rules.HighJumpTakeoffFactor(x - 0.1) <= Rules.HighJumpTakeoffFactor(x) + 1e-9, $"x={x}");
    }

    [Fact]
    public void TheBestAngleIsAroundSeventyFiveDegrees()
    {
        double best = 0, bestLift = 0;
        for (double a = 30; a <= 90; a += 1)
        {
            double lift = Rules.HighJumpVerticalSpeed(9, a);
            if (lift > bestLift) { bestLift = lift; best = a; }
        }
        Assert.InRange(best, 70, 80);
        // a mid-paced run at the best angle clears the opening heights with something to spare, but not forever
        double peak = Rules.HighJumpPeak(8.3, best, Rules.HighJumpZoneBest);
        Assert.True(Rules.HighJumpClears(peak, 2.30));
        Assert.True(Rules.HighJumpClears(peak, 2.34));
        Assert.False(Rules.HighJumpClears(peak, 2.50));
    }
}
