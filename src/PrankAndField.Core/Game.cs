using PrankAndField.Core.Events;
using PrankAndField.Core.Localization;

namespace PrankAndField.Core;

public enum GameState
{
    Title,
    NameEntry,
    EventIntro,
    Event,
    EventOutcome,
    RecordEntry,
    Ceremony,
    GameOver,
    PracticeMenu,
}

/// <summary>A spoken mark announcement: the event decides whether it is a time or a distance.</summary>
public readonly record struct MarkAnnouncement(EventKind Kind, double Mark);

/// <summary>Outcome of one player in the event that just ended, shown on the results card.</summary>
public sealed class EventOutcome
{
    public int PlayerIndex { get; init; }
    public double? Mark { get; init; }
    public bool Qualified { get; init; }
    public long Points { get; init; }
    public long Bonus { get; init; }
    public int RecordRank { get; init; }
    public bool ExtraChance { get; init; }
    public bool Eliminated { get; init; }
}

/// <summary>
/// Top-level state machine of a credit: title, name entry, the six events in rounds of rising
/// difficulty, medal ceremony and game over. Deterministic given the inputs and the seed.
/// </summary>
public sealed class Game
{
    public const double TickSeconds = 1.0 / 60.0;
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ.!?";

    private readonly ButtonEdges[] _edges = [new ButtonEdges(), new ButtonEdges()];
    private readonly Queue<AudioCue> _cues = new();
    private readonly Queue<MarkAnnouncement> _marks = new();
    private double _accumulator;
    private readonly uint _seed;

    public Game(Localizer? localizer = null, WorldRecords? records = null, uint seed = 12345, bool online = false)
    {
        IsOnline = online;
        Localizer = localizer ?? new Localizer();
        Records = records ?? new WorldRecords();
        _seed = seed;
        Rng = new Rng(seed);
        EnterTitle();
    }

    /// <summary>
    /// Online match: both peers run this simulation in lockstep, so the debug key (practice menu,
    /// which reseeds and changes the player count) is ignored.
    /// </summary>
    public bool IsOnline { get; }

    public Localizer Localizer { get; }
    public WorldRecords Records { get; }
    public Rng Rng { get; private set; }

    public GameState State { get; private set; }

    /// <summary>Seconds spent in the current state.</summary>
    public double StateTime { get; private set; }

    /// <summary>Seconds since the game object was created (for attract animations).</summary>
    public double TotalTime { get; private set; }

    public Player[] Players { get; } = [new Player(0), new Player(1)];
    public int PlayerCount { get; private set; } = 1;
    public int Round { get; private set; } = 1;
    public EventKind CurrentEvent { get; private set; } = EventKind.Dash100m;
    public EventSimulation? Simulation { get; private set; }
    public IReadOnlyList<EventOutcome> Outcomes { get; private set; } = [];

    /// <summary>Title menu: 0 = 1 player, 1 = 2 players.</summary>
    public int MenuSelection { get; private set; }

    /// <summary>Hidden practice menu selection (opened with the debug key on the title).</summary>
    public EventKind PracticeEvent { get; private set; } = EventKind.Dash100m;
    public int PracticeRound { get; private set; } = 1;

    /// <summary>Spoken mark announcements produced by the events (dequeued by the audio layer).</summary>
    public bool TryDequeueMark(out MarkAnnouncement mark) => _marks.TryDequeue(out mark);

    // --- text entry (names and record initials) ---
    public int EntryPlayer { get; private set; }
    public char[] EntryLetters { get; } = ['A', 'A', 'A'];
    public int EntryCursor { get; private set; }
    public int EntryRecordRank { get; private set; }
    public double EntryMark { get; private set; }

    /// <summary>Set when the record board changed; the host saves it and calls <see cref="MarkRecordsSaved"/>.</summary>
    public bool RecordsDirty { get; private set; }

    public void MarkRecordsSaved() => RecordsDirty = false;

    public IEnumerable<Player> ActivePlayers => Players.Take(PlayerCount).Where(p => !p.IsOut);

    public bool TryDequeueCue(out AudioCue cue) => _cues.TryDequeue(out cue);

    private void Cue(AudioCue cue) => _cues.Enqueue(cue);

    public void SetLanguage(string culture) => Localizer.SetCulture(culture);

    /// <summary>Advances the simulation by wall-clock seconds using fixed 60 Hz ticks.</summary>
    public void Advance(double seconds, InputFrame p1, InputFrame p2)
    {
        _accumulator += Math.Min(seconds, 0.25);
        while (_accumulator >= TickSeconds)
        {
            Tick(p1, p2);
            _accumulator -= TickSeconds;
        }
    }

