namespace PrankAndField.Core.Events;

public enum HighJumpState
{
    Approach,   // computer-driven run towards the bar
    Rising,     // button held: angle counts DOWN from 80
    Airborne,   // mash RUN for extra height
    Landed,
    Missed,
}

/// <summary>
/// High jump. The approach speed is fixed (as in the arcade), the action button takes off and, while held,
/// the angle counts down from 80 degrees. Once airborne, tapping RUN lifts the body over the bar.
/// </summary>
public sealed class HighJumpSimulation : FieldEventSimulation
{
    private readonly double[] _bar = new double[2];
    private readonly int[] _misses = new int[2];
    private double _airTime;
    private double _takeoffX;
    private double _travel;
    private double _peak;
    private int _mashes;
    private int _lastButton;
    private double _lockedAngle;

    public HighJumpSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.HighJump, round, athletes, rng)
    {
        for (int i = 0; i < 2; i++) _bar[i] = QualifyingMark;
    }

    protected override double StartX => Rules.HighJumpApproachStart;

    public HighJumpState State { get; private set; }
    public double BarHeight => _bar[CurrentAthlete.PlayerIndex];
    public double BarHeightFor(int playerIndex) => _bar[playerIndex];
    public bool BarFallen { get; private set; }
    public double BarFallAnim { get; private set; }
    public int Mashes => _mashes;
    public double PeakHeight => _peak;

    public override double SpeedReadout(int playerIndex) => State == HighJumpState.Airborne ? _mashes * 100 : CurrentAthlete.Runner.SpeedCm;

    public override double? FlightHeight => State == HighJumpState.Airborne ? Math.Clamp(CurrentAthlete.Y / 2.6, 0, 1) : null;

    protected override void BeginAttempt(Athlete a)
    {
        State = HighJumpState.Approach;
        ShowAngle = false;
        Angle = Rules.HighJumpAngleStart;
        BarFallen = false;
        BarFallAnim = 0;
        _mashes = 0;
        _lastButton = 0;
        a.X = Rules.HighJumpApproachStart;
        a.Runner.SpeedCm = Rules.HighJumpApproachSpeed;
        a.Runner.Locked = true; // speed is set by the computer
        a.Pose = Pose.Run;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case HighJumpState.Approach:
                {
                    double before = a.X;
                    a.Runner.Integrate(dt);
                    a.AdvanceAnim(a.X - before);
                    a.Pose = Pose.Run;
                    if (e.ActionPressed)
                    {
                        if (a.X >= Rules.HighJumpTakeoffMin && a.X <= Rules.HighJumpTakeoffMax)
                        {
                            State = HighJumpState.Rising;
                            ShowAngle = true;
                            Angle = Rules.HighJumpAngleStart;
                            _takeoffX = a.X;
                            a.Runner.SpeedCm = 0;
                            a.Pose = Pose.Jump;
                            Cue(AudioCue.SfxJump);
                        }
                        else if (a.X > Rules.HighJumpTakeoffMax)
                        {
                            Miss(a, true);
                        }
                    }
                    else if (a.X > Rules.HighJumpTakeoffMax + 0.3)
                    {
                        Miss(a, true); // ran into the bar
                    }
                }
                break;

            case HighJumpState.Rising:
                Angle = Math.Max(Rules.HighJumpAngleMin, Angle - Rules.HighJumpAngleRateDown * dt);
                if (!e.ActionHeld || Angle <= Rules.HighJumpAngleMin) Launch(a);
                break;

            case HighJumpState.Airborne:
                {
                    if (e.Run1Pressed) Mash(1);
                    if (e.Run2Pressed) Mash(2);
                    _airTime += dt;
                    double p = Math.Min(1, _airTime / Rules.HighJumpAirTime);
                    _peak = Rules.HighJumpPeak(_lockedAngle, _mashes);
                    a.X = _takeoffX + _travel * p;
                    a.Y = 4 * _peak * p * (1 - p);
                    a.Rotation = p * 180;
                    a.Pose = Pose.Flop;

                    // Crossing the bar plane (x = 0).
                    if (!BarFallen && a.X >= 0 && a.X - _travel * dt / Rules.HighJumpAirTime < 0)
                    {
                        if (a.Y < BarHeight + 0.02)
                        {
                            BarFallen = true;
                            Cue(AudioCue.SfxBarFall);
                        }
                    }
                    if (BarFallen) BarFallAnim = Math.Min(1, BarFallAnim + dt * 2);

                    if (p >= 1)
                    {
                        a.Y = 0;
                        a.Rotation = 0;
                        if (BarFallen || a.X < 0.2) Miss(a, false);
                        else Clear(a);
                    }
                }
                break;

            case HighJumpState.Missed:
            case HighJumpState.Landed:
                if (BarFallen) BarFallAnim = Math.Min(1, BarFallAnim + dt * 2);
                break;
        }
    }

    private void Mash(int button)
    {
        if (_lastButton != button) _mashes++;
        _lastButton = button;
    }

    private void Launch(Athlete a)
    {
        State = HighJumpState.Airborne;
        _lockedAngle = Angle;
        _airTime = 0;
        _travel = Rules.HighJumpTravel(_lockedAngle);
        _peak = Rules.HighJumpPeak(_lockedAngle, 0);
    }

    private void Clear(Athlete a)
    {
        State = HighJumpState.Landed;
        a.Pose = Pose.Celebrate;
        int p = a.PlayerIndex;
        double cleared = _bar[p];
        _bar[p] = Rules.Snap(_bar[p] + Rules.HighJumpBarStep);
        if (_misses[p] == 2 && CurrentTry == 3)
            AwardBonus(p, Rules.BonusPoints, "mole", -3.0, 0);
        CompleteAttempt(Attempt.Of(cleared), 2.4);
    }

    private void Miss(Athlete a, bool ranIntoBar)
    {
        State = HighJumpState.Missed;
        BarFallen = true;
        a.Pose = Pose.Fall;
        a.Runner.SpeedCm = 0;
        _misses[a.PlayerIndex]++;
        Cue(AudioCue.SfxBarFall);
        CompleteAttempt(Attempt.Fouled, 2.2);
    }
}
