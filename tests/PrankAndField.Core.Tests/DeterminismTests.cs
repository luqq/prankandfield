using PrankAndField.Core.Tests.Support;

namespace PrankAndField.Core.Tests;

/// <summary>
/// Online play runs the same simulation on both peers and only exchanges inputs, so a credit must
/// replay identically from the seed and the per-tick input masks.
/// </summary>
public class DeterminismTests
{
    private const int MaxTicks = 60 * 600;

    [Fact]
    public void ReplayingTheInputMasksReproducesEveryTick()
    {
        var (masks, checksums) = PlayTwoPlayerCredit(seed: 7);
        Assert.True(masks.Count > 60 * 60, "the credit should last more than a minute");

        var replay = new Game(seed: 7, online: true);
        for (int i = 0; i < masks.Count; i++)
        {
            replay.Tick(InputFrame.FromMask(masks[i].P1), InputFrame.FromMask(masks[i].P2));
            Assert.True(checksums[i] == replay.Checksum(), $"diverged at tick {i}");
        }
        Assert.Equal(GameState.Ceremony, replay.State);
    }

    [Fact]
    public void ADifferentSeedLeadsToADifferentChecksum()
    {
        var (masks, checksums) = PlayTwoPlayerCredit(seed: 7);
        var other = new Game(seed: 8, online: true);
        bool diverged = false;
        for (int i = 0; i < masks.Count && !diverged; i++)
        {
            other.Tick(InputFrame.FromMask(masks[i].P1), InputFrame.FromMask(masks[i].P2));
            diverged = checksums[i] != other.Checksum();
        }
        Assert.True(diverged);
    }

    [Fact]
    public void OnlineGamesIgnoreTheDebugKey()
    {
        var local = new Game();
        local.Tick(new InputFrame(Debug: true), InputFrame.None);
        Assert.Equal(GameState.PracticeMenu, local.State);

        var online = new Game(online: true);
        online.Tick(new InputFrame(Debug: true), InputFrame.None);
        Assert.Equal(GameState.Title, online.State);
    }

    [Fact]
    public void OnlineCreditsStartAtNameEntryForTwoPlayers()
    {
        var online = new Game(online: true);
        online.StartOnlineCredit();
        Assert.Equal(GameState.NameEntry, online.State);
        Assert.Equal(2, online.PlayerCount);
        Assert.Equal(0, online.EntryPlayer);

        Assert.Throws<InvalidOperationException>(() => new Game().StartOnlineCredit());
    }

    [Fact]
    public void EachOnlineSeatTypesOnlyItsOwnNameAtTheSameTime()
    {
        var game = new Game(online: true);
        game.StartOnlineCredit();
        Assert.True(game.SimultaneousNameEntry);

        void Press(InputFrame p1, InputFrame p2)
        {
            game.Tick(p1, p2);
            game.Tick(InputFrame.None, InputFrame.None);
        }

        // Both type on the same tick: each press only changes the presser's own slot.
        Press(new InputFrame(Up: true), new InputFrame(Down: true));
        Assert.Equal("BAA", game.NameSlots[0].Text);
        Assert.Equal("?AA", game.NameSlots[1].Text);

        // P2's START confirms P2 only; P1 keeps typing.
        Press(InputFrame.None, new InputFrame(Start: true));
        Assert.True(game.NameSlots[1].Done);
        Assert.False(game.NameSlots[0].Done);
        Assert.Equal(GameState.NameEntry, game.State);

        Press(InputFrame.None, new InputFrame(Up: true));
        Assert.Equal("?AA", game.NameSlots[1].Text);

        Press(new InputFrame(Action: true), InputFrame.None);
        Press(new InputFrame(Up: true), InputFrame.None);
        Press(new InputFrame(Start: true), InputFrame.None);
        Assert.Equal(GameState.EventIntro, game.State);
        Assert.Equal("BBA", game.Players[0].Name);
        Assert.Equal("?AA", game.Players[1].Name);
    }

    [Fact]
    public void OfflineTwoPlayerGamesStillTakeTurnsAtNameEntry()
    {
        var game = new Game();
        game.Tick(InputFrame.None, new InputFrame(Start: true));
        Assert.Equal(GameState.NameEntry, game.State);
        Assert.False(game.SimultaneousNameEntry);
    }

    /// <summary>Two bots play a credit up to the medal ceremony; returns the masks and checksum of every tick.</summary>
    private static (List<(int P1, int P2)> Masks, List<ulong> Checksums) PlayTwoPlayerCredit(uint seed)
    {
        var game = new Game(seed: seed, online: true);
        var bot1 = new Bot(0, 11);
        var bot2 = new Bot(1, 10);
        var masks = new List<(int, int)>();
        var checksums = new List<ulong>();

        void Step(InputFrame p1, InputFrame p2)
        {
            game.Tick(p1, p2);
            masks.Add((p1.ToMask(), p2.ToMask()));
            checksums.Add(game.Checksum());
        }

        // Select "2 players" on the title, then let the bots play.
        Step(new InputFrame(Down: true), InputFrame.None);
        Step(InputFrame.None, InputFrame.None);
        while (game.State != GameState.Ceremony && masks.Count < MaxTicks)
            Step(bot1.Next(game), bot2.Next(game));

        Assert.Equal(GameState.Ceremony, game.State);
        Assert.Equal(2, game.PlayerCount);
        return (masks, checksums);
    }
}
