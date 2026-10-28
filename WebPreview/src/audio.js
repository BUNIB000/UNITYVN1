// ===== Audio: built-in sound effects and generated music loops =====
const Audio2 = (() => {
  let ctx = null, master, musicBus, sfxBus, ambBus, noiseBuf;
  const st = { music: 0.55, sfx: 0.8, on: true };
  try { const s = JSON.parse(localStorage.getItem("vn_audio") || "null"); if (s) Object.assign(st, s); } catch (e) {}
  const save = () => { try { localStorage.setItem("vn_audio", JSON.stringify(st)); } catch (e) {} };

  function init() {
    if (ctx) { if (ctx.state === "suspended") ctx.resume(); return; }
    try { ctx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { return; }
    master = ctx.createGain(); master.connect(ctx.destination);
    musicBus = ctx.createGain(); musicBus.connect(master);
    sfxBus = ctx.createGain(); sfxBus.connect(master);
    ambBus = ctx.createGain(); ambBus.connect(master);
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const d = noiseBuf.getChannelData(0); for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    apply();
  }
  function apply() {
    if (!ctx) return;
    master.gain.value = st.on ? 1 : 0;
    musicBus.gain.value = st.music * 0.5;
    sfxBus.gain.value = st.sfx * 0.7;
    ambBus.gain.value = st.sfx * 0.35;
  }
  function tone(freq, t, dur, type, vol, bus, attack, slideTo) {
    const o = ctx.createOscillator(), g = ctx.createGain();
    o.type = type || "sine"; o.frequency.setValueAtTime(freq, t);
    if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(vol || 0.3, t + (attack || 0.005));
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(bus || sfxBus); o.start(t); o.stop(t + dur + 0.05);
  }
  function noise(t, dur, vol, type, freq, bus, q) {
    const s = ctx.createBufferSource(); s.buffer = noiseBuf;
    const f = ctx.createBiquadFilter(); f.type = type || "lowpass"; f.frequency.value = freq || 1000; if (q) f.Q.value = q;
    const g = ctx.createGain(); g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(f); f.connect(g); g.connect(bus || sfxBus); s.start(t, Math.random()); s.stop(t + dur + 0.05);
  }
  const N = n => 440 * Math.pow(2, (n - 69) / 12);

  const SFX = {
    click: t => tone(880, t, 0.06, "triangle", 0.2),
    pop: t => tone(520, t, 0.12, "sine", 0.3, null, 0.004, 980),
    blip: t => tone(1200, t, 0.04, "square", 0.05),
    success: t => [72, 76, 79, 84].forEach((n, i) => tone(N(n), t + i * 0.08, 0.3, "triangle", 0.22)),
    fail: t => [67, 63, 60].forEach((n, i) => tone(N(n), t + i * 0.12, 0.35, "triangle", 0.2)),
    whoosh: t => noise(t, 0.35, 0.25, "bandpass", 1400, null, 0.8),
    hit: t => tone(N(84), t, 0.12, "triangle", 0.25),
    miss: t => tone(160, t, 0.18, "sawtooth", 0.08, null, 0.004, 90),
    match: t => { tone(N(79), t, 0.15, "triangle", 0.2); tone(N(86), t + 0.08, 0.25, "triangle", 0.2); },
    right: t => { tone(N(76), t, 0.12, "triangle", 0.22); tone(N(83), t + 0.09, 0.25, "triangle", 0.22); },
    wrong: t => tone(180, t, 0.25, "square", 0.08, null, 0.004, 120),
    tick: t => tone(1500, t, 0.03, "sine", 0.1),
    count: t => tone(N(72), t, 0.15, "triangle", 0.25),
    go: t => tone(N(84), t, 0.35, "triangle", 0.3),
    door_bell: t => { tone(N(88), t, 1.2, "sine", 0.22); tone(N(84), t + 0.18, 1.4, "sine", 0.2); tone(N(100), t, 0.6, "sine", 0.05); },
    thunder: t => { noise(t, 2.2, 0.7, "lowpass", 220); noise(t + 0.1, 1.2, 0.4, "lowpass", 600); },
    door_open: t => { noise(t, 0.25, 0.3, "bandpass", 900, null, 3); tone(140, t + 0.05, 0.4, "sawtooth", 0.05, null, 0.01, 90); },
    door: t => noise(t, 0.2, 0.4, "lowpass", 400),
    knock: t => [0, 0.18, 0.36].forEach(d => noise(t + d, 0.08, 0.6, "lowpass", 300)),
    phone: t => [0, 0.15].forEach(d => tone(N(88), t + d, 0.1, "square", 0.06)),
    notify: t => { tone(N(81), t, 0.18, "sine", 0.2); tone(N(88), t + 0.1, 0.3, "sine", 0.2); },
    catch: t => tone(N(81), t, 0.1, "triangle", 0.22, null, 0.004, N(88)),
    ouch: t => tone(300, t, 0.2, "square", 0.08, null, 0.004, 140),
    flip: t => noise(t, 0.08, 0.25, "highpass", 2500),
    type: t => tone(1800 + Math.random() * 300, t, 0.018, "sine", 0.03),
  };
  function sfx(name) {
    init(); if (!ctx || !st.on) return;
    const n = (name || "").toLowerCase();
    let f = SFX[n];
    if (!f && n.startsWith("note_")) { const k = +n.slice(5) || 1; f = t => tone(N([60, 64, 67, 72, 76, 79][(k - 1) % 6]), t, 0.35, "triangle", 0.25); }
    if (!f) { if (/bell|ding/.test(n)) f = SFX.door_bell; else if (/thunder|boom|crash/.test(n)) f = SFX.thunder; else if (/door/.test(n)) f = SFX.door_open; else f = SFX.pop; }
    f(ctx.currentTime + 0.01);
  }
  function voice(pitch) {
    if (!ctx || !st.on) return;
    tone(pitch, ctx.currentTime, 0.05, "triangle", 0.035);
  }

  // ---------- music ----------
  let musicTimer = null, musicName = null, musicGain = null;
  const MODES = { major: [0, 2, 4, 5, 7, 9, 11], minor: [0, 2, 3, 5, 7, 8, 10] };
  function style(name) {
    const n = name.toLowerCase();
    if (/party|dance|club|battle|minigame|rhythm/.test(n)) return { bpm: 118, mode: "major", root: 57, prog: [0, 5, 3, 4], drums: 2, bright: 1 };
    if (/night|sad|mystery|secret|dark|rain/.test(n)) return { bpm: 72, mode: "minor", root: 57, prog: [0, 5, 2, 6], drums: 0, bright: 0 };
    if (/cafe|coffee|jazz/.test(n)) return { bpm: 92, mode: "major", root: 60, prog: [1, 4, 0, 5], drums: 1, bright: 0.6, seventh: 1 };
    if (/ready|morning|walk|happy/.test(n)) return { bpm: 104, mode: "major", root: 62, prog: [0, 4, 5, 3], drums: 1, bright: 1 };
    const h = Art.hash(n);
    return { bpm: 84 + h % 30, mode: h % 2 ? "minor" : "major", root: 55 + h % 8, prog: [0, 3 + h % 3, 5, 4], drums: h % 3 === 0 ? 1 : 0, bright: 0.5 };
  }
  function chordNotes(S, deg) {
    const sc = MODES[S.mode], pick = i => S.root + sc[(deg + i) % 7] + 12 * Math.floor((deg + i) / 7);
    const notes = [pick(0), pick(2), pick(4)]; if (S.seventh) notes.push(pick(6));
    return notes;
  }
  function music(name, fade) {
    init(); if (!ctx) return;
    if (!name || name === "none" || name === "stop" || name === "off") { stopMusic(fade); return; }
    if (name === musicName) return;
    stopMusic(1);
    musicName = name;
    const S = style(name), beat = 60 / S.bpm, bar = beat * 4;
    musicGain = ctx.createGain(); musicGain.gain.setValueAtTime(0.0001, ctx.currentTime); musicGain.gain.exponentialRampToValueAtTime(1, ctx.currentTime + 1.5);
    const lp = ctx.createBiquadFilter(); lp.type = "lowpass"; lp.frequency.value = 1800 + S.bright * 2200;
    musicGain.connect(lp); lp.connect(musicBus);
    const bus = musicGain;
    let next = ctx.currentTime + 0.1, barN = 0;
    const R = (() => { let s = Art.hash(name); return () => (s = (s * 16807) % 2147483647) / 2147483647; })();
    const pattern = Array.from({ length: 8 }, () => Math.floor(R() * 4));
    const sched = () => {
      while (next < ctx.currentTime + 0.6) {
        const deg = S.prog[barN % S.prog.length], ch = chordNotes(S, deg);
        // pad
        for (const n of ch) { tone(N(n), next, bar * 1.02, "triangle", 0.045, bus, 0.4); tone(N(n) * 1.004, next, bar * 1.02, "sine", 0.03, bus, 0.5); }
        // bass
        tone(N(ch[0] - 24), next, beat * 1.8, "triangle", 0.16, bus, 0.01);
        tone(N(ch[0] - 24 + (S.mode === "minor" ? 7 : 7)), next + beat * 2, beat * 1.8, "triangle", 0.12, bus, 0.01);
        // arpeggio
        for (let i = 0; i < 8; i++) {
          if (S.drums === 0 && i % 2) continue;
          const p = pattern[(i + barN) % 8];
          tone(N(ch[p % ch.length] + 12), next + i * beat / 2, beat * 0.9, "sine", 0.06 + S.bright * 0.02, bus, 0.005);
        }
        if (S.drums) for (let i = 0; i < 4; i++) {
          const t = next + i * beat;
          if (S.drums === 2 || i % 2 === 0) { const o = ctx.createOscillator(), g = ctx.createGain(); o.frequency.setValueAtTime(140, t); o.frequency.exponentialRampToValueAtTime(45, t + 0.15); g.gain.setValueAtTime(S.drums === 2 ? 0.5 : 0.25, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.2); o.connect(g); g.connect(bus); o.start(t); o.stop(t + 0.25); }
          noise(t + beat / 2, 0.05, S.drums === 2 ? 0.12 : 0.05, "highpass", 7000, bus);
          if (S.drums === 2 && i % 2 === 1) noise(t, 0.15, 0.18, "bandpass", 1800, bus, 0.7);
        }
        next += bar; barN++;
      }
    };
    sched(); musicTimer = setInterval(sched, 200);
  }
  function stopMusic(fade) {
    if (musicTimer) clearInterval(musicTimer); musicTimer = null;
    if (musicGain && ctx) { const g = musicGain; g.gain.cancelScheduledValues(ctx.currentTime); g.gain.setValueAtTime(g.gain.value || 0.5, ctx.currentTime); g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + (fade || 1)); setTimeout(() => g.disconnect(), (fade || 1) * 1000 + 300); }
    musicGain = null; musicName = null;
  }

  // ---------- ambience ----------
  let amb = null, ambName = null;
  function ambience(name) {
    init(); if (!ctx) return;
    if (amb) { const a = amb; a.g.gain.setTargetAtTime(0.0001, ctx.currentTime, 0.4); setTimeout(() => { try { a.src.stop(); } catch (e) {} }, 1500); amb = null; }
    ambName = name;
    if (!name || name === "none" || name === "stop") { ambName = null; return; }
    const src = ctx.createBufferSource(); src.buffer = noiseBuf; src.loop = true;
    const f = ctx.createBiquadFilter(); const g = ctx.createGain();
    const n = name.toLowerCase();
    if (/rain|storm|shower/.test(n)) { f.type = "highpass"; f.frequency.value = 900; g.gain.value = 0.35; }
    else { f.type = "bandpass"; f.frequency.value = 500; f.Q.value = 0.6; g.gain.value = 0.22; const lfo = ctx.createOscillator(), lg = ctx.createGain(); lfo.frequency.value = 0.35; lg.gain.value = 0.08; lfo.connect(lg); lg.connect(g.gain); lfo.start(); }
    src.connect(f); f.connect(g); g.connect(ambBus); src.start();
    amb = { src, g };
  }
  function stopAll() { stopMusic(0.6); ambience(null); }

  return {
    init, sfx, voice, music, ambience, stopAll, get state() { return st; },
    set(k, v) { st[k] = v; apply(); save(); },
    get musicName() { return musicName; }, get ambName() { return ambName; },
  };
})();
