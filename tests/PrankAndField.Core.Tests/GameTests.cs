using PrankAndField.Core.Events;
using PrankAndField.Core.Localization;
using PrankAndField.Core.Tests.Support;

namespace PrankAndField.Core.Tests;

public class GameTests
{
    [Fact]
    public void StartsOnTheTitleWithTheTitleSong()
    {
        var game = new Game();
        Assert.Equal(GameState.Title, game.State);
        var cues = new List<AudioCue>();
        while (game.TryDequeueCue(out var c)) cues.Add(c);
        Assert.Contains(AudioCue.SongTitle, cues);
    }

    [Fact]
    public void TitleMenuTogglesPlayersAndStartGoesToNameEntry()
    {
        var game = new Game();
        game.Tick(new InputFrame(Down: true), InputFrame.None);
        game.Tick(InputFrame.None, InputFrame.None);
        Assert.Equal(1, game.MenuSelection);
        game.Tick(new InputFrame(Start: true), InputFrame.None);
        Assert.Equal(GameState.NameEntry, game.State);
        Assert.Equal(2, game.PlayerCount);
    }

    [Fact]
    public void NameEntryCyclesLettersAndConfirmsWithAction()
    {
        var game = new Game();
        game.Tick(new InputFrame(Start: true), InputFrame.None);
        game.Tick(InputFrame.None, InputFrame.None);
        Assert.Equal(GameState.NameEntry, game.State);
        // next letter twice, previous once -> B
        Press(game, new InputFrame(Run2: true));
        Press(game, new InputFrame(Run2: true));
        Press(game, new InputFrame(Run1: true));
        Assert.Equal("BAA", game.EntryText);
        Press(game, new InputFrame(Action: true));
        Press(game, new InputFrame(Run2: true));
        Press(game, new InputFrame(Action: true));
        Press(game, new InputFrame(Action: true));
        Assert.Equal("BBA", game.Players[0].Name);
        Assert.Equal(GameState.EventIntro, game.State);
        Assert.Equal(EventKind.Dash100m, game.CurrentEvent);
    }

    [Fact]
    public void AFullRoundReachesTheMedalCeremony()
    {
        var game = new Game(seed: 42);
        var bot = new Bot(0, 11);
        var seen = new List<EventKind>();
        double elapsed = SimulationRunner.RunUntil(game, g => g.State == GameState.Ceremony, g =>
        {
            if (g.State == GameState.Event && (seen.Count == 0 || seen[^1] != g.CurrentEvent)) seen.Add(g.CurrentEvent);
            return (bot.Next(g), InputFrame.None);
        }, maxSeconds: 900);
        Assert.Equal(GameState.Ceremony, game.State);
        Assert.Equal(EventKindExtensions.InOrder, seen);
        Assert.False(game.Players[0].IsOut);
        Assert.True(game.Players[0].Score > 10_000, $"score {game.Players[0].Score}");
        Assert.True(elapsed < 900);

        // the ceremony leads to round 2 with tougher standards
        SimulationRunner.RunUntil(game, g => g.State == GameState.EventIntro, g => (InputFrame.None, InputFrame.None), 20);
        Assert.Equal(2, game.Round);
        Assert.Equal(EventKind.Dash100m, game.CurrentEvent);
    }

    [Fact]
    public void FailingTwoEventsEndsTheGame()
    {
        var game = new Game(seed: 9);
        var bot = new Bot(0) { Lazy = true };
        SimulationRunner.RunUntil(game, g => g.State == GameState.GameOver, g => (bot.Next(g), InputFrame.None), maxSeconds: 400);
        Assert.Equal(GameState.GameOver, game.State);
        Assert.True(game.Players[0].IsOut);
        // chances start at 1: the first failure spends it, the second eliminates
        Assert.Equal(EventKind.LongJump, game.CurrentEvent);

        SimulationRunner.RunUntil(game, g => g.State == GameState.Title, g => (InputFrame.None, InputFrame.None), 20);
        Assert.Equal(GameState.Title, game.State);
    }

    [Fact]
    public void AWorldRecordIsCreditedToThePlayerNameWithoutAskingAgain()
    {
        // A board with very slow "records" so the bot breaks the dash record on its first heat.
        var easy = new WorldRecords();
        var game = new Game(new Localizer(), easy, seed: 3);
        Assert.True(easy.TryLoadJson("{\"v\":1,\"records\":[[{\"m\":30,\"i\":\"AAA\"},{\"m\":31,\"i\":\"BBB\"},{\"m\":32,\"i\":\"CCC\"}],[{\"m\":1,\"i\":\"A\"},{\"m\":0.9,\"i\":\"B\"},{\"m\":0.8,\"i\":\"C\"}],[{\"m\":1,\"i\":\"A\"},{\"m\":0.9,\"i\":\"B\"},{\"m\":0.8,\"i\":\"C\"}],[{\"m\":30,\"i\":\"A\"},{\"m\":31,\"i\":\"B\"},{\"m\":32,\"i\":\"C\"}],[{\"m\":1,\"i\":\"A\"},{\"m\":0.9,\"i\":\"B\"},{\"m\":0.8,\"i\":\"C\"}],[{\"m\":1,\"i\":\"A\"},{\"m\":0.9,\"i\":\"B\"},{\"m\":0.8,\"i\":\"C\"}]]}"));
        // name entry: B A A, then play the dash
        game.Tick(new InputFrame(Start: true), InputFrame.None);
        game.Tick(InputFrame.None, InputFrame.None);
        Assert.Equal(GameState.NameEntry, game.State);
        Press(game, new InputFrame(Run2: true));
        game.Tick(new InputFrame(Start: true), InputFrame.None);
        Assert.Equal("BAA", game.Players[0].Name);
        var bot = new Bot(0, 11);
        var states = new List<GameState>();
        SimulationRunal(game, bot, states);
        Assert.DoesNotContain(GameState.RecordEntry, states);
        Assert.True(game.RecordsDirty);
        var best = easy.Best(EventKind.Dash100m);
        Assert.Equal("BAA", best.Initials);
        Assert.Equal(game.Outcomes[0].Mark, best.Mark);
        Assert.Equal(1, game.Outcomes[0].RecordRank);
        game.MarkRecordsSaved();
        Assert.False(game.RecordsDirty);
    }

