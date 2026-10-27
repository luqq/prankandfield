using PrankAndField.Core;
using PrankAndField.Engine.Graphics;

namespace PrankAndField.Engine.Rendering;

/// <summary>
/// Turns the current <see cref="Game"/> state into a <see cref="DrawList"/>. The renderer keeps a reusable
/// buffer and the visual-only particles (<see cref="Fx"/>); the host calls <see cref="Render"/> once per
/// displayed frame.
/// </summary>
public sealed class GameRenderer
{
    private readonly DrawList _list = new();
    private readonly Fx _fx = new();
    private readonly FxDirector _director = new();

    /// <summary>Seat played on this device in an online match (marked on shared screens), or null offline.</summary>
    public int? LocalSeat { get; set; }

    /// <summary>Particles alive right now (for tests and debugging).</summary>
    public int ParticleCount => _fx.Count;

    public DrawList Render(Game game)
    {
        var dl = _list;
        dl.Clear();
        double time = game.TotalTime;
        _director.Observe(game, _fx);
        _fx.Update(time);
        switch (game.State)
        {
            case GameState.Title:
                Scenes.DrawTitle(dl, game, time);
                break;
            case GameState.NameEntry:
                if (game.SimultaneousNameEntry) Scenes.DrawSimultaneousNameEntry(dl, game, time, LocalSeat);
                else Scenes.DrawTextEntry(dl, game, time, record: false);
                break;
            case GameState.RecordEntry:
                Scenes.DrawTextEntry(dl, game, time, record: true);
                break;
            case GameState.EventIntro:
                Scenes.DrawEventIntro(dl, game, time);
                break;
            case GameState.Event when game.Simulation is not null:
                dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
                EventRenderers.Draw(dl, game, game.Simulation, time, _fx);
                Hud.DrawScoreboard(dl, game, time);
                break;
            case GameState.EventOutcome:
                Scenes.DrawOutcome(dl, game, time);
                _fx.DrawScreen(dl);
                break;
            case GameState.Ceremony:
                Scenes.DrawCeremony(dl, game, time);
                _fx.DrawScreen(dl);
                break;
            case GameState.GameOver:
                Scenes.DrawGameOver(dl, game, time);
                break;
            case GameState.PracticeMenu:
                Scenes.DrawPracticeMenu(dl, game, time);
                break;
            default:
                dl.FillRect(0, 0, Screen.Width, Screen.Height, Palette.Black);
                break;
        }
        return dl;
    }
}
