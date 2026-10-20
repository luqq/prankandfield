namespace PrankAndField.Core.Tests;

public class RunnerTests
{
    [Fact]
    public void SteadySpeedMatchesClosedForm()
    {
        Assert.Equal(1000 / 0.9, Runner.SteadySpeedFor(10), 3);
        Assert.Equal(Runner.MaxSpeedCm, Runner.SteadySpeedFor(100));
    }

    [Theory]
    [InlineData(8, 780, 1000)]
    [InlineData(10, 1000, 1200)]
    [InlineData(13, 1250, 1442)]
    public void AlternatingTapsSettleNearTheSteadySpeed(double hz, double min, double max)
    {
        var r = new Runner();
        r.Reset();
        double clock = 0;
        int button = 1;
        for (int i = 0; i < 60 * 8; i++)
        {
            clock += Game.TickSeconds;
            if (clock >= 1.0 / hz)
            {
                clock -= 1.0 / hz;
                r.Press(button);
                button = 3 - button;
            }
            r.Integrate(Game.TickSeconds);
        }
        Assert.InRange(r.SpeedCm, min, max);
    }

    [Fact]
    public void RepeatingTheSameButtonIsMuchSlowerThanAlternating()
    {
        var alt = new Runner();
        var same = new Runner();
        for (int i = 0; i < 20; i++)
        {
            alt.Press(i % 2 == 0 ? 1 : 2);
            same.Press(1);
        }
        Assert.True(alt.SpeedCm > same.SpeedCm * 2, $"alternating {alt.SpeedCm} vs same {same.SpeedCm}");
    }

    [Fact]
    public void LockedRunnerIgnoresPressesAndKeepsSpeed()
    {
        var r = new Runner();
        r.Press(1);
        r.Press(2);
        double v = r.SpeedCm;
        r.Locked = true;
        r.Press(1);
        r.Integrate(1.0);
        Assert.Equal(v, r.SpeedCm);
        Assert.Equal(v / 100.0, r.PositionM, 6);
    }

    [Fact]
    public void SpeedDecaysToZeroWithoutInput()
    {
        var r = new Runner();
        r.Press(1);
        r.Press(2);
        r.Press(1);
        for (int i = 0; i < 60 * 15; i++) r.Integrate(Game.TickSeconds);
        Assert.Equal(0, r.SpeedCm);
    }

    [Fact]
    public void BallisticsRangeIsMaximalAtFortyFiveDegrees()
    {
        double r45 = Ballistics.Range(10, 45);
        Assert.True(r45 > Ballistics.Range(10, 30));
        Assert.True(r45 > Ballistics.Range(10, 60));
        Assert.Equal(100 / Ballistics.G, r45, 6);
    }
}
