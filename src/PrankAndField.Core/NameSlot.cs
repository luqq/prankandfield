namespace PrankAndField.Core;

/// <summary>The three letters one seat is typing during an online name entry.</summary>
public sealed class NameSlot
{
    public char[] Letters { get; } = ['A', 'A', 'A'];

    /// <summary>Letter being changed (0 to 2).</summary>
    public int Cursor { get; internal set; }

    /// <summary>True once the seat confirmed its name.</summary>
    public bool Done { get; internal set; }

    public string Text => new(Letters);

    internal void Reset(string initial)
    {
        string s = WorldRecords.NormalizeInitials(initial);
        for (int i = 0; i < 3; i++) Letters[i] = Game.Alphabet.Contains(s[i]) ? s[i] : 'A';
        Cursor = 0;
        Done = false;
    }
}
