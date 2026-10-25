using PrankAndField.Core;
using PrankAndField.Core.Localization;
using PrankAndField.Net;
using PrankAndField.Net.Client;

namespace PrankAndField.UI;

public enum OnlineStatus
{
    Connecting,
    WaitingForOpponent,
    Playing,
    Finished,
    OpponentLeft,
    Desynced,
    Failed,
}

/// <summary>
/// One online match seen from the UI: the relay connection, the room code while waiting and, once both
/// seats are taken, the <see cref="LockstepSession"/> that <see cref="GameHost"/> advances every frame.
/// </summary>
public sealed class OnlineMatch : IAsyncDisposable
{
    /// <summary>Ticks without the opponent's input before the UI shows that it is waiting.</summary>
    public const int LagNoticeTicks = 20;

    private readonly RelayClient _client;
    private readonly Localizer _localizer;
    private readonly CancellationTokenSource _cts = new();

    private OnlineMatch(Uri hubUrl, Localizer localizer)
    {
        _client = new RelayClient(hubUrl);
        _localizer = localizer;
        _client.OpponentLeft += () =>
        {
            if (Status is OnlineStatus.Playing or OnlineStatus.WaitingForOpponent) Status = OnlineStatus.OpponentLeft;
        };
    }

    public OnlineStatus Status { get; private set; } = OnlineStatus.Connecting;

    /// <summary>Room code to share, known once the room exists (both for the host and the guest).</summary>
    public string? Code { get; private set; }

    /// <summary>Why the connection failed, when <see cref="Status"/> is <see cref="OnlineStatus.Failed"/>.</summary>
    public string? Error { get; private set; }

    public LockstepSession? Session { get; private set; }

    /// <summary>1-based player number of this browser, once the match started.</summary>
    public int? PlayerNumber => Session is null ? null : Session.LocalSeat + 1;

    /// <summary>True while the simulation is held back by the opponent's late inputs.</summary>
    public bool IsLagging => Status == OnlineStatus.Playing && Session is { StalledSteps: >= LagNoticeTicks };

    public bool IsOver => Status is OnlineStatus.Finished or OnlineStatus.OpponentLeft or OnlineStatus.Desynced or OnlineStatus.Failed;

    /// <summary>Opens a room as P1. Returns immediately; follow <see cref="Status"/>.</summary>
    public static OnlineMatch Create(Uri hubUrl, Localizer localizer)
    {
        var match = new OnlineMatch(hubUrl, localizer);
        _ = match.RunAsync(null);
        return match;
    }

    /// <summary>Joins an existing room as P2. Returns immediately; follow <see cref="Status"/>.</summary>
    public static OnlineMatch Join(Uri hubUrl, string code, Localizer localizer)
    {
        var match = new OnlineMatch(hubUrl, localizer);
        _ = match.RunAsync(code.Trim().ToUpperInvariant());
        return match;
    }

    private async Task RunAsync(string? joinCode)
    {
        try
        {
            await _client.StartAsync(_cts.Token);
            if (joinCode is null)
            {
                Code = await _client.CreateRoomAsync(_cts.Token);
            }
            else
            {
                Code = joinCode;
                await _client.JoinRoomAsync(joinCode, _cts.Token);
            }
            Status = OnlineStatus.WaitingForOpponent;

            var settings = await _client.WaitForMatchAsync(_cts.Token);
            var game = new Game(_localizer, new WorldRecords(), settings.Seed, online: true);
            var session = new LockstepSession(settings, _client.Transport, game);
            session.DesyncDetected += _ => Status = OnlineStatus.Desynced;
            Session = session;
            if (Status == OnlineStatus.WaitingForOpponent) Status = OnlineStatus.Playing;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = OnlineStatus.Failed;
        }
    }

    /// <summary>
    /// Advances the match by wall-clock time with this browser's input. Both local control sets drive the
    /// local seat: online there is one player per browser.
    /// </summary>
    public void Advance(double seconds, int localMask)
    {
        if (Status != OnlineStatus.Playing || Session is null) return;
        Session.Advance(seconds, localMask);
        // After the game over the credit returns to the title screen: the match is over.
        if (Session.Game.State == GameState.Title) Status = OnlineStatus.Finished;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _client.DisposeAsync(); }
        catch (Exception) { /* closing anyway */ }
        _cts.Dispose();
    }
}
