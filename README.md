# Prank & Field

A vector-graphics tribute to Konami's **Track & Field** (*Hyper Olympic*, 1983): the six events, the
mustachioed athletes, the red-framed scoreboard, the packed grandstand, independently scrolling lanes,
the QUALIFY standards, the three-slot WORLD RECORD board with initials, the bird and the mole easter eggs,
and short original chiptune jingles **sung by a robotic voice**.

Everything is .NET 10. The game logic, the vector engine and the audio definitions are separate
projects; the same Razor component runs in a browser (Blazor WebAssembly) and in a desktop window
(Blazor Hybrid on the native WebView via Photino).

## Solution layout (`PrankAndField.slnx`)

| Project | Role |
|---|---|
| `src/PrankAndField.Core` | Game logic only: events, running/ballistics model, rules and qualifying table, scoring, world records (JSON persistence), the credit state machine and the localizer. No rendering, no audio playback. |
| `src/PrankAndField.Engine` | Vector rendering engine: a retained `DrawList` encoded as a flat `double[]`, a 5x7 pixel-style vector font (Latin + Spanish/Valencian accents, Japanese falls back to backend text), the athlete sprite (poses, run cycle, mustache), stadium, HUD and all screens. Backend-agnostic. |
| `src/PrankAndField.Audio` | Songs (notes + syllables), sound ids, voice lines and the cue mapping. Serialized to the JavaScript synthesizer. |
| `src/PrankAndField.Net` | Online play: deterministic lockstep session (input delay, checksum exchange), wire messages and the transport abstraction, plus an in-memory link for local play and tests. Not wired into the UI yet. |
| `src/PrankAndField.Net.Client` | SignalR client of the relay: creates or joins a room and exposes the match as an `IGameTransport`. |
| `src/PrankAndField.Server` | Relay server (ASP.NET Core + SignalR): private rooms by code, forwards inputs and checksums. Docker image for amd64/arm64; see its README. |
| `src/PrankAndField.UI` | Shared Razor Class Library: the `GameCanvas` component, `GameHost` (composes the three layers) and `prankandfield.js` (canvas backend, Web Audio synth with formant "robot singing", speech announcer, keyboard/gamepad/touch input, persistence). |
| `src/PrankAndField.Web` | Blazor WebAssembly host (uses `[JSExport]` for a JSON-free frame bridge). |
| `src/PrankAndField.Desktop` | Desktop host (PhotinoX.Blazor, Blazor Hybrid). |
| `tests/*` | xUnit suites for Core (physics, every event, full-game runs with a scripted bot, localization completeness), Engine (draw list encoding, font coverage, every screen of a whole game in every language), Audio (songs, cue map, catalog JSON, host frame), Net (two bots playing a credit over a laggy link, input delay, stalls, desync detection) and Server (rooms, CORS, two peers in lockstep through the real hub). |

Dependency direction: `Core ← Engine`, `Core ← Audio`, `Core ← Net ← Net.Client`, `Server` (standalone), `Core/Engine/Audio ← UI ← Web/Desktop`. Core never references the others.

## Running

Requires the .NET 10 SDK (pinned by `global.json`). The `wasm-tools` workload is recommended for the web host.

```bash
dotnet run --project src/PrankAndField.Web
```

Then open <http://localhost:5173>.

```bash
dotnet run --project src/PrankAndField.Desktop
```

Tests:

```bash
dotnet test PrankAndField.slnx
```

## Publishing to GitHub Pages

`.github/workflows/pages.yml` runs the tests, publishes the WebAssembly host and deploys it to GitHub Pages on
every push to `main` (or manually from the Actions tab). Enable it once in the repository settings:
*Settings → Pages → Build and deployment → Source: GitHub Actions*. The workflow rewrites `<base href>` to the
repository path, adds `.nojekyll` so the `_framework` folder is served, and copies `index.html` to `404.html`.

## Controls

