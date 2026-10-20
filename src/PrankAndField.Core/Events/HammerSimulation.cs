namespace PrankAndField.Core.Events;

public enum HammerState
{
    Ready,      // waiting for a RUN press
    Spinning,
    Flight,
    Landed,
    Fouled,
}

/// <summary>
/// Hammer throw. A RUN press starts the spin; every revolution is faster. Hold the action button to
/// count the angle, release when the hammer points down the sector. Ten revolutions make the athlete dizzy.
/// </summary>
public sealed class HammerSimulation : FieldEventSimulation
{
    private double _flightTime;
    private double _flightDuration;
    private double _foulTimer;
    private double _holdStartHeading;

    public HammerSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.HammerThrow, round, athletes, rng)
    {
    }

    protected override double StartX => 0;

    public HammerState State { get; private set; }

    /// <summary>Hammer direction in degrees: 0 = towards the sector (right), 90 = towards the camera (down).</summary>
    public double HammerHeading { get; private set; }
    public int SpinsCompleted { get; private set; }
    public double SpinSpeed => Rules.HammerSpinSpeed(SpinsCompleted);
    public bool Holding { get; private set; }

    /// <summary>True when the hammer is in the sweet spot (drawn red in the arcade): full distance.</summary>
    public bool InReleaseArc => Math.Abs(WrapDeg(HammerHeading)) <= Rules.HammerReleaseArc;

    /// <summary>True while the ball is anywhere in front of the thrower: a legal, if weaker, release.</summary>
    public bool InFrontHalf => Math.Abs(WrapDeg(HammerHeading)) <= Rules.HammerFoulArc;

    /// <summary>True while the ball passes behind the thrower: the moment to press the throw button (drawn blue).</summary>
    public bool InPressZone => Math.Abs(WrapDeg(HammerHeading)) >= 180 - Rules.HammerReleaseArc;

    public double HammerX { get; private set; }
    public double HammerY { get; private set; }
    public double Distance { get; private set; }

    public override double SpeedReadout(int playerIndex) => SpinSpeed * 400;

    public override double? FlightHeight => State == HammerState.Flight ? Math.Clamp(HammerY / Math.Max(3, Distance * 0.22), 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = HammerState.Ready;
        ShowAngle = false;
        Angle = 0;
        HammerHeading = 180;
        SpinsCompleted = 0;
        Holding = false;
        HammerX = 0;
        HammerY = 0;
        Distance = 0;
        a.Pose = Pose.Idle;
        a.Rotation = 0;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case HammerState.Ready:
                if (e.AnyRunPressed)
                {
                    State = HammerState.Spinning;
                    a.Pose = Pose.Spin;
                    Cue(AudioCue.SfxSpin);
                }
                break;

            case HammerState.Spinning:
                {
                    double prev = HammerHeading;
                    HammerHeading += SpinSpeed * 360 * dt;
                    a.Rotation = HammerHeading;
                    if (Math.Floor(prev / 360.0) < Math.Floor(HammerHeading / 360.0))
                    {
                        SpinsCompleted++;
                        Cue(AudioCue.SfxSpin);
                        if (SpinsCompleted >= Rules.HammerMaxSpins)
                        {
                            Dizzy(a);
                            break;
                        }
                    }

                    if (e.ActionPressed)
                    {
                        Holding = true;
                        ShowAngle = true;
                        Angle = 0;
                        _holdStartHeading = HammerHeading;
                    }
                    if (Holding)
                    {
                        // The angle follows the spin, so the timing stays the same whatever the speed:
                        // half a turn held (ball behind -> ball in front) is 45 degrees.
                        Angle = Math.Min(90, (HammerHeading - _holdStartHeading) * Rules.HammerAnglePerDegreeTurned);
                        if (!e.ActionHeld)
                        {
                            Holding = false;
                            if (InFrontHalf) Throw(a);
                            else WrongDirection(a);
                        }
                    }
                }
                break;

            case HammerState.Flight:
                {
                    _flightTime += dt;
                    double p = Math.Min(1, _flightTime / _flightDuration);
                    HammerX = Distance * p;
                    HammerY = 4 * Math.Max(3, Distance * 0.22) * p * (1 - p);
                    // The thrower stands still in the circle, facing the sector.
                    a.Rotation = 0;
                    a.Pose = _flightTime < 0.6 ? Pose.Release : Pose.Idle;
                    if (p >= 1)
                    {
                        HammerY = 0;
                        State = HammerState.Landed;
                        Cue(AudioCue.SfxLand);
                        CompleteAttempt(Attempt.Of(Distance), 2.4);
                    }
                }
                break;

            case HammerState.Fouled:
                _foulTimer -= dt;
                if (_foulTimer <= 0) CompleteAttempt(Attempt.Fouled);
                break;
        }
    }

    private void Throw(Athlete a)
    {
        State = HammerState.Flight;
        _flightTime = 0;
        double luck = Rng.Range(0.97, 1.03);
        double direction = Rules.HammerDirectionFactor(WrapDeg(HammerHeading));
        Distance = Math.Min(99.99, Rules.HammerDistance(SpinsCompleted, Angle, luck) * direction);
        _flightDuration = 1.4 + Distance * 0.02;
        HammerHeading = 0;
        a.Rotation = 0;
        a.Pose = Pose.Release;
        Cue(AudioCue.SfxThrow);
    }

    protected override void TickAfterAttempt(double dt, Athlete a)
    {
        if (State != HammerState.Landed) return;
        a.Rotation = 0;
        a.Pose = Rules.Qualifies(Kind, Round, Distance) ? Pose.Celebrate : Pose.Idle;
    }

    private void WrongDirection(Athlete a)
    {
        State = HammerState.Fouled;
        _foulTimer = 1.2;
        a.Pose = Pose.Fall;
        ShowBanner(BannerKind.WrongDirection, 0, a.PlayerIndex, 1.2);
        Cue(AudioCue.SfxWhistle);
    }

    private void Dizzy(Athlete a)
    {
        State = HammerState.Fouled;
        _foulTimer = 1.5;
        a.Pose = Pose.Dizzy;
        ShowBanner(BannerKind.Dizzy, 0, a.PlayerIndex, 1.5);
        Cue(AudioCue.SfxWhistle);
    }

    public static double WrapDeg(double deg)
    {
        deg %= 360;
        if (deg > 180) deg -= 360;
        if (deg < -180) deg += 360;
        return deg;
    }
}
