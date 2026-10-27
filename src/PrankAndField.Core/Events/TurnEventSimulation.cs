namespace PrankAndField.Core.Events;

public enum TurnPhase
{
    Announce,   // "READY" card
    Active,     // the player's single turn in progress
    Result,     // total on screen
    Finished,
}

/// <summary>
/// Shared turn logic for the scored Hyper Sports events (skeet, archery): every athlete plays one turn, in
/// order, and the mark is the total scored in that turn. Subclasses implement the turn itself.
/// </summary>
public abstract class TurnEventSimulation : EventSimulation
{
    private readonly double?[] _scores = new double?[2];
    private int _turnIndex;
    private double _phaseTimer;
    private double _turnTime;

    protected TurnEventSimulation(EventKind kind, int round, IReadOnlyList<Athlete> athletes, Rng rng)
        : base(kind, round, athletes, rng)
    {
        if (athletes.Count == 0) throw new ArgumentException("A turn event needs at least one athlete.", nameof(athletes));
        Phase = TurnPhase.Announce;
        _phaseTimer = 1.6;
        CurrentAthlete = Athletes[0];
        CurrentAthlete.ResetForEvent(StartX);
    }

    public TurnPhase Phase { get; private set; }
    public Athlete CurrentAthlete { get; private set; }

    protected abstract double StartX { get; }

    /// <summary>Seconds a turn may last before it is closed with the points scored so far (safety net).</summary>
    protected virtual double TurnTimeout => 120;

    /// <summary>Total of a player whose turn is over, or null while it has not played.</summary>
    public override double? MarkFor(int playerIndex) => _scores[playerIndex];

    protected override void TickCore(double dt, ButtonEdges p1, ButtonEdges p2)
    {
        switch (Phase)
        {
            case TurnPhase.Announce:
                _phaseTimer -= dt;
                if (_phaseTimer <= 0)
                {
                    Phase = TurnPhase.Active;
                    _turnTime = 0;
                    BeginTurn(CurrentAthlete);
                }
                break;

            case TurnPhase.Active:
                _turnTime += dt;
                TickTurn(dt, CurrentAthlete, InputFor(CurrentAthlete, p1, p2));
                if (Phase == TurnPhase.Active && _turnTime > TurnTimeout) CompleteTurn(RunningScore);
                break;

            case TurnPhase.Result:
                TickAfterTurn(dt, CurrentAthlete);
                _phaseTimer -= dt;
                if (_phaseTimer <= 0) NextTurn();
                break;
        }
    }

    /// <summary>Points scored so far in the turn in progress.</summary>
    public abstract double RunningScore { get; }

    private void NextTurn()
    {
        _turnIndex++;
        if (_turnIndex >= Athletes.Count)
        {
            Phase = TurnPhase.Finished;
            IsFinished = true;
            return;
        }
        CurrentAthlete = Athletes[_turnIndex];
        CurrentAthlete.ResetForEvent(StartX);
        Phase = TurnPhase.Announce;
        _phaseTimer = 1.4;
        ShowAngle = false;
    }

    /// <summary>Closes the current turn with its total and shows it.</summary>
    protected void CompleteTurn(double score, double resultSeconds = 2.6)
    {
        double snapped = Rules.Snap(Math.Max(0, score));
        _scores[CurrentAthlete.PlayerIndex] = snapped;
        Phase = TurnPhase.Result;
        _phaseTimer = resultSeconds;
        ShowBanner(BannerKind.Mark, snapped, CurrentAthlete.PlayerIndex, resultSeconds);
        if (Rules.Qualifies(Kind, Round, snapped)) Cue(AudioCue.SfxCrowd);
        AnnounceMark(snapped);
        CurrentAthlete.Pose = Rules.Qualifies(Kind, Round, snapped) ? Pose.Celebrate : Pose.Idle;
        OnTurnCompleted(CurrentAthlete, snapped);
    }

    protected abstract void BeginTurn(Athlete a);
    protected abstract void TickTurn(double dt, Athlete a, ButtonEdges e);
    protected virtual void TickAfterTurn(double dt, Athlete a) { }
    protected virtual void OnTurnCompleted(Athlete a, double score) { }

    /// <summary>Pays bonus points without an animal: banner, fanfare and the announcer.</summary>
    protected void AwardPlainBonus(int playerIndex, long points)
    {
        BonusPoints[playerIndex] += points;
        ShowBanner(BannerKind.Bonus, points, playerIndex, 1.6);
        Cue(AudioCue.SfxBonus);
        Cue(AudioCue.VoiceBonus);
    }
}
