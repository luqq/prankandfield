# Prank & Field relay server

ASP.NET Core + SignalR. Players open a private room by code; once both seats are taken the server sends each
one the shared seed and its seat, then forwards inputs and checksums to the opponent. The game itself runs
in lockstep on both clients (`PrankAndField.Net`); the server never simulates.

## Hub (`/hub`)

| Client calls | Returns / effect |
|---|---|
| `CreateRoom()` | `{ code }`: the creator is P1 (seat 0). |
| `JoinRoom(code)` | `{ ok, error }`: the joiner is P2. Both then receive `MatchStarted { seed, seat, inputDelay }`. |
| `SendInput(firstTick, masks[])` | Forwarded to the opponent as `Input(seat, firstTick, masks)`. |
| `SendChecksum(tick, checksum)` | Forwarded as `Checksum(seat, tick, checksum)`. |

When a player disconnects the room closes and the other one receives `OpponentLeft`. `GET /health` returns
`{ status, rooms }`. The client side is `PrankAndField.Net.Client.RelayClient`.

## Configuration

`appsettings.json`, section `Relay` (or environment variables such as `Relay__InputDelay=5`):

- `AllowedOrigins`: browser origins allowed by CORS. Defaults to `https://luqq.github.io` and the local dev
  server `http://localhost:5173`.
- `InputDelay`: ticks of input delay handed to both players (default 4, about 67 ms).
- `AccessKey`: shared key the game must send to open `/hub` (`Relay__AccessKey=...`). Empty leaves the hub
  open. `/health` never needs it.

## Access key

A light gate against strangers using the relay, not real authentication: the key travels inside the
published web game, so anyone who digs through its files can find it. It stops scripts and other sites
that don't have it, and it can be rotated at any time.

- The server reads `Relay__AccessKey` and answers 401 to `/hub` without the key (as `access_token` in the
  query string, which browsers use for WebSockets, or as `Authorization: Bearer`). It also answers 403 to a
  browser whose `Origin` is not in `AllowedOrigins`, because CORS does not stop WebSockets.
- The game sends `RelayKey` from `src/PrankAndField.Web/wwwroot/appsettings.json`. The repository keeps it
  empty; the Pages workflows fill it from the `RELAY_KEY` Actions secret at build time.

To set or rotate it: generate one (`openssl rand -hex 24`), save it as the `RELAY_KEY` secret of the
repository that deploys the web game, add `Relay__AccessKey` with the same value to the server's
`environment`, restart the container and redeploy the game. For a local test against your own server, the
`?relay=` override takes its key from `?relaykey=`.

## Running

```bash
dotnet run --project src/PrankAndField.Server          # http://localhost:5000
docker build -f src/PrankAndField.Server/Dockerfile -t prankandfield-server .
docker run -p 8080:8080 prankandfield-server
```

`.github/workflows/server-image.yml` publishes `ghcr.io/luqq/prankandfield-server:main` (and `:main-online`
from that branch) for amd64 and arm64. GHCR packages start private: make the package public in its settings, or `docker login ghcr.io` on the
server with a token that can read packages.

## Behind Pangolin

Pangolin's Traefik runs inside Docker, so it cannot reach a port published on the VM's localhost. Put the
container on Pangolin's network instead, with no published ports:

```yaml
# docker-compose.yml on the VM
services:
  paf:
    image: ghcr.io/luqq/prankandfield-server:main
    container_name: paf
    restart: unless-stopped
    environment:
      Relay__AccessKey: ${RELAY_KEY}   # from a .env file next to this compose file, never committed
    networks: [pangolin]

networks:
  pangolin:
    external: true
    name: pangolin_frontend
```

In Pangolin, add an HTTP resource (for example `paf.yourdomain`) targeting `http://paf:8080` and leave its
authentication (SSO / PIN) off: the browser opens the WebSocket from GitHub Pages and cannot log in first.
Pangolin terminates TLS, so the game connects to `wss://paf.yourdomain/hub`.
