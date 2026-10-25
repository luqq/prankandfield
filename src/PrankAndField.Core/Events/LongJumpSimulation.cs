namespace PrankAndField.Core.Events;

public enum JumpState
{
    Approach,
    Rising,     // button held: angle counts up, athlete frozen at take-off
    Flight,
    Landed,
}

/// <summary>Long jump: run up, take off before the board, hold to set the angle.</summary>
public sealed class LongJumpSimulation : FieldEventSimulation
{
    private double _flightTime;
    private double _flightDuration;
    private double _launchSpeed;
    private double _takeoffX;
    private bool _foulJump;

    public LongJumpSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.LongJump, round, athletes, rng)
    {
    }

    protected override double StartX => 0;

    public JumpState State { get; private set; }
    public double BoardX => Rules.LongJumpBoard;

    /// <summary>Distance of the jump in progress (metres from the board), for the sand marks.</summary>
    public double LandingDistance { get; private set; }

    public override double? FlightHeight => State == JumpState.Flight ? Math.Clamp(CurrentAthlete.Y / 1.6, 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = JumpState.Approach;
        ShowAngle = false;
        Angle = 0;
        _foulJump = false;
        LandingDistance = 0;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case JumpState.Approach:
                RunStep(a, e, dt);
                if (e.ActionPressed && a.Runner.SpeedCm > 0)
                {
                    TakeOff(a);
                }
                else if (a.X > BoardX + Rules.LongJumpOvershoot)
                {
                    // Ran straight into the pit.
                    a.Pose = Pose.Stumble;
                    a.Runner.SpeedCm = 0;
                    CompleteAttempt(Attempt.Fouled);
                }
                break;

            case JumpState.Rising:
                Angle = Math.Min(90, Angle + Rules.AngleRateUp * dt);
                if (!e.ActionHeld || Angle >= 90) Launch(a);
                break;

            case JumpState.Flight:
                _flightTime += dt;
                {
                    double t = Math.Min(_flightTime, _flightDuration);
                    double p = _flightDuration > 0 ? t / _flightDuration : 1;
                    a.X = _takeoffX + LandingDistance * p;
                    a.Y = 4 * Math.Max(0.6, Rules.LongJumpDistance(_launchSpeed, Angle) * 0.18) * p * (1 - p);
                    a.Pose = p < 0.7 ? Pose.Fly : Pose.Land;
                }
                if (_flightTime >= _flightDuration)
                {
                    a.Y = 0;
                    State = JumpState.Landed;
                    a.Pose = Pose.Land;
                    Cue(AudioCue.SfxLand);
                    double mark = a.X - BoardX;
                    // Landing short of the sand is a no-jump too, never a negative mark.
                    if (_foulJump || mark < Rules.LongJumpPitStart)
                    {
                        CompleteAttempt(Attempt.Fouled);
                    }
                    else
                    {
                        CompleteAttempt(Attempt.Of(mark));
                    }
                }
                break;
        }
    }

    private void TakeOff(Athlete a)
    {
        double gap = BoardX - a.X;
        _foulJump = gap < -Rules.LongJumpFoulTolerance;
        if (!_foulJump && gap <= Rules.LongJumpBoardBonusZone)
            a.Runner.SpeedCm = Math.Min(Runner.MaxSpeedCm + Rules.LongJumpBoardBonus, a.Runner.SpeedCm + Rules.LongJumpBoardBonus);
        if (!_foulJump && gap <= Rules.MoleZone)
            AwardBonus(a.PlayerIndex, Rules.BonusPoints, "mole", BoardX - 1.0, 0);

        _takeoffX = a.X;
        _launchSpeed = a.Runner.SpeedMetersPerSecond;
        a.Runner.Locked = true;
        a.Pose = Pose.Jump;
        State = JumpState.Rising;
        ShowAngle = true;
        Angle = 0;
        Cue(AudioCue.SfxJump);
    }

    private void Launch(Athlete a)
    {
        State = JumpState.Flight;
        _flightTime = 0;
        LandingDistance = _foulJump
            ? Rules.LongJumpDistance(_launchSpeed, Angle) * 0.9
            : Rules.LongJumpDistance(_launchSpeed, Angle);
        // Keep the arc readable: ~0.9 s for a 6 m jump, longer for bigger ones.
        _flightDuration = 0.55 + 0.07 * LandingDistance;
    }

    protected override void OnAttemptCompleted(Athlete a, Attempt result)
    {
        a.Runner.Locked = false;
        // Three identical marks summon the bird (arcade easter egg).
        var tries = AttemptsFor(a.PlayerIndex);
        if (CurrentTry == 3 && tries.All(t => t.Mark.HasValue) && tries.Select(t => t.Mark!.Value).Distinct().Count() == 1)
            AwardBonus(a.PlayerIndex, Rules.BonusPoints, "bird", a.X, 6);
    }
}
