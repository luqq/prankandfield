namespace PrankAndField.Core;

/// <summary>
/// Raw button state for one player during one simulation tick.
/// The arcade cabinet had RUN 1, JUMP/THROW (action) and RUN 2. We add Start and menu directions.
/// </summary>
public readonly record struct InputFrame(
    bool Run1 = false,
    bool Run2 = false,
    bool Action = false,
    bool Start = false,
    bool Up = false,
    bool Down = false,
    bool Left = false,
    bool Right = false,
    bool Debug = false)
{
    public const int Run1Bit = 1;
    public const int Run2Bit = 2;
    public const int ActionBit = 4;
    public const int StartBit = 8;
    public const int UpBit = 16;
    public const int DownBit = 32;
    public const int LeftBit = 64;
    public const int RightBit = 128;

    /// <summary>Hidden developer key: opens the practice menu from the title.</summary>
    public const int DebugBit = 256;

    public static readonly InputFrame None = new();

    public static InputFrame FromMask(int mask) => new(
        (mask & Run1Bit) != 0,
        (mask & Run2Bit) != 0,
        (mask & ActionBit) != 0,
        (mask & StartBit) != 0,
        (mask & UpBit) != 0,
        (mask & DownBit) != 0,
        (mask & LeftBit) != 0,
        (mask & RightBit) != 0,
        (mask & DebugBit) != 0);

    public int ToMask() =>
        (Run1 ? Run1Bit : 0) | (Run2 ? Run2Bit : 0) | (Action ? ActionBit : 0) | (Start ? StartBit : 0) |
        (Up ? UpBit : 0) | (Down ? DownBit : 0) | (Left ? LeftBit : 0) | (Right ? RightBit : 0) | (Debug ? DebugBit : 0);

    public bool AnyRun => Run1 || Run2;
}

/// <summary>Derives press/release edges from successive <see cref="InputFrame"/>s.</summary>
public sealed class ButtonEdges
{
    private InputFrame _prev;

    public InputFrame Current { get; private set; }
    public bool Run1Pressed { get; private set; }
    public bool Run2Pressed { get; private set; }
    public bool ActionPressed { get; private set; }
    public bool ActionReleased { get; private set; }
    public bool StartPressed { get; private set; }
    public bool UpPressed { get; private set; }
    public bool DownPressed { get; private set; }
    public bool LeftPressed { get; private set; }
    public bool RightPressed { get; private set; }
    public bool DebugPressed { get; private set; }

    public bool ActionHeld => Current.Action;
    public bool AnyRunPressed => Run1Pressed || Run2Pressed;
    public bool AnyPressed => AnyRunPressed || ActionPressed || StartPressed || UpPressed || DownPressed || LeftPressed || RightPressed;

    public void Update(InputFrame frame)
    {
        Run1Pressed = frame.Run1 && !_prev.Run1;
        Run2Pressed = frame.Run2 && !_prev.Run2;
        ActionPressed = frame.Action && !_prev.Action;
        ActionReleased = !frame.Action && _prev.Action;
        StartPressed = frame.Start && !_prev.Start;
        UpPressed = frame.Up && !_prev.Up;
        DownPressed = frame.Down && !_prev.Down;
        LeftPressed = frame.Left && !_prev.Left;
        RightPressed = frame.Right && !_prev.Right;
        DebugPressed = frame.Debug && !_prev.Debug;
        _prev = frame;
        Current = frame;
    }

    public void Reset()
    {
        _prev = default;
        Current = default;
        Run1Pressed = Run2Pressed = ActionPressed = ActionReleased = StartPressed = false;
        UpPressed = DownPressed = LeftPressed = RightPressed = DebugPressed = false;
    }
}
