# Despliegue

Dos cosas se despliegan: el **juego web** (GitHub Pages) y el **servidor relay** (contenedor Docker en una
VM de Oracle Cloud detrás de Pangolin y Cloudflare). No hay valores secretos en este documento: la clave vive
solo en el secreto de GitHub y en el `.env` de la VM.

## Juego web: GitHub Pages

- Workflow: `.github/workflows/pages.yml` («Publish web build to GitHub Pages»).
- Se dispara en cada push a `main` y a mano (`workflow_dispatch`).
- Pasos: instala `wasm-tools`, `dotnet test`, rellena `RelayKey` en
  `src/PrankAndField.Web/wwwroot/appsettings.json` con el secreto `RELAY_KEY` (si falta, avisa y el juego
  conecta sin clave), publica el host web, ajusta `<base href>` a `/prankandfield/`, versiona
  `prankandfield.css?v=<sha>`, añade `.nojekyll` y copia `index.html` a `404.html`.
- URL: <https://luqq.github.io/prankandfield/>.
- Requisito ya hecho: Settings → Pages → Source: **GitHub Actions**.
- El módulo JS se carga como `prankandfield.js?v=...` (`GameCanvas.razor`, `ModuleUrl`) para que un
  despliegue nuevo no use el JS en caché del anterior (causó el error `focusCanvas` del 25/10/2026). Si tras
  desplegar algo no cambia, recargar con **Ctrl+Shift+R**.
- `RelayUrl` apunta a `https://paf.josecardos.com/hub`. Si se deja vacío, desaparece el botón ONLINE.

## Imagen del servidor: GHCR

- Workflow: `.github/workflows/server-image.yml`. Se dispara con push a `main` (y a la antigua
  `main-online`) que toque `src/PrankAndField.Server/**`, `src/PrankAndField.Net/**`, `global.json`,
  `Directory.Build.props` o el propio workflow, y a mano.
- Construye `src/PrankAndField.Server/Dockerfile` para **linux/amd64 y linux/arm64** (la VM es Ampere,
  arm64) y publica `ghcr.io/luqq/prankandfield-server:main` y `:<sha>`.
- La etiqueta `:main-online` existe pero está congelada en el PR #7; usar siempre `:main`.
- Los paquetes de GHCR nacen privados: o se hace público el paquete (Settings del paquete → Change
  visibility), o en la VM se hace `docker login ghcr.io` con un PAT *classic* con `read:packages`.

## Servidor: VM Oracle «ampere01»

- VM Oracle Cloud Always Free (Ampere, arm64) con **Pangolin** (proxy inverso con túnel; contenedores
  `pangolin`, `gerbil` y `traefik`, compose en `/opt/pangolin`). La red Docker de Pangolin se llama
  **`pangolin_frontend`**; Traefik usa `network_mode: service:gerbil`.
- El servidor del juego vive en **`/opt/prankandfield-server/`**, con `docker-compose.yml` y `.env`.
- Contenedor **`paf`** en la red externa `pangolin_frontend`, **sin puertos publicados** (Traefik no
  llega a un `127.0.0.1:puerto` de la VM, porque para él «localhost» es su propio contenedor).

`/opt/prankandfield-server/docker-compose.yml`:

```yaml
services:
  paf:
    image: ghcr.io/luqq/prankandfield-server:main
    container_name: paf
    restart: unless-stopped
    environment:
      Relay__AccessKey: ${RELAY_KEY}
    networks: [pangolin]

networks:
  pangolin:
    external: true
    name: pangolin_frontend
```

`/opt/prankandfield-server/.env` (permisos `600`, nunca en Git):

```
RELAY_KEY=<la clave>
```

Actualizar el servidor tras un merge que cambie la imagen:

```bash
cd /opt/prankandfield-server
docker compose pull && docker compose up -d
docker exec gerbil wget -qO- http://paf:8080/health   # {"status":"ok","rooms":N}
```

### Pangolin

Recurso HTTP `paf.josecardos.com`, site **local**, target `http` → host `paf`, puerto `8080` (el puerto
interno del contenedor). **Autenticación (SSO/PIN) desactivada**: el navegador abre el WebSocket desde
GitHub Pages y no puede iniciar sesión antes. Pangolin termina TLS; los WebSockets pasan sin configuración.

### Cloudflare

El dominio `josecardos.com` está en Cloudflare con proxy (nube naranja). Los WebSockets funcionan en todos
los planes. Si aparece **error 526** (certificado de origen no válido), lo normal es que Traefik aún no tenga
el certificado de Let's Encrypt del subdominio: revisar `docker logs traefik`, poner temporalmente el
registro DNS en *DNS only* (nube gris) hasta que lo obtenga y volver a *Proxied*.

Comprobación desde fuera: `curl https://paf.josecardos.com/health`.

## Clave de acceso

Nombres (nunca el valor):

| Dónde | Nombre |
|---|---|
| Servidor (variable de entorno) | `Relay__AccessKey`, alimentada por `RELAY_KEY` del `.env` |
| Juego (`appsettings.json` del host web) | `RelayKey` (vacío en el repo) |
| GitHub Actions (repo `luqq/prankandfield`) | secreto `RELAY_KEY` |

Rotarla:

1. `openssl rand -hex 24`.
2. Guardarla como secreto `RELAY_KEY` en `luqq/prankandfield` (Settings → Secrets and variables → Actions).
3. Ponerla en `/opt/prankandfield-server/.env` y `docker compose up -d`.
4. Relanzar «Publish web build to GitHub Pages» desde Actions.

Entre los pasos 3 y 4 el juego publicado no podrá conectar. Si el servidor no tiene clave, acepta a
cualquiera; si el juego no la tiene y el servidor sí, el servidor responde 401.

## Problemas ya vistos

| Síntoma | Causa | Arreglo |
|---|---|---|
| No aparece el botón ONLINE | `RelayUrl` vacío, despliegue viejo o caché | Comprobar `appsettings.json` publicado, relanzar Pages, Ctrl+Shift+R |
| `focusCanvas` u otros errores JS tras desplegar | JS en caché de otra versión | Ya se versiona el módulo; forzar recarga |
| Error 526 en `paf.josecardos.com` | Certificado de origen en Traefik | Ver Cloudflare arriba |
| 401 al conectar | Clave distinta entre juego y servidor | Rotar con los pasos de arriba |
| 403 al conectar | `Origin` no permitido | Añadir el origen a `Relay__AllowedOrigins__N` |
| Traefik no llega al servidor | Puerto publicado en localhost en vez de red compartida | Contenedor en `pangolin_frontend`, target `paf:8080` |
