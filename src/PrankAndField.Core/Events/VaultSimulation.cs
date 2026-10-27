namespace PrankAndField.Core.Events;

public enum VaultState
{
    Approach,
    PreFlight,   // from the springboard to the hands on the horse
    OnHorse,     // handstand on the horse: press to push off
    Flight,      // tap RUN to twist, press at the end to stick the landing
    Landed,
    Fouled,
}

/// <summary>
/// Long horse (vault). Run up, press at the springboard, press again on the horse to push off, alternate
/// RUN in the air for twists and press just before landing to stick it. Judges score out of 10.
/// </summary>
public sealed class VaultSimulation : FieldEventSimulation
{
    private double _takeoffX;
    private double _time;
    private double _flightDuration;
    private double _flightHeight;
    private double _landingDistance;
    private double _spin;        // twists shown, chasing the target
    private double _spinTarget;  // twists counted (half per alternated press)
    private int _lastButton;
    private bool _stickTried;
    private double _foulTimer;

    public VaultSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.LongHorse, round, athletes, rng)
    {
    }

    protected override double StartX => 0;

    public VaultState State { get; private set; }
    public double BoardX => Rules.VaultBoard;
    public double HorseStart => Rules.VaultHorseStart;
    public double HorseEnd => Rules.VaultHorseStart + Rules.VaultHorseLength;
    public double HorseCentre => Rules.VaultHorseStart + Rules.VaultHorseLength / 2;

    /// <summary>Quality of the take-off (0..1), the push (0..1), twists counted and whether the landing was stuck.</summary>
    public double BoardQuality { get; private set; }
    public double PushQuality { get; private set; }
    public double Twists => _spinTarget;
    public bool Stuck { get; private set; }
    public double Score { get; private set; }

    /// <summary>Fraction of the push window elapsed while on the horse (for the readout).</summary>
    public double HorseTime => State == VaultState.OnHorse ? _time : 0;

    /// <summary>Fraction of the flight elapsed (0..1), 0 outside the flight.</summary>
    public double FlightProgress => State == VaultState.Flight ? Math.Clamp(_time / _flightDuration, 0, 1) : 0;

    /// <summary>True during the last moments of the flight, while a press still sticks the landing.</summary>
    public bool StickWindowOpen => State == VaultState.Flight && !_stickTried && _flightDuration - _time <= Rules.VaultStickWindow;

    public override double SpeedReadout(int playerIndex) => State is VaultState.Flight or VaultState.Landed ? _spinTarget * 250 : CurrentAthlete.Runner.SpeedCm;

    public override double? FlightHeight => State is VaultState.PreFlight or VaultState.Flight ? Math.Clamp(CurrentAthlete.Y / (Rules.VaultHorseHeight + 2.0), 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = VaultState.Approach;
        ShowAngle = false;
        Angle = 0;
        BoardQuality = 0;
        PushQuality = 0;
        _spin = 0;
        _spinTarget = 0;
        _lastButton = 0;
        Stuck = false;
        _stickTried = false;
        Score = 0;
        a.Rotation = 0;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case VaultState.Approach:
                RunStep(a, e, dt);
                if (e.ActionPressed && a.Runner.SpeedCm > 0)
                {
                    double gap = BoardX - a.X;
                    if (gap > Rules.VaultBoardZone || a.Runner.SpeedCm < 300)
                    {
                        Foul(a); // jumped far too early (or barely moving): lands short of the horse
                    }
                    else if (gap < -0.15)
                    {
                        Foul(a); // missed the board
                    }
                    else
                    {
                        TakeOff(a, gap);
                    }
                }
                else if (a.X > BoardX + Rules.LongJumpOvershoot)
                {
                    Foul(a); // ran into the horse
                }
                break;

            case VaultState.PreFlight:
                {
                    _time += dt;
                    double p = Math.Min(1, _time / Rules.VaultPreFlight);
                    a.X = _takeoffX + (HorseCentre - _takeoffX) * p;
                    a.Y = Rules.VaultHorseHeight * p + 0.5 * Math.Sin(p * Math.PI);
                    a.Rotation = 180 * p;
                    a.Pose = p < 0.5 ? Pose.Jump : Pose.Handstand;
                    if (p >= 1)
                    {
                        State = VaultState.OnHorse;
                        _time = 0;
                        a.X = HorseCentre;
                        a.Y = Rules.VaultHorseHeight;
                        a.Rotation = 180;
                        a.Pose = Pose.Handstand;
                    }
                }
                break;

            case VaultState.OnHorse:
                _time += dt;
                if (e.ActionPressed) Push(a, Math.Clamp(1 - Math.Abs(_time - Rules.VaultPushBest) / (Rules.VaultPushLatest - Rules.VaultPushBest), 0, 1));
                else if (_time >= Rules.VaultPushAuto) Push(a, 0);
                break;

            case VaultState.Flight:
                {
                    _time += dt;
                    double p = Math.Min(1, _time / _flightDuration);
                    if (e.Run1Pressed) Twist(1);
                    if (e.Run2Pressed) Twist(2);
                    if (e.ActionPressed && !_stickTried)
                    {
                        _stickTried = true;
                        Stuck = _flightDuration - _time <= Rules.VaultStickWindow;
                    }
                    // the shown spin chases the counted twists at 2.5 turns per second
                    _spin = Math.Min(_spinTarget, _spin + 2.5 * dt);
                    a.X = HorseCentre + _landingDistance * p;
                    a.Y = Rules.VaultHorseHeight * (1 - p) + 4 * _flightHeight * p * (1 - p);
                    a.Rotation = 180 + 180 * p + 360 * _spin;
                    a.Pose = Pose.Flop;
                    if (p >= 1) Land(a);
                }
                break;

            case VaultState.Fouled:
                _foulTimer -= dt;
                if (_foulTimer <= 0) CompleteAttempt(Attempt.Fouled);
                break;
        }
    }

    private void TakeOff(Athlete a, double gap)
    {
        BoardQuality = Math.Clamp(1 - Math.Abs(gap - Rules.VaultBoardSweetSpot) / (Rules.VaultBoardZone - Rules.VaultBoardSweetSpot), 0, 1);
        _takeoffX = a.X;
        a.Runner.Locked = true;
        a.Runner.SpeedCm = 0;
        State = VaultState.PreFlight;
        _time = 0;
        a.Pose = Pose.Jump;
        Cue(AudioCue.SfxSpring);
    }

    private void Push(Athlete a, double quality)
    {
        PushQuality = quality;
        State = VaultState.Flight;
        _time = 0;
        _flightDuration = Rules.VaultMinFlight + 0.5 * quality;
        _flightHeight = Rules.VaultMinHeight + 1.2 * quality;
        _landingDistance = 2.0 + 1.5 * quality;
        a.Pose = Pose.Flop;
        Cue(AudioCue.SfxJump);
    }

    private void Twist(int button)
    {
        if (_lastButton == button || _spinTarget >= Rules.VaultMaxTwists) return;
        _lastButton = button;
        _spinTarget += 0.5;
    }

    private void Land(Athlete a)
    {
        _spin = _spinTarget;
        a.Y = 0;
        a.X = HorseCentre + _landingDistance;
        State = VaultState.Landed;
        Score = Rules.VaultScore(BoardQuality, PushQuality, _spinTarget, Stuck);
        bool halfTwist = Math.Abs(_spinTarget - Math.Floor(_spinTarget + 1e-9)) > 1e-9;
        a.Rotation = 0;
        a.Pose = halfTwist ? Pose.Fall : Pose.Land;
        Cue(AudioCue.SfxLand);
        CompleteAttempt(Attempt.Of(Score), 2.6);
    }

    private void Foul(Athlete a)
    {
        State = VaultState.Fouled;
        _foulTimer = 1.0;
        a.Runner.SpeedCm = 0;
        a.Runner.Locked = false;
        a.Y = 0;
        a.Rotation = 0;
        a.Pose = Pose.Stumble;
        Cue(AudioCue.SfxWhistle);
    }

    protected override void TickAfterAttempt(double dt, Athlete a)
    {
        if (State != VaultState.Landed) return;
        if (a.Pose != Pose.Fall) a.Pose = Rules.Qualifies(Kind, Round, Score) ? Pose.Celebrate : Pose.Idle;
    }

    protected override void OnAttemptCompleted(Athlete a, Attempt result) => a.Runner.Locked = false;
}