    /// <summary>One fixed step of 1/60 s.</summary>
    public void Tick(InputFrame p1, InputFrame p2)
    {
        _edges[0].Update(p1);
        _edges[1].Update(p2);
        StateTime += TickSeconds;
        TotalTime += TickSeconds;

        switch (State)
        {
            case GameState.Title: TickTitle(); break;
            case GameState.NameEntry: TickTextEntry(isRecord: false); break;
            case GameState.EventIntro: if (StateTime >= 3.2 || AnyStart()) StartEvent(); break;
            case GameState.Event: TickEvent(); break;
            case GameState.EventOutcome: if (StateTime >= 3.6 || (StateTime > 0.8 && AnyStart())) AfterOutcome(); break;
            case GameState.RecordEntry: TickTextEntry(isRecord: true); break;
            case GameState.Ceremony: if (StateTime >= 7.0 || (StateTime > 1.0 && AnyStart())) NextRound(); break;
            case GameState.GameOver: if (StateTime >= 5.0 || (StateTime > 1.0 && AnyStart())) EnterTitle(); break;
            case GameState.PracticeMenu: TickPracticeMenu(); break;
        }
    }

    private void TickPracticeMenu()
    {
        var e = _edges[0];
        int count = EventKindExtensions.InOrder.Length;
        if (e.DownPressed || e.Run2Pressed) { PracticeEvent = (EventKind)(((int)PracticeEvent + 1) % count); Cue(AudioCue.SfxMenuMove); }
        if (e.UpPressed || e.Run1Pressed) { PracticeEvent = (EventKind)(((int)PracticeEvent + count - 1) % count); Cue(AudioCue.SfxMenuMove); }
        if (e.RightPressed) { PracticeRound = Math.Min(Rules.MaxRound, PracticeRound + 1); Cue(AudioCue.SfxMenuMove); }
        if (e.LeftPressed) { PracticeRound = Math.Max(1, PracticeRound - 1); Cue(AudioCue.SfxMenuMove); }
        if (e.DebugPressed) { EnterTitle(); return; }
        if (e.StartPressed || e.ActionPressed)
        {
            Cue(AudioCue.SfxMenuSelect);
            JumpToEvent(PracticeEvent, PracticeRound, MenuSelection + 1);
        }
    }

    private bool AnyStart() => _edges[0].StartPressed || _edges[1].StartPressed;

    private void SetState(GameState s)
    {
        State = s;
        StateTime = 0;
    }

    // ------------------------------------------------------------------ title

    private void EnterTitle()
    {
        Simulation = null;
        Outcomes = [];
        SetState(GameState.Title);
        Cue(AudioCue.SongTitle);
        Cue(AudioCue.VoiceTitle);
    }

    private void TickTitle()
    {
        var e0 = _edges[0];
        var e1 = _edges[1];
        if (e0.DebugPressed && !IsOnline)
        {
            SetState(GameState.PracticeMenu);
            Cue(AudioCue.SfxMenuSelect);
            return;
        }
        if (e0.UpPressed || e0.DownPressed || e0.Run1Pressed || e0.Run2Pressed || e1.UpPressed || e1.DownPressed)
        {
            MenuSelection = 1 - MenuSelection;
            Cue(AudioCue.SfxMenuMove);
        }
        if (e0.StartPressed || e0.ActionPressed || e1.StartPressed)
        {
            PlayerCount = MenuSelection + 1;
            if (e1.StartPressed) PlayerCount = 2;
            Cue(AudioCue.SfxMenuSelect);
            BeginGame();
        }
    }

    /// <summary>
    /// Online match: skips the title menu and starts a two-player credit at name entry. Both peers call it
    /// on the same tick so their simulations stay in lockstep.
    /// </summary>
    public void StartOnlineCredit()
    {
        if (!IsOnline) throw new InvalidOperationException("Only online games skip the title.");
        MenuSelection = 1;
        PlayerCount = 2;
        BeginGame();
    }

    private void BeginGame()
    {
        Rng = new Rng(_seed + (uint)(TotalTime * 1000));
        Round = 1;
        for (int i = 0; i < Players.Length; i++)
        {
            Players[i].ResetForNewGame("AAA");
            Players[i].IsOut = i >= PlayerCount;
            Players[i].IsHuman = true;
        }
        EntryPlayer = 0;
        StartTextEntry(Players[0].Name);
        SetState(GameState.NameEntry);
        Cue(AudioCue.SongNameEntry);
    }

    // ------------------------------------------------------------------ text entry

    private void StartTextEntry(string initial)
    {
        string s = WorldRecords.NormalizeInitials(initial);
        for (int i = 0; i < 3; i++) EntryLetters[i] = Alphabet.Contains(s[i]) ? s[i] : 'A';
        EntryCursor = 0;
    }

    public string EntryText => new(EntryLetters);

