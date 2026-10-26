# Arquitectura del multijugador online

Estado a 26/10/2026: funcionando en producción, 1 contra 1, salas privadas por código. Todo está en `main`.

## Idea general

**Lockstep determinista por entradas con un relay.** Los dos navegadores ejecutan la misma simulación
(`PrankAndField.Core.Game`) con la misma semilla. Por la red solo viajan las entradas de cada jugador (una
máscara de 9 bits por tick) y, cada segundo, un checksum del estado. El servidor no simula nada: crea la
sala, reparte semilla y asiento, y reenvía mensajes al rival.

```
Navegador J1 (seat 0)                 Servidor relay (SignalR)               Navegador J2 (seat 1)
GameHost ─ OnlineMatch                 MatchHub + RoomRegistry                OnlineMatch ─ GameHost
   └ LockstepSession ─ RelayClient ──► /hub ── reenvía Input/Checksum ──► RelayClient ─ LockstepSession
        └ Game (Core, 60 Hz)                                                        └ Game (Core, 60 Hz)
```

Se eligió así porque `Game` ya era determinista a 60 Hz con su propio `Rng` y una entrada diminuta por
tick. Las pruebas de campo van por turnos, así que solo las carreras notan la latencia, y un retardo de
entrada de 4 ticks (unos 67 ms) la oculta bien. WebRTC/P2P y el rollback se dejaron para más adelante (ver
[pendientes](pendientes.md)).

## Piezas y dónde están

| Pieza | Fichero | Qué hace |
|---|---|---|
| Simulación | `src/PrankAndField.Core/Game.cs` | `new Game(records, seed, online: true)` y `StartOnlineCredit()` saltan el título y empiezan a 2 jugadores. `Checksum()` resume el estado que decide la partida. `IsOnline` ignora el Konami Code dentro de la partida online. |
| Entrada | `src/PrankAndField.Core/InputFrame.cs` | Máscara de 9 bits por jugador y tick (`InputFrame.FromMask`). |
| Lockstep | `src/PrankAndField.Net/LockstepSession.cs` | Programa la entrada local `InputDelay` ticks por delante, la envía y solo simula un tick cuando tiene las dos entradas. Cada `ChecksumInterval` (60 ticks = 1 s) intercambia `Game.Checksum()`; si difieren, marca `DesyncTick` y se para. `Advance()` acumula tiempo real y no pierde pulsaciones rápidas que caen en un frame sin tick. |
| Mensajes | `src/PrankAndField.Net/NetMessage.cs` | `InputMessage(seat, firstTick, masks[])` y `ChecksumMessage(seat, tick, checksum)`. |
| Transporte | `src/PrankAndField.Net/IGameTransport.cs`, `InMemoryLink.cs` | Canal fiable y ordenado que la sesión consulta desde el bucle. `InMemoryLink` simula latencia para tests y juego local. |
| Cliente relay | `src/PrankAndField.Net.Client/RelayClient.cs` | Cliente SignalR: crea o se une a una sala y expone la partida como `IGameTransport`. Envía la clave como `access_token`. |
| Servidor | `src/PrankAndField.Server/` (`MatchHub`, `RoomRegistry`, `RelayAccess`, `RelayOptions`) | ASP.NET Core + SignalR. Códigos de 5 caracteres sin ambiguos (`ABCDEFGHJKMNPQRSTUVWXYZ23456789`). Al entrar el segundo jugador envía `MatchStarted { seed, seat, inputDelay }`. Si alguien se desconecta, cierra la sala y avisa `OpponentLeft`. Ver su [README](../src/PrankAndField.Server/README.md). |
| Partida en la UI | `src/PrankAndField.UI/OnlineMatch.cs` | Conexión + código + `LockstepSession`. Estados: `Connecting`, `WaitingForOpponent`, `Playing`, `Finished`, `OpponentLeft`, `Desynced`, `Failed`. Muestra «esperando» tras 20 ticks sin entrada del rival. |
| Host | `src/PrankAndField.UI/GameHost.cs` | `Game` devuelve la partida online si la hay. En online, las dos manos del teclado local (`p1Mask \| p2Mask`) mueven al atleta de tu asiento. `OnlineLocked` bloquea el online si se usó el modo práctica. |
| Panel | `src/PrankAndField.UI/GameCanvas.razor` | Botón **ONLINE**, crear/unirse, enlace de invitación `?sala=CODIGO` (también `?room=`), override `?relay=<hub>` y `?relaykey=`. |
| Configuración web | `src/PrankAndField.Web/wwwroot/appsettings.json` | `RelayUrl` (vacío oculta el botón ONLINE) y `RelayKey` (vacío en el repo; lo rellena el workflow). |

