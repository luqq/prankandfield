namespace PrankAndField.Core.Events;

public enum BannerKind
{
    None,
    Foul,
    Mark,          // BannerValue holds the mark
    FalseStart,
    Qualified,
    NotQualified,
    Bonus,         // BannerValue holds points
    WorldRecord,
    Dizzy,
    WrongDirection,
}

/// <summary>Short-lived scripted decoration spawned by an easter egg (mole, bird...).</summary>
public sealed class FieldCritter
{
    public FieldCritter(string kind, double x, double y, double lifetime)
    {
        Kind = kind;
        X = x;
        Y = y;
        Lifetime = lifetime;
    }

    public string Kind { get; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Age { get; set; }
    public double Lifetime { get; }
    public bool Expired => Age >= Lifetime;
}

/// <summary>
/// Base class of every event simulation. Subclasses advance a fixed-step state machine and expose
/// plain state for the renderer; nothing in here knows about pixels or sounds.
/// </summary>
public abstract class EventSimulation
{
    private readonly Queue<AudioCue> _cues = new();

    protected EventSimulation(EventKind kind, int round, IReadOnlyList<Athlete> athletes, Rng rng)
    {
        Kind = kind;
        Round = round;
        Athletes = athletes;
        Rng = rng;
    }

    public EventKind Kind { get; }
    public int Round { get; }
    public IReadOnlyList<Athlete> Athletes { get; }
    protected Rng Rng { get; }

    public double QualifyingMark => Rules.QualifyingMark(Kind, Round);
    public bool IsFinished { get; protected set; }
    public double Elapsed { get; protected set; }

    public BannerKind Banner { get; protected set; }
    public double BannerValue { get; protected set; }
    public int BannerPlayer { get; protected set; }
    protected double BannerTimer { get; set; }

    public bool ShowAngle { get; protected set; }
    public double Angle { get; protected set; }

    public List<FieldCritter> Critters { get; } = new();

    /// <summary>Bonus points awarded during the event per player index (easter eggs, ties...).</summary>
    public long[] BonusPoints { get; } = new long[2];

    public Athlete? AthleteFor(int playerIndex) => Athletes.FirstOrDefault(a => a.PlayerIndex == playerIndex);

    /// <summary>Best valid mark of the player in this event, or null.</summary>
    public abstract double? MarkFor(int playerIndex);

    public bool DidQualify(int playerIndex)
    {
        var mark = MarkFor(playerIndex);
        return mark.HasValue && Rules.Qualifies(Kind, Round, mark.Value);
    }

    /// <summary>Advance by dt seconds. Inputs are indexed by player (0 = P1, 1 = P2).</summary>
    public void Tick(double dt, ButtonEdges p1, ButtonEdges p2)
    {
        if (IsFinished) return;
        Elapsed += dt;
        if (BannerTimer > 0)
        {
            BannerTimer -= dt;
            if (BannerTimer <= 0) Banner = BannerKind.None;
        }
        foreach (var c in Critters) c.Age += dt;
        Critters.RemoveAll(c => c.Expired);
        TickCore(dt, p1, p2);
    }

    protected abstract void TickCore(double dt, ButtonEdges p1, ButtonEdges p2);

    protected void Cue(AudioCue cue) => _cues.Enqueue(cue);

    public bool TryDequeueCue(out AudioCue cue) => _cues.TryDequeue(out cue);

    protected void ShowBanner(BannerKind kind, double value = 0, int player = 0, double seconds = 1.8)
    {
        Banner = kind;
        BannerValue = value;
        BannerPlayer = player;
        BannerTimer = seconds;
    }

    protected void AwardBonus(int playerIndex, long points, string critterKind, double x, double y)
    {
        BonusPoints[playerIndex] += points;
        Critters.Add(new FieldCritter(critterKind, x, y, 2.5));
        Cue(critterKind == "bird" ? AudioCue.SfxBird : AudioCue.SfxMole);
        Cue(AudioCue.SfxBonus);
        Cue(AudioCue.VoiceBonus);
    }

    protected static ButtonEdges InputFor(Athlete a, ButtonEdges p1, ButtonEdges p2) => a.PlayerIndex == 0 ? p1 : p2;

    /// <summary>Speed readout for the SPEED bar (cm/s).</summary>
    public virtual double SpeedReadout(int playerIndex) => AthleteFor(playerIndex)?.Runner.SpeedCm ?? 0;

    /// <summary>Mark carried by the most recent <see cref="AudioCue.VoiceMark"/> cue.</summary>
    public double LastMarkForVoice { get; private set; }

    protected void AnnounceMark(double mark)
    {
        LastMarkForVoice = Rules.Snap(mark);
        Cue(AudioCue.VoiceMark);
    }

    /// <summary>
    /// Height of whatever is flying (athlete or implement) as a fraction of its typical apex, or null
    /// when nothing is airborne. Drives the rising/falling flight tone.
    /// </summary>
    public virtual double? FlightHeight => null;
}
