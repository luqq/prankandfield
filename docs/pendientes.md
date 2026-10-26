# Pendientes e ideas

Lo que quedaba abierto al cerrar la primera etapa del online (26/10/2026). Nada de esto bloquea el juego.

## Limpieza

- [ ] Borrar o archivar el repo `luqq/prankandfieldonline` (obsoleto, ver [decisiones](decisiones.md#ramas-y-repos-retirados)).
- [ ] Quitar `main-online` de los disparadores de `.github/workflows/server-image.yml` y borrar la rama
      `main-online` y las ramas `online/*` y `claude/*` ya fusionadas.
- [ ] Actualizar el `README.md` del servidor si cambia el compose de la VM.

## Por comprobar (no verificado desde el repo)

- [ ] Que el secreto `RELAY_KEY` está creado en `luqq/prankandfield` y coincide con el `.env` de la VM. Si
      el último despliegue de Pages avisó «RELAY_KEY secret not set», el juego conecta sin clave.
- [ ] Que el paquete GHCR `prankandfield-server` es público o que la VM tiene `docker login ghcr.io`.
- [ ] Que el compose de la VM usa la etiqueta `:main` y no `:main-online`.

## Calidad

- [ ] Workflow de CI para pull requests (`dotnet test` en `pull_request`). Hoy los tests solo corren al
      publicar desde `main`.
- [ ] Probar el online de punta a punta con Playwright en CI (dos navegadores contra un servidor local).

## Funcionalidad online

- [ ] Reconexión: si un jugador pierde la conexión, la sala se cierra y no se puede volver.
- [ ] Validar récords en el servidor re-simulando la partida con `PrankAndField.Core` (se propuso en el
      diseño, no se hizo). Hoy un cliente modificado puede hacer trampas.
- [ ] Récords online persistentes o una tabla global.
- [ ] Rollback (predicción y re-simulación) si el retardo de 4 ticks se nota en conexiones lentas.
- [ ] Emparejamiento público (sin código) o espectadores.
- [ ] Más de 2 jugadores: exige cambiar los arrays fijos de 2 en `Game`, `EventSimulation` y
      `FieldEventSimulation`.
- [ ] Online en el host de escritorio (habría que comprobar que `Math.Sin/Cos` dan lo mismo que en WASM).

## Documentación

- [ ] Las secciones «Online play» y «Hidden practice menu» del `README.md` deben seguir al día si cambia la
      UI.