| Action | Player 1 | Player 2 | Gamepad |
|---|---|---|---|
| RUN 1 / RUN 2 (alternate them!) | `Z` / `X` | `←` / `→` (or `N` / `M`) | A / B |
| JUMP / THROW | `Space` | `↑` (or right Shift) | X / Y |
| START | `Enter` or `1` | `2` | Start |
| Menu up / down | `↑` / `↓` | | D-pad |

Touch buttons appear on coarse-pointer devices. On the title screen `↑`/`↓` (or the run buttons) pick 1 or 2 players.

### Events (round-1 standards match the arcade)

| Event | Technique | Qualify |
|---|---|---|
| 100 m dash | Alternate RUN after the gun. Pressing before it is a flying start; the third one loses the heat. | 16.00 s |
| Long jump | Run, press JUMP before the board, hold to raise the angle (about 42-45 degrees is best). Past the board = FOUL. Take off within 3 cm of the board and a mole pays 1000. | 6.00 m |
| Javelin | Run, hold THROW to raise the angle (you may keep tapping RUN), release before the line. Holding RUN in flight carries the javelin up to 20 % farther. A very steep, fast throw hits the bird. | 60.00 m |
| 110 m hurdles | Run and press JUMP just before each hurdle. | 14.50 s |
| Hammer | Press RUN once to spin (each turn is faster). Press THROW when the ball turns blue behind you, release when it glows red in front: that half turn is a 45 degree throw. Any release in the front half is legal but shorter off-centre; behind you is a foul. Ten turns make you dizzy. | 73.00 m |
| High jump | The approach is automatic. Press JUMP near the bar; while held the angle counts down from 80. Tap RUN in the air to lift over the bar. Clear only on the third try after two misses for the mole. | 2.30 m |

Later rounds raise the standards (`Rules.cs`). Every 100,000 points earns an extra chance. World records are
credited to the three-letter name you enter at the start of the credit; the game never asks for initials again.

## Localization

English is the reference; Spanish (es-ES), Valencian (ca-ES-valencia) and Japanese (ja) are included as
embedded JSON in `src/PrankAndField.Core/Localization/Resources`. A test fails if any culture misses a key.
The announcer uses the browser speech voice of the selected language; the sung lyrics stay in English (they are
nonsense syllables for the formant synth anyway).

## Audio

No samples are shipped. `prankandfield.js` synthesizes everything with Web Audio: square/triangle chip voices,
a sawtooth lead run through three formant band-pass filters (one vowel per syllable) for the robotic singing,
procedural effects (gun, crowd, hurdle, bar, bird, mole...), a flight tone whose pitch rises and falls with the
height of the athlete or implement, and `speechSynthesis` for the announcer (a female voice of the selected
language when the system has one; marks and times are read aloud after every attempt). The melodies are
original compositions written for this tribute, not transcriptions of the arcade music.

## Hidden practice menu

Press `P` (or `F2`) on the title screen: pick any event with up/down, the round with left/right (the
standards update live), and start it with JUMP. `P` goes back. Handy to test a single event without playing
the whole round.

## Debug / practice helpers

From the browser console:

```js
await paf.state()                 // simulation snapshot
paf.jumpTo("hammer", 2)           // practice an event at a given round
paf.setLanguage("ca-ES-valencia")
```

## Fidelity notes and sources

Layout, palette, standards and tricks were taken from the arcade attract screens and from:
[Wikipedia](https://en.wikipedia.org/wiki/Track_%26_Field_(video_game)),
[StrategyWiki](https://strategywiki.org/wiki/Track_%26_Field),
[Museum of the Game](https://arcade-museum.com/Videogame/track-field) and the
[TASVideos submission](https://tasvideos.org/5673S) (speed in cm/s, 1442 cm/s human maximum, hurdle penalties).
Default world records are the real 1983 records, with the EEE/FFF/GGG placeholder initials of the original.
