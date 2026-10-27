namespace PrankAndField.Core.Events;

public enum LiftState
{
    Ready,      // gripping the bar, waiting for the first RUN press
    Clean,      // alternate RUN to bring the bar to the chest before time runs out
    Jerk,       // PRESS! prompt: press JUMP/THROW in time
    Hold,       // bar overhead: tap RUN 1 / RUN 2 to keep the balance
    Lifted,     // three white lights
    Dropped,
}

/// <summary>
/// Weightlifting (clean and jerk). Alternate RUN to clean the bar within five seconds, press JUMP/THROW
/// when PRESS! shows, then hold it overhead for a few seconds nudging the balance with RUN 1 / RUN 2.
/// A good lift adds weight to the bar for the next try, like the high jump bar.
/// </summary>
public sealed class WeightliftingSimulation : FieldEventSimulation
{
    private readonly double[] _weight = new double[2];
    private int _lastButton;
    private double _cleanTime;
    private double _jerkTimer;
    private double _holdTime;
    private double _driftTimer;
    private double _drift;

    public WeightliftingSimulation(int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(EventKind.Weightlifting, round, athletes, rng)
    {
        for (int i = 0; i < 2; i++) _weight[i] = QualifyingMark;
    }

    protected override double StartX => 0;

    public LiftState State { get; private set; }
    public double Weight => _weight[CurrentAthlete.PlayerIndex];
    public double WeightFor(int playerIndex) => _weight[playerIndex];

    /// <summary>Power built up by the presses and the power the bar needs (the clean completes at 1.0).</summary>
    public double Power { get; private set; }
    public double PowerNeeded => Rules.CleanPowerNeeded(Weight);
    public double PowerFraction => Math.Clamp(Power / PowerNeeded, 0, 1);

    /// <summary>Height of the bar: 0 on the floor, 1 at the chest, 2 overhead.</summary>
    public double BarHeight => State switch
    {
        LiftState.Ready => 0,
        LiftState.Clean => PowerFraction,
        LiftState.Jerk => 1,
        LiftState.Hold or LiftState.Lifted => 2,
        _ => 0,
    };

    /// <summary>True while the PRESS! prompt shows.</summary>
    public bool JerkPrompt => State == LiftState.Jerk && _jerkTimer >= Rules.JerkDelay;

    /// <summary>Fraction of the PRESS! window still open (1 when it appears, 0 when the bar drops).</summary>
    public double JerkWindowLeft => JerkPrompt ? Math.Clamp(1 - (_jerkTimer - Rules.JerkDelay) / Rules.JerkWindow, 0, 1) : 1;

    /// <summary>Seconds left to clean the bar.</summary>
    public double CleanTimeLeft => State == LiftState.Clean ? Math.Max(0, Rules.CleanTimeLimit - _cleanTime) : Rules.CleanTimeLimit;

    /// <summary>Balance while the bar is overhead: 0 centred, beyond +/-1 the bar drops.</summary>
    public double Balance { get; private set; }

    /// <summary>Fraction of the hold completed.</summary>
    public double HoldProgress => State == LiftState.Hold ? Math.Clamp(_holdTime / Rules.HoldSeconds, 0, 1) : State == LiftState.Lifted ? 1 : 0;

    /// <summary>White lights lit by the judges (3 on a good lift).</summary>
    public int Lights { get; private set; }

    public override double SpeedReadout(int playerIndex) => State is LiftState.Ready or LiftState.Clean ? PowerFraction * 1500 : State == LiftState.Hold ? HoldProgress * 1500 : 1500;

    protected override void BeginAttempt(Athlete a)
    {
        State = LiftState.Ready;
        ShowAngle = false;
        Power = 0;
        _lastButton = 0;
        _cleanTime = 0;
        _jerkTimer = 0;
        _holdTime = 0;
        _driftTimer = 0;
        _drift = 0;
        Balance = 0;
        Lights = 0;
        a.X = 0;
        a.Runner.Locked = true;
        a.Rotation = 0;
        a.Pose = Pose.Land;
    }

    protected override void TickAttempt(double dt, Athlete a, ButtonEdges e)
    {
        switch (State)
        {
            case LiftState.Ready:
                a.Pose = Pose.Land;
                if (e.AnyRunPressed)
                {
                    State = LiftState.Clean;
                    Cue(AudioCue.SfxClank);
                    ApplyPresses(e);
                }
                break;

            case LiftState.Clean:
                _cleanTime += dt;
                Power = Math.Max(0, Power - Rules.CleanDecay * dt);
                ApplyPresses(e);
                a.Pose = PowerFraction < 0.5 ? Pose.Land : Pose.Clean;
                if (Power >= PowerNeeded)
                {
                    Power = PowerNeeded;
                    State = LiftState.Jerk;
                    _jerkTimer = 0;
                    a.Pose = Pose.Clean;
                    Cue(AudioCue.SfxClank);
                }
                else if (_cleanTime >= Rules.CleanTimeLimit)
                {
                    Drop(a);
                }
                break;

            case LiftState.Jerk:
                _jerkTimer += dt;
                a.Pose = Pose.Clean;
                if (e.ActionPressed)
                {
                    if (_jerkTimer >= Rules.JerkDelay) BeginHold(a);
                    else Drop(a); // too early
                }
                else if (_jerkTimer > Rules.JerkDelay + Rules.JerkWindow)
                {
                    Drop(a); // never pressed
                }
                break;

            case LiftState.Hold:
                _holdTime += dt;
                _driftTimer -= dt;
                if (_driftTimer <= 0)
                {
                    _driftTimer = 0.6;
                    _drift = Rng.Range(-1, 1);
                }
                // the bar drifts at random and, like an inverted pendulum, a tilt grows on its own until a press corrects it
                Balance += _drift * Rules.HoldDrift * dt + Balance * Rules.HoldInstability * dt;
                if (e.Run1Pressed) Balance -= Rules.HoldNudge;
                if (e.Run2Pressed) Balance += Rules.HoldNudge;
                a.Rotation = Balance * 12;
                a.Pose = Pose.Lift;
                if (Math.Abs(Balance) > Rules.HoldLimit)
                {
                    Drop(a);
                }
                else if (_holdTime >= Rules.HoldSeconds)
                {
                    Lifted(a);
                }
                break;
        }
    }

    private void ApplyPresses(ButtonEdges e)
    {
        if (e.Run1Pressed) Press(1);
        if (e.Run2Pressed) Press(2);
    }

    private void Press(int button)
    {
        double impulse = _lastButton == 0 || _lastButton != button ? Rules.CleanImpulseAlternate : Rules.CleanImpulseRepeat;
        _lastButton = button;
        Power += impulse;
    }

    private void BeginHold(Athlete a)
    {
        State = LiftState.Hold;
        _holdTime = 0;
        _driftTimer = 0.3;
        _drift = 0;
        Balance = 0;
        a.Pose = Pose.Lift;
        Cue(AudioCue.SfxClank);
    }

    private void Lifted(Athlete a)
    {
        State = LiftState.Lifted;
        Lights = 3;
        a.Rotation = 0;
        a.Pose = Pose.Lift;
        Cue(AudioCue.SfxLights);
        int p = a.PlayerIndex;
        double lifted = _weight[p];
        _weight[p] = Rules.Snap(_weight[p] + Rules.WeightStep);
        CompleteAttempt(Attempt.Of(lifted), 2.6);
    }

    private void Drop(Athlete a)
    {
        State = LiftState.Dropped;
        a.Rotation = 0;
        a.Pose = Pose.Collapse;
        Cue(AudioCue.SfxClank);
        CompleteAttempt(Attempt.Fouled, 2.2);
    }

    protected override void TickAfterAttempt(double dt, Athlete a)
    {
        if (State == LiftState.Lifted) a.Pose = Pose.Celebrate;
    }

    protected override void OnAttemptCompleted(Athlete a, Attempt result) => a.Runner.Locked = false;
}
