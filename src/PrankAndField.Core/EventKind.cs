namespace PrankAndField.Core;

/// <summary>The six events of the 1983 arcade, in the original order.</summary>
public enum EventKind
{
    Dash100m = 0,
    LongJump = 1,
    JavelinThrow = 2,
    Hurdles110m = 3,
    HammerThrow = 4,
    HighJump = 5,
}

public static class EventKindExtensions
{
    public static readonly EventKind[] InOrder =
    [
        EventKind.Dash100m, EventKind.LongJump, EventKind.JavelinThrow,
        EventKind.Hurdles110m, EventKind.HammerThrow, EventKind.HighJump,
    ];

    /// <summary>Running events are timed (lower is better) and run as simultaneous heats.</summary>
    public static bool IsTimed(this EventKind kind) => kind is EventKind.Dash100m or EventKind.Hurdles110m;

    /// <summary>Field events give three tries per player, played in turns.</summary>
    public static bool IsField(this EventKind kind) => !kind.IsTimed();

    public static EventKind Next(this EventKind kind) => (EventKind)(((int)kind + 1) % InOrder.Length);

    public static string Key(this EventKind kind) => kind switch
    {
        EventKind.Dash100m => "event.dash100",
        EventKind.LongJump => "event.longjump",
        EventKind.JavelinThrow => "event.javelin",
        EventKind.Hurdles110m => "event.hurdles110",
        EventKind.HammerThrow => "event.hammer",
        EventKind.HighJump => "event.highjump",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
