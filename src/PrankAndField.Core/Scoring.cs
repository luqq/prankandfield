namespace PrankAndField.Core;

/// <summary>Point values. The arcade awarded roughly (20 - time) x 1000 for a dash, so we follow that scale.</summary>
public static class Scoring
{
    public static long PointsFor(EventKind kind, double mark)
    {
        double points = kind switch
        {
            EventKind.Dash100m => (20.0 - mark) * 1000,
            EventKind.Hurdles110m => (22.0 - mark) * 1000,
            EventKind.LongJump => mark * 500,
            EventKind.JavelinThrow => mark * 50,
            EventKind.HammerThrow => mark * 50,
            EventKind.HighJump => mark * 1000,
            _ => 0,
        };
        return Math.Max(0, (long)Math.Round(points / 10.0) * 10);
    }
}
