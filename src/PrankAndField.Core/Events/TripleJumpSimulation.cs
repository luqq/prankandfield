namespace PrankAndField.Core.Events;

public enum TripleJumpState
{
    Approach,
    Rising,     // button held: angle counts up, athlete frozen at take-off
    Flight,
    Contact,    // on the ground between phases: press for the next one
    Landed,
}

/// <summary>
/// Triple jump: run up, press before the board for the hop, then press again as the feet touch the ground
/// for the step and the jump. Each press is held to set that phase's angle; a late press costs speed.
/// The mark is measured from the board to the final landing.
/// </summary>
public sealed class TripleJumpSimulation : FieldEventSimulation
{
    private readonly double?[] _landings = new double?[3];
    private double _flightTime;
    private double _flightDuration;
    private double _speed;
    private double _takeoffX;
    private double _phaseDistance;
    private double _contactTimer;
    private bool _foulJump;

    public TripleJumpSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.TripleJump, round, athletes, rng)
    {
    }

    protected override double StartX => 0;

    public TripleJumpState State { get; private set; }
    public double BoardX => Rules.TripleJumpBoard;
    public double PitStart => BoardX + Rules.TripleJumpPitStart;

    /// <summary>0 = hop, 1 = step, 2 = jump.</summary>
    public int JumpPhase { get; private set; }

    /// <summary>Where each phase landed (metres from the start), once it did.</summary>
    public IReadOnlyList<double?> Landings => _landings;

    /// <summary>Phases launched without a press in time.</summary>
    public int LateTakeoffs { get; private set; }

    /// <summary>Fraction of the contact window left while on the ground between phases.</summary>
    public double ContactLeft => State == TripleJumpState.Contact ? Math.Clamp(_contactTimer / Rules.TripleJumpContactWindow, 0, 1) : 0;

    public override double? FlightHeight => State == TripleJumpState.Flight ? Math.Clamp(CurrentAthlete.Y / 1.6, 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = TripleJumpState.Approach;
        ShowAngle = false;
        Angle = 0;
        JumpPhase = 0;
        LateTakeoffs = 0;
        _foulJump = false;
        Array.Fill(_landings, null);
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case TripleJumpState.Approach:
                RunStep(a, e, dt);
                if (e.ActionPressed && a.Runner.SpeedCm > 0)
                {
                    double gap = BoardX - a.X;
                    _foulJump = gap < -Rules.LongJumpFoulTolerance;
                    _speed = a.Runner.SpeedMetersPerSecond;
                    a.Runner.Locked = true;
                    BeginRising(a);
                }
                else if (a.X > BoardX + Rules.TripleJumpOvershoot)
                {
                    a.Pose = Pose.Stumble;
                    a.Runner.SpeedCm = 0;
                    CompleteAttempt(Attempt.Fouled);
                }
                break;

            case TripleJumpState.Rising:
                Angle = Math.Min(90, Angle + Rules.AngleRateUp * dt);
                if (!e.ActionHeld || Angle >= 90) Launch(a);
                break;

            case TripleJumpState.Flight:
                {
                    _flightTime += dt;
                    double p = Math.Min(1, _flightTime / _flightDuration);
                    a.X = _takeoffX + _phaseDistance * p;
                    a.Y = 4 * Math.Max(0.5, _phaseDistance * 0.18) * p * (1 - p);
                    a.Pose = JumpPhase < 2 ? (p < 0.6 ? Pose.Jump : Pose.Land) : (p < 0.7 ? Pose.Fly : Pose.Land);
                    if (p >= 1) TouchDown(a);
                }
                break;

            case TripleJumpState.Contact:
                _contactTimer -= dt;
                if (e.ActionPressed)
                {
                    BeginRising(a);
                }
                else if (_contactTimer <= 0)
                {
                    // Too late: a flat, slow bounce into the next phase.
                    LateTakeoffs++;
                    _speed *= Rules.TripleJumpLateSpeedKept / Rules.TripleJumpSpeedKept;
                    Angle = Rules.TripleJumpLateAngle;
                    ShowAngle = false;
                    Launch(a);
                }
                break;
        }
    }

    private void BeginRising(Athlete a)
    {
        _takeoffX = a.X;
        State = TripleJumpState.Rising;
        ShowAngle = true;
        Angle = 0;
        a.Pose = Pose.Jump;
        Cue(AudioCue.SfxJump);
    }

    private void Launch(Athlete a)
    {
        State = TripleJumpState.Flight;
        _flightTime = 0;
        _phaseDistance = Rules.TripleJumpPhaseDistance(JumpPhase, _speed, Angle);
        if (_foulJump) _phaseDistance *= 0.9;
        _flightDuration = 0.45 + 0.07 * _phaseDistance;
    }

    private void TouchDown(Athlete a)
    {
        a.Y = 0;
        _landings[JumpPhase] = a.X;
        Cue(AudioCue.SfxLand);
        if (JumpPhase == 2)
        {
            State = TripleJumpState.Landed;
            a.Pose = Pose.Land;
            double mark = a.X - BoardX;
            if (_foulJump || mark < Rules.TripleJumpPitStart) CompleteAttempt(Attempt.Fouled);
            else CompleteAttempt(Attempt.Of(mark));
            return;
        }
        JumpPhase++;
        _speed *= Rules.TripleJumpSpeedKept;
        State = TripleJumpState.Contact;
        _contactTimer = Rules.TripleJumpContactWindow;
        ShowAngle = false;
        a.Pose = Pose.Land;
    }

    protected override void OnAttemptCompleted(Athlete a, Attempt result) => a.Runner.Locked = false;

    protected override void TickAfterAttempt(double dt, Athlete a)
    {
        if (State != TripleJumpState.Landed) return;
        var at = AttemptsFor(a.PlayerIndex)[CurrentTry - 1];
        a.Pose = at.Mark is double m && Rules.Qualifies(Kind, Round, m) ? Pose.Celebrate : (at.Foul ? Pose.Stumble : Pose.Idle);
    }
}
