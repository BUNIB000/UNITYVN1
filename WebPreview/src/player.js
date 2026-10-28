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
      case "look": { const s = splitAnim(a.value); c.emotion = s.emotion || a.value.toLowerCase(); c.rawEmotion = s.raw; return; }
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
      <button class="tab" data-t="art">${ICON.spark}My art</button>
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
    if (libTab === "art") renderArt(sc);
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
    const userCover = ArtStore.get("cover/" + Core.slug(meta.cover || "cover"));
    if (userCover) { const c = document.createElement("canvas"); c.width = cv.width; c.height = cv.height; const g = c.getContext("2d"); const k = Math.max(c.width / userCover.naturalWidth, c.height / userCover.naturalHeight); g.drawImage(userCover, (c.width - userCover.naturalWidth * k) / 2, (c.height - userCover.naturalHeight * k) / 2, userCover.naturalWidth * k, userCover.naturalHeight * k); coverStage.bg = c; coverStage.setWeather("none"); coverStage.active = true; coverStage.loopGesture = null; return; }
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


  // ---------- my art ----------
  const EXPR = ["neutral", "happy", "sad", "angry", "surprised", "worried", "embarrassed", "scared", "think", "laugh", "cry", "smug", "love"];
  const FRAMES = ["neutral_talk", "neutral_blink", "happy_talk", "happy_blink", "sad_talk", "sad_blink", "surprised_talk", "surprised_blink"];
  const POSES = ["wave", "cheer", "point", "shrug", "dance"];
  const OUTFIT_FOLDERS = ["base", "dress", "suit", "sweater"];
  // Sheet layouts, matching the sprite sheet prompts in the art doc (reading order: rows top to bottom, left to right).
  const SHEETS = [
    { id: "cast", name: "Cast lineup (3)", rows: 1, cols: 3, cast: true },
    { id: "test", name: "Test sheet (9)", rows: 3, cols: 3, names: ["neutral", "neutral_talk", "neutral_blink", "happy", "happy_talk", "sad", "angry", "surprised", "wave"] },
    { id: "full1", name: "Full sheet 1 (16)", rows: 4, cols: 4, names: ["neutral", "neutral_talk", "neutral_blink", "happy", "happy_talk", "happy_blink", "sad", "sad_talk", "sad_blink", "surprised", "surprised_talk", "surprised_blink", "angry", "worried", "embarrassed", "scared"] },
    { id: "full2", name: "Full sheet 2 (10)", rows: 2, cols: 5, names: ["think", "laugh", "cry", "smug", "love", "wave", "cheer", "point", "shrug", "dance"] },
    { id: "props", name: "Props (3)", rows: 1, cols: 3, props: true, keys: ["img/old_key", "mg/timing/icon", "mg/memory/card_back"] },
  ];
  // Parts for the puppet mode, in the order of the parts sheet (3 rows of 5). Left and right are as seen on screen.
  const RIG_HEADS_EXTRA = ["head_worried", "head_embarrassed", "head_scared", "head_think", "head_laugh", "head_cry", "head_smug", "head_love"];
  const RIG_PARTS = ["head", "head_happy", "head_sad", "head_angry", "head_surprised", "head_talk", "head_blink", "torso", "upper_arm_l", "upper_arm_r", "forearm_l", "forearm_r", "leg_l", "leg_r", "hair_back"];
  SHEETS.splice(4, 0, { id: "parts", name: "Parts sheet (15)", rows: 3, cols: 5, rig: true, align: 7, names: RIG_PARTS });
  SHEETS.splice(5, 0, { id: "heads", name: "Extra heads (8)", rows: 2, cols: 4, rig: true, align: 8, names: RIG_HEADS_EXTRA });
  const EXTRA_KEYS = [["img/old_key", "old key"], ["mg/timing/icon", "timing icon"], ["mg/memory/card_back", "memory card back"], ["cover/coffee_cover", "cover"]];
  const artState = { tab: "chars", who: null, outfit: "base", removeBg: true };
  let picker = null;
  function pickFile(multiple) {
    return new Promise(res => {
      if (picker) picker.remove();
      picker = document.createElement("input"); picker.type = "file"; picker.accept = "image/*"; picker.multiple = !!multiple; picker.hidden = true;
      picker.onchange = () => res([...picker.files]);
      document.body.appendChild(picker); picker.click();
    });
  }
  function storyScenes() {
    const set = new Map();
    for (const k of Object.keys(STORIES)) for (const i of Core.parse(STORIES[k], k).ins) if (i.type === "background") { const s = Core.slug(i.name).replace(/^(int|ext|i_e|int_ext|ext_int)_/, ""); set.set(s, i.name); }
    return [...set.keys()];
  }
  function storyCast() {
    const cast = {};
    for (const k of Object.keys(STORIES)) Object.assign(cast, Core.parse(STORIES[k], k).chars);
    return cast;
  }
  function renderArt(sc) {
    const cast = storyCast(), ids = Object.keys(cast);
    if (!artState.who) artState.who = ids[0];
    const total = ["char/", "bg/", "img/", "cover/", "mg/"].reduce((s, p) => s + ArtStore.count(p), 0);
    sc.innerHTML = `<div class="lib-head"><h1>My art</h1><span>${total} image${total === 1 ? "" : "s"}</span></div>
      <p class="note">Tap a box to pick a picture from your phone. It replaces the built-in art right away in the episodes. Pictures stay on this device only. The art prompts doc lists what to make for each box.</p>
      <div class="art-row" id="art-demo"></div>
      <div class="seg" id="art-tabs"><button data-v="chars">Characters</button><button data-v="bgs">Backgrounds</button><button data-v="extra">Props & games</button></div>
      <div id="art-body" style="display:flex;flex-direction:column;gap:.7em"></div>`;
    sc.querySelectorAll("#art-tabs button").forEach(b => { b.classList.toggle("on", b.dataset.v === artState.tab); b.onclick = () => { artState.tab = b.dataset.v; Audio2.sfx("click"); renderArt(sc); }; });
    const demo = sc.querySelector("#art-demo");
    const demoBtn = UI.el("button", "btn", total ? "Reload the test art" : "Load the test art");
    demoBtn.onclick = async () => {
      demoBtn.disabled = true; demoBtn.textContent = "Loading…";
      try { await loadTestArt(n => { demoBtn.textContent = `Loading ${n}…`; }); Audio2.sfx("pop"); }
      catch (e) { UI.toast(e.message || "Could not load the test art"); }
      renderArt(sc);
    };
    demo.appendChild(demoBtn);
    if (total) { const clr = UI.el("button", "btn alt", "Remove all my art"); clr.onclick = async () => { await ArtStore.clear(""); renderArt(sc); }; demo.appendChild(clr); }
    const body = sc.querySelector("#art-body");
    const slotGrid = (title, list, keyFor, opts) => {
      const wrap = UI.el("div", "", `<div class="sub">${UI.esc(title)}</div>`);
      const grid = UI.el("div", "slots" + (opts && opts.wide ? " wide" : ""));
      list.forEach(name => {
        const key = keyFor(name), im = ArtStore.get(key);
        const b = UI.el("button", "slot" + (im ? " filled" : ""), `${im ? `<img src="${im.src}" alt="">` : '<span class="plus">+</span>'}<span class="l">${UI.esc(name.replace(/_/g, " "))}</span>${im ? `<span class="x" role="button" aria-label="Remove">${ICON.close}</span>` : ""}`);
        b.onclick = async e => {
          if (e.target.closest(".x")) { await ArtStore.remove(key); renderArt(sc); return; }
          const files = await pickFile(false); if (!files.length) return;
          b.classList.add("busy");
          try { await ArtStore.importFile(key, files[0], opts && opts.import); Audio2.sfx("pop"); }
          catch (err) { UI.toast(err.message); }
          renderArt(sc);
        };
        grid.appendChild(b);
      });
      wrap.appendChild(grid); body.appendChild(wrap);
    };
    if (artState.tab === "chars") {
      const who = artState.who, folder = artState.outfit;
      const row = UI.el("div", "art-row", `<div class="seg">${ids.map(id => `<button data-id="${id}" class="${id === who ? "on" : ""}">${UI.esc(cast[id].name && !cast[id].name.includes("{") ? cast[id].name : id.charAt(0) + id.slice(1).toLowerCase())}</button>`).join("")}</div>`);
      row.querySelectorAll("button").forEach(b => b.onclick = () => { artState.who = b.dataset.id; renderArt(sc); });
      body.appendChild(row);
      const row2 = UI.el("div", "art-row", `<label for="art-outfit">Outfit</label><select id="art-outfit">${OUTFIT_FOLDERS.map(o => `<option value="${o}" ${o === folder ? "selected" : ""}>${o === "base" ? "Main look" : o}</option>`).join("")}</select>
        <label class="check"><input type="checkbox" id="art-rmbg" ${artState.removeBg ? "checked" : ""}>Remove plain background</label>`);
      row2.querySelector("select").onchange = e => { artState.outfit = e.target.value; renderArt(sc); };
      row2.querySelector("#art-rmbg").onchange = e => { artState.removeBg = e.target.checked; };
      body.appendChild(row2);
      const tools = UI.el("div", "row", "");
      tools.style.cssText = "justify-content:flex-start;margin:0";
      const test = UI.el("button", "btn", "Watch animation test"); test.onclick = () => { Audio2.init(); Audio2.sfx("pop"); animationTest(who); };
      const many = UI.el("button", "btn alt", "Add many by file name");
      many.onclick = async () => {
        const files = await pickFile(true); let n = 0, skipped = [];
        for (const f of files) {
          const name = f.name.replace(/\.[^.]+$/, "").toLowerCase().replace(/[\s-]+/g, "_");
          if (![...EXPR, ...FRAMES, ...POSES].includes(name) && !/^[a-z]+(_[a-z]+)*$/.test(name)) { skipped.push(f.name); continue; }
          try { await ArtStore.importFile(`char/${who}/${folder}/${name}`, f, { removeBg: artState.removeBg, alpha: true }); n++; } catch (e) { skipped.push(f.name); }
        }
        UI.toast(`Added ${n} image${n === 1 ? "" : "s"}${skipped.length ? `, skipped ${skipped.length}` : ""}`);
        renderArt(sc);
      };
      const sheetBtn = UI.el("button", "btn alt", "Add a sheet");
      sheetBtn.onclick = () => sheetCutter(sc, artState.sheet || "test");
      tools.append(test, sheetBtn, many); body.appendChild(tools);
      const key = n => `char/${who}/${folder}/${n}`;
      const imp = { import: { removeBg: artState.removeBg, alpha: true } };
      Object.defineProperty(imp.import, "removeBg", { get: () => artState.removeBg });
      slotGrid("Expressions", EXPR, key, imp);
      slotGrid("Talking and blinking", FRAMES, key, imp);
      slotGrid("Gestures", POSES, key, imp);
      slotGrid("Parts for full animation (head, torso, arms, legs)", RIG_PARTS.concat(RIG_HEADS_EXTRA), n => `char/${who}/rig/${n}`, imp);
      if (Rig.has(who)) {
        const hs = UI.el("div", "art-row", `<label for="art-headk">Head size</label><input id="art-headk" type="range" min="0.5" max="1.6" step="0.02" value="${Rig.headMul(who)}" style="flex:1;min-width:8em"><span class="note">Parts are on: this character is animated limb by limb.</span>`);
        hs.querySelector("input").oninput = e => Rig.setHeadMul(who, +e.target.value);
        body.appendChild(hs);
      }
      const clr = UI.el("button", "btn alt", `Clear ${UI.esc(who)}'s art`); clr.style.alignSelf = "flex-start";
      clr.onclick = async () => { await ArtStore.clear(`char/${who}/`); renderArt(sc); };
      body.appendChild(clr);
    }
    if (artState.tab === "bgs") {
      slotGrid("Scenes in the episodes", storyScenes(), n => "bg/" + n, { wide: true, import: { maxW: 1600, maxH: 2200 } });
    }
    if (artState.tab === "extra") {
      const sb = UI.el("button", "btn alt", "Add a props sheet"); sb.style.alignSelf = "flex-start";
      sb.onclick = () => sheetCutter(sc, "props");
      body.appendChild(sb);
      slotGrid("Close-ups and cover", ["old_key", "coffee_cover"], n => n === "coffee_cover" ? "cover/coffee_cover" : "img/" + n, { wide: true, import: { alpha: true } });
      slotGrid("Mini games", ["timing_icon", "memory_card_back"], n => n === "timing_icon" ? "mg/timing/icon" : "mg/memory/card_back", { wide: true, import: { alpha: true } });
    }
  }
  // Cuts a sheet into separate pictures, shows them with where each will go, and saves them on confirm.
  async function sheetCutter(sc, layoutId) {
    const files = await pickFile(false); if (!files.length) return;
    const file = files[0], cast = storyCast(), ids = Object.keys(cast);
    const nameOf = id => cast[id].name && !cast[id].name.includes("{") ? cast[id].name : id.charAt(0) + id.slice(1).toLowerCase();
    const st = { layout: layoutId, who: artState.who, grid: false, pieces: [], targets: [] };
    const s = UI.el("div", "sheet cutter");
    UI.frame.appendChild(s);
    const options = () => {
      const L = SHEETS.find(x => x.id === st.layout), short = !L.cast && !L.props;
      let o = `<option value="">Skip</option>`;
      for (const id of ids) o += `<optgroup label="${UI.esc(nameOf(id))}">${[...EXPR, ...FRAMES, ...POSES].map(n => `<option value="char/${id}/${artState.outfit}/${n}">${short && id === st.who ? "" : UI.esc(nameOf(id)) + ": "}${n.replace(/_/g, " ")}</option>`).join("")}</optgroup>`;
      for (const id of ids) o += `<optgroup label="${UI.esc(nameOf(id))} parts">${RIG_PARTS.concat(RIG_HEADS_EXTRA).map(n => `<option value="char/${id}/rig/${n}">${short && id === st.who ? "" : UI.esc(nameOf(id)) + ": "}${n.replace(/_/g, " ")}</option>`).join("")}</optgroup>`;
      o += `<optgroup label="Props and games">${EXTRA_KEYS.map(([k, l]) => `<option value="${k}">${l}</option>`).join("")}</optgroup>`;
      return o;
    };
    const defaults = L => st.pieces.map((_, i) => L.cast ? (ids[i] ? `char/${ids[i]}/${artState.outfit}/neutral` : "") : L.props ? (L.keys[i] || "") : L.rig ? (L.names[i] ? `char/${st.who}/rig/${L.names[i]}` : "") : (L.names[i] ? `char/${st.who}/${artState.outfit}/${L.names[i]}` : ""));
    const cut = async () => {
      const L = SHEETS.find(x => x.id === st.layout);
      s.innerHTML = `<h2>Cutting the sheet…</h2>`;
      try { st.pieces = await ArtStore.cutSheet(file, { removeBg: artState.removeBg, figures: !L.props && !L.rig, alignFirst: L.rig ? L.align : 0, rows: st.grid || L.rig ? L.rows : 0, cols: st.grid || L.rig ? L.cols : 0 }); }
      catch (e) { UI.toast(e.message); s.remove(); return; }
      st.targets = defaults(L);
      render();
    };
    const render = () => {
      const L = SHEETS.find(x => x.id === st.layout), got = st.pieces.length;
      const expect = L.cast ? ids.length : L.props ? L.keys.length : L.names.length;
      s.innerHTML = `<button class="close" aria-label="Close">${ICON.close}</button><h2>Cut a sheet</h2>
        <div class="art-row"><label for="cut-layout">Layout</label><select id="cut-layout">${SHEETS.map(x => `<option value="${x.id}" ${x.id === st.layout ? "selected" : ""}>${x.name}</option>`).join("")}</select>
        ${L.cast || L.props ? "" : `<label for="cut-who">For</label><select id="cut-who">${ids.map(id => `<option value="${id}" ${id === st.who ? "selected" : ""}>${UI.esc(nameOf(id))}</option>`).join("")}</select>`}</div>
        ${L.rig ? "" : `<div class="art-row"><span class="seg"><button data-g="0" class="${st.grid ? "" : "on"}">Find figures</button><button data-g="1" class="${st.grid ? "on" : ""}">Cut as ${L.rows}×${L.cols} grid</button></span></div>`}
        <p class="note ${got === expect ? "" : "warn"}">Found ${got} picture${got === 1 ? "" : "s"}${got === expect ? "." : `, but this layout has ${expect}. ${st.grid ? "Check the boxes below, or try Find figures." : "Try Cut as grid, or check the boxes below."}`} Change any box to put a picture somewhere else${artState.outfit !== "base" ? ` (outfit: ${UI.esc(artState.outfit)})` : ""}.</p>
        <div class="slots cut-grid">${st.pieces.map((c, i) => `<div class="slot filled"><img src="${c.toDataURL()}" alt=""><select data-i="${i}" aria-label="Where picture ${i + 1} goes">${options()}</select></div>`).join("")}</div>
        <div class="row" style="justify-content:flex-start;margin:0"><button class="btn" data-a="save">Save ${st.targets.filter(Boolean).length} picture${st.targets.filter(Boolean).length === 1 ? "" : "s"}</button><button class="btn alt" data-a="cancel">Cancel</button></div>`;
      s.querySelectorAll(".cut-grid select").forEach(sel => { sel.value = st.targets[+sel.dataset.i]; sel.onchange = () => { st.targets[+sel.dataset.i] = sel.value; render(); }; });
      s.querySelector("#cut-layout").onchange = e => { st.layout = e.target.value; artState.sheet = st.layout; const N = SHEETS.find(x => x.id === st.layout); cut(); };
      const w = s.querySelector("#cut-who"); if (w) w.onchange = e => { st.who = e.target.value; st.targets = defaults(SHEETS.find(x => x.id === st.layout)); render(); };
      s.querySelectorAll(".seg button").forEach(b => b.onclick = () => { const g = b.dataset.g === "1"; if (g !== st.grid) { st.grid = g; cut(); } });
    };
    s.onclick = async e => {
      const b = e.target.closest("button"); if (!b || b.closest(".seg")) return;
      if (b.classList.contains("close") || b.dataset.a === "cancel") { s.remove(); return; }
      if (b.dataset.a === "save") {
        b.disabled = true; b.textContent = "Saving…";
        const used = new Set();
        for (let i = 0; i < st.pieces.length; i++) { const k = st.targets[i]; if (!k || used.has(k)) continue; used.add(k); await ArtStore.storeCanvas(k, st.pieces[i], true); }
        Audio2.sfx("pop"); UI.toast(`Saved ${used.size} picture${used.size === 1 ? "" : "s"}`);
        const L = SHEETS.find(x => x.id === st.layout);
        if (!L.cast && !L.props) artState.who = st.who;
        s.remove(); renderArt(sc);
      }
    };
    cut();
  }
  // The pictures you sent, already cut into pieces and published beside this page.
  async function loadTestArt(progress) {
    let man;
    try { const r = await fetch("demo_art/manifest.json", { cache: "no-cache" }); if (!r.ok) throw 0; man = await r.json(); }
    catch (e) { throw new Error("The test art is not available in this copy of the preview."); }
    const keys = Object.keys(man); let done = 0;
    const queue = keys.slice();
    const worker = async () => {
      while (queue.length) {
        const k = queue.shift();
        const r = await fetch("demo_art/" + man[k]); if (!r.ok) continue;
        await ArtStore.putBlob(k, await r.blob());
        progress(++done + "/" + keys.length);
      }
    };
    await Promise.all([worker(), worker(), worker(), worker()]);
    ArtStore.refresh();
  }
  function animationTest(id) {
    const cast = storyCast(), d = cast[id];
    const defLine = `character ${id} ${Object.entries(d.opts).filter(([k]) => k !== "name").map(([k, v]) => `${k}=${v}`).join(" ")} name="${d.name && !d.name.includes("{") ? d.name : id.charAt(0) + id.slice(1).toLowerCase()}"`;
    const line = (anim, text) => `${id} (${anim})\n${text}\n`;
    const src = `${defLine}

INT. COFFEE SHOP - DAY
@music cafe
@${id} enters from left to center

${line("talk_happy", "Hi! I just walked in. Watch my mouth move while I talk, and keep an eye out for blinks.")}
${line("neutral", "This is my neutral face.")}
${line("sad", "This is sad...")}
${line("angry", "This is angry!")}
${line("surprised", "Surprised!")}
${line("worried", "Worried...")}
${line("embarrassed", "Embarrassed.")}
${line("scared", "Scared!")}
${line("think", "Hmm, thinking.")}
${line("laugh", "Ha ha ha!")}
${line("cry", "Crying...")}
${line("smug", "Feeling smug.")}
${line("love", "In love.")}
@${id} is wave
@${id} jumps
@${id} nods
@${id} laughs
@${id} is cheer
@${id} shrugs
@${id} spins
@${id} walks to left
@${id} runs to right
@${id} walks to center and faces front
@zoom on ${id} 1.6 in 0.6
${line("happy", "And a close-up. That's everything!")}
@zoom reset
@${id} is dance
@${id} exits right
`;
    play("__lab", { source: src, fresh: true });
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
