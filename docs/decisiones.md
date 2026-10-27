# Decisiones e historia

Registro de lo que se decidió y por qué, para no volver a discutirlo. Fechas de 2026.

## Decisiones vigentes

| Tema | Decisión | Motivo |
|---|---|---|
| Modelo de red | Lockstep determinista por entradas, retardo de 4 ticks, checksum cada segundo | `Game` ya era determinista a 60 Hz; la entrada cabe en 9 bits por tick; las pruebas de campo van por turnos |
| Servidor | Relay ASP.NET Core + SignalR de una instancia; no simula | Mínimo consumo y código; cambiar de proveedor es trivial |
| Topología | Sin WebRTC/P2P en la fase 1 | Simplicidad; el relay atraviesa cualquier NAT |
| Jugadores | Solo 1 contra 1, salas privadas por código de 5 caracteres | El juego tiene arrays fijos de 2 jugadores |
| Hosting | VM Oracle Cloud Always Free que el usuario ya tenía, con Pangolin, detrás de Cloudflare | Gratis y sin arranque en frío. Se valoraron Azure Container Apps (escala a cero) y Fly.io (~2-3 $/mes) |
| Dominio | `https://paf.josecardos.com` (`/hub`, `/health`) | Subdominio del usuario en Cloudflare |
| Récords online | Tabla propia y vacía por partida; no tocan los locales | Los récords locales darían bonus distintos a cada par y romperían el lockstep |
| Seguridad | Clave compartida (`RELAY_KEY`) + comprobación de `Origin` | Barrera ligera; se acepta que la clave es visible en el juego publicado |
| Modo práctica | Se abre con el **Konami Code** en el título (↑↑↓↓←→←→ B A; antes era P/F2). Esc/Back sale. Usarlo bloquea el online hasta recargar | La práctica cambia semilla y jugadores; evita trampas y desincronizaciones |
| Jugador 2 | Piel morena (`Palette.SkinP2`) | Distinguir a los atletas |
| Crédito | «Diseñado y desarrollado por Hiryu Soft» con un logo de dragón, pequeño a la derecha | Petición del usuario |
| Ramas | Todo en `main`; el usuario fusiona los PR a mano | Se probó a aislar el online en `main-online` y luego se decidió unificar |
| Modo Hyper Sports (27/10) | Seleccionable en el título: el menú pasa de 2 a 4 entradas (Prank & Field / Hyper Sports × 1 o 2 jugadores) recorridas con ↑↓ o los botones de correr. Mismo motor: `GameMode` en `Game`, `EventKind` 6-11, `MarkUnit` por prueba (segundos, metros, puntos, platos, kg), `Rules`, `Scoring` y `WorldRecords` con 12 filas | Petición del usuario: «el mismo motor de juego y personajes». Una lista de 4 entradas funciona con teclado, mando y botones táctiles sin añadir ejes |
| Pruebas de Hyper Sports | Natación (respirar al aviso), tiro al plato (dos escopetas, miras fijas, 20 platos, dobles y plato dorado), potro (trampolín, impulso, giros, clavar la caída, nota sobre 10), tiro con arco (diana que sube y baja, viento, 6 flechas), triple salto (pulsar en cada apoyo) y halterofilia (arrancar, ¡empuja!, equilibrio). Las de carrera y de intentos reutilizan `RaceSimulation` y `FieldEventSimulation`; las de un solo turno usan la nueva `TurnEventSimulation` | Mecánicas del arcade de 1984 adaptadas a los dos botones de correr y el de saltar |
| Hyper Sports online | No en esta fase: `Game.StartOnlineCredit(GameMode)` ya acepta el modo, pero el relay no lo transmite y el online juega siempre Prank & Field | Cambiar el protocolo rompe la compatibilidad entre clientes hasta redesplegar la imagen del servidor; se deja para un PR propio |
| Récords de Hyper Sports | La tabla pasa a 12 filas; los JSON de 6 filas guardados antes se cargan y conservan los valores por defecto de las nuevas pruebas | No perder los récords locales de nadie |
| Calibrado de Hyper Sports | Mínimas y récords ajustados con el bot de los tests (`tests/.../Support/Bot.cs`, 9-16 pulsaciones/s): natación 440 cm/s máx. (11 pulsaciones/s ≈ 24,6 s), triple salto ≈ 2× longitud, potro nota 5 + tabla 1,5 + impulso 1,5 + 0,5 por giro + 0,3 por clavar, pesas +5 kg por levantamiento válido | Que un jugador medio supere la ronda 1 y los récords sean alcanzables pero no triviales |

