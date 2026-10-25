// Prank & Field — browser backend.
// Responsibilities: drive the .NET game loop, paint the vector draw list on a <canvas>,
// collect keyboard / gamepad / touch input, synthesize the chiptune + robotic singing with
// Web Audio, speak announcer lines with the Speech Synthesis API and persist records.

const LOGICAL_W = 256;
const LOGICAL_H = 224;
const ASSEMBLY = "PrankAndField.UI";

const BIT = { run1: 1, run2: 2, action: 4, start: 8, up: 16, down: 32, left: 64, right: 128, debug: 256 };

const state = {
  canvas: null,
  ctx: null,
  stage: null,
  root: null,
  bridge: null,
  audio: null,
  running: false,
  raf: 0,
  lastTs: 0,
  p1: 0,
  p2: 0,
  touch: 0,
  // Presses that happened since the last frame; guarantees a tap shorter than a frame still counts.
  latch1: 0,
  latch2: 0,
  latchTouch: 0,
  resizeObserver: null,
  listeners: [],
  fps: { frames: 0, last: 0 },
};

// ------------------------------------------------------------------ bridge to .NET

async function createBridge() {
  // Blazor WebAssembly exposes the runtime; [JSExport] methods marshal arrays without JSON.
  if (typeof globalThis.getDotnetRuntime === "function") {
    try {
      const runtime = globalThis.getDotnetRuntime(0);
      let exports;
      try { exports = await runtime.getAssemblyExports(ASSEMBLY + ".dll"); }
      catch { exports = await runtime.getAssemblyExports(ASSEMBLY); }
      const api = exports.PrankAndField.UI.BrowserInterop;
      return {
        kind: "wasm",
        init: (r, l) => api.Initialize(r ?? null, l ?? null),
        frame: (dt, a, b) => api.Frame(dt, a, b),
        setLanguage: (l) => api.SetLanguage(l),
        getLanguage: () => api.GetLanguage(),
        getRecords: () => api.GetRecordsJson(),
        getCatalog: () => api.GetAudioCatalogJson(),
        getVoices: () => api.GetVoicePhrasesJson(),
        debugState: () => api.GetDebugState(),
        jumpTo: (kind, round, players) => api.JumpToEvent(kind, round, players),
      };
    } catch (err) {
      console.warn("[paf] JSExport bridge unavailable, falling back to JSInvokable", err);
    }
  }
  // Blazor Hybrid (desktop WebView) or any other host: JSON over the DotNet bridge.
  const call = (name, ...args) => DotNet.invokeMethodAsync(ASSEMBLY, name, ...args);
  return {
    kind: "hybrid",
    init: (r, l) => call("Initialize", r ?? null, l ?? null),
    frame: (dt, a, b) => call("Frame", dt, a, b),
    setLanguage: (l) => call("SetLanguage", l),
    getLanguage: () => call("GetLanguage"),
    getRecords: () => call("GetRecordsJson"),
    getCatalog: () => call("GetAudioCatalogJson"),
    getVoices: () => call("GetVoicePhrasesJson"),
    debugState: () => call("GetDebugState"),
    jumpTo: (kind, round, players) => call("JumpToEvent", kind, round, players),
  };
}

const EVENT_IDS = { dash: 0, longjump: 1, javelin: 2, hurdles: 3, hammer: 4, highjump: 5 };

/**
 * Console helpers for practice and debugging, e.g. `paf.jumpTo("hammer", 2)` or `await paf.state()`.
 * Also handy for automated play-testing.
 */
function installConsoleHelpers() {
  globalThis.paf = {
    state: async () => JSON.parse(await state.bridge.debugState()),
    jumpTo: (event, round = 1, players = 1) => {
      const id = typeof event === "number" ? event : EVENT_IDS[String(event).toLowerCase()];
      if (id === undefined) throw new Error("unknown event; use one of " + Object.keys(EVENT_IDS).join(", "));
      return state.bridge.jumpTo(id, round, players);
    },
    setLanguage,
    setMuted,
  };
}

// ------------------------------------------------------------------ public API

