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

## Running

```bash
dotnet run --project src/PrankAndField.Server          # http://localhost:5000
docker build -f src/PrankAndField.Server/Dockerfile -t prankandfield-server .
docker run -p 8080:8080 prankandfield-server
```

`.github/workflows/server-image.yml` publishes `ghcr.io/luqq/prankandfield-server:main-online` for amd64 and
arm64. GHCR packages start private: make the package public in its settings, or `docker login ghcr.io` on the
server with a token that can read packages.

## Behind Pangolin

Run the container on the VM listening only on localhost and publish it as a Pangolin resource:

```yaml
# docker-compose.yml on the VM
services:
  prankandfield-server:
    image: ghcr.io/luqq/prankandfield-server:main-online
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080"
```

In Pangolin, add an HTTP resource (for example `paf.yourdomain`) targeting `localhost:8080` and leave its
authentication (SSO / PIN) off: the browser opens the WebSocket from GitHub Pages and cannot log in first.
Pangolin terminates TLS, so the game connects to `wss://paf.yourdomain/hub`. If Pangolin runs in its own
Docker network, point the resource at the container name instead and attach the container to that network.
