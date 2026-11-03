using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using PrankAndField.Core;

namespace PrankAndField.Net.Client;

/// <summary>
/// Connection to the relay server (PrankAndField.Server). Create or join a room, wait for
/// <see cref="WaitForMatchAsync"/>, then hand <see cref="Transport"/> to a <see cref="LockstepSession"/>.
/// </summary>
public sealed class RelayClient : IAsyncDisposable
{
    // Wire contract with MatchHub (PrankAndField.Server).
    private sealed record RoomCreated(string Code);
    private sealed record JoinResult(bool Ok, string? Error);
    // Mode is missing from a relay that predates game modes: it only knows Track & Field.
    private sealed record MatchStart(uint Seed, int Seat, int InputDelay, int Mode = 0);

    private readonly HubConnection _connection;
    private readonly RelayTransport _transport;
    private readonly TaskCompletionSource<MatchSettings> _match = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="hubUrl">The relay hub, e.g. <c>https://paf.example.com/hub</c>.</param>
    /// <param name="accessKey">Shared key of the relay (its <c>Relay:AccessKey</c>), or null when it has none.</param>
    /// <param name="configure">Extra connection options (tests use it to reach an in-memory server).</param>
    public RelayClient(Uri hubUrl, string? accessKey = null, Action<HttpConnectionOptions>? configure = null)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(accessKey)) options.AccessTokenProvider = () => Task.FromResult<string?>(accessKey);
                configure?.Invoke(options);
            })
            .Build();
        _transport = new RelayTransport(_connection);

        _connection.On<MatchStart>("MatchStarted", m =>
        {
            if (Enum.IsDefined((GameMode)m.Mode)) _match.TrySetResult(new MatchSettings(m.Seed, m.Seat, m.InputDelay, Mode: (GameMode)m.Mode));
            else _match.TrySetException(new NotSupportedException($"The relay started a match in an unknown game mode ({m.Mode}): update the game."));
        });
        _connection.On<int, int, int[]>("Input", (seat, firstTick, masks) => _transport.Enqueue(new InputMessage(seat, firstTick, masks)));
        _connection.On<int, int, ulong>("Checksum", (seat, tick, checksum) => _transport.Enqueue(new ChecksumMessage(seat, tick, checksum)));
        _connection.On("OpponentLeft", () =>
        {
            OpponentHasLeft = true;
            OpponentLeft?.Invoke();
        });
        _connection.Closed += _ =>
        {
            _match.TrySetCanceled();
            return Task.CompletedTask;
        };
    }

    /// <summary>The match channel: inputs and checksums of the opponent arrive here.</summary>
    public IGameTransport Transport => _transport;

    public bool OpponentHasLeft { get; private set; }

    /// <summary>Raised when the other peer disconnects; the room is closed on the server.</summary>
    public event Action? OpponentLeft;

    public HubConnectionState State => _connection.State;

    public Task StartAsync(CancellationToken cancellationToken = default) => _connection.StartAsync(cancellationToken);

    /// <summary>
    /// Opens a room as P1 for a match of <paramref name="mode"/> and returns its code for the other player.
    /// A Track &amp; Field room uses the original <c>CreateRoom</c> call, so it works against any relay; the
    /// other modes need a relay that knows <c>CreateRoomWithMode</c>, otherwise this throws
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public async Task<string> CreateRoomAsync(GameMode mode = GameMode.TrackAndField, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == GameMode.TrackAndField) return (await _connection.InvokeAsync<RoomCreated>("CreateRoom", cancellationToken)).Code;
        try
        {
            return (await _connection.InvokeAsync<RoomCreated>("CreateRoomWithMode", (int)mode, cancellationToken)).Code;
        }
        catch (HubException ex) when (IsUnknownHubMethod(ex))
        {
            throw new NotSupportedException("The relay server does not know game modes yet: it must be updated before this mode can be played online.", ex);
        }
    }

    /// <summary>
    /// True when a hub call failed because the server has no such method: a relay that predates it. The server
    /// answers "Method does not exist" while binding the arguments of an unknown target (and "Unknown hub method"
    /// for a call without arguments); a <see cref="HubException"/> thrown by the method itself says something else.
    /// </summary>
    public static bool IsUnknownHubMethod(HubException ex) =>
        ex.Message.Contains("Method does not exist", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Unknown hub method", StringComparison.OrdinalIgnoreCase);

    /// <summary>Joins a room as P2. Throws when the code is unknown or the room is already full.</summary>
    public async Task JoinRoomAsync(string code, CancellationToken cancellationToken = default)
    {
        var result = await _connection.InvokeAsync<JoinResult>("JoinRoom", code, cancellationToken);
        if (!result.Ok) throw new InvalidOperationException($"Could not join room {code}: {result.Error}");
    }

    /// <summary>Completes when both seats are taken, with the seed and seat chosen by the server.</summary>
    public Task<MatchSettings> WaitForMatchAsync(CancellationToken cancellationToken = default) => _match.Task.WaitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _transport.Complete();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Queues incoming messages for the game loop and sends outgoing ones from a single loop, so they
    /// leave in the order the session produced them.
    /// </summary>
    private sealed class RelayTransport : IGameTransport
    {
        private readonly HubConnection _connection;
        private readonly ConcurrentQueue<NetMessage> _inbox = new();
        private readonly Channel<NetMessage> _outbox = Channel.CreateUnbounded<NetMessage>(new UnboundedChannelOptions { SingleReader = true });

        public RelayTransport(HubConnection connection)
        {
            _connection = connection;
            _ = Task.Run(PumpAsync);
        }

        public void Enqueue(NetMessage message) => _inbox.Enqueue(message);

        public void Send(NetMessage message) => _outbox.Writer.TryWrite(message);

        public bool TryReceive(out NetMessage message) => _inbox.TryDequeue(out message!);

        public void Complete() => _outbox.Writer.TryComplete();

        private async Task PumpAsync()
        {
            await foreach (var message in _outbox.Reader.ReadAllAsync())
            {
                try
                {
                    switch (message)
                    {
                        case InputMessage input:
                            await _connection.SendAsync("SendInput", input.FirstTick, input.Masks);
                            break;
                        case ChecksumMessage check:
                            await _connection.SendAsync("SendChecksum", check.Tick, check.Checksum);
                            break;
                    }
                }
                catch (Exception) when (_connection.State != HubConnectionState.Connected)
                {
                    // Not connected (yet or anymore): the session will stall waiting for the opponent.
                }
            }
        }
    }
}
