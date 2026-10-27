using PrankAndField.Core.Events;

namespace PrankAndField.Core.Tests.Support;

/// <summary>
/// A scripted player used by the tests: it taps the run buttons at a given rhythm and plays every event
/// with a sensible technique, so whole games can be simulated deterministically.
/// </summary>
public sealed class Bot
{
    private readonly int _player;
    private readonly double _tapsPerSecond;
    private double _tapClock;
    private int _nextButton = 1;
    private double _holdUntil = -1;
    private bool _pressedRun;
    private EventSimulation? _lastSim;
    private bool _pulse;

    public Bot(int player, double tapsPerSecond = 11)
    {
        _player = player;
        _tapsPerSecond = tapsPerSecond;
    }

    /// <summary>When true the bot never runs (used to fail events on purpose).</summary>
    public bool Lazy { get; set; }

    /// <summary>Metres before the board / line at which the bot takes off or throws.</summary>
    public double TakeoffMargin { get; set; } = 0.6;

    /// <summary>Taps per second in the air during a vault (each alternated tap is half a twist).</summary>
    public double TwistTaps { get; set; } = 8;

    /// <summary>Take-off angle held for every phase of the triple jump.</summary>
    public double TripleJumpAngle { get; set; } = 40;

    /// <summary>When set, the archer aims this many metres off the centre on purpose (to test misses).</summary>
    public double AimOffset { get; set; }

    /// <summary>
    /// Picks the bow angle that puts the arrow on the predicted centre of the target: the hold time, the
    /// flight time, the drop and the wind are all known, so the bot scans the angles for the best one.
    /// </summary>
    private double AimAngle(ArcherySimulation ar)
    {
        double best = 0, bestErr = double.MaxValue;
        for (double deg = 0; deg <= Rules.ArcheryMaxAngle; deg += 0.25)
        {
            double rad = deg * Math.PI / 180.0;
            double flight = ar.TargetX / (Rules.ArrowSpeed * Math.Cos(rad));
            double hold = deg / Rules.ArcheryAngleRate;
            double y = Rules.ArcheryBowHeight + ar.TargetX * Math.Tan(rad) - Ballistics.G * ar.TargetX * ar.TargetX / (2 * Rules.ArrowSpeed * Rules.ArrowSpeed * Math.Cos(rad) * Math.Cos(rad))
                       + ar.Wind * Rules.ArcheryWindLift;
            double err = Math.Abs(y - (ar.TargetYAt(hold + flight) + AimOffset));
            if (err < bestErr) { bestErr = err; best = deg; }
        }
        return best;
    }

    /// <summary>Input for the whole game: presses Start on menus, plays the events.</summary>
    public InputFrame Next(Game game)
    {
        if (game.State != GameState.Event || game.Simulation is null)
        {
            _holdUntil = -1;
            _pressedRun = false;
            return game.State switch
            {
                GameState.Title => Pulse(new InputFrame(Start: _player == 0)),
                GameState.NameEntry or GameState.RecordEntry => Pulse(new InputFrame(Start: true)),
                _ => InputFrame.None,
            };
        }
        return NextForSimulation(game.Simulation);
    }

