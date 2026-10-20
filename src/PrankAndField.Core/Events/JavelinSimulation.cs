namespace PrankAndField.Core.Events;

public enum ThrowState
{
    Approach,
    WindUp,     // button held, angle counting up, athlete still moving
    Flight,
    Landed,
}

/// <summary>Javelin throw: approach, hold to set the angle, release before the line.</summary>
public sealed class JavelinSimulation : FieldEventSimulation
{
    private double _flightTime;
    private double _flightDuration;
    private double _launchSpeed;
    private double _launchX;
    private double _holdSeconds;
    private bool _foul;
    private bool _birdHit;

    public JavelinSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.JavelinThrow, round, athletes, rng)
    {
    }

    protected override double StartX => 0;

    public ThrowState State { get; private set; }
    public double LineX => Rules.JavelinLine;

    /// <summary>Javelin position (metres) and orientation while flying or stuck in the ground.</summary>
    public double JavelinX { get; private set; }
    public double JavelinY { get; private set; }
    public double JavelinAngle { get; private set; }
    public bool JavelinVisible { get; private set; }
    public double Distance { get; private set; }
    public double ApexHeight { get; private set; }
    public bool BirdHit => _birdHit;

    public override double? FlightHeight => State == ThrowState.Flight ? Math.Clamp((JavelinY - 1.8) / Math.Max(1, ApexHeight), 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = ThrowState.Approach;
        ShowAngle = false;
        Angle = 0;
        _foul = false;
        _birdHit = false;
        _holdSeconds = 0;
        JavelinVisible = false;
        Distance = 0;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case ThrowState.Approach:
                RunStep(a, e, dt);
                if (e.ActionPressed && a.Runner.SpeedCm > 0)
                {
                    State = ThrowState.WindUp;
                    ShowAngle = true;
                    Angle = 0;
                    a.Pose = Pose.Throw;
                }
                else if (a.X > LineX)
                {
                    Foul(a);
                }
                break;

            case ThrowState.WindUp:
                {
                    // Like the arcade, you can keep tapping RUN while the throw button is held.
                    double before = a.X;
                    a.Runner.ApplyEdges(e);
                    a.Runner.Integrate(dt);
                    a.AdvanceAnim(a.X - before);
                    a.Pose = Pose.Throw;
                    // The angle climbs while the button is held and parks at 90; the throw happens on release.
                    Angle = Math.Min(90, Angle + Rules.JavelinAngleRate * dt);
                    if (a.X > LineX)
                    {
                        Foul(a);
                    }
                    else if (!e.ActionHeld)
                    {
                        Release(a);
                    }
                }
                break;

            case ThrowState.Flight:
                {
                    // The thrower plants his feet at the release point and watches the javelin fly.
                    _releaseTimer -= dt;
                    a.Pose = _releaseTimer > 0 ? Pose.Release : Pose.Idle;
                    if (e.Current.AnyRun) _holdSeconds += dt;

                    _flightTime += dt;
                    double t = Math.Min(_flightTime, _flightDuration);
                    double p = _flightDuration > 0 ? t / _flightDuration : 1;
                    double holdBonus = 1 + Rules.JavelinRunHoldBonus * Math.Min(1, _holdSeconds / Math.Max(0.1, _flightDuration));
                    Distance = _baseDistance * holdBonus;
                    JavelinX = _launchX + Distance * p;
                    JavelinY = 1.8 + 4 * ApexHeight * p * (1 - p);
                    JavelinAngle = Angle * (1 - 2 * p); // nose up then down
                    if (!_birdHit && JavelinY > Rules.JavelinBirdApex && p < 0.5)
                    {
                        _birdHit = true;
                        AwardBonus(a.PlayerIndex, Rules.BonusPoints, "bird", JavelinX, Rules.JavelinBirdApex);
                    }
                    if (_flightTime >= _flightDuration)
                    {
                        JavelinY = 0;
                        JavelinAngle = -50;
                        State = ThrowState.Landed;
                        Cue(AudioCue.SfxLand);
                        CompleteAttempt(Attempt.Of(Distance), 2.4);
                    }
                }
                break;
        }
    }

    private double _baseDistance;
    private double _releaseTimer;

    private void Release(Athlete a)
    {
        State = ThrowState.Flight;
        _flightTime = 0;
        _releaseTimer = 0.6;
        _launchSpeed = Rules.JavelinLaunchSpeed(a.Runner.SpeedMetersPerSecond);
        _launchX = a.X;
        a.Runner.SpeedCm = 0; // stops dead at the release point
        _baseDistance = Rules.JavelinDistance(_launchSpeed, Angle);
        Distance = _baseDistance;
        ApexHeight = Math.Max(2, Ballistics.PeakHeight(_launchSpeed, Angle) * 1.4);
        _flightDuration = 1.2 + 0.025 * _baseDistance + ApexHeight * 0.03;
        JavelinVisible = true;
        JavelinX = _launchX;
        JavelinY = 1.8;
        a.Pose = Pose.Release;
        Cue(AudioCue.SfxThrow);
    }

    private void Foul(Athlete a)
    {
        _foul = true;
        a.Runner.SpeedCm = 0;
        a.Pose = Pose.Stumble;
        JavelinVisible = false;
        CompleteAttempt(Attempt.Fouled);
    }

    protected override void TickAfterAttempt(double dt, Athlete a)
    {
        if (_foul) return;
        a.Pose = Rules.Qualifies(Kind, Round, Distance) ? Pose.Celebrate : Pose.Idle;
    }
}
