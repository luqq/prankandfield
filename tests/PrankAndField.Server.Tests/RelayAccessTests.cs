using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using PrankAndField.Net.Client;

namespace PrankAndField.Server.Tests;

/// <summary>The hub only lets in clients with the shared key and, for browsers, an allowed Origin.</summary>
public class RelayAccessTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Key = "test-key-0123456789";

    private WebApplicationFactory<Program> Locked => factory.WithWebHostBuilder(b => b.UseSetting("Relay:AccessKey", Key));

    private static async Task<RelayClient> ConnectAsync(WebApplicationFactory<Program> app, string? key, string? origin = null)
    {
        var server = app.Server;
        var client = new RelayClient(new Uri(server.BaseAddress, MatchHub.Path), key, options =>
        {
            options.Transports = HttpTransportType.WebSockets;
            options.SkipNegotiation = true;
            options.WebSocketFactory = async (context, ct) =>
            {
                var ws = server.CreateWebSocketClient();
                string? token = context.Options.AccessTokenProvider is { } provider ? await provider() : null;
                ws.ConfigureRequest = request =>
                {
                    if (token is not null) request.Headers.Authorization = "Bearer " + token;
                    if (origin is not null) request.Headers.Origin = origin;
                };
                return await ws.ConnectAsync(context.Uri, ct);
            };
        });
        await client.StartAsync();
        return client;
    }

    [Fact]
    public async Task TheRightKeyOpensTheHub()
    {
        await using var client = await ConnectAsync(Locked, Key);
        Assert.Matches("^[A-Z0-9]{5}$", await client.CreateRoomAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task AMissingOrWrongKeyIsRejected(string? key)
    {
        await Assert.ThrowsAnyAsync<Exception>(async () => await (await ConnectAsync(Locked, key)).DisposeAsync());
    }

    [Fact]
    public async Task TheKeyIsAlsoAcceptedInTheQueryString()
    {
        // Browsers send it like this, because a WebSocket cannot carry an Authorization header there.
        var response = await Locked.CreateClient().PostAsync($"{MatchHub.Path}/negotiate?negotiateVersion=1&access_token={Key}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var denied = await Locked.CreateClient().PostAsync($"{MatchHub.Path}/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task AnUnknownBrowserOriginIsRejected()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () => await (await ConnectAsync(Locked, Key, "https://evil.example")).DisposeAsync());
        await using var allowed = await ConnectAsync(Locked, Key, "https://luqq.github.io");
        Assert.Equal(Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected, allowed.State);
    }

    [Fact]
    public async Task WithoutAKeyTheHubStaysOpen()
    {
        await using var client = await ConnectAsync(factory, null);
        Assert.Matches("^[A-Z0-9]{5}$", await client.CreateRoomAsync());
    }

    [Fact]
    public async Task HealthNeedsNoKey()
    {
        var response = await Locked.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