export async function start(canvasId, rootId, language) {
  stop();
  state.canvas = document.getElementById(canvasId);
  state.root = document.getElementById(rootId);
  state.stage = state.canvas.parentElement;
  state.ctx = state.canvas.getContext("2d", { alpha: false });
  state.bridge = await createBridge();
  state.audio = new AudioEngine();

  let records = null;
  let lang = language;
  try {
    records = localStorage.getItem("paf.records");
    if (!lang) lang = localStorage.getItem("paf.lang") || navigator.language || "en";
  } catch { /* storage may be unavailable */ }

  await state.bridge.init(records, lang);
  const actual = await state.bridge.getLanguage();
  state.audio.setCatalog(JSON.parse(await state.bridge.getCatalog()));
  state.audio.setVoices(JSON.parse(await state.bridge.getVoices()));
  try { localStorage.setItem("paf.lang", actual); } catch { /* ignore */ }

  installInput();
  installResize();
  installConsoleHelpers();
  resize();
  state.running = true;
  state.lastTs = performance.now();
  state.raf = requestAnimationFrame(loop);
  state.canvas.focus();
  console.info(`[paf] started (${state.bridge.kind} bridge), language ${actual}`);
  return actual;
}

export function stop() {
  state.running = false;
  if (state.raf) cancelAnimationFrame(state.raf);
  state.raf = 0;
  for (const [target, type, fn, opts] of state.listeners) target.removeEventListener(type, fn, opts);
  state.listeners = [];
  if (state.resizeObserver) { state.resizeObserver.disconnect(); state.resizeObserver = null; }
  if (state.audio) state.audio.stopMusic();
}

export async function setLanguage(lang) {
  if (!state.bridge) return;
  await state.bridge.setLanguage(lang);
  const actual = await state.bridge.getLanguage();
  state.audio.setVoices(JSON.parse(await state.bridge.getVoices()));
  try { localStorage.setItem("paf.lang", actual); } catch { /* ignore */ }
}

export function setMuted(muted) {
  if (state.audio) state.audio.setMuted(muted);
}

export function focusCanvas() {
  state.canvas?.focus();
}

export function toggleFullscreen(rootId) {
  const el = document.getElementById(rootId);
  if (!el) return;
  if (document.fullscreenElement) document.exitFullscreen?.();
  else el.requestFullscreen?.();
}

// ------------------------------------------------------------------ main loop

async function loop(ts) {
  if (!state.running) return;
  const dt = Math.min(0.1, Math.max(0, (ts - state.lastTs) / 1000));
  state.lastTs = ts;
  const p1 = state.p1 | state.latch1 | state.touch | state.latchTouch | gamepadMask(0);
  const p2 = state.p2 | state.latch2 | gamepadMask(1);
  state.latch1 = 0;
  state.latch2 = 0;
  state.latchTouch = 0;
  let buf;
  try {
    buf = await state.bridge.frame(dt, p1, p2);
  } catch (err) {
    console.error("[paf] frame failed", err);
    state.running = false;
    return;
  }
  render(buf);
  if (state.running) state.raf = requestAnimationFrame(loop);
}

// ------------------------------------------------------------------ rendering

function installResize() {
  if (typeof ResizeObserver !== "undefined") {
    state.resizeObserver = new ResizeObserver(() => resize());
    state.resizeObserver.observe(state.stage);
  } else {
    listen(window, "resize", resize);
  }
}

function resize() {
  const stage = state.stage;
  const canvas = state.canvas;
  if (!stage || !canvas) return;
  const sw = Math.max(64, stage.clientWidth);
  const sh = Math.max(56, stage.clientHeight);
  let w = sw, h = sw * LOGICAL_H / LOGICAL_W;
  if (h > sh) { h = sh; w = sh * LOGICAL_W / LOGICAL_H; }
  const dpr = Math.min(3, window.devicePixelRatio || 1);
  canvas.style.width = Math.floor(w) + "px";
  canvas.style.height = Math.floor(h) + "px";
  canvas.width = Math.max(1, Math.floor(w * dpr));
  canvas.height = Math.max(1, Math.floor(h * dpr));
}