    private static void SimulationRunal(Game game, Bot bot, List<GameState> states)
    {
        SimulationRunner.RunUntil(game, g => g.State == GameState.EventOutcome, g =>
        {
            if (states.Count == 0 || states[^1] != g.State) states.Add(g.State);
            return (bot.Next(g), InputFrame.None);
        }, 120);
        Assert.Equal(GameState.EventOutcome, game.State);
        // the outcome leads straight to the next event
        SimulationRunner.RunUntil(game, g => g.State != GameState.EventOutcome, g =>
        {
            if (states[^1] != g.State) states.Add(g.State);
            return (InputFrame.None, InputFrame.None);
        }, 10);
        if (states[^1] != game.State) states.Add(game.State);
        Assert.Equal(GameState.EventIntro, game.State);
        Assert.Equal(EventKind.LongJump, game.CurrentEvent);
    }

    [Fact]
    public void TwoPlayersTakeTurnsInFieldEventsAndShareTheHeat()
    {
        var game = new Game(seed: 11);
        game.Tick(new InputFrame(Down: true), InputFrame.None);
        game.Tick(InputFrame.None, InputFrame.None);
        game.Tick(new InputFrame(Start: true), InputFrame.None);
        var b1 = new Bot(0, 11);
        var b2 = new Bot(1, 10);
        SimulationRunner.RunUntil(game, g => g.State == GameState.Event && g.CurrentEvent == EventKind.LongJump,
            g => (b1.Next(g), b2.Next(g)), 120);
        Assert.Equal(2, game.PlayerCount);
        var lj = Assert.IsType<LongJumpSimulation>(game.Simulation);
        Assert.Equal(2, lj.Athletes.Count);
        var turns = new List<(int, int)>();
        SimulationRunner.RunUntil(game, g => g.State != GameState.Event, g =>
        {
            if (g.Simulation is LongJumpSimulation s)
            {
                var t = (s.CurrentAthlete.PlayerIndex, s.CurrentTry);
                if (turns.Count == 0 || turns[^1] != t) turns.Add(t);
            }
            return (b1.Next(g), b2.Next(g));
        }, 200);
        Assert.Equal([(0, 1), (1, 1), (0, 2), (1, 2), (0, 3), (1, 3)], turns);
    }

    [Fact]
    public void HiddenPracticeMenuJumpsToAnyEventAndRound()
    {
        var game = new Game();
        Press(game, new InputFrame(Debug: true));
        Assert.Equal(GameState.PracticeMenu, game.State);
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Down: true));
        Press(game, new InputFrame(Right: true));
        Press(game, new InputFrame(Right: true));
        Assert.Equal(EventKind.JavelinThrow, game.PracticeEvent);
        Assert.Equal(3, game.PracticeRound);
        Press(game, new InputFrame(Up: true));
        Assert.Equal(EventKind.LongJump, game.PracticeEvent);
        Press(game, new InputFrame(Action: true));
        Assert.Equal(GameState.EventIntro, game.State);
        Assert.Equal(EventKind.LongJump, game.CurrentEvent);
        Assert.Equal(3, game.Round);
        Assert.Equal(1, game.PlayerCount);

        // the debug key on the menu goes back to the title
        var again = new Game();
        Press(again, new InputFrame(Debug: true));
        Press(again, new InputFrame(Debug: true));
        Assert.Equal(GameState.Title, again.State);
    }

    [Fact]
    public void FinishingARaceAnnouncesTheTime()
    {
        var game = new Game(seed: 8);
        game.JumpToEvent(EventKind.Dash100m);
        var bot = new Bot(0, 11);
        var marks = new List<MarkAnnouncement>();
        SimulationRunner.RunUntil(game, g => g.State == GameState.EventOutcome, g =>
        {
            while (g.TryDequeueMark(out var m)) marks.Add(m);
            return (bot.Next(g), InputFrame.None);
        }, 120);
        var mark = Assert.Single(marks);
        Assert.Equal(EventKind.Dash100m, mark.Kind);
        Assert.InRange(mark.Mark, 8, 12);
    }

    [Fact]
    public void LanguageCanBeSwitchedAtRuntime()
    {
        var game = new Game();
        game.SetLanguage("ja");
        Assert.Equal("ja", game.Localizer.Culture);
        Assert.Equal("100メートル走", game.Localizer[EventKind.Dash100m.Key()]);
    }

    [Fact]
    public void AdvanceUsesFixedSteps()
    {
        var game = new Game();
        game.Advance(0.1, InputFrame.None, InputFrame.None);
        Assert.Equal(6 * Game.TickSeconds, game.TotalTime, 6);
        game.Advance(10, InputFrame.None, InputFrame.None); // clamped to 0.25 s per call
        Assert.Equal(21 * Game.TickSeconds, game.TotalTime, 6);
    }

    private static void Press(Game game, InputFrame f)
    {
        game.Tick(f, InputFrame.None);
        game.Tick(InputFrame.None, InputFrame.None);
    }
}
