namespace PrankAndField.Core;

/// <summary>Persistent per-player state for one credit.</summary>
public sealed class Player
{
    public Player(int index)
    {
        Index = index;
    }

    public int Index { get; }
    public string Name { get; set; } = "AAA";
    public long Score { get; private set; }
    public int Chances { get; set; } = Rules.InitialChances;
    public bool IsOut { get; set; }
    public bool IsHuman { get; set; } = true;

    /// <summary>Best mark achieved in the current event, or null when nothing valid yet.</summary>
    public double? CurrentMark { get; set; }
    public bool QualifiedCurrent { get; set; }
    public int RecordRankCurrent { get; set; }

    private long _nextExtraChanceAt = Rules.ExtraChanceEvery;

    /// <summary>Adds points and grants extra chances every 100,000 points. Returns true if a chance was earned.</summary>
    public bool AddScore(long points)
    {
        if (points <= 0) return false;
        Score += points;
        bool earned = false;
        while (Score >= _nextExtraChanceAt)
        {
            Chances++;
            _nextExtraChanceAt += Rules.ExtraChanceEvery;
            earned = true;
        }
        return earned;
    }

    public void ResetForNewGame(string name)
    {
        Name = name;
        Score = 0;
        Chances = Rules.InitialChances;
        IsOut = false;
        CurrentMark = null;
        QualifiedCurrent = false;
        RecordRankCurrent = 0;
        _nextExtraChanceAt = Rules.ExtraChanceEvery;
    }

    public void BeginEvent()
    {
        CurrentMark = null;
        QualifiedCurrent = false;
        RecordRankCurrent = 0;
    }
}