function render(buf) {
  const canvas = state.canvas, ctx = state.ctx;
  const scale = Math.min(canvas.width / LOGICAL_W, canvas.height / LOGICAL_H);
  const ox = (canvas.width - LOGICAL_W * scale) / 2;
  const oy = (canvas.height - LOGICAL_H * scale) / 2;
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.fillStyle = "#000";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.setTransform(scale, 0, 0, scale, ox, oy);
  ctx.lineCap = "round";
  ctx.lineJoin = "round";

  let i = 0;
  const n = buf.length;
  let clipDepth = 0;
  let color = "#000";
  while (i < n) {
    const op = buf[i];
    switch (op) {
      case 0: // end
        i = n;
        break;
      case 1: { // color
        color = `rgba(${buf[i + 1]},${buf[i + 2]},${buf[i + 3]},${buf[i + 4] / 255})`;
        ctx.fillStyle = color;
        ctx.strokeStyle = color;
        i += 5;
        break;
      }
      case 2: // rect
        ctx.fillRect(buf[i + 1], buf[i + 2], buf[i + 3], buf[i + 4]);
        i += 5;
        break;
      case 3: { // polygon
        const count = buf[i + 1];
        ctx.beginPath();
        ctx.moveTo(buf[i + 2], buf[i + 3]);
        for (let k = 1; k < count; k++) ctx.lineTo(buf[i + 2 + 2 * k], buf[i + 3 + 2 * k]);
        ctx.closePath();
        ctx.fill();
        i += 2 + 2 * count;
        break;
      }
      case 4: // circle
        ctx.beginPath();
        ctx.arc(buf[i + 1], buf[i + 2], buf[i + 3], 0, Math.PI * 2);
        ctx.fill();
        i += 4;
        break;
      case 5: // line
        ctx.lineWidth = buf[i + 5];
        ctx.beginPath();
        ctx.moveTo(buf[i + 1], buf[i + 2]);
        ctx.lineTo(buf[i + 3], buf[i + 4]);
        ctx.stroke();
        i += 6;
        break;
      case 6: { // text
        const x = buf[i + 1], y = buf[i + 2], size = buf[i + 3], align = buf[i + 4], len = buf[i + 5];
        let s = "";
        for (let k = 0; k < len; k++) s += String.fromCharCode(buf[i + 6 + k]);
        ctx.font = `bold ${size}px "Segoe UI", "Yu Gothic UI", "Meiryo", "Noto Sans JP", "Hiragino Sans", sans-serif`;
        ctx.textAlign = align === 1 ? "center" : align === 2 ? "right" : "left";
        ctx.textBaseline = "alphabetic";
        ctx.fillText(s, x, y);
        i += 6 + len;
        break;
      }
      case 7: // clip push
        ctx.save();
        ctx.beginPath();
        ctx.rect(buf[i + 1], buf[i + 2], buf[i + 3], buf[i + 4]);
        ctx.clip();
        clipDepth++;
        i += 5;
        break;
      case 8: // clip pop
        if (clipDepth > 0) { ctx.restore(); clipDepth--; ctx.fillStyle = color; ctx.strokeStyle = color; }
        i += 1;
        break;
      case 9: // ellipse
        ctx.beginPath();
        ctx.ellipse(buf[i + 1], buf[i + 2], buf[i + 3], buf[i + 4], 0, 0, Math.PI * 2);
        ctx.fill();
        i += 5;
        break;
      case 10: { // polyline
        const width = buf[i + 1], count = buf[i + 2];
        ctx.lineWidth = width;
        ctx.beginPath();
        ctx.moveTo(buf[i + 3], buf[i + 4]);
        for (let k = 1; k < count; k++) ctx.lineTo(buf[i + 3 + 2 * k], buf[i + 4 + 2 * k]);
        ctx.stroke();
        i += 3 + 2 * count;
        break;
      }
      case 100: { // audio section
        const count = buf[i + 1];
        for (let k = 0; k < count; k++) {
          const kind = buf[i + 2 + 3 * k], a = buf[i + 3 + 3 * k], b = buf[i + 4 + 3 * k];
          try { handleAudio(kind, a, b); } catch (err) { console.warn("[paf] audio event failed", kind, err); } // audio must never stop the game
        }
        i += 2 + 3 * count;
        break;
      }
      default:
        console.warn("[paf] unknown opcode", op, "at", i);
        i = n;
    }
  }
  while (clipDepth > 0) { ctx.restore(); clipDepth--; }
}

