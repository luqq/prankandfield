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

    public GameHost()
    {
        Game = new Game(new Localizer(), new WorldRecords(), (uint)Environment.TickCount);
    }

    public Game Game { get; private set; }

    /// <summary>Restores persisted records and language. Safe to call before the first frame.</summary>
    public void Initialize(string? recordsJson, string? language)
    {
        if (!string.IsNullOrWhiteSpace(recordsJson)) Game.Records.TryLoadJson(recordsJson);
        if (!string.IsNullOrWhiteSpace(language)) Game.SetLanguage(language);
    }

    public void SetLanguage(string language) => Game.SetLanguage(language);

    public string Language => Game.Localizer.Culture;

    public string RecordsJson => Game.Records.ToJson();

    public string AudioCatalogJson => AudioCatalog.ToJson();

    public string VoicePhrasesJson => AudioCatalog.VoicePhrasesJson(Game.Localizer);

    /// <summary>
    /// Advances the game by <paramref name="dtSeconds"/> with the given input masks, renders a frame and
    /// returns the encoded draw list followed by pending audio events. When the record board changed,
    /// an audio-section marker event (kind 9) tells the host to persist <see cref="RecordsJson"/>.
    /// </summary>
    public double[] Frame(double dtSeconds, int p1Mask, int p2Mask)
    {
        Game.Advance(dtSeconds, InputFrame.FromMask(p1Mask), InputFrame.FromMask(p2Mask));
        _sequencer.Drain(Game);
        var dl = _renderer.Render(Game);
        var events = _sequencer.Take();
        if (Game.RecordsDirty)
        {
            Game.MarkRecordsSaved();
            var withSave = new (double, double, double)[events.Length + 1];
            events.CopyTo(withSave, 0);
            withSave[^1] = (9, 0, 0);
            events = withSave;
        }
        dl.AppendAudio(events);
        return dl.ToArray();
    }

    /// <summary>Test hook: replaces the game (e.g. to inject a seeded one).</summary>
    public void Reset(Game game) => Game = game;

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
                    snapshot["holding"] = hm.Holding;
                    snapshot["inArc"] = hm.InReleaseArc;
                    snapshot["pressZone"] = hm.InPressZone;
                    snapshot["inFront"] = hm.InFrontHalf;
                    break;
                case Core.Events.HighJumpSimulation hj:
                    snapshot["phase"] = hj.Phase.ToString();
                    snapshot["sub"] = hj.State.ToString();
                    snapshot["try"] = hj.CurrentTry;
                    snapshot["bar"] = hj.BarHeight;
                    break;
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }
}