## Historia del online (PRs de `luqq/prankandfield`)

| PR | Fecha | Contenido |
|---|---|---|
| #1 | 24/10 | Simulación preparada para lockstep: modo online, `Game.Checksum()`, `GameHost.Step/Present`, tests de determinismo |
| #2 | 24/10 | Proyecto `PrankAndField.Net`: `LockstepSession`, `IGameTransport`, `InMemoryLink`, `Game.StartOnlineCredit()` |
| #3 | 24/10 | Workflow para desplegar `main-online` desde este repo. **Cerrado sin fusionar**: se sustituyó por el repo `prankandfieldonline` |
| #4 | 24/10 | `PrankAndField.Server` (SignalR), `PrankAndField.Net.Client.RelayClient`, Dockerfile amd64/arm64, workflow de imagen GHCR |
| #5 | 24/10 | UI online: panel ONLINE, `OnlineMatch`, `GameHost` en online, `RelayUrl`, invitaciones `?sala=` |
| #6 | 25/10 | `RelayUrl = https://paf.josecardos.com/hub` |
| #7 | 25/10 | Correcciones: módulo JS versionado (error `focusCanvas`), nombres simultáneos online, botones táctiles traducidos, crédito Hiryu Soft, clave de acceso, pulsaciones rápidas en lockstep |
| #8 | 25/10 | Fusión de `main-online` en `main`. Desde aquí el online se publica en `luqq.github.io/prankandfield` |
| #9 | 25/10 | Konami Code para el modo práctica y bloqueo del online tras usarlo; piel morena del J2 |
| #10 | 25/10 | Longitud: caer antes de la arena (`Rules.LongJumpPitStart` = 1 m tras la tabla) es nulo. Jabalina: el atleta se planta al pulsar lanzar y la marca se mide desde la línea de nulo |
| #11 | 26/10 | Documentación para agentes: `AGENTS.md`, `docs/` |
| Hyper Sports | 27/10 | Modo Hyper Sports en el título con sus seis pruebas, textos en los 4 idiomas, sonidos, tests y calibrado (rama `claude/project-thread-ikowl7`) |

Todos están fusionados en `main` (comprobado el 26/10/2026; `main` = `acc72b4`).

## Ramas y repos retirados

- **`main-online`**: rama de desarrollo del online entre el 24 y el 25/10. Se creó para no publicar el
  multijugador en el juego de `main` mientras estaba a medias. El 25/10 el usuario pidió fusionarlo todo a
  `main` (PR #8) y la regla de «no tocar `main`» dejó de valer. La rama sigue en GitHub, congelada en el
  PR #7, y ya no se usa.
- **`luqq/prankandfieldonline`**: repo creado el 24/10 solo para publicar `main-online` en
  `luqq.github.io/prankandfieldonline` (cada repo solo puede tener un sitio de Pages). Tenía un único
  workflow (`deploy.yml`) que cada 30 minutos compilaba `prankandfield@main-online`. **Está obsoleto**:
  publica una versión vieja (sin los PR #9 y #10) y el juego actual es `luqq.github.io/prankandfield`. Se
  puede borrar o archivar; al hacerlo deja de funcionar su URL y se pierde su copia del secreto `RELAY_KEY`,
  que no hace falta.
- Ramas de trabajo ya fusionadas que se pueden borrar: `online/determinismo`, `online/lockstep`,
  `online/deploy-pages`, `online/ui`, `online/relay-url`, `online/fixes`, `claude/project-thread-*`.

## Documento original de diseño

El análisis inicial (24/10) se escribió fuera del repo; su contenido útil está resumido aquí y en
[arquitectura-multijugador.md](arquitectura-multijugador.md). Hallazgos de entonces que ya se resolvieron:
la semilla de `GameHost` salía de `Environment.TickCount`, `Advance(dt)` aplicaba una máscara a todos los
ticks de un frame, los récords locales daban +3000 puntos distintos a cada par y la tecla de depuración abría
la práctica.