    private void TickTextEntry(bool isRecord)
    {
        var e = _edges[Math.Min(EntryPlayer, 1)];
        var other = _edges[1 - Math.Min(EntryPlayer, 1)];
        int idx = Alphabet.IndexOf(EntryLetters[EntryCursor]);
        if (e.Run2Pressed || e.RightPressed || e.UpPressed)
        {
            EntryLetters[EntryCursor] = Alphabet[(idx + 1) % Alphabet.Length];
            Cue(AudioCue.SfxMenuMove);
        }
        else if (e.Run1Pressed || e.LeftPressed || e.DownPressed)
        {
            EntryLetters[EntryCursor] = Alphabet[(idx - 1 + Alphabet.Length) % Alphabet.Length];
            Cue(AudioCue.SfxMenuMove);
        }

        bool accept = e.StartPressed || other.StartPressed;
        if (e.ActionPressed)
        {
            Cue(AudioCue.SfxMenuSelect);
            EntryCursor++;
            if (EntryCursor >= 3) accept = true;
        }
        if (!accept) return;

        string text = EntryText;
        if (isRecord)
        {
            // Kept for completeness: records are now credited to the name chosen at the start.
            Records.Submit(CurrentEvent, EntryMark, text);
            RecordsDirty = true;
            Players[EntryPlayer].Name = text;
            Cue(AudioCue.SfxMenuSelect);
            ContinueAfterEvent();
        }
        else
        {
            Players[EntryPlayer].Name = text;
            EntryPlayer++;
            if (EntryPlayer < PlayerCount)
            {
                StartTextEntry(Players[EntryPlayer].Name);
                StateTime = 0;
            }
            else
            {
                EnterEventIntro(EventKind.Dash100m);
            }
        }
    }

    // ------------------------------------------------------------------ events

    private void EnterEventIntro(EventKind kind)
    {
        CurrentEvent = kind;
        foreach (var p in Players) p.BeginEvent();
        Simulation = null;
        SetState(GameState.EventIntro);
        Cue(AudioCue.SongEventIntro);
    }

    private void StartEvent()
    {
        var humans = ActivePlayers.ToList();
        var athletes = new List<Athlete>();
        if (CurrentEvent.IsTimed())
        {
            foreach (var p in humans) athletes.Add(new Athlete(p.Index, false, p.Name));
            if (athletes.Count == 1)
            {
                int cpuIndex = 1 - athletes[0].PlayerIndex;
                athletes.Add(new Athlete(cpuIndex, true, "CPU"));
            }
            athletes.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
        }
        else
        {
            foreach (var p in humans) athletes.Add(new Athlete(p.Index, false, p.Name));
        }

        Simulation = CreateSimulation(CurrentEvent, Round, athletes, Rng);
        for (int i = 0; i < 2; i++) _edges[i].Reset();
        SetState(GameState.Event);
        Cue(AudioCue.SongEventStart);
    }

