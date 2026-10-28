// ===== Story player: runs a story on the stage; library, menus, saves =====
const Player = (() => {
  let stage, coverStage, runner = null, prog = null, key = null, source = null, runId = 0;
  let history = [];
  const store = {
    get(k, d) { try { const v = localStorage.getItem(k); return v == null ? d : JSON.parse(v); } catch (e) { return d; } },
    set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) {} },
    del(k) { try { localStorage.removeItem(k); } catch (e) {} },
  };
  const EPISODES = () => Object.keys(STORIES).map(k => ({ key: k, meta: Core.parse(STORIES[k], k).meta })).sort((a, b) => (+a.meta.episode || 0) - (+b.meta.episode || 0));
  let vars = new Core.Vars();
  const savedVars = store.get("vn_vars", null); if (savedVars) vars.v = savedVars;

  function displayName(id) {
    const d = prog && prog.chars[id.toUpperCase()];
    if (d && d.name) return vars.interpolate(d.name, resolver);
    return id.charAt(0) + id.slice(1).toLowerCase();
  }
  function resolver(name) { if (prog && prog.chars[name.toUpperCase()] && name === name.toUpperCase()) { const d = prog.chars[name]; return d.name && !d.name.includes("{" + name) ? vars.interpolate(d.name) : name; } return null; }
  const say = t => vars.interpolate(t, resolver);

  function hud(show) {
    const h = UI.hud;
    h.hidden = !show;
    h.querySelector("[data-auto]").classList.toggle("on", UI.mode.auto);
    h.querySelector("[data-skip]").classList.toggle("on", UI.mode.skip);
  }
  function setSkip(on) { UI.mode.skip = on; Clock.scale = on ? 6 : 1; hud(true); UI.kick(); }
  function setAuto(on) { UI.mode.auto = on; hud(true); UI.kick(); }

  function stopRun() {
    runId++;
    UI.clearAll(); Clock.clear(); Clock.scale = 1; UI.mode.skip = false;
    UI.mgLayer.innerHTML = "";
  }

  async function play(k, opts) {
    opts = opts || {};
    stopRun();
    const my = runId;
    key = k; source = opts.source || STORIES[k];
    prog = Core.parse(source, k);
    const errs = prog.errors.filter(e => !e.warn);
    if (errs.length) { showErrors(errs); return; }
    stage.reset(); stage.defs = prog.chars;
    if (opts.fresh) { history = []; }
    runner = new Core.Runner(prog, vars);
    closeScreens();
    const fadeEl = UI.frame.querySelector("#fade"); fadeEl.style.opacity = 1;
    if (opts.restore) {
      const s = opts.restore;
      vars.v = s.vars; stage.restore(s.stage); runner.pc = s.pc; runner.stack = s.stack || [];
      history = s.history || [];
      if (s.music) Audio2.music(s.music); if (s.amb) Audio2.ambience(s.amb);
    } else if (!opts.keepAudio) Audio2.stopAll();
    hud(true);
    UI.fade(0, 0.6);
    await loop(my);
  }

  async function loop(my) {
    for (;;) {
      if (my !== runId) return;
      let ins;
      try { ins = runner.next(); } catch (e) { showErrors([{ line: e.line, msg: e.message }]); return; }
      try { await exec(ins, my); }
      catch (e) { console.error(e); showErrors([{ line: ins.line, msg: e.message || String(e) }]); return; }
      if (ins.type === "end" || ins.type === "next") return;
    }
  }

  function autosave() {
    if (!runner || key === "__lab") return;
    store.set("vn_save_" + key, { pc: runner.cur, stack: runner.stack, vars: vars.v, stage: stage.snapshot(), music: Audio2.musicName, amb: Audio2.ambName, history: history.slice(-60), t: Date.now() });
  }

  async function exec(ins, my) {
    switch (ins.type) {
      case "say": return sayLine(ins, my);
      case "character": {
        const p = runCharacter(ins);
        if (!ins.parallel) await p;
        return;
      }
      case "background": return background(ins);
      case "command": { const p = command(ins); if (!ins.parallel) await p; return; }
      case "choice": {
        const vis = runner.visible(ins);
        if (!vis.length) { runner.pc = ins.end; return; }
        if (UI.mode.skip) setSkip(false);
        const i = await UI.choice(ins.prompt ? say(ins.prompt) : null, vis.map(v => ({ text: say(v.o.text), enabled: v.enabled, tag: v.o.tag, isDefault: v.o.isDefault })), ins.timer);
        if (my !== runId) return;
        history.push({ name: "You chose", text: say(vis[i].o.text) });
        runner.choose(ins, vis[i].o);
        return;
      }
      case "input": {
        if (UI.mode.skip) setSkip(false);
        const v = await UI.ask(say(ins.prompt), ins.def);
        if (my !== runId) return;
        runner.submitInput(ins, v);
        return;
      }
      case "minigame": {
        if (UI.mode.skip) setSkip(false);
        UI.clearBubble();
        hud(false);
        const r = await Minigames.run(ins.game, ins.params, ins.data, { stage, vars, say, displayName, story: true });
        if (my !== runId) return;
        hud(true);
        runner.resolveMinigame(ins, r.won, r.score);
        return;
      }
      case "next": return endEpisode(ins.story, my);
      case "end": return endEpisode(null, my);
    }
  }

  async function sayLine(ins, my) {
    autosave();
    const text = say(ins.text);
    const c = ins.speaker ? stage.chars.get(ins.speaker.toUpperCase()) : null;
    const onStage = c && c.onStage;
    if (ins.speaker && !c && prog.chars[ins.speaker.toUpperCase()]) stage.character(ins.speaker);
    if (onStage && ins.anim) c.play(ins.anim);
    const name = ins.speaker ? displayName(ins.speaker) : null;
    history.push({ name, text });
    let n = 0;
    const pitch = c ? 380 + (Art.hash(c.id) % 7) * 45 : 0;
    if (onStage) c.talking = true;
    await UI.say({
      name, text, style: ins.style,
      color: c ? c.color : ins.speaker ? Art.makeLook(ins.speaker.toUpperCase(), prog.chars[ins.speaker.toUpperCase()] && prog.chars[ins.speaker.toUpperCase()].opts).color : null,
      anchor: onStage ? () => { if (!c.onStage) return null; const h = c.headPx(); return stage.toScreen(h.x, h.y); } : null,
      onLetter: ch => {
        if (!onStage || ins.style === "thought" || ins.style === "phone") return;
        if (/[\s.,!?…]/.test(ch)) { c.mouthOpen = false; n = 0; return; }
        n++;
        if (n % 2 === 1) c.mouthOpen = !c.mouthOpen;
        if (n % 3 === 1) Audio2.voice(pitch + (/[aeiou]/i.test(ch) ? 40 : 0));
      },
      onDone: () => { if (onStage) { c.mouthOpen = false; c.talking = false; } },
    });
    if (onStage) { c.mouthOpen = false; c.talking = false; }
    if (my === runId) UI.clearBubble();
  }

  async function background(ins) {
    UI.clearBubble();
    UI.hideItem(null, true);
    const first = !stage.bg;
    const tr = ins.transition || (first ? "cut" : ins.heading ? "fade" : "dissolve");
    const dur = ins.duration || (tr === "fade" ? 1.2 : 0.8);
    await stage.setBackground(ins.name, tr === "dissolve" || tr === "crossfade" ? "dissolve" : tr === "fade" ? "fade" : tr === "cut" ? "cut" : "dissolve", dur, (to, d) => UI.fade(to, d));
    if (ins.heading) {
      const m = ins.name.match(/^(?:INT\.?\/EXT|EXT\.?\/INT|I\/E|INT|EXT)[.\s]+(.*?)(?:\s+-\s+(.*))?$/);
      if (m) UI.location(titleCase(m[1]), m[2] ? titleCase(m[2]) : null);
    }
  }
  const titleCase = s => s.toLowerCase().replace(/\b[a-z]/g, c => c.toUpperCase());

  async function runCharacter(ins) {
    const c = stage.character(ins.character);
    for (const a of ins.actions) await act(c, a);
  }

  async function act(c, a) {
    const S = stage;
    if (a.layer != null) c.layer = a.layer;
    switch (a.kind) {
      case "enter": {
        const to = S.resolve(a.pos || (c.onStage ? null : { named: "center" }), c);
        const from = (a.value || "fade").toLowerCase();
        c.onStage = true; c.gesture = null;
        if (from === "left" || from === "right") {
          c.x = from === "left" ? -90 : 410; c.y = to.y; c.scale = to.scale; c.alpha = 1;
          await S.move(c, to, "walk", a.duration);
        } else if (from === "bottom" || from === "top") {
          c.x = to.x; c.scale = to.scale; c.y = from === "bottom" ? -380 : 300; c.alpha = 1;
          await S.move(c, to, "move", a.duration || 0.7);
        } else {
          Object.assign(c, { x: to.x, y: to.y, scale: to.scale });
          await Clock.tween(a.duration || 0.5, k => c.alpha = k);
        }
        return;
      }
      case "exit": {
        if (!c.onStage) return;
        const dir = (a.value || (c.x < 160 ? "left" : "right")).toLowerCase();
        if (dir === "left" || dir === "right") await S.move(c, { x: dir === "left" ? -100 : 420, y: c.y, scale: c.scale }, "walk", a.duration);
        else if (dir === "bottom") await S.move(c, { x: c.x, y: -400, scale: c.scale }, "move", a.duration || 0.6);
        else await Clock.tween(a.duration || 0.5, k => c.alpha = 1 - k);
        c.onStage = false; c.alpha = 0; c.gesture = null;
        return;
      }
      case "walk": case "run": case "move": {
        const to = S.resolve(a.pos, c);
        if (!c.onStage) { c.onStage = true; c.alpha = 1; c.x = to.x < 160 ? -90 : 410; c.y = to.y; c.scale = to.scale; }
        await S.move(c, to, a.kind, a.duration);
        return;
      }
      case "spot": {
        const to = S.resolve(a.pos, c);
        Object.assign(c, { x: to.x, y: to.y, scale: to.scale, onStage: true, alpha: 1 });
        return;
      }
      case "animate": {
        const d = c.play(a.value);
        if (c.onStage && d) await Clock.wait(Math.min(d, a.duration || 1.0));
        return;
      }
      case "look": { const s = splitAnim(a.value); if (s.emotion) c.emotion = s.emotion; else c.emotion = a.value.toLowerCase(); return; }
      case "face": {
        const v = a.value.toLowerCase();
        if (v === "left") c.facing = -1; else if (v === "right") c.facing = 1; else if (v === "front" || v === "center" || v === "forward" || v === "camera") c.facing = 0;
        else { const o = S.chars.get(a.value.toUpperCase()); if (o) c.facing = o.x < c.x ? -1 : 1; }
        return;
      }
      case "wear": {
        const slot = (a.slot || "outfit").toLowerCase();
        if (c.onStage) { Audio2.sfx("whoosh"); await Clock.tween(0.15, k => c.alpha = 1 - k * 0.7); }
        c.look.slots[slot] = a.value.toLowerCase();
        if (c.onStage) await Clock.tween(0.2, k => c.alpha = 0.3 + 0.7 * k);
        return;
      }
      case "appear": {
        const to = S.resolve(a.pos, c);
        Object.assign(c, { x: to.x, y: to.y, scale: to.scale, onStage: true });
        await Clock.tween(a.duration || 0.4, k => c.alpha = k);
        return;
      }
      case "disappear": await Clock.tween(a.duration || 0.4, k => c.alpha = 1 - k); c.onStage = false; return;
      case "layer": return;
    }
  }

  async function command(ins) {
    const A = ins.args, O = ins.opts, t = O.time != null ? Core.tryFloat(O.time) : null, a0 = (A[0] || "").toLowerCase();
    switch (ins.verb) {
      case "wait": return Clock.wait(Core.tryFloat(A[0]) ?? t ?? 1);
      case "zoom": case "camera": {
        if (a0 === "reset" || a0 === "out" || a0 === "off") return stage.camTo(1, 0.5, 0.5, t ?? 0.6);
        let i = 0; if (a0 === "on" || a0 === "to") i = 1;
        const target = A[i] && !Core.isNum(A[i]) ? stage.chars.get(A[i].toUpperCase()) : null;
        const z = Core.tryFloat(A[target ? i + 1 : i]) ?? 1.4;
        if (target && target.onStage) return stage.focusOn(target, z, t ?? 0.6);
        return stage.camTo(z, 0.5, 0.45, t ?? 0.6);
      }
      case "pan": { const w = A.find(x => /left|right|center/i.test(x)) || "center"; return stage.camTo(stage.cam.z, { left: 0, right: 1, center: 0.5 }[w.toLowerCase()], stage.cam.fy, t ?? 1); }
      case "shake": return stage.shake(Core.tryFloat(A[0]) ?? t ?? 0.5, Core.tryFloat(O.strength) ? Core.tryFloat(O.strength) * 10 : 12);
      case "flash": return UI.flash(A[0] && !Core.isNum(A[0]) ? Art.hex(A[0], ({ white: "#fff", red: "#ff4d4d", black: "#000", yellow: "#ffe066" })[a0] || "#fff") : "#fff", t);
      case "fade": {
        const out = a0 !== "in";
        const col = A.find(x => /black|white|red/i.test(x));
        return UI.fade(out ? 1 : 0, t ?? Core.tryFloat(A.find(Core.isNum)) ?? 0.8, col ? ({ black: "#000", white: "#fff", red: "#7a0010" })[col.toLowerCase()] : null);
      }
      case "transition": await UI.fade(1, 0.4); await Clock.wait(0.2); return UI.fade(0, 0.4);
      case "music": return Audio2.music(a0 === "stop" ? null : A[0]);
      case "sound": case "voice": return Audio2.sfx(A[0]);
      case "ambience": return Audio2.ambience(a0 === "stop" ? null : A[0]);
      case "stop": if (!a0 || a0 === "all") Audio2.stopAll(); else if (a0 === "music") Audio2.music(null); else if (/amb/.test(a0)) Audio2.ambience(null); return;
      case "notify": return UI.toast(say(A.join(" ")), O.icon);
      case "weather": stage.setWeather(a0); if (a0 === "rain" || a0 === "storm") { if (!Audio2.ambName) Audio2.ambience("rain"); } else if (Audio2.ambName === "rain") Audio2.ambience(null); return;
      case "tint": return stage.setTint(a0, t);
      case "show": case "overlay": UI.showItem(A[0]); return Clock.wait(0.3);
      case "hide": UI.hideItem(A[0] && a0 !== "all" ? A[0] : null); return;
      case "clear": UI.hideItem(null); stage.setWeather("none"); return;
      case "vibrate": try { navigator.vibrate && navigator.vibrate(200); } catch (e) {} return;
      case "autosave": autosave(); return;
    }
  }

  async function endEpisode(next, my) {
    UI.clearBubble();
    store.set("vn_vars", vars.v);
    store.del("vn_save_" + key);
    const done = store.get("vn_done", {}); done[key] = true; store.set("vn_done", done);
    if (key === "__lab") { await Clock.wait(0.6); showCard("The End", "Your script played to the end.", [["Back to the lab", () => { stopRun(); openLibrary("lab"); }]]); return; }
    await UI.fade(0.75, 1.0);
    if (my !== runId) return;
    hud(false);
    const ep = prog.meta.episode ? `Episode ${prog.meta.episode}` : "Episode";
    const nextKey = next && STORIES[next] ? next : null;
    const nextMeta = nextKey ? Core.parse(STORIES[nextKey], nextKey).meta : null;
    const btns = [];
    if (nextKey) btns.push([`Play Episode ${nextMeta.episode || ""}`.trim(), () => { UI.fade(1, 0.01); play(nextKey, { fresh: false }); }]);
    btns.push(["Library", () => { stopRun(); Audio2.stopAll(); openLibrary(); }, !!nextKey]);
    showCard(`End of ${ep}`, prog.meta.title ? `“${prog.meta.title}”` + (nextMeta ? `<br><br>Next: <b>${UI.esc(nextMeta.title || nextKey)}</b>` : "<br><br>To be continued...") : "", btns, true);
  }

  function showCard(title, body, buttons, html) {
    const c = UI.el("div", "card", `<h2>${UI.esc(title)}</h2>${body ? `<p>${html ? body : UI.esc(body)}</p>` : ""}<div class="row"></div>`);
    const row = c.querySelector(".row");
    buttons.forEach(([label, fn, alt]) => { const b = UI.el("button", "btn" + (alt ? " alt" : ""), UI.esc(label)); b.onclick = () => { Audio2.sfx("pop"); c.remove(); fn(); }; row.appendChild(b); });
    UI.ui.appendChild(c);
    return c;
  }
  function showErrors(errs) {
    hud(false);
    UI.fade(0, 0.2);
    const list = errs.slice(0, 8).map(e => `<div style="text-align:left;margin:.4em 0;font-size:.88em"><b>Line ${e.line}:</b> ${UI.esc(e.msg)}</div>`).join("");
    const c = showCard("This story has a problem", "", [["Back", () => { stopRun(); openLibrary(key === "__lab" ? "lab" : null); }]]);
    c.querySelector("h2").insertAdjacentHTML("afterend", list);
  }

  // ---------- pause menu ----------
  function menu() {
    if (UI.frame.querySelector(".sheet")) return;
    const s = UI.el("div", "sheet");
    const render = () => {
      s.innerHTML = `<button class="close" aria-label="Close">${ICON.close}</button><h2>Paused</h2>
        <button class="mbtn" data-a="resume">Resume <span>${ICON.play}</span></button>
        <button class="mbtn" data-a="history">History <span>${ICON.history}</span></button>
        <div class="setting"><label for="spd">Text speed</label><span>${UI.mode.speed < 30 ? "Slow" : UI.mode.speed < 60 ? "Normal" : "Fast"}</span><input id="spd" type="range" min="15" max="90" value="${UI.mode.speed}"></div>
        <div class="setting"><label for="mv">Music</label><span></span><input id="mv" type="range" min="0" max="100" value="${Math.round(Audio2.state.music * 100)}"></div>
        <div class="setting"><label for="sv">Sound effects</label><span></span><input id="sv" type="range" min="0" max="100" value="${Math.round(Audio2.state.sfx * 100)}"></div>
        <button class="mbtn" data-a="restart">Restart episode <span>${ICON.restart}</span></button>
        <button class="mbtn" data-a="library">Back to library <span>${ICON.home}</span></button>`;
      s.querySelector("#spd").oninput = e => { UI.mode.speed = +e.target.value; UI.saveMode(); s.querySelector("#spd").previousElementSibling.textContent = UI.mode.speed < 30 ? "Slow" : UI.mode.speed < 60 ? "Normal" : "Fast"; };
      s.querySelector("#mv").oninput = e => Audio2.set("music", e.target.value / 100);
      s.querySelector("#sv").oninput = e => Audio2.set("sfx", e.target.value / 100);
    };
    render();
    s.onclick = e => {
      const b = e.target.closest("button"); if (!b) return;
      const a = b.dataset.a || (b.classList.contains("close") ? "resume" : null);
      if (a === "resume") s.remove();
      if (a === "history") {
        s.innerHTML = `<button class="close" aria-label="Close">${ICON.close}</button><h2>History</h2><div class="history">${history.slice(-60).map(h => `<div>${h.name ? `<b>${UI.esc(h.name)}</b>` : ""}${UI.esc(h.text)}</div>`).join("") || "<div>Nothing yet.</div>"}</div>`;
        s.scrollTop = s.scrollHeight;
      }
      if (a === "restart") { s.remove(); store.del("vn_save_" + key); if (key === "__lab") play("__lab", { source, fresh: true }); else play(key, { fresh: true }); }
      if (a === "library") { s.remove(); autosave(); stopRun(); Audio2.stopAll(); openLibrary(); }
    };
    UI.frame.appendChild(s);
  }

  // ---------- library ----------
  let libTab = "stories", libEl = null;
  function closeScreens() { UI.screens.innerHTML = ""; libEl = null; const sh = UI.frame.querySelector(".sheet"); if (sh) sh.remove(); }
  function openLibrary(tab) {
    if (tab) libTab = tab;
    hud(false);
    UI.frame.querySelector("#fade").style.opacity = 0;
    stage.reset();
    UI.screens.innerHTML = "";
    libEl = UI.el("div", "lib");
    libEl.innerHTML = `<div class="lib-scroll"></div><nav class="tabs">
      <button class="tab" data-t="stories">${ICON.book}Stories</button>
      <button class="tab" data-t="games">${ICON.pad}Mini games</button>
      <button class="tab" data-t="lab">${ICON.code}Script lab</button></nav>`;
    libEl.querySelectorAll(".tab").forEach(b => b.onclick = () => { Audio2.sfx("click"); libTab = b.dataset.t; renderTab(); });
    UI.screens.appendChild(libEl);
    renderTab();
  }
  function renderTab() {
    libEl.querySelectorAll(".tab").forEach(b => b.classList.toggle("on", b.dataset.t === libTab));
    const sc = libEl.querySelector(".lib-scroll"); sc.innerHTML = ""; sc.scrollTop = 0;
    coverStage.active = false;
    if (libTab === "stories") renderStories(sc);
    if (libTab === "games") renderGames(sc);
    if (libTab === "lab") renderLab(sc);
  }
  function renderStories(sc) {
    const eps = EPISODES();
    const meta = eps[0].meta;
    const done = store.get("vn_done", {});
    const saved = eps.map(e => store.get("vn_save_" + e.key, null));
    const resumeIdx = saved.findIndex(Boolean);
    const firstUndone = eps.findIndex(e => !done[e.key]);
    sc.innerHTML = `<div class="lib-head"><h1>Stories</h1><span>VN Engine · web preview</span></div>
      <button class="cover" id="cover-btn" aria-label="Play ${UI.esc(meta.series)}"><canvas></canvas><div class="info">
        <div class="chips"><span class="chip">${UI.esc(meta.genre || "Story")}</span><span class="chip">${eps.length} episodes</span></div>
        <h2>${UI.esc(meta.series || meta.title)}</h2><p>${UI.esc(meta.description || "")}</p>
        <span class="btn">${resumeIdx >= 0 ? "Continue" : firstUndone > 0 ? `Play Episode ${eps[firstUndone].meta.episode}` : "Play"}</span></div></button>
      <div class="eyebrow">Episodes</div>`;
    eps.forEach((e, i) => {
      const b = UI.el("button", "ep" + (done[e.key] ? " done" : ""), `<span class="num">${done[e.key] ? ICON.check.replace("<svg", '<svg width="22" height="22"') : (e.meta.episode || i + 1)}</span><span class="t"><b>${UI.esc(e.meta.title || e.key)}</b><small>${saved[i] ? "In progress" : done[e.key] ? "Finished · play again" : `Episode ${e.meta.episode || i + 1}`}</small></span><span class="play">${saved[i] ? "Resume" : "Play"}</span>`);
      b.onclick = () => { Audio2.init(); Audio2.sfx("pop"); if (saved[i]) play(e.key, { restore: saved[i] }); else { if (i === 0) { vars.v = {}; store.del("vn_vars"); } play(e.key, { fresh: true }); } };
      sc.appendChild(b);
    });
    sc.appendChild(UI.el("p", "note", "Choices, outfits and names carry over between episodes. Your progress saves on this device."));
    sc.querySelector("#cover-btn").onclick = () => {
      Audio2.init(); Audio2.sfx("pop");
      if (resumeIdx >= 0) play(eps[resumeIdx].key, { restore: saved[resumeIdx] });
      else { const i = firstUndone > 0 ? firstUndone : 0; if (i === 0) { vars.v = {}; store.del("vn_vars"); } play(eps[i].key, { fresh: true }); }
    };
    // live animated cover
    const cv = sc.querySelector(".cover canvas");
    coverStage.canvas = cv; coverStage.g = cv.getContext("2d");
    const r = cv.getBoundingClientRect();
    coverStage.resize(Math.max(10, r.width), Math.max(10, r.height));
    coverStage.reset();
    const p1 = Core.parse(STORIES[eps[0].key], "c");
    coverStage.defs = p1.chars;
    coverStage.bgName = "INT. COFFEE SHOP - DAY"; coverStage.bg = Art.background(coverStage.bgName, cv.width, cv.height);
    const m = coverStage.character("MAYA"), l = coverStage.character("LEO");
    Object.assign(m, { x: 105, y: 40, scale: 1.05, onStage: true, alpha: 1, facing: 1, emotion: "happy" });
    Object.assign(l, { x: 225, y: 40, scale: 1.1, onStage: true, alpha: 1, facing: -1, emotion: "smug" });
    coverStage.setWeather("sparkles");
    coverStage.active = true;
    coverStage.loopGesture = () => { const c = Math.random() < 0.5 ? m : l; c.play(["wave", "laugh", "think", "talk_happy", "love", "shrug"][Math.floor(Math.random() * 6)]); };
  }
  function renderGames(sc) {
    sc.innerHTML = `<div class="lib-head"><h1>Mini games</h1><span>${Minigames.list.length} built in</span></div><p class="note">Every game runs from one line in a story, like <code>minigame memory pairs=6</code>, and can be reskinned with your own art in the Unity engine. Tap one to try it.</p><div class="grid"></div>`;
    const grid = sc.querySelector(".grid");
    Minigames.list.forEach(g => {
      const b = UI.el("button", "game", `<span class="gi" style="background:${g.color}">${ICON[g.icon]}</span><b>${UI.esc(g.name)}</b><small>${UI.esc(g.blurb)}</small>`);
      b.onclick = () => { Audio2.init(); Audio2.sfx("pop"); arcade(g); };
      grid.appendChild(b);
    });
  }
  async function arcade(g) {
    stopRun(); const my = runId;
    closeScreens();
    prog = Core.parse(STORIES[Object.keys(STORIES)[0]], "arcade");
    stage.reset(); stage.defs = prog.chars;
    stage.bgName = g.scene || "INT. COFFEE SHOP - DAY"; stage.bg = Art.background(stage.bgName, stage.canvas.width, stage.canvas.height);
    if (g.id === "dressup") { const p = stage.character("PLAYER"); Object.assign(p, { x: 160, y: 0, scale: 1, onStage: true, alpha: 1, emotion: "happy" }); }
    Audio2.music("minigame");
    const data = g.data ? g.data.slice() : [];
    await Minigames.run(g.id, Object.assign({ retries: "yes" }, g.params || {}), data, { stage, vars: new Core.Vars(), say: s => s, displayName: id => id === "PLAYER" ? "You" : id, story: false });
    if (my !== runId) return;
    Audio2.stopAll();
    openLibrary("games");
  }
  function renderLab(sc) {
    const eps = EPISODES();
    const draft = store.get("vn_lab", null);
    sc.innerHTML = `<div class="lib-head"><h1>Script lab</h1><span>Write and play</span></div>
      <p class="note">This is the same story language the Unity engine reads. Edit a line, then tap Play to see it on stage.</p>
      <div class="lab" style="display:flex;flex-direction:column;gap:.7em">
        <div style="display:flex;gap:.5em;align-items:center;flex-wrap:wrap"><label for="lab-src" class="eyebrow">Start from</label><select id="lab-src">${eps.map(e => `<option value="${e.key}">Episode ${e.meta.episode}: ${UI.esc(e.meta.title)}</option>`).join("")}<option value="__blank">A tiny example</option></select></div>
        <textarea id="lab-text" spellcheck="false" aria-label="Story script"></textarea>
        <div class="errs" id="lab-errs"></div>
        <div class="row" style="justify-content:flex-start;margin:0"><button class="btn" id="lab-play">Play</button><button class="btn alt" id="lab-check">Check</button></div>
      </div>`;
    const ta = sc.querySelector("#lab-text"), sel = sc.querySelector("#lab-src");
    const TINY = `INT. BEDROOM - EVENING\n@music getting_ready\n\ncharacter MAYA name="Maya" color=#ff6f91 haircolor=#3b2230 hair=ponytail\n\n@MAYA enters from left to center and is wave\nMAYA (talk_happy)\nHi! Change this line and press Play.\n\nchoice "Pick one"\n    "Make her laugh" [heart]\n        @MAYA is laugh\n        MAYA (laugh): Ha! Good one.\n    "Surprise her"\n        @MAYA jumps\n        MAYA (surprised): Whoa!\n\n@MAYA exits right\n`;
    ta.value = draft || STORIES[eps[0].key];
    const check = () => {
      const p = Core.parse(ta.value, "__lab");
      const box = sc.querySelector("#lab-errs");
      box.innerHTML = p.errors.length ? p.errors.slice(0, 12).map(e => `<div class="err"><b>${e.warn ? "Note" : "Line " + e.line}</b> · ${UI.esc(e.msg)}</div>`).join("") : `<div class="ok">No problems found. ${p.ins.length} steps, ${Object.keys(p.chars).length} characters.</div>`;
      return p;
    };
    sel.onchange = () => { ta.value = sel.value === "__blank" ? TINY : STORIES[sel.value]; store.set("vn_lab", ta.value); check(); };
    ta.oninput = () => store.set("vn_lab", ta.value);
    sc.querySelector("#lab-check").onclick = () => { Audio2.sfx("click"); check(); };
    sc.querySelector("#lab-play").onclick = () => { Audio2.init(); const p = check(); if (p.errors.some(e => !e.warn)) { Audio2.sfx("wrong"); return; } Audio2.sfx("pop"); play("__lab", { source: ta.value, fresh: true }); };
    check();
  }

  function init(st, cs) {
    stage = st; coverStage = cs;
    const h = UI.hud;
    h.innerHTML = `<button class="hud-btn" data-menu aria-label="Menu">${ICON.menu}</button><div class="hud-right"><button class="hud-btn" data-auto>${ICON.auto}Auto</button><button class="hud-btn" data-skip>${ICON.ff}Skip</button></div>`;
    h.querySelector("[data-menu]").onclick = () => { Audio2.sfx("click"); menu(); };
    h.querySelector("[data-auto]").onclick = () => { Audio2.sfx("click"); setAuto(!UI.mode.auto); };
    h.querySelector("[data-skip]").onclick = () => { Audio2.sfx("click"); setSkip(!UI.mode.skip); };
    h.hidden = true;
  }

  return { init, play, openLibrary, get stage() { return stage; }, store };
})();
