namespace PrankAndField.Core;

/// <summary>
/// Every event of the two games: the six of Track &amp; Field (1983) in the original order, followed by the
/// six of Hyper Sports (1984). The numeric values are persisted in the record board, so they never change.
/// </summary>
public enum EventKind
{
    // Track & Field (1983)
    Dash100m = 0,
    LongJump = 1,
    JavelinThrow = 2,
    Hurdles110m = 3,
    HammerThrow = 4,
    HighJump = 5,

    // Hyper Sports (1984)
    Swimming100m = 6,
    SkeetShooting = 7,
    LongHorse = 8,
    Archery = 9,
    TripleJump = 10,
    Weightlifting = 11,
}

/// <summary>How a mark is measured and shown.</summary>
public enum MarkUnit
{
    /// <summary>Seconds with hundredths; lower is better ("9 SEC 95").</summary>
    Seconds,
    /// <summary>Metres with centimetres ("8 M 90").</summary>
    Metres,
    /// <summary>Judges' score with two decimals, 10.00 is perfect ("9 PTS 50").</summary>
    Score,
    /// <summary>Whole points ("48 PTS").</summary>
    Points,
    /// <summary>Whole targets hit ("16 HITS").</summary>
    Hits,
    /// <summary>Kilograms lifted ("150 KG").</summary>
    Kilograms,
}

/// <summary>The two games that share the engine: each has its own six events, standards and record rows.</summary>
public enum GameMode
{
    TrackAndField = 0,
    HyperSports = 1,
}

public static class GameModeExtensions
{
    public static readonly GameMode[] All = [GameMode.TrackAndField, GameMode.HyperSports];

    private static readonly EventKind[] TrackAndFieldEvents =
    [
        EventKind.Dash100m, EventKind.LongJump, EventKind.JavelinThrow,
        EventKind.Hurdles110m, EventKind.HammerThrow, EventKind.HighJump,
    ];

    private static readonly EventKind[] HyperSportsEvents =
    [
        EventKind.Swimming100m, EventKind.SkeetShooting, EventKind.LongHorse,
        EventKind.Archery, EventKind.TripleJump, EventKind.Weightlifting,
    ];

    /// <summary>The events of the game, in the order they are played.</summary>
    public static EventKind[] Events(this GameMode mode) => mode == GameMode.HyperSports ? HyperSportsEvents : TrackAndFieldEvents;

    public static EventKind FirstEvent(this GameMode mode) => mode.Events()[0];

    public static string Key(this GameMode mode) => mode == GameMode.HyperSports ? "mode.hypersports" : "mode.trackfield";
}

public static class EventKindExtensions
{
    /// <summary>The six Track &amp; Field events in the original order (kept for the 1983 game and older callers).</summary>
    public static readonly EventKind[] InOrder = GameMode.TrackAndField.Events();

    /// <summary>Every event of both games, Track &amp; Field first.</summary>
    public static readonly EventKind[] All = [.. GameMode.TrackAndField.Events(), .. GameMode.HyperSports.Events()];

    public static GameMode Mode(this EventKind kind) => (int)kind >= (int)EventKind.Swimming100m ? GameMode.HyperSports : GameMode.TrackAndField;

    public static MarkUnit Unit(this EventKind kind) => kind switch
    {
        EventKind.Dash100m or EventKind.Hurdles110m or EventKind.Swimming100m => MarkUnit.Seconds,
        EventKind.LongJump or EventKind.JavelinThrow or EventKind.HammerThrow or EventKind.HighJump or EventKind.TripleJump => MarkUnit.Metres,
        EventKind.LongHorse => MarkUnit.Score,
        EventKind.Archery => MarkUnit.Points,
        EventKind.SkeetShooting => MarkUnit.Hits,
        EventKind.Weightlifting => MarkUnit.Kilograms,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Timed events are measured in seconds: lower is better.</summary>
    public static bool IsTimed(this EventKind kind) => kind.Unit() == MarkUnit.Seconds;

    /// <summary>
    /// Races (and the swim) are run as simultaneous heats, with a computer opponent filling the second lane.
    /// Every other event is played in turns.
    /// </summary>
    public static bool IsSimultaneous(this EventKind kind) => kind.IsTimed();

    /// <summary>Events with three tries per player, played in turns (the field events and the Hyper Sports apparatus).</summary>
    public static bool IsField(this EventKind kind) => kind is EventKind.LongJump or EventKind.JavelinThrow or EventKind.HammerThrow
        or EventKind.HighJump or EventKind.LongHorse or EventKind.TripleJump or EventKind.Weightlifting;

    /// <summary>Whole-number marks (hits, points) are shown without decimals.</summary>
    public static bool IsWholeNumber(this EventKind kind) => kind.Unit() is MarkUnit.Points or MarkUnit.Hits or MarkUnit.Kilograms;

    /// <summary>The event that follows this one in its game, wrapping to the first.</summary>
    public static EventKind Next(this EventKind kind)
    {
        var events = kind.Mode().Events();
        int i = Array.IndexOf(events, kind);
        return events[(i + 1) % events.Length];
    }

    /// <summary>True for the last event of its game: the one that leads to the medal ceremony.</summary>
    public static bool IsLastOfGame(this EventKind kind)
    {
        var events = kind.Mode().Events();
        return events[^1] == kind;
    }

    public static string Key(this EventKind kind) => kind switch
    {
        EventKind.Dash100m => "event.dash100",
        EventKind.LongJump => "event.longjump",
        EventKind.JavelinThrow => "event.javelin",
        EventKind.Hurdles110m => "event.hurdles110",
        EventKind.HammerThrow => "event.hammer",
        EventKind.HighJump => "event.highjump",
        EventKind.Swimming100m => "event.swimming",
        EventKind.SkeetShooting => "event.skeet",
        EventKind.LongHorse => "event.longhorse",
        EventKind.Archery => "event.archery",
        EventKind.TripleJump => "event.triplejump",
        EventKind.Weightlifting => "event.weightlifting",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Localization key of the one-line instructions shown on the event card.</summary>
    public static string InstructionKey(this EventKind kind) => "instr." + kind.Key()["event.".Length..];
}
