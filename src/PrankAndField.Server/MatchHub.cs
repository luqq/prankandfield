using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace PrankAndField.Server;

/// <summary>What the hub sends to clients. Method names are the wire contract with PrankAndField.Net.Client.</summary>
public static class HubEvents
{
    public const string MatchStarted = "MatchStarted";
    public const string Input = "Input";
    public const string Checksum = "Checksum";
    public const string OpponentLeft = "OpponentLeft";
}

public sealed record RoomCreated(string Code);

public sealed record JoinResult(bool Ok, string? Error);

/// <summary>Sent to each peer once both seats are taken.</summary>
public sealed record MatchStart(uint Seed, int Seat, int InputDelay);

/// <summary>
/// Relay hub: private rooms by code, then plain forwarding of inputs and checksums to the opponent.
/// The seat of every message comes from the server's own records, never from the client.
/// </summary>
public sealed class MatchHub(RoomRegistry rooms, IOptions<RelayOptions> options) : Hub
{
    public const string Path = "/hub";
    public const int MaxMasksPerMessage = 120;

    public RoomCreated CreateRoom()
    {
        var room = rooms.Create(Context.ConnectionId);
        return new RoomCreated(room.Code);
    }

    public async Task<JoinResult> JoinRoom(string code)
    {
        var error = rooms.TryJoin(code ?? "", Context.ConnectionId, out var room);
        if (error != JoinError.None) return new JoinResult(false, error.ToString());

        int delay = Math.Clamp(options.Value.InputDelay, 1, 30);
        await Clients.Client(room!.Seats[0]!).SendAsync(HubEvents.MatchStarted, new MatchStart(room.Seed, 0, delay));
        await Clients.Caller.SendAsync(HubEvents.MatchStarted, new MatchStart(room.Seed, 1, delay));
        return new JoinResult(true, null);
    }

    public Task SendInput(int firstTick, int[] masks)
    {
        if (masks is null || masks.Length is 0 or > MaxMasksPerMessage || firstTick < 0) throw new HubException("Invalid input batch.");
        if (!rooms.TryGetSeat(Context.ConnectionId, out var room, out int seat) || room.Opponent(seat) is not { } opponent)
            return Task.CompletedTask;
        return Clients.Client(opponent).SendAsync(HubEvents.Input, seat, firstTick, masks);
    }

    public Task SendChecksum(int tick, ulong checksum)
    {
        if (!rooms.TryGetSeat(Context.ConnectionId, out var room, out int seat) || room.Opponent(seat) is not { } opponent)
            return Task.CompletedTask;
        return Clients.Client(opponent).SendAsync(HubEvents.Checksum, seat, tick, checksum);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (rooms.Leave(Context.ConnectionId) is { } opponent)
            await Clients.Client(opponent).SendAsync(HubEvents.OpponentLeft);
        await base.OnDisconnectedAsync(exception);
    }
}
