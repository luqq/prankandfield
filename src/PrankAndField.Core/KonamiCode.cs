namespace PrankAndField.Core;

/// <summary>
/// Watches one seat's presses for the Konami code: up, up, down, down, left, right, left, right, B, A.
/// B is RUN 2 and A is RUN 1 or JUMP/THROW, which matches the face buttons of a standard gamepad.
/// </summary>
public sealed class KonamiCode
{
    private enum Token { None, Up, Down, Left, Right, B, A }

    private static readonly Token[] Sequence =
        [Token.Up, Token.Up, Token.Down, Token.Down, Token.Left, Token.Right, Token.Left, Token.Right, Token.B, Token.A];

    /// <summary>How many buttons of the sequence have been entered so far.</summary>
    public int Progress { get; private set; }

    public void Reset() => Progress = 0;

    /// <summary>Feeds this tick's presses; returns true on the tick that completes the code.</summary>
    public bool Feed(ButtonEdges e)
    {
        var token = Read(e);
        if (token == Token.None) return false;
        if (token == Sequence[Progress])
        {
            Progress++;
            if (Progress < Sequence.Length) return false;
            Progress = 0;
            return true;
        }
        // A stray up still counts as the start of a new attempt (and "up, up, up" keeps the first two).
        Progress = token == Token.Up ? (Progress == 2 ? 2 : 1) : 0;
        return false;
    }

    private static Token Read(ButtonEdges e) =>
        e.UpPressed ? Token.Up :
        e.DownPressed ? Token.Down :
        e.LeftPressed ? Token.Left :
        e.RightPressed ? Token.Right :
        e.Run2Pressed ? Token.B :
        e.Run1Pressed || e.ActionPressed ? Token.A :
        Token.None;
}