function handleAudio(kind, a, b) {
  const audio = state.audio;
  switch (kind) {
    case 1: audio.playSong(a, b === 1); break;
    case 2: audio.playSound(a); break;
    case 3: audio.speak(a); break;
    case 4: audio.stopMusic(); break;
    case 5: audio.speakMark(a, b === 1); break;
    case 6: audio.flightTone(a / 1000, b === 1); break;
    case 9: saveRecords(); break;
  }
}

async function saveRecords() {
  try {
    const json = await state.bridge.getRecords();
    localStorage.setItem("paf.records", json);
  } catch (err) {
    console.warn("[paf] could not persist records", err);
  }
}

// ------------------------------------------------------------------ input

function listen(target, type, fn, opts) {
  target.addEventListener(type, fn, opts);
  state.listeners.push([target, type, fn, opts]);
}

const KEYMAP_P1 = {
  KeyZ: BIT.run1, KeyX: BIT.run2, Space: BIT.action, Enter: BIT.start, Digit1: BIT.start, Numpad1: BIT.start,
  ArrowUp: BIT.up, ArrowDown: BIT.down, KeyW: BIT.up, KeyS: BIT.down, KeyA: BIT.left, KeyD: BIT.right,
  KeyP: BIT.debug, F2: BIT.debug, // hidden practice menu
};
const KEYMAP_P2 = {
  ArrowLeft: BIT.run1, ArrowRight: BIT.run2, ArrowUp: BIT.action, Digit2: BIT.start, Numpad2: BIT.start,
  KeyN: BIT.run1, KeyM: BIT.run2, ShiftRight: BIT.action, ControlRight: BIT.action,
};
const PREVENT = new Set(["Space", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Enter"]);

function installInput() {
  const onKey = (down) => (e) => {
    if (e.target && (e.target.tagName === "SELECT" || e.target.tagName === "INPUT")) return;
    let handled = false;
    const m1 = KEYMAP_P1[e.code];
    if (m1) { state.p1 = down ? (state.p1 | m1) : (state.p1 & ~m1); if (down) state.latch1 |= m1; handled = true; }
    const m2 = KEYMAP_P2[e.code];
    if (m2) { state.p2 = down ? (state.p2 | m2) : (state.p2 & ~m2); if (down) state.latch2 |= m2; handled = true; }
    if (handled) {
      if (down && !e.repeat) state.audio.unlock();
      if (PREVENT.has(e.code)) e.preventDefault();
    }
  };
  listen(window, "keydown", onKey(true));
  listen(window, "keyup", onKey(false));
  listen(window, "blur", () => { state.p1 = 0; state.p2 = 0; state.touch = 0; state.latch1 = 0; state.latch2 = 0; state.latchTouch = 0; });

  // touch buttons
  const buttons = state.root.querySelectorAll("[data-paf-key]");
  for (const btn of buttons) {
    const bit = BIT[btn.dataset.pafKey] || 0;
    const press = (e) => { e.preventDefault(); state.touch |= bit; state.latchTouch |= bit; btn.classList.add("is-down"); state.audio.unlock(); };
    const release = (e) => { e.preventDefault(); state.touch &= ~bit; btn.classList.remove("is-down"); };
    listen(btn, "pointerdown", press);
    listen(btn, "pointerup", release);
    listen(btn, "pointercancel", release);
    listen(btn, "pointerleave", release);
    listen(btn, "contextmenu", (e) => e.preventDefault());
  }
  listen(state.canvas, "pointerdown", () => { state.audio.unlock(); state.canvas.focus(); });
}

function gamepadMask(index) {
  if (!navigator.getGamepads) return 0;
  let pads;
  try { pads = navigator.getGamepads(); } catch { return 0; }
  const pad = pads && pads[index];
  if (!pad) return 0;
  const b = (k) => pad.buttons[k] && (pad.buttons[k].pressed || pad.buttons[k].value > 0.5);
  let m = 0;
  if (b(0)) m |= BIT.run1;
  if (b(1)) m |= BIT.run2;
  if (b(2) || b(3) || b(5) || b(7)) m |= BIT.action;
  if (b(9)) m |= BIT.start;
  if (b(12)) m |= BIT.up;
  if (b(13)) m |= BIT.down;
  if (b(14)) m |= BIT.left;
  if (b(15)) m |= BIT.right;
  const ay = pad.axes[1] || 0;
  if (ay < -0.6) m |= BIT.up;
  if (ay > 0.6) m |= BIT.down;
  return m;
}

// ------------------------------------------------------------------ audio

const FORMANTS = {
  a: [[800, 8, 1.0], [1150, 10, 0.5], [2900, 12, 0.25]],
  e: [[400, 8, 1.0], [1700, 10, 0.45], [2600, 12, 0.3]],
  i: [[280, 8, 1.0], [2300, 12, 0.3], [3000, 14, 0.25]],
  o: [[450, 8, 1.0], [800, 10, 0.5], [2600, 12, 0.15]],
  u: [[330, 8, 1.0], [700, 10, 0.4], [2500, 12, 0.1]],
};

function vowelOf(syllable) {
  const s = (syllable || "a").toLowerCase();
  const m = s.match(/[aeiouy]/);
  if (!m) return "a";
  const v = m[0] === "y" ? "i" : m[0];
  return FORMANTS[v] ? v : "a";
}

function consonantOf(syllable) {
  const s = (syllable || "").toLowerCase();
  const c = s.match(/^[^aeiouy]+/);
  if (!c) return null;
  const first = c[0][0];
  if ("sfhxz".includes(first) || c[0].startsWith("sh") || c[0].startsWith("ch")) return "fric";
  if ("ptkbdgqc".includes(first)) return "stop";
  return "soft";
}

function midiToFreq(m) { return 440 * Math.pow(2, (m - 69) / 12); }

class AudioEngine {
  constructor() {
    this.ctx = null;
    this.master = null;
    this.music = null;
    this.catalog = { songs: [] };
    this.voices = { lang: "en-US", phrases: {} };
    this.muted = false;
    this.current = null;
    this.timer = 0;
    this.noiseBuffer = null;
    this.flight = null;
    // Chrome loads voices asynchronously; make sure the list is populated.
    if ("speechSynthesis" in window) {
      window.speechSynthesis.getVoices();
      window.speechSynthesis.addEventListener?.("voiceschanged", () => window.speechSynthesis.getVoices());
    }
  }

  unlock() {
    if (!this.ctx) {
      const Ctx = window.AudioContext || window.webkitAudioContext;
      if (!Ctx) return;
      this.ctx = new Ctx();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.muted ? 0 : 0.6;
      this.master.connect(this.ctx.destination);
      this.music = this.ctx.createGain();
      this.music.gain.value = 0.55;
      this.music.connect(this.master);
      this.noiseBuffer = this.makeNoise();
      if (this.pendingSong) { const p = this.pendingSong; this.pendingSong = null; this.playSong(p.id, p.loop); }
    }
    if (this.ctx.state === "suspended") this.ctx.resume();
  }

  setCatalog(catalog) { this.catalog = catalog || { songs: [] }; }
  setVoices(voices) { this.voices = voices || { lang: "en-US", phrases: {} }; }

  setMuted(muted) {
    this.muted = muted;
    if (this.master) this.master.gain.value = muted ? 0 : 0.6;
    if (muted && window.speechSynthesis) window.speechSynthesis.cancel();
    if (muted) this.flightTone(0, false);
  }

  makeNoise() {
    const len = this.ctx.sampleRate * 1.5;
    const buffer = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    return buffer;
  }

  // ---- music ----

  playSong(id, loop) {
    if (!this.ctx) { this.pendingSong = { id, loop }; return; }
    const song = this.catalog.songs.find((s) => s.id === id);
    if (!song) return;
    this.stopMusic();
    const beat = 60 / song.bpm;
    const voices = [
      { notes: song.lead, idx: 0, time: 0, kind: "lead" },
      { notes: song.bass, idx: 0, time: 0, kind: "bass" },
      { notes: song.arp, idx: 0, time: 0, kind: "arp" },
    ];
    const total = song.lead.reduce((a, n) => a + n.b, 0) * beat;
    this.current = { song, beat, voices, start: this.ctx.currentTime + 0.05, loop: !!(loop || song.loop), total, nodes: [] };
    this.schedule();
    this.timer = setInterval(() => this.schedule(), 90);
  }

  stopMusic() {
    if (this.timer) clearInterval(this.timer);
    this.timer = 0;
    if (this.current) {
      for (const n of this.current.nodes) { try { n.stop(); } catch { /* already stopped */ } }
    }
    this.current = null;
    this.pendingSong = null;
  }

  schedule() {
    const cur = this.current;
    if (!cur) return;
    const now = this.ctx.currentTime;
    const horizon = now + 0.35;
    for (const v of cur.voices) {
      if (!v.notes.length) continue;
      while (true) {
        if (v.idx >= v.notes.length) {
          if (!cur.loop) break;
          v.idx = 0;
          v.time = Math.max(v.time, 0);
        }
        const t = cur.start + v.time;
        if (t > horizon) break;
        const note = v.notes[v.idx];
        const dur = note.b * cur.beat;
        if (note.m > 0 && t >= now - 0.05) this.scheduleNote(v.kind, note, t, dur);
        v.time += dur;
        v.idx++;
        if (v.idx >= v.notes.length && !cur.loop) break;
      }
    }
    // auto-stop non-looping songs when every voice is done
    if (!cur.loop && cur.voices.every((v) => v.idx >= v.notes.length)) {
      const endAt = cur.start + cur.total + 1.0;
      if (now > endAt) this.stopMusic();
    }
    // prune finished nodes
    if (cur.nodes.length > 200) cur.nodes.splice(0, cur.nodes.length - 120);
  }

  scheduleNote(kind, note, t, dur) {
    const ctx = this.ctx;
    const f = midiToFreq(note.m);
    if (kind === "lead") {
      this.sing(f, t, dur, note.s);
      return;
    }
    const osc = ctx.createOscillator();
    osc.type = kind === "bass" ? "square" : "triangle";
    osc.frequency.value = f;
    const g = ctx.createGain();
    const peak = kind === "bass" ? 0.18 : 0.12;
    const len = Math.min(dur * 0.9, kind === "bass" ? 0.25 : 0.14);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(peak, t + 0.005);
    g.gain.exponentialRampToValueAtTime(0.0001, t + len);
    osc.connect(g);
    g.connect(this.music);
    osc.start(t);
    osc.stop(t + len + 0.02);
    this.current.nodes.push(osc);
  }

  // Robotic singing: a buzzy sawtooth through three formant band-passes with vibrato,
  // doubled by a square wave, with a short noise burst for the consonant onset.
  sing(freq, t, dur, syllable) {
    const ctx = this.ctx;
    const vowel = vowelOf(syllable);
    const env = ctx.createGain();
    const attack = 0.03, release = 0.06;
    const hold = Math.max(0.05, dur - attack - release - 0.02);
    env.gain.setValueAtTime(0.0001, t);
    env.gain.exponentialRampToValueAtTime(0.9, t + attack);
    env.gain.setValueAtTime(0.9, t + attack + hold);
    env.gain.exponentialRampToValueAtTime(0.0001, t + attack + hold + release);
    env.connect(this.music);

    const saw = ctx.createOscillator();
    saw.type = "sawtooth";
    saw.frequency.value = freq;
    const vib = ctx.createOscillator();
    vib.frequency.value = 6;
    const vibGain = ctx.createGain();
    vibGain.gain.value = freq * 0.015;
    vib.connect(vibGain);
    vibGain.connect(saw.frequency);

    for (const [ff, q, gain] of FORMANTS[vowel]) {
      const bp = ctx.createBiquadFilter();
      bp.type = "bandpass";
      bp.frequency.value = ff;
      bp.Q.value = q;
      const g = ctx.createGain();
      g.gain.value = gain * 0.5;
      saw.connect(bp);
      bp.connect(g);
      g.connect(env);
    }
    // chip doubling an octave below for body
    const sq = ctx.createOscillator();
    sq.type = "square";
    sq.frequency.value = freq / 2;
    const sqGain = ctx.createGain();
    sqGain.gain.value = 0.05;
    sq.connect(sqGain);
    sqGain.connect(env);

    const end = t + attack + hold + release + 0.05;
    saw.start(t); saw.stop(end);
    vib.start(t); vib.stop(end);
    sq.start(t); sq.stop(end);
    this.current.nodes.push(saw, vib, sq);

    const cons = consonantOf(syllable);
    if (cons) this.noise(t - 0.01, cons === "fric" ? 0.09 : 0.03, cons === "fric" ? 3500 : 1800, cons === "fric" ? 0.12 : 0.2);
  }

  noise(t, dur, freq, gain, type = "bandpass") {
    const ctx = this.ctx;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    const filt = ctx.createBiquadFilter();
    filt.type = type;
    filt.frequency.value = freq;
    filt.Q.value = 0.8;
    const g = ctx.createGain();
    const t0 = Math.max(t, ctx.currentTime);
    g.gain.setValueAtTime(gain, t0);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    src.connect(filt); filt.connect(g); g.connect(this.master);
    src.start(t0); src.stop(t0 + dur + 0.02);
    return { src, filt, g };
  }

  tone(t, dur, f0, f1, type, gain) {
    const ctx = this.ctx;
    const osc = ctx.createOscillator();
    osc.type = type;
    const t0 = Math.max(t, ctx.currentTime);
    osc.frequency.setValueAtTime(f0, t0);
    if (f1 && f1 !== f0) osc.frequency.exponentialRampToValueAtTime(f1, t0 + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, t0);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    osc.connect(g); g.connect(this.master);
    osc.start(t0); osc.stop(t0 + dur + 0.02);
  }

  // ---- effects ----

  playSound(id) {
    if (!this.ctx || this.muted) return;
    const t = this.ctx.currentTime;
    switch (id) {
      case 1: // gun
        this.noise(t, 0.25, 900, 0.9, "lowpass");
        this.tone(t, 0.12, 180, 40, "square", 0.5);
        break;
      case 2: // step
        this.tone(t, 0.05, 120, 60, "sine", 0.15);
        break;
      case 3: // jump
        this.tone(t, 0.18, 300, 900, "square", 0.18);
        break;
      case 4: // land
        this.noise(t, 0.12, 400, 0.4, "lowpass");
        this.tone(t, 0.1, 90, 50, "sine", 0.4);
        break;
      case 5: // throw
        this.noise(t, 0.3, 1200, 0.3, "bandpass");
        this.tone(t, 0.25, 600, 1500, "triangle", 0.1);
        break;
      case 6: // hurdle hit
        this.noise(t, 0.15, 2500, 0.5, "highpass");
        this.tone(t, 0.3, 700, 150, "sawtooth", 0.25);
        break;
      case 7: // bar fall
        this.tone(t, 0.4, 1200, 300, "triangle", 0.25);
        this.tone(t + 0.15, 0.3, 900, 200, "triangle", 0.2);
        this.noise(t + 0.3, 0.2, 600, 0.3, "lowpass");
        break;
      case 8: { // crowd roar
        const n = this.noise(t, 1.6, 700, 0.0001, "bandpass");
        n.g.gain.setValueAtTime(0.0001, t);
        n.g.gain.exponentialRampToValueAtTime(0.5, t + 0.3);
        n.g.gain.exponentialRampToValueAtTime(0.0001, t + 1.6);
        break;
      }
      case 9: // bird
        for (let k = 0; k < 4; k++) this.tone(t + k * 0.09, 0.07, 2200 + k * 150, 3200, "sine", 0.2);
        break;
      case 10: // mole
        this.tone(t, 0.15, 220, 160, "square", 0.2);
        this.tone(t + 0.18, 0.15, 260, 200, "square", 0.2);
        break;
      case 11: // bonus
        [523, 659, 784, 1047].forEach((f, k) => this.tone(t + k * 0.08, 0.14, f, f, "square", 0.2));
        break;
      case 12: // menu move
        this.tone(t, 0.06, 900, 1200, "square", 0.15);
        break;
      case 13: // menu select
        this.tone(t, 0.08, 700, 700, "square", 0.2);
        this.tone(t + 0.09, 0.14, 1050, 1050, "square", 0.2);
        break;
      case 14: // whistle
        this.tone(t, 0.35, 2600, 2700, "sine", 0.3);
        this.tone(t + 0.05, 0.3, 2650, 2550, "sine", 0.15);
        break;
      case 15: // spin whoosh
        this.noise(t, 0.2, 500, 0.25, "bandpass");
        break;
      case 16: // tick
        this.tone(t, 0.03, 1500, 1500, "square", 0.1);
        break;
    }
  }

  // ---- flight tone: pitch follows the height of whatever is flying ----

  flightTone(height, active) {
    if (!this.ctx) return;
    const now = this.ctx.currentTime;
    if (active) {
      if (!this.flight) {
        const osc = this.ctx.createOscillator();
        osc.type = "triangle";
        const g = this.ctx.createGain();
        g.gain.setValueAtTime(0.0001, now);
        g.gain.exponentialRampToValueAtTime(this.muted ? 0.0001 : 0.12, now + 0.05);
        osc.connect(g);
        g.connect(this.master);
        osc.start(now);
        this.flight = { osc, g };
      }
      // two octaves from 220 Hz at ground level to 880 Hz at the apex
      const freq = 220 * Math.pow(2, 2 * Math.max(0, Math.min(1, height)));
      this.flight.osc.frequency.setTargetAtTime(freq, now, 0.03);
    } else if (this.flight) {
      const { osc, g } = this.flight;
      this.flight = null;
      g.gain.setTargetAtTime(0.0001, now, 0.04);
      osc.stop(now + 0.3);
    }
  }

  // ---- announcer (female voice, queued; urgent start-sequence calls interrupt) ----

  static URGENT = new Set([2, 3, 4, 5, 6]); // on your marks, get set, go, false start, foul

  pickVoice(lang) {
    const synth = window.speechSynthesis;
    const voices = synth.getVoices();
    if (!voices.length) return null;
    const two = (lang || "en").toLowerCase().slice(0, 2);
    const female = /female|femenin|mujer|woman|zira|helena|sabina|laura|lucia|elvira|paulina|hazel|susan|aria|jenny|libby|sonia|ayumi|haruka|nanami|sayaka|montserrat|joana|herena|alba|elsa|mia|samantha|karen|moira|tessa|fiona|victoria|kyoko|o-ren|alicia|lupe|paloma|monica|mónica|esperanza|estrella|jane|emma|olivia|natasha|neerja|rishi\b(?!.*male)/i;
    const male = /\bmale\b|masculin|hombre|david|mark|pablo|jorge|raul|raúl|alvaro|álvaro|ichiro|george|daniel|james|ryan|guy|brian|christopher|eric|jacob|liam|conrad|enrique|andrés|andres|sergio|ricardo|diego/i;
    const sameLang = voices.filter((v) => v.lang && v.lang.toLowerCase().startsWith(two));
    const pool = sameLang.length ? sameLang : voices;
    return pool.find((v) => female.test(v.name) && !male.test(v.name))
        || pool.find((v) => !male.test(v.name))
        || pool[0];
  }

  say(text, urgent) {
    if (this.muted || !text || !("speechSynthesis" in window)) return;
    try {
      const synth = window.speechSynthesis;
      if (urgent) synth.cancel();
      const u = new SpeechSynthesisUtterance(text);
      u.lang = this.voices.lang || "en-US";
      u.pitch = 1.15;
      u.rate = 1.0;
      u.volume = 1.0;
      const voice = this.pickVoice(u.lang);
      if (voice) u.voice = voice;
      synth.speak(u);
    } catch (err) {
      console.warn("[paf] speech failed", err);
    }
  }

  speak(id) {
    this.say(this.voices.phrases[String(id)], AudioEngine.URGENT.has(id));
  }

  /** Announces a mark: `cents` hundredths of a second or of a metre. */
  speakMark(cents, isTime) {
    const whole = Math.floor(cents / 100);
    const frac = String(cents % 100).padStart(2, "0");
    const template = isTime ? this.voices.time : this.voices.distance;
    if (!template) return;
    this.say(template.replace("{0}", String(whole)).replace("{1}", frac), false);
  }
}