    public static EventSimulation CreateSimulation(EventKind kind, int round, IReadOnlyList<Athlete> athletes, Rng rng) => kind switch
    {
        EventKind.Dash100m => new DashSimulation(round, athletes, rng),
        EventKind.LongJump => new LongJumpSimulation(round, athletes, rng),
        EventKind.JavelinThrow => new JavelinSimulation(round, athletes, rng),
        EventKind.Hurdles110m => new HurdlesSimulation(round, athletes, rng),
        EventKind.HammerThrow => new HammerSimulation(round, athletes, rng),
        EventKind.HighJump => new HighJumpSimulation(round, athletes, rng),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private void TickEvent()
    {
        var sim = Simulation!;
        sim.Tick(TickSeconds, _edges[0], _edges[1]);
        while (sim.TryDequeueCue(out var cue))
        {
            if (cue == AudioCue.VoiceMark) _marks.Enqueue(new MarkAnnouncement(CurrentEvent, sim.LastMarkForVoice));
            else Cue(cue);
        }
        if (sim.IsFinished) FinishEvent();
    }

    private void FinishEvent()
    {
        var sim = Simulation!;
        var outcomes = new List<EventOutcome>();
        bool anyQualified = false;
        bool anyRecord = false;
        foreach (var p in ActivePlayers.ToList())
        {
            double? mark = sim.MarkFor(p.Index);
            bool qualified = sim.DidQualify(p.Index);
            long points = mark.HasValue ? Scoring.PointsFor(CurrentEvent, mark.Value) : 0;
            long bonus = sim.BonusPoints[p.Index];
            // Records go on the board right away under the name given at the start of the credit.
            int rank = mark.HasValue ? Records.Submit(CurrentEvent, mark.Value, p.Name) : 0;
            if (rank > 0) RecordsDirty = true;
            if (rank == 1) { points += Rules.WorldRecordBonus; anyRecord = true; }
            bool extra = p.AddScore(points + bonus);
            p.CurrentMark = mark;
            p.QualifiedCurrent = qualified;
            p.RecordRankCurrent = rank;
            bool eliminated = false;
            if (!qualified)
            {
                p.Chances--;
                if (p.Chances < 0)
                {
                    p.IsOut = true;
                    eliminated = true;
                }
            }
            anyQualified |= qualified;
            outcomes.Add(new EventOutcome
            {
                PlayerIndex = p.Index,
                Mark = mark,
                Qualified = qualified,
                Points = points,
                Bonus = bonus,
                RecordRank = rank,
                ExtraChance = extra,
                Eliminated = eliminated,
            });
        }
        Outcomes = outcomes;
        SetState(GameState.EventOutcome);
        if (anyRecord)
        {
            Cue(AudioCue.SongWorldRecord);
            Cue(AudioCue.VoiceNewWorldRecord);
        }
        else if (anyQualified)
        {
            Cue(AudioCue.SongQualified);
            Cue(AudioCue.VoiceQualified);
        }
        else
        {
            Cue(AudioCue.SongFailed);
            Cue(AudioCue.VoiceNotQualified);
        }
    }

    private void AfterOutcome() => ContinueAfterEvent();

    private void ContinueAfterEvent()
    {
        Simulation = null;
        if (!ActivePlayers.Any())
        {
            SetState(GameState.GameOver);
            Cue(AudioCue.SongGameOver);
            Cue(AudioCue.VoiceGameOver);
            return;
        }
        if (CurrentEvent == EventKind.HighJump)
        {
            SetState(GameState.Ceremony);
            Cue(AudioCue.SongCeremony);
            return;
        }
        EnterEventIntro(CurrentEvent.Next());
    }

    private void NextRound()
    {
        Round = Math.Min(Round + 1, Rules.MaxRound + 10);
        EnterEventIntro(EventKind.Dash100m);
    }

    /// <summary>Champion of the current credit (highest score among the players).</summary>
    public Player Leader => Players.Take(PlayerCount).OrderByDescending(p => p.Score).First();

    /// <summary>
    /// Practice / debugging helper: starts a fresh single-player credit directly at the given event and round,
    /// skipping the title and name entry.
    /// </summary>
    public void JumpToEvent(EventKind kind, int round = 1, int players = 1)
    {
        PlayerCount = Math.Clamp(players, 1, 2);
        Rng = new Rng(_seed + (uint)(TotalTime * 1000) + 1);
        for (int i = 0; i < Players.Length; i++)
        {
            Players[i].ResetForNewGame(i == 0 ? "AAA" : "BBB");
            Players[i].IsOut = i >= PlayerCount;
        }
        Round = Math.Max(1, round);
        EnterEventIntro(kind);
    }

    /// <summary>
    /// Hash of the state that decides the outcome of a credit. Two peers that ran the same ticks with the
    /// same seed and inputs get the same value; a mismatch means their simulations diverged.
    /// </summary>
    public ulong Checksum()
    {
        var h = new StateHash();
        h.Add((int)State);
        h.Add(StateTime);
        h.Add(Rng.State);
        h.Add(Round);
        h.Add((int)CurrentEvent);
        h.Add(PlayerCount);
        h.Add(MenuSelection);
        h.Add(EntryPlayer);
        h.Add(EntryCursor);
        foreach (char c in EntryLetters) h.Add(c);
        foreach (var p in Players)
        {
            h.Add(p.Score);
            h.Add(p.Chances);
            h.Add(p.IsOut);
            h.Add(p.CurrentMark ?? -1);
            foreach (char c in p.Name) h.Add(c);
        }
        if (Simulation is { } sim)
        {
            h.Add(sim.Elapsed);
            h.Add(sim.IsFinished);
            h.Add(sim.BonusPoints[0]);
            h.Add(sim.BonusPoints[1]);
            foreach (var a in sim.Athletes)
            {
                h.Add(a.X);
                h.Add(a.Y);
                h.Add(a.Runner.SpeedCm);
                h.Add((int)a.Pose);
            }
        }
        return h.Value;
    }

    /// <summary>FNV-1a over the raw bits of each value.</summary>
    private struct StateHash
    {
        public ulong Value = 14695981039346656037UL;

        public StateHash() { }

        public void Add(long v)
        {
            for (int i = 0; i < 8; i++)
            {
                Value ^= (byte)(v >> (8 * i));
                Value *= 1099511628211UL;
            }
        }

        public void Add(int v) => Add((long)v);
        public void Add(uint v) => Add((long)v);
        public void Add(bool v) => Add(v ? 1L : 0L);
        public void Add(double v) => Add(BitConverter.DoubleToInt64Bits(v));
    }
}
