namespace PrankAndField.Core.Events;

public enum ArcheryState
{
    Aim,     // hold the button to raise the angle, release to shoot
    Flight,
    Scored,  // the arrow's points on screen
}

/// <summary>An arrow stuck in the target: its offset from the centre, in metres (positive = above).</summary>
public readonly record struct StuckArrow(double Offset, int Points);

/// <summary>
/// Archery. The target slides up and down at the far end of the range; hold JUMP/THROW to raise the bow,
/// release to shoot. The wind lifts or drops the arrow. Six arrows, ten points for the centre.
/// </summary>
public sealed class ArcherySimulation : TurnEventSimulation
{
    private double _targetPhase;
    private double _time;
    private double _launchAngle;
    private bool _holding;
    private int _tens;

    public ArcherySimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.Archery, round, athletes, rng)
    {
    }

    protected override double StartX => 0;

    public ArcheryState State { get; private set; }

    /// <summary>0-based index of the arrow being shot.</summary>
    public int ArrowIndex { get; private set; }
    public int Points { get; private set; }
    public int LastPoints { get; private set; }
    public double Wind { get; private set; }
    public double TargetY { get; private set; } = Rules.ArcheryTargetCentre;
    public double ArrowX { get; private set; }
    public double ArrowY { get; private set; }
    public double ArrowAngle { get; private set; }
    public bool ArrowVisible => State == ArcheryState.Flight;
    public List<StuckArrow> Stuck { get; } = new();
    public double TargetX => Rules.ArcheryTargetX;

    public override double RunningScore => Points;

    /// <summary>Where the centre of the target will be in <paramref name="secondsAhead"/> seconds (it bobs on a sine).</summary>
    public double TargetYAt(double secondsAhead)
    {
        double speed = Rules.ArcheryTargetSpeed + Rules.ArcheryTargetSpeedStep * ArrowIndex;
        return Rules.ArcheryTargetCentre + Rules.ArcheryTargetSwing * Math.Sin(_targetPhase + speed * secondsAhead);
    }

    public override double SpeedReadout(int playerIndex) => Points * 20;

    public override double? FlightHeight => State == ArcheryState.Flight ? Math.Clamp((ArrowY - Rules.ArcheryBowHeight) / 2.0 + 0.3, 0, 1) : null;

    protected override void BeginTurn(Athlete a)
    {
        a.X = 0;
        a.Pose = Pose.Aim;
        ArrowIndex = 0;
        Points = 0;
        _tens = 0;
        _targetPhase = 0;
        Stuck.Clear();
        NewArrow();
    }

    private void NewArrow()
    {
        Wind = Math.Round(Rng.Range(-1, 1), 1);
        Angle = 0;
        ShowAngle = false;
        _holding = false;
        _time = 0;
        State = ArcheryState.Aim;
        LastPoints = -1;
    }

    protected override void TickTurn(double dt, Athlete a, ButtonEdges e)
    {
        double speed = Rules.ArcheryTargetSpeed + Rules.ArcheryTargetSpeedStep * ArrowIndex;
        _targetPhase += speed * dt;
        TargetY = Rules.ArcheryTargetCentre + Rules.ArcheryTargetSwing * Math.Sin(_targetPhase);

        switch (State)
        {
            case ArcheryState.Aim:
                _time += dt;
                a.Pose = Pose.Aim;
                if (e.ActionPressed && !_holding)
                {
                    _holding = true;
                    ShowAngle = true;
                    Angle = 0;
                }
                if (_holding)
                {
                    Angle = Math.Min(Rules.ArcheryMaxAngle, Angle + Rules.ArcheryAngleRate * dt);
                    if (!e.ActionHeld || Angle >= Rules.ArcheryMaxAngle) Shoot();
                }
                else if (_time > Rules.ArcheryTimeout)
                {
                    Shoot(); // the arrow leaves by itself, flat
                }
                break;

            case ArcheryState.Flight:
                {
                    _time += dt;
                    var (x, y) = Ballistics.At(Rules.ArrowSpeed, _launchAngle, _time);
                    ArrowX = x;
                    ArrowY = Rules.ArcheryBowHeight + y + Wind * Rules.ArcheryWindLift * Math.Clamp(x / TargetX, 0, 1);
                    double rad = _launchAngle * Math.PI / 180.0;
                    double vx = Rules.ArrowSpeed * Math.Cos(rad);
                    double vy = Rules.ArrowSpeed * Math.Sin(rad) - Ballistics.G * _time + Wind * Rules.ArcheryWindLift / Math.Max(0.1, TargetX / vx);
                    ArrowAngle = Math.Atan2(vy, vx) * 180.0 / Math.PI;
                    if (ArrowX >= TargetX)
                    {
                        ArrowX = TargetX;
                        Impact(a);
                    }
                    else if (ArrowY <= 0)
                    {
                        ArrowY = 0;
                        Miss();
                    }
                }
                break;

            case ArcheryState.Scored:
                _time += dt;
                if (_time >= 1.1)
                {
                    ArrowIndex++;
                    if (ArrowIndex >= Rules.ArcheryArrows) CompleteTurn(Points);
                    else NewArrow();
                }
                break;
        }
    }

    private void Shoot()
    {
        _launchAngle = Angle;
        State = ArcheryState.Flight;
        _time = 0;
        ArrowX = 0;
        ArrowY = Rules.ArcheryBowHeight;
        ArrowAngle = Angle;
        ShowAngle = false;
        Cue(AudioCue.SfxBow);
    }

    private void Impact(Athlete a)
    {
        double offset = ArrowY - TargetY;
        int pts = Rules.ArcheryPoints(offset);
        if (pts == 0)
        {
            Miss();
            return;
        }
        Stuck.Add(new StuckArrow(offset, pts));
        Points += pts;
        LastPoints = pts;
        Cue(AudioCue.SfxArrowHit);
        if (pts == 10)
        {
            _tens++;
            if (_tens % Rules.ArcheryBullseyeStreak == 0) AwardPlainBonus(a.PlayerIndex, Rules.BonusPoints);
        }
        else _tens = 0;
        State = ArcheryState.Scored;
        _time = 0;
    }

    private void Miss()
    {
        LastPoints = 0;
        _tens = 0;
        Cue(AudioCue.SfxLand);
        State = ArcheryState.Scored;
        _time = 0;
    }
}
