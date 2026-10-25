using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using PrankAndField.Core;
using PrankAndField.Core.Tests.Support;
using PrankAndField.Net;
using PrankAndField.Net.Client;

namespace PrankAndField.Server.Tests;

public class RelayTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private async Task<RelayClient> ConnectAsync()
    {
        var server = factory.Server;
        var client = new RelayClient(new Uri(server.BaseAddress, MatchHub.Path), configure: options =>
        {
            options.Transports = HttpTransportType.WebSockets;
            options.SkipNegotiation = true;
            options.WebSocketFactory = async (context, ct) =>
                await server.CreateWebSocketClient().ConnectAsync(context.Uri, ct);
        });
        await client.StartAsync();
        return client;
    }

    private async Task<(RelayClient Host, RelayClient Guest, MatchSettings HostMatch, MatchSettings GuestMatch)> StartMatchAsync()
    {
        var host = await ConnectAsync();
        var guest = await ConnectAsync();
        string code = await host.CreateRoomAsync();
        await guest.JoinRoomAsync(code);
        var hostMatch = await host.WaitForMatchAsync().WaitAsync(Timeout);
        var guestMatch = await guest.WaitForMatchAsync().WaitAsync(Timeout);
        return (host, guest, hostMatch, guestMatch);
    }

    [Fact]
    public async Task HealthReportsOk()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"ok\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BothPlayersGetTheSameSeedAndTheirOwnSeat()
    {
        var (host, guest, hostMatch, guestMatch) = await StartMatchAsync();
        await using var _ = host;
        await using var __ = guest;

        Assert.Equal(hostMatch.Seed, guestMatch.Seed);
        Assert.Equal(0, hostMatch.LocalSeat);
        Assert.Equal(1, guestMatch.LocalSeat);
        Assert.Equal(4, hostMatch.InputDelay);
        Assert.Equal(hostMatch.InputDelay, guestMatch.InputDelay);
    }

    [Fact]
    public async Task UnknownRoomsCannotBeJoined()
    {
        var (host, guest, _, _) = await StartMatchAsync();
        await using var third = await ConnectAsync();
        await using var _h = host;
        await using var _g = guest;

        await Assert.ThrowsAsync<InvalidOperationException>(() => third.JoinRoomAsync("ZZZZZ"));
    }

    [Fact]
    public async Task TwoPeersPlayInLockstepThroughTheRelay()
    {
        var (host, guest, hostMatch, guestMatch) = await StartMatchAsync();
        await using var _h = host;
        await using var _g = guest;
        var p1 = new LockstepSession(hostMatch, host.Transport);
        var p2 = new LockstepSession(guestMatch, guest.Transport);
        var bot1 = new Bot(0, 11);
        var bot2 = new Bot(1, 10);

        // Name entry and the start of the 100 m dash, about 15 s of game time.
        const int Ticks = 60 * 15;
        var deadline = DateTime.UtcNow + Timeout;
        while ((p1.CurrentTick < Ticks || p2.CurrentTick < Ticks) && DateTime.UtcNow < deadline)
        {
            if (p1.CurrentTick < Ticks) p1.TryStep(bot1.Next(p1.Game).ToMask());
            if (p2.CurrentTick < Ticks) p2.TryStep(bot2.Next(p2.Game).ToMask());
            if (p1.IsWaitingForRemote || p2.IsWaitingForRemote) await Task.Delay(1);
        }

        Assert.Equal(Ticks, p1.CurrentTick);
        Assert.Equal(Ticks, p2.CurrentTick);
        Assert.Equal(p1.Game.Checksum(), p2.Game.Checksum());
        Assert.NotEqual(GameState.NameEntry, p1.Game.State);
        Assert.False(p1.IsDesynced);
        Assert.False(p2.IsDesynced);
    }

    [Fact]
    public async Task TheOpponentIsToldWhenAPlayerLeaves()
    {
        var (host, guest, _, _) = await StartMatchAsync();
        await using var _g = guest;
        var left = new TaskCompletionSource();
        guest.OpponentLeft += () => left.TrySetResult();

        await host.DisposeAsync();

        await left.Task.WaitAsync(Timeout);
        Assert.True(guest.OpponentHasLeft);
    }

    [Theory]
    [InlineData("https://luqq.github.io", true)]
    [InlineData("https://example.com", false)]
    public async Task OnlyThePagesSiteMayOpenTheHub(string origin, bool allowed)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, $"{MatchHub.Path}/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(allowed, response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) && values.Contains(origin));
    }
}