    /// <summary>Input for a standalone simulation.</summary>
    public InputFrame NextForSimulation(EventSimulation sim)
    {
        double dt = Game.TickSeconds;
        if (!ReferenceEquals(sim, _lastSim)) { _lastSim = sim; _holdUntil = -1; _pressedRun = false; }
        if (Lazy) return InputFrame.None;

        bool run = false;
        bool action = false;
        var a = sim.AthleteFor(_player);
        switch (sim)
        {
            case SwimmingSimulation sw:
                if (sw.Phase == RacePhase.Running && a is not null && !a.Finished)
                {
                    run = Tap(dt);
                    action = sw.BreathPrompt(_player); // breathe as soon as the prompt shows
                }
                break;
            case HurdlesSimulation h:
                if (h.Phase == RacePhase.Running && a is not null && !a.Finished)
                {
                    run = Tap(dt);
                    var next = h.HurdlesFor(_player).FirstOrDefault(x => x.X > a.X - 0.2);
                    if (next is not null && !h.IsAirborne(_player))
                    {
                        double gap = next.X - a.X;
                        double v = a.Runner.SpeedMetersPerSecond;
                        if (gap > 0 && gap < v * Rules.HurdleAirTime * 0.5) action = true;
                    }
                }
                break;
            case RaceSimulation r:
                if (r.Phase == RacePhase.Running && a is not null && !a.Finished) run = Tap(dt);
                break;
            case LongJumpSimulation lj when lj.CurrentAthlete.PlayerIndex == _player && lj.Phase == AttemptPhase.Active:
                if (lj.State == JumpState.Approach)
                {
                    if (a!.X >= lj.BoardX - TakeoffMargin) { action = true; _holdUntil = sim.Elapsed + 0.40; }
                    else run = Tap(dt);
                }
                else if (lj.State == JumpState.Rising) action = sim.Elapsed < _holdUntil;
                break;
            case JavelinSimulation jv when jv.CurrentAthlete.PlayerIndex == _player && jv.Phase == AttemptPhase.Active:
                if (jv.State == ThrowState.Approach)
                {
                    if (a!.X >= jv.LineX - TakeoffMargin) action = true;
                    else run = Tap(dt);
                }
                else if (jv.State == ThrowState.WindUp) action = jv.Angle < 42;
                else if (jv.State == ThrowState.Flight) run = true; // hold RUN for the extra distance
                break;
            case HammerSimulation hm when hm.CurrentAthlete.PlayerIndex == _player && hm.Phase == AttemptPhase.Active:
                if (hm.State == HammerState.Ready)
                {
                    if (!_pressedRun) { _pressedRun = true; run = true; }
                }
                else if (hm.State == HammerState.Spinning)
                {
                    _pressedRun = false;
                    if (hm.SpinsCompleted >= 6)
                    {
                        // press as the ball passes behind, release when it glows red in front (~45 degrees)
                        if (!hm.Holding) action = hm.InPressZone;
                        else action = !(hm.InReleaseArc && hm.Angle >= 30);
                    }
                }
                break;
            case HighJumpSimulation hj when hj.CurrentAthlete.PlayerIndex == _player && hj.Phase == AttemptPhase.Active:
                if (hj.State == HighJumpState.Approach)
                {
                    if (a!.X >= -0.9) { action = true; _holdUntil = sim.Elapsed + 0.2; }
                }
                else if (hj.State == HighJumpState.Rising) action = sim.Elapsed < _holdUntil;
                else if (hj.State == HighJumpState.Airborne) run = Tap(dt, 14);
                break;
            case SkeetSimulation sk when sk.CurrentAthlete.PlayerIndex == _player && sk.Phase == TurnPhase.Active:
                {
                    // fire the gun whose sight has a clay in it, one press every other tick
                    _pulse = !_pulse;
                    bool left = sk.TargetInSight(0) && _pulse;
                    bool right = sk.TargetInSight(1) && _pulse;
                    return new InputFrame(Run1: left, Run2: right);
                }
            case VaultSimulation v when v.CurrentAthlete.PlayerIndex == _player && v.Phase == AttemptPhase.Active:
                if (v.State == VaultState.Approach)
                {
                    if (a!.X >= v.BoardX - Rules.VaultBoardSweetSpot - 0.05) action = true;
                    else run = Tap(dt);
                }
                else if (v.State == VaultState.OnHorse) action = v.HorseTime >= Rules.VaultPushBest - 0.02;
                else if (v.State == VaultState.Flight)
                {
                    run = Tap(dt, TwistTaps);
                    action = v.StickWindowOpen;
                }
                break;
            case ArcherySimulation ar when ar.CurrentAthlete.PlayerIndex == _player && ar.Phase == TurnPhase.Active:
                if (ar.State == ArcheryState.Aim)
                {
                    if (_holdUntil < 0) _holdUntil = AimAngle(ar);
                    action = ar.Angle < _holdUntil - 0.6;
                }
                else _holdUntil = -1;
                break;
            case TripleJumpSimulation tj when tj.CurrentAthlete.PlayerIndex == _player && tj.Phase == AttemptPhase.Active:
                if (tj.State == TripleJumpState.Approach)
                {
                    if (a!.X >= tj.BoardX - TakeoffMargin) action = true;
                    else run = Tap(dt);
                }
                else if (tj.State == TripleJumpState.Rising) action = tj.Angle < TripleJumpAngle;
                else if (tj.State == TripleJumpState.Contact) action = true;
                break;
            case WeightliftingSimulation wl when wl.CurrentAthlete.PlayerIndex == _player && wl.Phase == AttemptPhase.Active:
                if (wl.State is LiftState.Ready or LiftState.Clean) run = Tap(dt);
                else if (wl.State == LiftState.Jerk) action = wl.JerkPrompt;
                else if (wl.State == LiftState.Hold)
                {
                    _pulse = !_pulse;
                    if (wl.Balance > 0.25) return new InputFrame(Run1: _pulse);
                    if (wl.Balance < -0.25) return new InputFrame(Run2: _pulse);
                }
                break;
        }

        if (!run) return new InputFrame(Action: action);
        return _nextButton == 1 ? new InputFrame(Run1: true, Action: action) : new InputFrame(Run2: true, Action: action);
    }

    private InputFrame Pulse(InputFrame f)
    {
        _pulse = !_pulse;
        return _pulse ? f : InputFrame.None;
    }

    /// <summary>Returns true on the ticks where a button should be held (one tick per tap, alternating buttons).</summary>
    private bool Tap(double dt, double? rate = null)
    {
        double r = rate ?? _tapsPerSecond;
        _tapClock += dt;
        if (_tapClock >= 1.0 / r)
        {
            _tapClock -= 1.0 / r;
            _nextButton = _nextButton == 1 ? 2 : 1;
            return true;
        }
        return false;
    }
}

public static class SimulationRunner
{
    /// <summary>Ticks a simulation until it finishes or the time budget is exhausted. Returns elapsed seconds.</summary>
    public static double RunUntilFinished(EventSimulation sim, Func<EventSimulation, (InputFrame P1, InputFrame P2)> inputs, double maxSeconds = 120)
    {
        var e1 = new ButtonEdges();
        var e2 = new ButtonEdges();
        double t = 0;
        while (!sim.IsFinished && t < maxSeconds)
        {
            var (p1, p2) = inputs(sim);
            e1.Update(p1);
            e2.Update(p2);
            sim.Tick(Game.TickSeconds, e1, e2);
            t += Game.TickSeconds;
        }
        return t;
    }

    public static double RunUntil(Game game, Func<Game, bool> stop, Func<Game, (InputFrame P1, InputFrame P2)> inputs, double maxSeconds = 600)
    {
        double t = 0;
        while (!stop(game) && t < maxSeconds)
        {
            var (p1, p2) = inputs(game);
            game.Tick(p1, p2);
            t += Game.TickSeconds;
        }
        return t;
    }

    public static List<AudioCue> DrainCues(EventSimulation sim)
    {
        var list = new List<AudioCue>();
        while (sim.TryDequeueCue(out var c)) list.Add(c);
        return list;
    }
}