Dirección de dependencias: `Core ← Net ← Net.Client ← UI`. `Server` es independiente (no referencia ningún
otro proyecto): solo reenvía, así que no conoce `Game`. El host de escritorio
(`PrankAndField.Desktop`) **no tiene online**: no pasa `RelayUrl`.

## Flujo de una partida

1. J1 pulsa ONLINE → `CreateRoom()` → recibe el código y un enlace `https://luqq.github.io/prankandfield/?sala=CODIGO`.
2. J2 abre el enlace o escribe el código → `JoinRoom(code)`.
3. El servidor elige la semilla y manda a cada uno `MatchStarted(seed, seat, inputDelay)`.
4. Cada cliente crea `LockstepSession` con un `WorldRecords` **nuevo y vacío**: los récords online no tocan
   los locales (si no, uno tendría el bonus de récord y el otro no, y se desincronizarían).
5. Entrada de nombres **simultánea**: cada uno escribe el suyo a la vez, en su color, con «(TÚ)» en su
   columna (`Game.SimultaneousNameEntry`, `NameSlot`).
6. Se juega el crédito normal. Al volver al título la partida queda `Finished`.

## Reglas de determinismo (no romper)

- Nada de tiempo real, `Random`, `Environment.TickCount`, `DateTime.Now` ni orden de diccionarios dentro
  de `Core`. Todo azar sale de `Game.Rng` (xorshift propio).
- El estado nuevo que influya en el resultado debe entrar en `Game.Checksum()`; si no, una desincronización
  pasará desapercibida.
- Las entradas se aplican por tick, nunca por frame (`GameHost.Step`/`Present` separan simulación y pintado).
- Los récords locales (localStorage) no pueden afectar a la simulación online.
- El modo práctica (Konami Code) cambia semilla y número de jugadores: en una `Game` online se ignora, y en
  la local, si se usó, `Game.PracticeUsed` bloquea el online hasta recargar la página.
- `Math.Sin/Cos` podrían diferir entre WASM y escritorio; hoy no importa porque solo juega online el host
  web (los dos pares usan el mismo runtime WASM).
- Tests que lo vigilan: `tests/PrankAndField.Core.Tests/DeterminismTests.cs`,
  `tests/PrankAndField.Net.Tests/LockstepSessionTests.cs` (dos bots juegan un crédito con enlace con lag,
  retardo, bloqueos, detección de desync) y `tests/PrankAndField.Server.Tests/RelayTests.cs` (dos pares en
  lockstep a través del hub real).

## Seguridad

- **Clave de acceso compartida** (`Relay__AccessKey` en el servidor, `RelayKey` en el juego): frena scripts y
  otras webs, pero no es autenticación real, porque va dentro del juego publicado. Se puede rotar en
  cualquier momento ([despliegue](despliegue.md#clave-de-acceso)).
- **Origen**: el servidor responde 403 a un navegador cuyo `Origin` no esté en `Relay:AllowedOrigins`
  (por defecto `https://luqq.github.io` y `http://localhost:5173`), porque CORS no frena WebSockets.
- `/health` es público y nunca pide la clave.
- No hay validación de récords en el servidor: un cliente modificado puede hacer trampas contra su rival.
