using PrankAndField.Audio;
using PrankAndField.Core;
using PrankAndField.Core.Localization;
using PrankAndField.Engine.Graphics;
using PrankAndField.Engine.Rendering;

namespace PrankAndField.UI;

/// <summary>
/// Composes the three layers (core simulation, vector renderer, audio sequencer) behind a single
/// per-frame entry point. Both hosts (WebAssembly and desktop WebView) drive this object from JavaScript.
/// </summary>
public sealed class GameHost
{
    private static readonly Lock Sync = new();
    private static GameHost? _instance;

    private readonly GameRenderer _renderer = new();
    private readonly CueSequencer _sequencer = new();

    public static GameHost Instance
    {
        get
        {
            lock (Sync)
            {
                return _instance ??= new GameHost();
            }
        }
    }

    private Game _localGame;

    public GameHost()
    {
        _localGame = new Game(new Localizer(), new WorldRecords(), (uint)Environment.TickCount);
    }

    /// <summary>The game on screen: the online match once it started, otherwise the local one.</summary>
    public Game Game => Online?.Session?.Game ?? _localGame;

    /// <summary>Current online match, from creating or joining a room until the player leaves it.</summary>
    public OnlineMatch? Online { get; private set; }

    /// <summary>
    /// True once the practice menu (Konami code) was used in the local game: online play stays off until the
    /// page is reloaded or the game restarted.
    /// </summary>
    public bool OnlineLocked => _localGame.PracticeUsed;

    /// <summary>Opens a room on the relay at <paramref name="hubUrl"/>; the other player joins with its code.</summary>
    public OnlineMatch CreateOnline(Uri hubUrl, string? accessKey = null)
    {
        EnsureOnlineAllowed();
        return Online = OnlineMatch.Create(hubUrl, accessKey, _localGame.Localizer);
    }

    /// <summary>Joins the room <paramref name="code"/> on the relay at <paramref name="hubUrl"/>.</summary>
    public OnlineMatch JoinOnline(Uri hubUrl, string code, string? accessKey = null)
    {
        EnsureOnlineAllowed();
        return Online = OnlineMatch.Join(hubUrl, accessKey, code, _localGame.Localizer);
    }

    private void EnsureOnlineAllowed()
    {
        if (OnlineLocked) throw new InvalidOperationException("Online play is locked after using the practice menu.");
    }

    /// <summary>Closes the online match and goes back to the local game where it was left.</summary>
    public async Task LeaveOnlineAsync()
    {
        if (Online is not { } match) return;
        Online = null;
        await match.DisposeAsync();
    }

    /// <summary>Restores persisted records and language. Safe to call before the first frame.</summary>
    public void Initialize(string? recordsJson, string? language)
    {
        if (!string.IsNullOrWhiteSpace(recordsJson)) _localGame.Records.TryLoadJson(recordsJson);
        if (!string.IsNullOrWhiteSpace(language)) _localGame.SetLanguage(language);
    }

    // The online game shares the local game's localizer.
    public void SetLanguage(string language) => _localGame.SetLanguage(language);

    public string Language => Game.Localizer.Culture;

    /// <summary>The local record board; online matches play on a fresh board that is never persisted.</summary>
    public string RecordsJson => _localGame.Records.ToJson();

    public string AudioCatalogJson => AudioCatalog.ToJson();

    public string VoicePhrasesJson => AudioCatalog.VoicePhrasesJson(Game.Localizer);

    /// <summary>
    /// Advances the game by <paramref name="dtSeconds"/> with the given input masks, renders a frame and
    /// returns the encoded draw list followed by pending audio events (see <see cref="Present"/>).
    /// During an online match both masks drive this browser's seat; the local game stays paused.
    /// </summary>
    public double[] Frame(double dtSeconds, int p1Mask, int p2Mask)
    {
        if (Online is { Session: not null } match) match.Advance(dtSeconds, p1Mask | p2Mask);
        else _localGame.Advance(dtSeconds, InputFrame.FromMask(p1Mask), InputFrame.FromMask(p2Mask));
        return Present();
    }

    /// <summary>
    /// Runs exactly one fixed tick with the given input masks, without rendering. An online session calls
    /// this once per tick whose inputs both peers have confirmed, then <see cref="Present"/> once per frame.
    /// </summary>
    public void Step(int p1Mask, int p2Mask) => Game.Tick(InputFrame.FromMask(p1Mask), InputFrame.FromMask(p2Mask));

