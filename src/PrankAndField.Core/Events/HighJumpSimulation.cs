namespace PrankAndField.Core.Events;

public enum HighJumpState
{
    Approach,   // run-up driven by the RUN buttons
    Rising,     // button held at the take-off spot: the angle counts up from 30
    Airborne,   // flopping over (or into) the bar
    Landed,     // on the mat with the bar still up
    Missed,     // bar down, fell short or ran into the uprights
}

/// <summary>
/// High jump. The athlete runs up with the RUN buttons, presses JUMP inside the zone painted before the
/// bar and holds it to set the angle; the lift comes from the run-up speed, the angle (about 75 degrees
/// is best) and how close to the white line he took off. Inside the zone the apex is always over the bar,
/// so the timing decides *how much* height is lost, never where the arc ends. The bar rises
/// <see cref="Rules.HighJumpBarStep"/> after every valid jump and stays after a miss.
/// </summary>
public sealed class HighJumpSimulation : FieldEventSimulation
{
    private readonly double[] _bar = new double[2];
    private readonly int[] _misses = new int[2];
    private double _takeoffX;
    private double _runSpeed;
    private double _rise;
    private double _travel;
    private double _flightTime;
    private double _flightDuration;
    private bool _crossedBar;

    public HighJumpSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.HighJump, round, athletes, rng)
    {
        for (int i = 0; i < 2; i++) _bar[i] = QualifyingMark;
    }

    protected override double StartX => Rules.HighJumpApproachStart;

    public HighJumpState State { get; private set; }

    /// <summary>Height of the bar for the athlete jumping now (each player has his own bar).</summary>
    public double BarHeight => _bar[CurrentAthlete.PlayerIndex];
    public double BarHeightFor(int playerIndex) => _bar[playerIndex];

    /// <summary>Misses so far in this event, per player.</summary>
    public int MissesFor(int playerIndex) => _misses[playerIndex];

    public bool BarFallen { get; private set; }

    /// <summary>0 while the bar is up, growing to 1 as it tumbles onto the mat.</summary>
    public double BarFallAnim { get; private set; }

    /// <summary>Where the jumper planted his foot (metres from the bar plane); valid from <see cref="HighJumpState.Rising"/> on.</summary>
    public double TakeoffX => _takeoffX;

    /// <summary>Where the arc ends; valid once airborne.</summary>
    public double LandingX => _takeoffX + _travel;

    /// <summary>Fraction of the flight done (0..1) while airborne, 1 afterwards.</summary>
    public double FlightProgress => State == HighJumpState.Airborne && _flightDuration > 0 ? Math.Min(1, _flightTime / _flightDuration) : (State is HighJumpState.Landed or HighJumpState.Missed ? 1 : 0);

    /// <summary>Highest point the body centre reaches in the current jump.</summary>
    public double PeakHeight => Rules.HighJumpHipHeight + _rise;

    /// <summary>True when the jump ends on the mat (past the bar plane) rather than short of it.</summary>
    public bool OnMat => State switch
    {
        HighJumpState.Airborne or HighJumpState.Landed => LandingX > 0,
        HighJumpState.Missed => LandingX > 0 && _travel > 0 && CurrentAthlete.X > 0,
        _ => false,
    };

    public override double? FlightHeight => State == HighJumpState.Airborne ? Math.Clamp(CurrentAthlete.Y / Math.Max(1.0, _rise), 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = HighJumpState.Approach;
        ShowAngle = false;
        Angle = Rules.HighJumpAngleStart;
        BarFallen = false;
        BarFallAnim = 0;
        _crossedBar = false;
        _travel = 0;
        _rise = 0;
        _flightTime = 0;
        _flightDuration = 0;
        _takeoffX = Rules.HighJumpApproachStart;
        a.Pose = Pose.Idle;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case HighJumpState.Approach:
                // The press is honoured where the jumper was when the button went down (before this
                // tick's stride), so the take-off spot is the one the player saw.
                if (e.ActionPressed && a.Runner.SpeedCm > 0)
                {
                    Plant(a);
                    break;
                }
                RunStep(a, e, dt);
                if (a.X > Rules.HighJumpCrashX) Crash(a); // ran into the uprights
                break;

            case HighJumpState.Rising:
                Angle = Math.Min(90, Angle + Rules.HighJumpAngleRate * dt);
                if (!e.ActionHeld || Angle >= 90) Launch(a);
                break;

            case HighJumpState.Airborne:
                Fly(a, dt);
                break;

            case HighJumpState.Landed:
            case HighJumpState.Missed:
                AdvanceBarFall(dt);
                break;
        }
    }

    protected override void TickAfterAttempt(double dt, Athlete a) => AdvanceBarFall(dt);

    private void AdvanceBarFall(double dt)
    {
        if (BarFallen) BarFallAnim = Math.Min(1, BarFallAnim + dt * 2);
    }

    /// <summary>JUMP pressed: the foot is planted here and the angle starts counting while the button is held.</summary>
    private void Plant(Athlete a)
    {
        State = HighJumpState.Rising;
        ShowAngle = true;
        Angle = Rules.HighJumpAngleStart;
        _takeoffX = a.X;
        _runSpeed = a.Runner.SpeedMetersPerSecond;
        a.Runner.Locked = true; // the SPEED bar keeps the take-off speed
        a.Pose = Pose.Jump;
        Cue(AudioCue.SfxJump);
    }

    /// <summary>Button released (or the meter maxed out): the jump itself.</summary>
    private void Launch(Athlete a)
    {
        State = HighJumpState.Airborne;
        _rise = Rules.HighJumpRise(_runSpeed, Angle, _takeoffX);
        // Inside the zone the apex sits over the bar. Before it the arc keeps the length of a jump from the
        // far edge, so an early jumper comes down onto the bar or lands short of it; past the zone the arc
        // keeps a minimum length, so a late jumper is still rising when he meets the bar.
        _travel = Math.Max(0.8, -2 * Math.Max(_takeoffX, Rules.HighJumpZoneFar));
        double vy = Math.Sqrt(2 * Ballistics.G * _rise);
        _flightDuration = Math.Max(Rules.HighJumpMinFlight, 2 * vy / Ballistics.G);
        _flightTime = 0;
        _crossedBar = false;
        a.AnimPhase = 0;
    }

    private void Fly(Athlete a, double dt)
    {
        _flightTime += dt;
        double p = Math.Min(1, _flightTime / _flightDuration);
        a.X = _takeoffX + _travel * p;
        a.Y = Math.Max(0, 4 * _rise * p * (1 - p));
        a.AnimPhase = p; // the flop pose arches first and kicks the legs up later
        a.Pose = p < 0.1 ? Pose.Jump : Pose.Flop;
        a.Rotation = p < 0.1 ? 0 : (p < 0.5 ? 90 * (p - 0.1) / 0.4 : 90 + 70 * (p - 0.5) / 0.5);

        if (!_crossedBar && a.X >= 0)
        {
            _crossedBar = true;
            double pc = Math.Clamp(-_takeoffX / _travel, 0, 1);
            double bodyCentre = Rules.HighJumpHipHeight + 4 * _rise * pc * (1 - pc);
            if (!Rules.HighJumpClears(bodyCentre, BarHeight)) KnockBar();
        }
        AdvanceBarFall(dt);

        if (p >= 1)
        {
            a.Y = 0;
            a.Rotation = 0;
            if (BarFallen) Miss(a);
            else if (!_crossedBar) Miss(a); // came down short of the bar
            else Clear(a);
        }
    }

    private void KnockBar()
    {
        BarFallen = true;
        Cue(AudioCue.SfxBarFall);
    }

    private void Clear(Athlete a)
    {
        State = HighJumpState.Landed;
        a.Pose = Pose.Celebrate;
        int p = a.PlayerIndex;
        double cleared = _bar[p];
        _bar[p] = Rules.Snap(_bar[p] + Rules.HighJumpBarStep);
        Cue(AudioCue.SfxLand);
        if (_misses[p] == 2 && CurrentTry == 3)
            AwardBonus(p, Rules.BonusPoints, "mole", -3.0, 0);
        CompleteAttempt(Attempt.Of(cleared), 2.4);
    }

    private void Miss(Athlete a)
    {
        State = HighJumpState.Missed;
        a.Pose = Pose.Fall;
        a.Runner.SpeedCm = 0;
        Cue(AudioCue.SfxLand);
        CompleteAttempt(Attempt.Fouled, 2.2);
    }

    /// <summary>Too close to the bar, jumping or not: the uprights stop him and the bar comes down.</summary>
    private void Crash(Athlete a)
    {
        State = HighJumpState.Missed;
        a.X = Math.Min(a.X, Rules.HighJumpCrashX);
        a.Runner.SpeedCm = 0;
        a.Pose = Pose.Fall;
        KnockBar();
        Cue(AudioCue.SfxHurdleHit);
        CompleteAttempt(Attempt.Fouled, 2.2);
    }

    protected override void OnAttemptCompleted(Athlete a, Attempt result)
    {
        a.Runner.Locked = false;
        // Every failed try counts, including one that timed out without a jump: two of them and a
        // clearance on the third summon the mole.
        if (result.Foul) _misses[a.PlayerIndex]++;
    }
}
