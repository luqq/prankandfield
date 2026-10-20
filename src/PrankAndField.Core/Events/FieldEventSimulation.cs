namespace PrankAndField.Core.Events;

public readonly record struct Attempt(double? Mark, bool Foul)
{
    public bool Done => Mark.HasValue || Foul;
    public static readonly Attempt Pending = new(null, false);
    public static Attempt Of(double mark) => new(Rules.Snap(mark), false);
    public static readonly Attempt Fouled = new(null, true);
}

public enum AttemptPhase
{
    Announce,   // "1ST TRY" card
    Active,     // event-specific attempt in progress
    Result,     // mark or FOUL on screen
    Finished,
}

/// <summary>
/// Shared turn logic for the four field events: each athlete gets three tries, played in turns
/// (1st try of every athlete, then 2nd...). Subclasses implement the attempt itself.
/// </summary>
public abstract class FieldEventSimulation : EventSimulation
{
    private readonly Attempt[][] _attempts;
    private int _turnIndex; // 0..(athletes*tries)
    private double _phaseTimer;
    private double _attemptTime;

    protected FieldEventSimulation(EventKind kind, int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(kind, round, athletes, rng)
    {
        if (athletes.Count == 0) throw new ArgumentException("A field event needs at least one athlete.", nameof(athletes));
        _attempts = new Attempt[2][];
        for (int i = 0; i < 2; i++)
        {
            _attempts[i] = new Attempt[Rules.TriesPerFieldEvent];
            Array.Fill(_attempts[i], Attempt.Pending);
        }
        Phase = AttemptPhase.Announce;
        _phaseTimer = 1.6;
        CurrentAthlete = Athletes[0];
        CurrentTry = 1;
        CurrentAthlete.ResetForEvent(StartX);
        AnnounceTry();
    }

    public AttemptPhase Phase { get; private set; }
    public Athlete CurrentAthlete { get; private set; }

    /// <summary>1-based try number of the current attempt.</summary>
    public int CurrentTry { get; private set; }

    protected abstract double StartX { get; }

    public IReadOnlyList<Attempt> AttemptsFor(int playerIndex) => _attempts[playerIndex];

    public override double? MarkFor(int playerIndex)
    {
        double? best = null;
        foreach (var at in _attempts[playerIndex])
        {
            if (at.Mark is double m && (best is null || m > best.Value)) best = m;
        }
        return best;
    }

    protected override void TickCore(double dt, ButtonEdges p1, ButtonEdges p2)
    {
        switch (Phase)
        {
            case AttemptPhase.Announce:
                _phaseTimer -= dt;
                if (_phaseTimer <= 0)
                {
                    Phase = AttemptPhase.Active;
                    _attemptTime = 0;
                    BeginAttempt(CurrentAthlete);
                }
                break;

            case AttemptPhase.Active:
                _attemptTime += dt;
                TickAttempt(dt, CurrentAthlete, InputFor(CurrentAthlete, p1, p2));
                if (Phase == AttemptPhase.Active && _attemptTime > Rules.FieldAttemptTimeout)
                {
                    // Nobody is playing: the attempt is forfeited so the game never hangs.
                    CurrentAthlete.Runner.SpeedCm = 0;
                    CurrentAthlete.Pose = Pose.Idle;
                    CompleteAttempt(Attempt.Fouled);
                }
                break;

            case AttemptPhase.Result:
                TickAfterAttempt(dt, CurrentAthlete);
                _phaseTimer -= dt;
                if (_phaseTimer <= 0) NextTurn();
                break;
        }
    }

    private void AnnounceTry()
    {
        Cue(CurrentTry switch
        {
            1 => AudioCue.VoiceTryNumber1,
            2 => AudioCue.VoiceTryNumber2,
            _ => AudioCue.VoiceTryNumber3,
        });
    }

    private void NextTurn()
    {
        _turnIndex++;
        int total = Athletes.Count * Rules.TriesPerFieldEvent;
        if (_turnIndex >= total)
        {
            Phase = AttemptPhase.Finished;
            IsFinished = true;
            return;
        }
        CurrentTry = _turnIndex / Athletes.Count + 1;
        CurrentAthlete = Athletes[_turnIndex % Athletes.Count];
        CurrentAthlete.ResetForEvent(StartX);
        Phase = AttemptPhase.Announce;
        _phaseTimer = 1.4;
        ShowAngle = false;
        AnnounceTry();
    }

    /// <summary>Records the outcome of the current attempt and shows the banner.</summary>
    protected void CompleteAttempt(Attempt result, double resultSeconds = 2.2)
    {
        _attempts[CurrentAthlete.PlayerIndex][CurrentTry - 1] = result;
        Phase = AttemptPhase.Result;
        _phaseTimer = resultSeconds;
        if (result.Foul)
        {
            ShowBanner(BannerKind.Foul, 0, CurrentAthlete.PlayerIndex, resultSeconds);
            Cue(AudioCue.VoiceFoul);
        }
        else
        {
            ShowBanner(BannerKind.Mark, result.Mark!.Value, CurrentAthlete.PlayerIndex, resultSeconds);
            if (Rules.Qualifies(Kind, Round, result.Mark.Value)) Cue(AudioCue.SfxCrowd);
            AnnounceMark(result.Mark.Value);
        }
        OnAttemptCompleted(CurrentAthlete, result);
    }

    protected abstract void BeginAttempt(Athlete a);
    protected abstract void TickAttempt(double dt, Athlete a, ButtonEdges e);
    protected virtual void TickAfterAttempt(double dt, Athlete a) { }
    protected virtual void OnAttemptCompleted(Athlete a, Attempt result) { }

    /// <summary>Common approach-run step: apply presses, integrate, animate.</summary>
    protected static void RunStep(Athlete a, ButtonEdges e, double dt)
    {
        double before = a.X;
        a.Runner.ApplyEdges(e);
        a.Runner.Integrate(dt);
        a.AdvanceAnim(a.X - before);
        a.Pose = a.Runner.SpeedCm <= 0 ? Pose.Idle : (a.Runner.SpeedCm < 100 ? Pose.Walk : Pose.Run);
    }
}
