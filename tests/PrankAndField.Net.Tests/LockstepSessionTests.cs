using PrankAndField.Core;
using PrankAndField.Core.Tests.Support;

namespace PrankAndField.Net.Tests;

public class LockstepSessionTests
{
    private const int MaxFrames = 60 * 900;

    private static (LockstepSession P1, LockstepSession P2) Match(InMemoryLink link, uint seed = 21, uint? seed2 = null, int delay = 4) =>
        (new LockstepSession(new MatchSettings(seed, 0, delay), link.A),
         new LockstepSession(new MatchSettings(seed2 ?? seed, 1, delay), link.B));

    /// <summary>One frame of both peers: each samples its own bot, then the link moves messages along.</summary>
    private static void Frame(InMemoryLink link, LockstepSession a, Bot botA, LockstepSession b, Bot botB)
    {
        a.TryStep(botA.Next(a.Game).ToMask());
        b.TryStep(botB.Next(b.Game).ToMask());
        link.Pump();
    }

    [Fact]
    public void BothPeersPlayTheSameCreditOverALaggyLink()
    {
        var link = new InMemoryLink(latencyFrames: 3);
        var (p1, p2) = Match(link);
        var bot1 = new Bot(0, 11);
        var bot2 = new Bot(1, 10);

        int frames = 0;
        while (p1.Game.State is not (GameState.Ceremony or GameState.GameOver) && frames++ < MaxFrames)
            Frame(link, p1, bot1, p2, bot2);
        // Let the trailing peer catch up to the same tick.
        while (p2.CurrentTick < p1.CurrentTick && frames++ < MaxFrames)
        {
            p2.TryStep(0);
            link.Pump();
        }

        Assert.True(p1.Game.State is GameState.Ceremony or GameState.GameOver, $"stuck in {p1.Game.State}");
        Assert.True(p1.CurrentTick > 60 * 60, "the credit should last more than a minute");
        Assert.Equal(p1.CurrentTick, p2.CurrentTick);
        Assert.Equal(p1.Game.Checksum(), p2.Game.Checksum());
        Assert.False(p1.IsDesynced);
        Assert.False(p2.IsDesynced);
        Assert.Equal(2, p1.Game.PlayerCount);
        Assert.Equal(p1.Game.Players[0].Score, p2.Game.Players[0].Score);
        Assert.Equal(p1.Game.Players[1].Score, p2.Game.Players[1].Score);
    }

    [Fact]
    public void ALocalInputIsAppliedAfterTheInputDelay()
    {
        var link = new InMemoryLink();
        var (p1, p2) = Match(link, delay: 3);
        var action = new InputFrame(Action: true).ToMask();
        var entry = new List<int>();

        // P1 presses JUMP (next letter) on the first sampled frame only.
        for (int frame = 0; frame < 8; frame++)
        {
            p1.TryStep(frame == 0 ? action : 0);
            p2.TryStep(0);
            link.Pump();
            entry.Add(p1.Game.NameSlots[0].Cursor);
        }

        // Tick 3 is the first one that carries the press, so the cursor moves when it is simulated.
        Assert.Equal([0, 0, 0, 1, 1, 1, 1, 1], entry);
        Assert.Equal(p1.Game.Checksum(), p2.Game.Checksum());
    }

    [Fact]
    public void ThePeerWaitsWhenTheRemoteInputIsLate()
    {
        var link = new InMemoryLink { Paused = true };
        var (p1, p2) = Match(link, delay: 2);

        for (int i = 0; i < 10; i++)
        {
            p1.TryStep(0);
            p2.TryStep(0);
            link.Pump();
        }

        // Only the pre-filled delay ticks can run without the other side.
        Assert.Equal(2, p1.CurrentTick);
        Assert.True(p1.IsWaitingForRemote);
        Assert.Equal(8, p1.StalledSteps);
        // It stops sampling once it is a whole delay ahead.
        Assert.Equal(p1.CurrentTick + 3, p1.NextLocalTick);

        link.Paused = false;
        for (int i = 0; i < 5; i++)
        {
            p1.TryStep(0);
            p2.TryStep(0);
            link.Pump();
        }
        Assert.False(p1.IsWaitingForRemote);
        Assert.Equal(p1.CurrentTick, p2.CurrentTick);
        Assert.Equal(p1.Game.Checksum(), p2.Game.Checksum());
    }

    [Fact]
    public void DifferentSimulationsAreReportedAsADesync()
    {
        var link = new InMemoryLink(latencyFrames: 1);
        var (p1, p2) = Match(link, seed: 21, seed2: 22);
        var bot1 = new Bot(0, 11);
        var bot2 = new Bot(1, 10);
        int? reported = null;
        p1.DesyncDetected += tick => reported = tick;

        for (int frame = 0; frame < 60 * 120 && !p1.IsDesynced; frame++)
            Frame(link, p1, bot1, p2, bot2);

        Assert.True(p1.IsDesynced);
        Assert.Equal(p1.DesyncTick, reported);
        Assert.Equal(0, p1.DesyncTick!.Value % p1.Settings.ChecksumInterval);
        // A desynced session stops simulating.
        int tick = p1.CurrentTick;
        p1.TryStep(0);
        Assert.Equal(tick, p1.CurrentTick);
    }

    [Fact]
    public void AdvanceRunsOneStepPerSixtiethOfASecond()
    {
        var link = new InMemoryLink();
        var (p1, _) = Match(link, delay: 4);
        // Without the other peer only the four delay ticks can run, out of six attempted.
        Assert.Equal(4, p1.Advance(0.1 + 1e-9, 0));
        Assert.Equal(4, p1.CurrentTick);
    }

    [Fact]
    public void TheSessionNeedsAnOnlineGame()
    {
        var link = new InMemoryLink();
        Assert.Throws<ArgumentException>(() => new LockstepSession(new MatchSettings(1, 0), link.A, new Game()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockstepSession(new MatchSettings(1, 2), link.A));
    }
}
