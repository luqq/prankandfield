# Guía para agentes: Prank & Field

Instrucciones para cualquier agente (Claude Code, Codex, Gemini...) o persona que retome este repositorio.
Léela entera antes de tocar código. La documentación del proyecto está en [`docs/`](docs/README.md), en
español; el [`README.md`](README.md) (en inglés) describe el juego, los controles y la estructura.

## Qué es

Homenaje con gráficos vectoriales al *Track & Field* de Konami (1983), hecho en .NET 10. Se juega en el
navegador (Blazor WebAssembly, publicado en GitHub Pages) y en escritorio (Photino). Desde octubre de 2026
tiene **multijugador online 1 contra 1**: lockstep determinista por entradas a través de un servidor relay
SignalR propio.

- Juego publicado: <https://luqq.github.io/prankandfield/> (rama `main`).
- Servidor relay: <https://paf.josecardos.com> (`/hub` para SignalR, `/health` para comprobarlo).
- Autor y crédito en la interfaz: «Diseñado y desarrollado por Hiryu Soft».

## Mapa rápido

| Quiero... | Mira |
|---|---|
| Entender el multijugador | [docs/arquitectura-multijugador.md](docs/arquitectura-multijugador.md) |
| Desplegar el juego o el servidor, rotar la clave | [docs/despliegue.md](docs/despliegue.md) |
| Saber por qué algo es como es | [docs/decisiones.md](docs/decisiones.md) |
| Ver qué queda por hacer | [docs/pendientes.md](docs/pendientes.md) |
| API del hub y Docker del servidor | [src/PrankAndField.Server/README.md](src/PrankAndField.Server/README.md) |

## Compilar y probar

Requiere el SDK de .NET 10 (`global.json` fija 10.0.100 con `rollForward: latestFeature`). Para el host web
conviene el workload `wasm-tools` (`dotnet workload install wasm-tools`).

```bash
dotnet test PrankAndField.slnx                                   # todas las suites
dotnet run --project src/PrankAndField.Web                       # http://localhost:5173
dotnet run --project src/PrankAndField.Server --urls http://localhost:5000
# online en local: abrir http://localhost:5173/?relay=http://localhost:5000/hub en dos navegadores
```

En un contenedor Linux sin SDK: `apt-get install -y dotnet-sdk-10.0`. Si hace falta Docker, arrancar
`dockerd &` antes de `docker build`. Para probar el online de punta a punta se ha usado Playwright con dos
navegadores Chromium contra el servidor local y `?relay=`.

**No hay CI en los pull requests**: los tests solo se ejecutan en el workflow de Pages al hacer push a
`main`. Ejecuta `dotnet test` antes de abrir un PR.

## Reglas de trabajo

- **Rama de trabajo**: ramifica desde `main` y abre el PR contra `main`. La rama `main-online` ya no se
  usa (se fusionó en `main` en el PR #8).
- **El usuario fusiona los PR a mano.** No los fusiones tú aunque tengas permisos.
- Cada push a `main` **publica el juego en producción** (Pages) y, si toca el servidor, una imagen nueva
  `:main`. Trata `main` como producción.
- **Determinismo ante todo**: cualquier cambio en `PrankAndField.Core` debe dar el mismo resultado en los
  dos navegadores con la misma semilla y las mismas entradas. Nada de `DateTime.Now`, `Random`,
  `Environment.TickCount`, iterar diccionarios sin orden fijo ni depender del tiempo real dentro de la
  simulación. Usa `Game.Rng`. Si añades estado, inclúyelo en `Game.Checksum()`. Lee la sección
  «Reglas de determinismo» de [docs/arquitectura-multijugador.md](docs/arquitectura-multijugador.md).
- **Localización**: todo texto visible va en los cuatro JSON de
  `src/PrankAndField.Core/Localization/Resources` (en, es-ES, ca-ES-valencia, ja). Un test falla si falta
  una clave.
- **Secretos**: nunca escribas en el repo, en issues, en PR ni en documentos el valor de `RELAY_KEY` ni de
  ningún token. `appsettings.json` del host web lleva `RelayKey` vacío a propósito.
- Idioma: el usuario escribe en español; el código, los comentarios y el README están en inglés. Los
  documentos de `docs/` están en español.

## Repositorios relacionados

- `luqq/prankandfieldonline`: **obsoleto**. Solo tenía un workflow que publicaba la antigua rama
  `main-online` en <https://luqq.github.io/prankandfieldonline/>. Desde que el online está en `main`, el
  juego bueno es <https://luqq.github.io/prankandfield/>. Ver [docs/decisiones.md](docs/decisiones.md).
- `luqq/personal-workspace`: workspace personal del usuario; tiene una ficha de este repo en
  `docs/repos/prankandfield.md` y lo clona en `repos/prankandfield`.
