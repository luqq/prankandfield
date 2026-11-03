namespace PrankAndField.Core.Events;

public enum ThrowState
{
    Approach,
    WindUp,     // button held, the dial climbing from 20 to 70, athlete planted where he pressed
    Flight,
    Landed,
}

/// <summary>
/// Javelin throw: run up, press THROW before the line to plant the feet, hold while the angle dial climbs
/// and release to throw; the dial topping out throws by itself. The mark is measured from the foul line,
/// so planting early costs distance, and it is fixed at the release: nothing done in flight changes it.
/// </summary>
public sealed class JavelinSimulation : FieldEventSimulation
{
    private double _flightTime;
    private double _flightDuration;
    private double _launchSpeed;
    private double _launchX;
    private double _runUpSpeed;
    private double _flightLength;
    private double _releaseTimer;
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
    /// <summary>Mark of the throw in progress: metres from the foul line to where the javelin is going to land.</summary>
    public double Distance { get; private set; }
    public double ApexHeight { get; private set; }
    public bool BirdHit => _birdHit;

    /// <summary>Angle the javelin left the hand at (degrees), once released.</summary>
    public double ReleaseAngle { get; private set; }

    public override double? FlightHeight => State == ThrowState.Flight ? Math.Clamp((JavelinY - 1.8) / Math.Max(1, ApexHeight), 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = ThrowState.Approach;
        ShowAngle = false;
        Angle = 0;
        ReleaseAngle = 0;
        _foul = false;
        _birdHit = false;
        JavelinVisible = false;
        Distance = 0;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case ThrowState.Approach:
                RunStep(a, e, dt);
                if (a.X > LineX)
                {
                    Foul(a);
                }
                else if (e.ActionPressed && a.Runner.SpeedCm > 0)
                {
                    // The thrower plants his feet here; the run-up speed is kept for the launch.
                    _runUpSpeed = a.Runner.SpeedMetersPerSecond;
                    a.Runner.SpeedCm = 0;
                    State = ThrowState.WindUp;
                    ShowAngle = true;
                    Angle = Rules.JavelinAngleStart;
                    a.Pose = Pose.Throw;
                }
                break;

            case ThrowState.WindUp:
                a.Pose = Pose.Throw;
                // The dial climbs while the button is held. Letting go throws at the angle reached; the dial
                // reaching the top throws by itself, so holding too long costs distance but never the attempt.
                // A release on the very tick the dial tops out is the same 70-degree throw.
                Angle = Math.Min(Rules.JavelinAngleMax, Angle + Rules.JavelinAngleRate * dt);
                if (!e.ActionHeld || Angle >= Rules.JavelinAngleMax - 1e-9) Release(a);
                break;

            case ThrowState.Flight:
                {
                    // The thrower stays planted at the release point and watches the javelin fly.
                    _releaseTimer -= dt;
                    a.Pose = _releaseTimer > 0 ? Pose.Release : Pose.Idle;

                    _flightTime += dt;
                    double t = Math.Min(_flightTime, _flightDuration);
                    double p = _flightDuration > 0 ? t / _flightDuration : 1;
                    JavelinX = _launchX + _flightLength * p;
                    JavelinY = 1.8 + 4 * ApexHeight * p * (1 - p);
                    JavelinAngle = ReleaseAngle * (1 - 2 * p); // nose up then down
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
                        if (Distance > 0) CompleteAttempt(Attempt.Of(Distance), 2.4);
                        else Foul(a, keepJavelin: true); // landed behind the line
                    }
                }
                break;
        }
    }

    private void Release(Athlete a)
    {
        State = ThrowState.Flight;
        ReleaseAngle = Angle;
        _flightTime = 0;
        _releaseTimer = 0.6;
        _launchSpeed = Rules.JavelinLaunchSpeed(_runUpSpeed);
        _launchX = a.X;
        _flightLength = Rules.JavelinDistance(_launchSpeed, ReleaseAngle);
        Distance = _launchX + _flightLength - LineX;
        ApexHeight = Math.Max(2, Ballistics.PeakHeight(_launchSpeed, ReleaseAngle) * 1.4);
        _flightDuration = 1.2 + 0.025 * _flightLength + ApexHeight * 0.03;
        JavelinVisible = true;
        JavelinX = _launchX;
        JavelinY = 1.8;
        a.Pose = Pose.Release;
        Cue(AudioCue.SfxThrow);
    }

    private void Foul(Athlete a, bool keepJavelin = false)
    {
        _foul = true;
        a.Runner.SpeedCm = 0;
        a.Pose = Pose.Stumble;
        JavelinVisible = keepJavelin;
        CompleteAttempt(Attempt.Fouled);
    }

    protected override void TickAfterAttempt(double dt, Athlete a)
    {
        if (_foul) return;
        a.Pose = Rules.Qualifies(Kind, Round, Distance) ? Pose.Celebrate : Pose.Idle;
    }
}
