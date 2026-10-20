namespace PrankAndField.Core.Tests;

public class RulesTests
{
    [Theory]
    [InlineData(EventKind.Dash100m)]
    [InlineData(EventKind.LongJump)]
    [InlineData(EventKind.JavelinThrow)]
    [InlineData(EventKind.Hurdles110m)]
    [InlineData(EventKind.HammerThrow)]
    [InlineData(EventKind.HighJump)]
    public void QualifyingStandardsGetHarderEveryRound(EventKind kind)
    {
        for (int round = 1; round < Rules.MaxRound; round++)
        {
            double a = Rules.QualifyingMark(kind, round);
            double b = Rules.QualifyingMark(kind, round + 1);
            Assert.True(Rules.IsBetter(kind, b, a) || a == b, $"{kind} round {round}: {a} -> {b}");
        }
        // beyond the table the last value sticks
        Assert.Equal(Rules.QualifyingMark(kind, Rules.MaxRound), Rules.QualifyingMark(kind, Rules.MaxRound + 5));
    }

    [Fact]
    public void RoundOneMatchesTheArcadeDefaults()
    {
        Assert.Equal(16.00, Rules.QualifyingMark(EventKind.Dash100m, 1));
        Assert.Equal(6.00, Rules.QualifyingMark(EventKind.LongJump, 1));
        Assert.Equal(60.00, Rules.QualifyingMark(EventKind.JavelinThrow, 1));
        Assert.Equal(14.50, Rules.QualifyingMark(EventKind.Hurdles110m, 1));
        Assert.Equal(73.00, Rules.QualifyingMark(EventKind.HammerThrow, 1));
        Assert.Equal(2.30, Rules.QualifyingMark(EventKind.HighJump, 1));
    }

    [Fact]
    public void TimedEventsQualifyWhenFasterFieldEventsWhenFarther()
    {
        Assert.True(Rules.Qualifies(EventKind.Dash100m, 1, 15.99));
        Assert.True(Rules.Qualifies(EventKind.Dash100m, 1, 16.00));
        Assert.False(Rules.Qualifies(EventKind.Dash100m, 1, 16.01));
        Assert.True(Rules.Qualifies(EventKind.LongJump, 1, 6.00));
        Assert.False(Rules.Qualifies(EventKind.LongJump, 1, 5.99));
    }

    [Fact]
    public void SnapKeepsTwoDecimals()
    {
        Assert.Equal(9.95, Rules.Snap(9.949));
        Assert.Equal(9.95, Rules.Snap(9.9500001));
    }

    [Fact]
    public void ScoringFollowsTheArcadeScale()
    {
        Assert.Equal(6200, Scoring.PointsFor(EventKind.Dash100m, 13.80));
        Assert.Equal(0, Scoring.PointsFor(EventKind.Dash100m, 25));
        Assert.True(Scoring.PointsFor(EventKind.LongJump, 7.5) > Scoring.PointsFor(EventKind.LongJump, 6.0));
        Assert.Equal(0, Scoring.PointsFor(EventKind.JavelinThrow, 50) % 10);
    }
}

public class WorldRecordsTests
{
    [Fact]
    public void DefaultsAreSortedBestFirst()
    {
        var r = new WorldRecords();
        foreach (var kind in EventKindExtensions.InOrder)
        {
            var row = r.For(kind);
            Assert.Equal(3, row.Count);
            for (int i = 1; i < row.Count; i++)
                Assert.False(Rules.IsBetter(kind, row[i].Mark, row[i - 1].Mark), $"{kind} not sorted");
        }
        Assert.Equal(9.95, r.Best(EventKind.Dash100m).Mark);
        Assert.Equal(8.90, r.Best(EventKind.LongJump).Mark);
    }

    [Fact]
    public void SubmitInsertsAndShiftsTheBoard()
    {
        var r = new WorldRecords();
        Assert.Equal(0, r.RankOf(EventKind.Dash100m, 10.50));
        Assert.Equal(2, r.RankOf(EventKind.Dash100m, 9.98));
        Assert.Equal(2, r.Submit(EventKind.Dash100m, 9.98, "abc"));
        var row = r.For(EventKind.Dash100m);
        Assert.Equal((9.95, "EEE"), (row[0].Mark, row[0].Initials));
        Assert.Equal((9.98, "ABC"), (row[1].Mark, row[1].Initials));
        Assert.Equal((10.00, "FFF"), (row[2].Mark, row[2].Initials));
        Assert.True(r.IsWorldRecord(EventKind.LongJump, 9.00));
        Assert.Equal(1, r.Submit(EventKind.LongJump, 9.00, "WOW"));
        Assert.Equal(9.00, r.Best(EventKind.LongJump).Mark);
    }

    [Fact]
    public void JsonRoundTripsAndRejectsGarbage()
    {
        var r = new WorldRecords();
        r.Submit(EventKind.HammerThrow, 90.12, "HAM");
        string json = r.ToJson();
        var r2 = new WorldRecords();
        Assert.True(r2.TryLoadJson(json));
        Assert.Equal(90.12, r2.Best(EventKind.HammerThrow).Mark);
        Assert.Equal("HAM", r2.Best(EventKind.HammerThrow).Initials);

        var r3 = new WorldRecords();
        Assert.False(r3.TryLoadJson("not json"));
        Assert.False(r3.TryLoadJson("{\"v\":1,\"records\":[]}"));
        Assert.False(r3.TryLoadJson(null));
        Assert.Equal(9.95, r3.Best(EventKind.Dash100m).Mark);
    }

    [Theory]
    [InlineData("ab", "ABA")]
    [InlineData("", "AAA")]
    [InlineData("hello", "HEL")]
    [InlineData("  xyz ", "XYZ")]
    public void InitialsAreNormalized(string input, string expected) => Assert.Equal(expected, WorldRecords.NormalizeInitials(input));
}

public class PlayerTests
{
    [Fact]
    public void ExtraChanceEveryHundredThousand()
    {
        var p = new Player(0);
        Assert.Equal(Rules.InitialChances, p.Chances);
        Assert.False(p.AddScore(60_000));
        Assert.True(p.AddScore(45_000));
        Assert.Equal(Rules.InitialChances + 1, p.Chances);
        Assert.True(p.AddScore(200_000));
        Assert.Equal(Rules.InitialChances + 3, p.Chances);
        Assert.False(p.AddScore(0));
    }
}