    /// <summary>
    /// Renders the current state and returns the encoded draw list followed by pending audio events.
    /// When the record board changed, an audio-section marker event (kind 9) tells the host to persist
    /// <see cref="RecordsJson"/>.
    /// </summary>
    public double[] Present()
    {
        _sequencer.Drain(Game);
        _renderer.LocalSeat = Online?.Session?.LocalSeat;
        var dl = _renderer.Render(Game);
        var events = _sequencer.Take();
        // Only the local board is persisted; an online match's board is discarded with it.
        bool saveRecords = Game.RecordsDirty && ReferenceEquals(Game, _localGame);
        if (Game.RecordsDirty) Game.MarkRecordsSaved();
        if (saveRecords)
        {
            var withSave = new (double, double, double)[events.Length + 1];
            events.CopyTo(withSave, 0);
            withSave[^1] = (9, 0, 0);
            events = withSave;
        }
        dl.AppendAudio(events);
        return dl.ToArray();
    }

    /// <summary>Test hook: replaces the local game (e.g. to inject a seeded one).</summary>
    public void Reset(Game game) => _localGame = game;

    /// <summary>Compact JSON snapshot of the simulation, for debugging and automated play-testing from JavaScript.</summary>
    public string DebugStateJson()
    {
        var g = Game;
        var sim = g.Simulation;
        var snapshot = new Dictionary<string, object?>
        {
            ["state"] = g.State.ToString(),
            ["event"] = g.CurrentEvent.ToString(),
            ["round"] = g.Round,
            ["players"] = g.PlayerCount,
            ["score"] = g.Players[0].Score,
            ["chances"] = g.Players[0].Chances,
            ["stateTime"] = Math.Round(g.StateTime, 2),
        };
        if (sim is not null)
        {
            snapshot["finished"] = sim.IsFinished;
            snapshot["angle"] = Math.Round(sim.Angle, 1);
            snapshot["banner"] = sim.Banner.ToString();
            var a = sim.AthleteFor(0) ?? sim.Athletes[0];
            snapshot["x"] = Math.Round(a.X, 2);
            snapshot["y"] = Math.Round(a.Y, 2);
            snapshot["speed"] = Math.Round(a.Runner.SpeedCm);
            snapshot["pose"] = a.Pose.ToString();
            switch (sim)
            {
                case Core.Events.HurdlesSimulation h:
                    snapshot["phase"] = h.Phase.ToString();
                    snapshot["time"] = Math.Round(h.RaceTime, 2);
                    snapshot["airborne"] = h.IsAirborne(0);
                    snapshot["fallen"] = h.FallenCount(0);
                    snapshot["nextHurdle"] = h.HurdlesFor(0).FirstOrDefault(x => x.X > a.X)?.X;
                    break;
                case Core.Events.RaceSimulation r:
                    snapshot["phase"] = r.Phase.ToString();
                    snapshot["time"] = Math.Round(r.RaceTime, 2);
                    break;
                case Core.Events.LongJumpSimulation lj:
                    snapshot["phase"] = lj.Phase.ToString();
                    snapshot["sub"] = lj.State.ToString();
                    snapshot["try"] = lj.CurrentTry;
                    snapshot["board"] = lj.BoardX;
                    break;
                case Core.Events.JavelinSimulation jv:
                    snapshot["phase"] = jv.Phase.ToString();
                    snapshot["sub"] = jv.State.ToString();
                    snapshot["try"] = jv.CurrentTry;
                    snapshot["line"] = jv.LineX;
                    break;
                case Core.Events.HammerSimulation hm:
                    snapshot["phase"] = hm.Phase.ToString();
                    snapshot["sub"] = hm.State.ToString();
                    snapshot["try"] = hm.CurrentTry;
                    snapshot["spins"] = hm.SpinsCompleted;
                    snapshot["revolutions"] = hm.Revolutions;
                    snapshot["heading"] = Core.Events.HammerSimulation.WrapDeg(hm.HammerHeading);
                    snapshot["inArc"] = hm.InReleaseArc;
                    snapshot["inFront"] = hm.InFrontHalf;
                    break;
                case Core.Events.HighJumpSimulation hj:
                    snapshot["phase"] = hj.Phase.ToString();
                    snapshot["sub"] = hj.State.ToString();
                    snapshot["try"] = hj.CurrentTry;
                    snapshot["bar"] = hj.BarHeight;
                    snapshot["barFallen"] = hj.BarFallen;
                    snapshot["zone"] = new[] { Rules.HighJumpZoneFar, Rules.HighJumpZoneNear };
                    snapshot["takeoff"] = Math.Round(hj.TakeoffX, 2);
                    snapshot["peak"] = Math.Round(hj.PeakHeight, 2);
                    break;
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }
}
