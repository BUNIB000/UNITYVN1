// ===== Mini games: shared frame (intro, countdown, timer, results, retries) + 13 games =====
const Minigames = (() => {
  const games = {}, list = [];
  const el = UI.el, esc = UI.esc;
  const R = Math.random;
  const shuffle = a => { for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(R() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; } return a; };
  function reg(id, meta, fn) { games[id] = fn; list.push(Object.assign({ id }, meta)); }

  function confetti(root) {
    const c = document.createElement("canvas"); c.style.cssText = "position:absolute;inset:0;width:100%;height:100%;pointer-events:none";
    root.appendChild(c);
    const W = c.width = root.clientWidth * 2, H = c.height = root.clientHeight * 2, g = c.getContext("2d");
    const P = Array.from({ length: 120 }, () => ({ x: W / 2 + (R() - 0.5) * W * 0.3, y: H * 0.45, vx: (R() - 0.5) * W * 1.4, vy: -H * (0.6 + R() * 0.8), r: R() * 6, vr: (R() - 0.5) * 12, c: ["#ff5c8a", "#ffb547", "#3cc88f", "#5cc8ff", "#b07cff", "#fff"][Math.floor(R() * 6)], s: 6 + R() * 8 }));
    let last = performance.now(), life = 0;
    const step = now => {
      const dt = Math.min(0.05, (now - last) / 1000); last = now; life += dt;
      g.clearRect(0, 0, W, H);
      for (const p of P) { p.vy += H * 1.4 * dt; p.vx *= 0.99; p.x += p.vx * dt; p.y += p.vy * dt; p.r += p.vr * dt; g.save(); g.translate(p.x, p.y); g.rotate(p.r); g.fillStyle = p.c; g.fillRect(-p.s / 2, -p.s / 4, p.s, p.s / 2); g.restore(); }
      if (life < 3 && c.isConnected) requestAnimationFrame(step); else c.remove();
    };
    requestAnimationFrame(step);
  }

  async function run(id, params, data, env) {
    const fn = games[id];
    const meta = list.find(g => g.id === id);
    if (!fn) { UI.toast(`There is no mini game called "${id}"`); return { won: true, score: 0 }; }
    params = params || {};
    const P = {
      str: (k, d) => params[k] != null ? params[k] : d,
      num: (k, d) => { const v = Core.tryFloat(params[k]); return v == null ? d : v; },
      bool: (k, d) => params[k] == null ? d : !/^(no|false|off|0)$/i.test(params[k]),
    };
    const diff = { easy: 0.75, normal: 1, hard: 1.3 }[(params.difficulty || "normal").toLowerCase()] || 1;
    let retries = params.retries != null ? (/^yes|unlimited|true$/i.test(params.retries) ? Infinity : +params.retries || 0) : params.retry != null ? (/^(yes|true)$/i.test(params.retry) ? Infinity : 0) : 0;
    const title = P.str("title", meta.name);
    const instructions = P.str("instructions", meta.how);
    const prevMusic = Audio2.musicName;
    if (params.music) Audio2.music(params.music);
    const root = el("div", "mg" + (meta.light ? " light" : ""));
    UI.mgLayer.appendChild(root);
    const card = (h, body, buttons) => new Promise(res => {
      const c = el("div", "card", `<h2>${esc(h)}</h2>${body}<div class="row"></div>`);
      buttons.forEach(([label, val, cls]) => { const b = el("button", "btn " + (cls || ""), esc(label)); b.onclick = () => { Audio2.sfx("pop"); c.remove(); res(val); }; c.querySelector(".row").appendChild(b); });
      root.appendChild(c);
    });
    if (!P.bool("skipintro", false) && !meta.noIntro) {
      await card(title, `<div class="gi" style="width:3.4em;height:3.4em;border-radius:1em;margin:0 auto .8em;display:grid;place-items:center;color:#fff;background:${meta.color}">${ICON[meta.icon].replace("<svg", '<svg width="34" height="34"')}</div><p>${esc(instructions)}</p>`, [["Start", 1]]);
    }
    let result;
    for (let attempt = 0; ; attempt++) {
      root.querySelectorAll(".mg-hud,.mg-board").forEach(e => e.remove());
      const hud = el("div", "mg-hud", `<div class="top"><span class="title">${esc(title)}</span><span class="score"></span></div>`);
      const board = el("div", "mg-board");
      root.appendChild(board); root.appendChild(hud);
      const loops = new Set();
      let timeBar = null;
      const ctx = {
        root, board, hud, params, data: data || [], diff, env, P, num: P.num, str: P.str, bool: P.bool,
        sfx: n => Audio2.sfx(params[n + "sound"] || n),
        score(t) { hud.querySelector(".score").textContent = t; },
        loop(f) { const w = dt => f(dt); loops.add(w); UI.updaters.add(w); return () => { loops.delete(w); UI.updaters.delete(w); }; },
        timer(sec) {
          if (!sec) return { done: new Promise(() => {}), stop() {}, add() {}, left: () => Infinity, frac: () => 1 };
          if (!timeBar) { timeBar = el("div", "mg-time", "<i></i>"); hud.appendChild(timeBar); }
          const bar = timeBar.firstChild; let end = Clock.now + sec, stopped = false, resolve, lastTick = 99;
          const done = new Promise(r => resolve = r);
          const stop = ctx.loop(() => {
            if (stopped) return;
            const left = end - Clock.now;
            bar.style.transform = `scaleX(${Math.max(0, left / sec)})`;
            timeBar.classList.toggle("low", left < sec * 0.2);
            if (left < 5 && Math.ceil(left) < lastTick) { lastTick = Math.ceil(left); if (left > 0) Audio2.sfx("tick"); }
            if (left <= 0) { stopped = true; stop(); resolve("timeout"); }
          });
          return { done, stop() { stopped = true; stop(); }, add(s) { end += s; }, left: () => end - Clock.now, frac: () => Math.max(0, (end - Clock.now) / sec) };
        },
        feedback(text, color, y) {
          const f = el("div", "fb", esc(text)); f.style.color = color || "#fff"; f.style.top = (y == null ? 40 : y) + "%";
          root.appendChild(f); setTimeout(() => f.remove(), 750);
        },
        wait: s => Clock.wait(s),
      };
      if (!meta.noCountdown) {
        const cd = el("div", "count"); root.appendChild(cd);
        for (const n of ["3", "2", "1", "Go!"]) { cd.innerHTML = `<span>${n}</span>`; Audio2.sfx(n === "Go!" ? "go" : "count"); await Clock.wait(n === "Go!" ? 0.5 : 0.65); }
        cd.remove();
      }
      try { result = await fn(ctx); }
      finally { loops.forEach(w => UI.updaters.delete(w)); }
      if (meta.noResult) break;
      const won = !!result.won;
      Audio2.sfx(won ? "success" : "fail");
      if (won) confetti(root);
      const stars = won ? Math.max(1, Math.min(3, result.stars || 3)) : 0;
      const starHtml = `<div class="stars">${[0, 1, 2].map(i => `<svg viewBox="0 0 24 24" fill="currentColor" class="${i < stars ? "on" : ""}" style="animation-delay:${0.15 + i * 0.15}s"><path d="M12 2.5l2.9 6.1 6.6.8-4.9 4.6 1.3 6.6L12 17.3l-5.9 3.3 1.3-6.6L2.5 9.4l6.6-.8z"/></svg>`).join("")}</div>`;
      const left = retries - attempt;
      const btns = [];
      if (!won && left > 0) btns.push(["Try again", "retry", "gold"]);
      btns.push([won ? "Continue" : (left > 0 ? "Give up" : "Continue"), "ok", !won && left > 0 ? "alt" : ""]);
      const ans = await card(won ? P.str("wintext", "You did it!") : P.str("losetext", "Not quite..."), starHtml + (result.detail ? `<p>${esc(result.detail)}</p>` : ""), btns);
      if (ans !== "retry") break;
    }
    root.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 250, fill: "forwards" });
    await Clock.wait(0.25);
    root.remove();
    if (params.music && prevMusic) Audio2.music(prevMusic);
    return { won: !!result.won, score: result.score || 0 };
  }

  const center = (ctx, html) => { const c = el("div", "mg-center", html || ""); ctx.board.appendChild(c); return c; };
  const pct = (a, b) => b ? a / b : 0;
  const starsFor = k => k >= 0.95 ? 3 : k >= 0.7 ? 2 : 1;

  // ---------- reaction ----------
  reg("qte", { name: "Quick reflexes", icon: "bolt", color: "#ff7a45", blurb: "Tap each target before its ring closes.", how: "Tap each target before the ring closes around it!" }, async ctx => {
    const count = ctx.num("count", 6), win = ctx.num("window", 1.2) / ctx.diff, need = ctx.num("need", Math.ceil(count * 0.75));
    let hits = 0; ctx.score(`0 / ${need}`);
    for (let i = 0; i < count; i++) {
      await ctx.wait(0.35 + R() * 0.3);
      const b = ctx.board, W = b.clientWidth, H = b.clientHeight, s = W * 0.2;
      const t = el("button", "", `<span style="position:absolute;inset:0;border-radius:50%;background:radial-gradient(circle at 40% 35%,#fff3b0,#ffb547);box-shadow:0 .3em 0 #c98618;display:grid;place-items:center;color:#7a4a00">${ICON.bolt.replace("<svg", '<svg width="40%"')}</span><span class="ring" style="position:absolute;inset:-60%;border-radius:50%;border:.3em solid #fff;pointer-events:none"></span>`);
      t.style.cssText = `position:absolute;width:${s}px;height:${s}px;left:${s * 0.6 + R() * (W - s * 2.2)}px;top:${H * 0.12 + R() * (H * 0.62)}px;animation:pop .25s both`;
      b.appendChild(t);
      const ring = t.querySelector(".ring");
      const hit = await new Promise(res => {
        t.onpointerdown = e => { e.stopPropagation(); res(true); };
        const start = Clock.now;
        const stop = ctx.loop(() => { const k = (Clock.now - start) / win; ring.style.transform = `scale(${Math.max(0.42, 1 - k * 0.58)})`; ring.style.opacity = 1 - k * 0.3; if (k >= 1) { stop(); res(false); } });
        t.addEventListener("pointerdown", stop, { once: true });
      });
      if (hit) { hits++; ctx.sfx("hit"); ctx.feedback(["Nice!", "Great!", "Quick!"][i % 3], "#ffe08a", 35); }
      else { ctx.sfx("miss"); ctx.feedback("Missed", "#ff8a8a", 35); }
      ctx.score(`${hits} / ${need}`);
      t.remove();
    }
    return { won: hits >= need, score: hits, stars: starsFor(pct(hits, count)), detail: `${hits} of ${count} hit` };
  });

  reg("timing", { name: "Timing bar", icon: "target", color: "#ff5c8a", blurb: "Stop the marker in the zone. Pouring, lockpicking, archery.", how: "Tap when the marker is inside the green zone!" }, async ctx => {
    const rounds = ctx.num("rounds", 3), need = ctx.num("need", Math.ceil(rounds * 0.67)), zone = ctx.num("zone", 0.18) / ctx.diff, speed = ctx.num("speed", 1) * ctx.diff;
    const c = center(ctx, `<div style="font-family:var(--font-title);font-size:1.1em" class="rnd"></div>
      <div style="width:6em;height:6em;color:#fff">${ArtStore.get("mg/timing/icon") ? `<img src="${ArtStore.get("mg/timing/icon").src}" alt="" style="width:100%;height:100%;object-fit:contain">` : ICON.coffee}</div>
      <div class="bar" style="position:relative;width:88%;height:3em;border-radius:999px;background:#fff;box-shadow:inset 0 0 0 .3em #f0e4f3,0 .3em 0 rgba(0,0,0,.25);overflow:hidden">
        <div class="z" style="position:absolute;top:0;bottom:0;background:linear-gradient(90deg,#7de3b4,#3cc88f)"></div>
        <div class="p" style="position:absolute;top:0;bottom:0;width:.8%;background:#fff;opacity:.9"></div>
        <div class="n" style="position:absolute;top:-.2em;bottom:-.2em;width:.5em;margin-left:-.25em;border-radius:.3em;background:#2b1d2c"></div></div>
      <button class="btn" style="font-size:1.3em;padding:.7em 2.4em">Tap!</button>`);
    const z = c.querySelector(".z"), n = c.querySelector(".n"), rnd = c.querySelector(".rnd"), btn = c.querySelector(".btn"), p = c.querySelector(".p");
    let hits = 0; ctx.score(`0 / ${need}`);
    for (let r = 0; r < rounds; r++) {
      rnd.textContent = `Round ${r + 1} of ${rounds}`;
      const z0 = 0.15 + R() * (0.7 - zone);
      z.style.left = z0 * 100 + "%"; z.style.width = zone * 100 + "%"; p.style.left = (z0 + zone / 2) * 100 + "%";
      let pos = 0; const t0 = Clock.now, sp = speed * (0.9 + r * 0.15);
      const stop = ctx.loop(() => { const ph = ((Clock.now - t0) * sp * 0.85) % 2; pos = ph < 1 ? ph : 2 - ph; n.style.left = pos * 100 + "%"; });
      await new Promise(res => { const h = e => { e.stopPropagation(); ctx.board.removeEventListener("pointerdown", h); res(); }; ctx.board.addEventListener("pointerdown", h); });
      stop();
      const inside = pos >= z0 && pos <= z0 + zone, perfect = Math.abs(pos - (z0 + zone / 2)) < zone * 0.15;
      if (inside) { hits++; ctx.sfx("hit"); ctx.feedback(perfect ? "Perfect!" : "Good!", "#7de3b4", 22); } else { ctx.sfx("miss"); ctx.feedback("Miss", "#ff8a8a", 22); }
      ctx.score(`${hits} / ${need}`);
      btn.disabled = true; await ctx.wait(0.7); btn.disabled = false;
    }
    return { won: hits >= need, score: hits, stars: starsFor(pct(hits, rounds)), detail: `${hits} of ${rounds} in the zone` };
  });

  reg("mash", { name: "Button mash", icon: "hand", color: "#e0457b", blurb: "Tap fast to fill the meter. Arm wrestling, pushing doors.", how: "Tap the button as fast as you can to fill the meter!" }, async ctx => {
    const taps = ctx.num("taps", 30) * ctx.diff, time = ctx.num("time", 7), decay = ctx.num("decay", 3) * ctx.diff;
    const c = center(ctx, `<div class="meter" style="height:1.6em"><i></i></div><button class="big-btn">TAP!</button>`);
    const bar = c.querySelector("i"), btn = c.querySelector(".big-btn");
    let v = 0, count = 0, won = false;
    const T = ctx.timer(time);
    const res = await new Promise(resolve => {
      btn.onpointerdown = e => { e.preventDefault(); v += 1; count++; ctx.sfx("pop"); btn.animate([{ transform: "scale(.92)" }, { transform: "scale(1)" }], { duration: 120 }); if (v >= taps) resolve("win"); };
      ctx.loop(dt => { v = Math.max(0, v - decay * dt); bar.style.width = Math.min(100, v / taps * 100) + "%"; ctx.score(`${Math.round(v / taps * 100)}%`); });
      T.done.then(() => resolve("time"));
    });
    T.stop(); won = res === "win";
    return { won, score: count, stars: won ? (T.frac() > 0.4 ? 3 : T.frac() > 0.15 ? 2 : 1) : 0, detail: `${count} taps` };
  });

  // ---------- puzzles ----------
  const FACES = [["heart", "#ff5c8a"], ["star", "#ffb547"], ["moon", "#7a6cff"], ["coffee", "#8a5a3c"], ["leaf", "#3cc88f"], ["diamond", "#5cc8ff"], ["flower", "#ff7aa1"], ["cake", "#e48a5a"], ["music2", "#b07cff"], ["gift", "#e0457b"], ["ring", "#d9a520"], ["letter", "#5a8fd9"]];
  reg("memory", { name: "Memory cards", icon: "cards", color: "#b07cff", blurb: "Flip two at a time to find every pair.", how: "Flip two cards at a time and find all the pairs!" }, async ctx => {
    const pairs = Math.min(12, ctx.num("pairs", 6)), time = ctx.num("time", 60) / ctx.diff;
    const faces = shuffle(FACES.slice()).slice(0, pairs);
    const deck = shuffle(faces.concat(faces).map((f, i) => ({ f, i })));
    const cols = pairs <= 6 ? 3 : 4;
    const c = center(ctx);
    const grid = el("div", "tile-grid"); grid.style.gridTemplateColumns = `repeat(${cols}, minmax(0,1fr))`; grid.style.maxWidth = cols === 3 ? "78%" : "92%";
    c.appendChild(grid);
    let open = [], found = 0, busy = false, moves = 0;
    ctx.score(`0 / ${pairs}`);
    const T = ctx.timer(time);
    const res = await new Promise(resolve => {
      deck.forEach(d => {
        const back = ArtStore.get("mg/memory/card_back");
        const m = el("button", "mcard", `<div class="in"><div class="b"${back ? ` style="background:url(${back.src}) center/cover"` : ""}></div><div class="f" style="color:${d.f[1]}">${ICON[d.f[0]]}</div></div>`);
        m.onclick = async () => {
          if (busy || m.classList.contains("up") || m.classList.contains("got")) return;
          m.classList.add("up"); ctx.sfx("flip"); open.push([m, d]);
          if (open.length === 2) {
            busy = true; moves++;
            const [[a, da], [b, db]] = open; open = [];
            if (da.f[0] === db.f[0]) { await ctx.wait(0.3); a.classList.add("got"); b.classList.add("got"); ctx.sfx("match"); found++; ctx.score(`${found} / ${pairs}`); if (found === pairs) resolve("win"); }
            else { await ctx.wait(0.7); a.classList.remove("up"); b.classList.remove("up"); }
            busy = false;
          }
        };
        grid.appendChild(m);
      });
      T.done.then(() => resolve("time"));
    });
    T.stop();
    return { won: res === "win", score: found, stars: res === "win" ? starsFor(T.frac() + 0.4) : 0, detail: res === "win" ? `${moves} moves` : `${found} of ${pairs} pairs` };
  });

  function sceneImage(ctx, size) {
    const st = ctx.env.stage, name = ctx.str("image", null) || st.bgName || "INT. COFFEE SHOP - DAY";
    const src = Art.background(name, 540, 960);
    const c = document.createElement("canvas"); c.width = c.height = size;
    c.getContext("2d").drawImage(src, 0, (960 - 540) * 0.35, 540, 540, 0, 0, size, size);
    return c.toDataURL("image/jpeg", 0.85);
  }
  reg("slide", { name: "Sliding puzzle", icon: "puzzle", color: "#3fa69a", blurb: "Rebuild a scrambled picture, tile by tile.", how: "Slide the tiles back into place to rebuild the picture." }, async ctx => {
    const n = Math.max(2, Math.min(5, ctx.num("size", 3))), time = ctx.num("time", 150) / ctx.diff;
    const img = sceneImage(ctx, 600);
    const c = center(ctx, `<div style="font-size:.85em;opacity:.85">Tap a tile next to the gap</div>`);
    const box = el("div"); box.style.cssText = "position:relative;width:86%;aspect-ratio:1;max-width:100%;border-radius:.8em;overflow:hidden;background:rgba(255,255,255,.12);box-shadow:0 .5em 1.5em rgba(0,0,0,.4)";
    c.appendChild(box);
    let pos = [...Array(n * n).keys()]; // pos[tile] = cell
    let empty = n * n - 1;
    for (let k = 0; k < 60 * n; k++) {
      const er = Math.floor(empty / n), ec = empty % n;
      const nb = [[er - 1, ec], [er + 1, ec], [er, ec - 1], [er, ec + 1]].filter(([r, cc]) => r >= 0 && r < n && cc >= 0 && cc < n).map(([r, cc]) => r * n + cc);
      const cell = nb[Math.floor(R() * nb.length)], t = pos.indexOf(cell);
      pos[t] = empty; empty = cell;
    }
    const tiles = [];
    let moves = 0;
    const place = () => tiles.forEach((t, i) => { const cell = pos[i]; t.style.transform = `translate(${(cell % n) * 100}%, ${Math.floor(cell / n) * 100}%)`; });
    const solved = () => pos.every((p, i) => i === n * n - 1 || p === i);
    ctx.score("0 moves");
    const T = ctx.timer(time);
    const res = await new Promise(resolve => {
      for (let i = 0; i < n * n - 1; i++) {
        const t = el("button", "", `<span style="position:absolute;right:.3em;bottom:.2em;font-family:var(--font-title);font-size:.8em;color:#fff;text-shadow:0 .1em .2em rgba(0,0,0,.7)">${i + 1}</span>`);
        t.style.cssText = `position:absolute;left:0;top:0;width:${100 / n}%;height:${100 / n}%;background-image:url(${img});background-size:${n * 100}% ${n * 100}%;background-position:${(i % n) / (n - 1) * 100}% ${Math.floor(i / n) / (n - 1) * 100}%;transition:transform .14s;box-shadow:inset 0 0 0 1px rgba(255,255,255,.5)`;
        t.onclick = () => {
          const cell = pos[i], r = Math.floor(cell / n), cc = cell % n, er = Math.floor(empty / n), ec = empty % n;
          if (Math.abs(r - er) + Math.abs(cc - ec) !== 1) { ctx.sfx("miss"); return; }
          pos[i] = empty; empty = cell; moves++; ctx.sfx("flip"); ctx.score(`${moves} moves`); place();
          if (solved()) resolve("win");
        };
        tiles.push(t); box.appendChild(t);
      }
      place();
      T.done.then(() => resolve("time"));
    });
    T.stop();
    return { won: res === "win", score: moves, stars: res === "win" ? starsFor(T.frac() + 0.35) : 0, detail: `${moves} moves` };
  });

  reg("hidden", { name: "Hidden objects", icon: "search", color: "#5a8fd9", blurb: "Find every listed item in the scene.", how: "Find every item on the list. Wrong taps cost time!" }, async ctx => {
    let names = (ctx.str("items", "") || "").split(",").map(s => s.trim()).filter(Boolean);
    ctx.data.forEach(l => l.trim() && names.push(l.trim()));
    if (!names.length) names = ["key", "ring", "letter", "star"];
    const time = ctx.num("time", 45) / ctx.diff, size = ctx.num("size", 130) / 1000, vis = ctx.num("visibility", 0.8);
    const iconFor = n => { const k = n.toLowerCase(); return ICON[k] ? k : ["star", "heart", "moon", "diamond", "leaf", "gift"][Art.hash(k) % 6]; };
    const b = ctx.board;
    const scene = el("div"); scene.style.cssText = `position:absolute;inset:0 0 4.2em 0;background:url(${Art.background(ctx.env.stage.bgName || "INT. COFFEE SHOP - DAY", 540, 960).toDataURL("image/jpeg", 0.8)}) center/cover;border-radius:0`;
    b.appendChild(scene);
    const listEl = el("div"); listEl.style.cssText = "position:absolute;left:0;right:0;bottom:0;height:4.2em;display:flex;gap:.4em;align-items:center;justify-content:center;flex-wrap:wrap;padding:.4em;background:rgba(20,10,26,.85)";
    b.appendChild(listEl);
    let found = 0; ctx.score(`0 / ${names.length}`);
    const T = ctx.timer(time);
    const res = await new Promise(resolve => {
      scene.onpointerdown = e => { if (e.target !== scene) return; T.add(-1.5); ctx.sfx("wrong"); ctx.feedback("-1.5s", "#ff8a8a", 30); };
      names.forEach((raw, i) => {
        const [nm, at] = raw.split("@");
        const chip = el("span", "chip", esc(nm.replace(/_/g, " "))); chip.style.fontSize = ".8em"; listEl.appendChild(chip);
        const ic = iconFor(nm);
        const it = el("button", "", ICON[ic]);
        let x = 0.1 + R() * 0.8, y = 0.12 + R() * 0.75;
        if (at) { const [ax, ay] = at.split(",").map(Number); if (!isNaN(ax)) { x = ax; y = ay; } }
        const s = scene.clientWidth * size + 24;
        it.style.cssText = `position:absolute;width:${s}px;height:${s}px;left:calc(${x * 100}% - ${s / 2}px);top:calc(${y * 100}% - ${s / 2}px);color:${FACES[Art.hash(nm) % FACES.length][1]};opacity:${vis};transform:rotate(${(R() - 0.5) * 60}deg);filter:drop-shadow(0 .1em .1em rgba(0,0,0,.4))`;
        it.onpointerdown = e => {
          e.stopPropagation(); if (it.dataset.got) return; it.dataset.got = 1; found++;
          ctx.sfx("match"); ctx.score(`${found} / ${names.length}`);
          it.animate([{ transform: "scale(1)" }, { transform: "scale(1.8)", opacity: 0 }], { duration: 400, fill: "forwards" });
          chip.style.textDecoration = "line-through"; chip.style.opacity = ".45";
          if (found === names.length) resolve("win");
        };
        scene.appendChild(it);
      });
      T.done.then(() => resolve("time"));
    });
    T.stop();
    return { won: res === "win", score: found, stars: res === "win" ? starsFor(T.frac() + 0.4) : 0, detail: `${found} of ${names.length} found` };
  });

  reg("scramble", { name: "Word scramble", icon: "abc", color: "#e48a5a", blurb: "Unscramble the letters to spell the word.", how: "Tap the letters in order to spell the hidden word." }, async ctx => {
    const word = ctx.str("word", "LATTE").toUpperCase(), hint = ctx.str("hint", null), time = ctx.num("time", 40) / ctx.diff;
    let letters = shuffle(word.split("")); let tries = 0;
    while (letters.join("") === word && word.length > 1 && tries++ < 10) letters = shuffle(letters);
    const c = center(ctx, `${hint ? `<div style="text-align:center;font-size:.95em;opacity:.9">${esc(hint)}</div>` : ""}<div class="slots" style="display:flex;gap:.35em;flex-wrap:wrap;justify-content:center"></div><div class="pool" style="display:flex;gap:.35em;flex-wrap:wrap;justify-content:center;margin-top:1em"></div>`);
    const slotsEl = c.querySelector(".slots"), poolEl = c.querySelector(".pool");
    const slots = word.split("").map(() => { const s = el("button", "letter slot"); slotsEl.appendChild(s); return { el: s, from: null }; });
    ctx.score(`${word.length} letters`);
    const T = ctx.timer(time);
    const res = await new Promise(resolve => {
      const tiles = letters.map(L => { const t = el("button", "letter", L); poolEl.appendChild(t); return t; });
      tiles.forEach(t => t.onclick = async () => {
        const s = slots.find(s => !s.from); if (!s) return;
        s.from = t; s.el.textContent = t.textContent; t.classList.add("used"); ctx.sfx("pop");
        if (slots.every(s => s.from)) {
          if (slots.map(s => s.el.textContent).join("") === word) { slots.forEach(s => s.el.style.background = "#3cc88f"); resolve("win"); }
          else { ctx.sfx("wrong"); slotsEl.style.animation = "jitter .1s linear 4"; await ctx.wait(0.45); slotsEl.style.animation = ""; slots.forEach(s => { s.from.classList.remove("used"); s.from = null; s.el.textContent = ""; }); }
        }
      });
      slots.forEach(s => s.el.onclick = () => { if (!s.from) return; s.from.classList.remove("used"); s.from = null; s.el.textContent = ""; ctx.sfx("flip"); });
      T.done.then(() => resolve("time"));
    });
    T.stop();
    return { won: res === "win", score: res === "win" ? 1 : 0, stars: res === "win" ? starsFor(T.frac() + 0.4) : 0, detail: res === "win" ? null : `The word was ${word}` };
  });

  // ---------- mind ----------
  reg("quiz", { name: "Quiz", icon: "quiz", color: "#5cc8ff", blurb: "Answer questions written right in the story.", how: "Pick the right answer for each question.", noCountdown: true }, async ctx => {
    const qs = []; let cur = null;
    for (const raw of ctx.data) {
      const l = raw.trim();
      if (/^q\s*:/i.test(l)) { cur = { q: l.replace(/^q\s*:\s*/i, ""), a: [] }; qs.push(cur); }
      else if (cur && /^[*-]/.test(l)) cur.a.push({ t: l.slice(1).trim(), ok: l[0] === "*" });
    }
    if (!qs.length) qs.push({ q: "What does this engine read?", a: [{ t: "Plain-text stories", ok: true }, { t: "Spreadsheets", ok: false }, { t: "Videos", ok: false }] });
    const order = ctx.bool("shuffle", true) ? shuffle(qs.slice()) : qs;
    const pass = ctx.num("pass", Math.ceil(qs.length * 0.6)), per = ctx.num("time", 0);
    let right = 0;
    for (let i = 0; i < order.length; i++) {
      const q = order[i];
      ctx.board.innerHTML = "";
      ctx.score(`${i + 1} / ${order.length}`);
      const c = center(ctx, `<div class="card" style="position:static;transform:none;width:100%;animation:pop .3s both"><div class="eyebrow" style="color:#8a7a90;margin-bottom:.4em">Question ${i + 1}</div><h2 style="font-size:1.2em">${esc(q.q)}</h2><div class="ans" style="display:flex;flex-direction:column;gap:.5em"></div></div>`);
      const ans = c.querySelector(".ans");
      const answers = ctx.bool("shuffleanswers", true) ? shuffle(q.a.slice()) : q.a;
      const T = per ? ctx.timer(per) : null;
      const pick = await new Promise(resolve => {
        answers.forEach(a => { const b = el("button", "opt", `<span>${esc(a.t)}</span>`); b.style.boxShadow = "0 .25em 0 #d9c9df"; b.style.background = "#f6f0f8"; b.onclick = () => resolve({ a, b }); ans.appendChild(b); });
        if (T) T.done.then(() => resolve(null));
      });
      if (T) T.stop();
      const btns = [...ans.children];
      btns.forEach((b, k) => { if (answers[k].ok) { b.style.background = "#3cc88f"; b.style.color = "#fff"; } b.disabled = true; });
      if (pick && pick.a.ok) { right++; ctx.sfx("right"); }
      else { ctx.sfx("wrong"); if (pick) { pick.b.style.background = "#ff5d5d"; pick.b.style.color = "#fff"; } }
      await ctx.wait(1.1);
    }
    return { won: right >= pass, score: right, stars: starsFor(pct(right, order.length)), detail: `${right} of ${order.length} correct` };
  });

  reg("keypad", { name: "Code keypad", icon: "keypad", color: "#46b39d", blurb: "Enter the code to open a safe, phone or door.", how: "Enter the right code. Look for clues!" }, async ctx => {
    const code = String(ctx.str("code", "1234")), tries0 = ctx.num("tries", 3), hint = ctx.str("hint", null), time = ctx.num("time", 0), auto = ctx.bool("autosubmit", true);
    const c = center(ctx, `${hint ? `<div style="background:#fff3b0;color:#5a4400;padding:.6em .8em;border-radius:.3em;transform:rotate(-2deg);box-shadow:0 .3em .6em rgba(0,0,0,.3);font-size:.9em;max-width:90%;text-align:center">${esc(hint)}</div>` : ""}
      <div style="background:#2a2f38;padding:1em;border-radius:1.2em;width:80%;box-shadow:0 .6em 1.6em rgba(0,0,0,.5)"><div class="display"></div><div class="keys" style="display:grid;grid-template-columns:repeat(3,1fr);gap:.5em;margin-top:.9em"></div></div>`);
    const disp = c.querySelector(".display"), keys = c.querySelector(".keys");
    let entry = "", tries = tries0;
    ctx.score(`${tries} tries`);
    const T = ctx.timer(time);
    const render = () => disp.textContent = entry.padEnd(code.length, "•").split("").join("");
    render();
    const res = await new Promise(resolve => {
      const submit = async () => {
        if (entry === code) { disp.style.color = "#3cc88f"; disp.textContent = "OPEN"; ctx.sfx("success"); await ctx.wait(0.5); resolve("win"); return; }
        tries--; ctx.score(`${tries} ${tries === 1 ? "try" : "tries"}`); ctx.sfx("wrong");
        disp.classList.add("bad"); disp.textContent = "ERROR"; await ctx.wait(0.6); disp.classList.remove("bad");
        entry = ""; render();
        if (tries <= 0) resolve("out");
      };
      ["1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK"].forEach(k => {
        const b = el("button", "key", k); if (k === "OK") { b.style.background = "#3cc88f"; b.style.color = "#fff"; } if (k === "C") { b.style.background = "#efe7f2"; }
        b.onclick = () => {
          ctx.sfx("tick");
          if (k === "C") { entry = ""; render(); return; }
          if (k === "OK") { if (entry.length) submit(); return; }
          if (entry.length >= code.length) return;
          entry += k; render();
          if (auto && entry.length === code.length) submit();
        };
        keys.appendChild(b);
      });
      T.done.then(() => resolve("time"));
    });
    T.stop();
    return { won: res === "win", score: tries, stars: res === "win" ? (tries === tries0 ? 3 : tries > 1 ? 2 : 1) : 0, detail: res === "win" ? null : "The keypad locks." };
  });

  reg("sequence", { name: "Follow the pattern", icon: "simon", color: "#ff9f1c", blurb: "Watch the pads light up, then repeat.", how: "Watch the pattern, then tap the pads in the same order." }, async ctx => {
    const length = ctx.num("length", 5), pads = Math.max(3, Math.min(6, ctx.num("pads", 4))), start = ctx.num("start", 2);
    const cols = ["#ff5c8a", "#3cc88f", "#5cc8ff", "#ffb547", "#b07cff", "#ff7a45"];
    const c = center(ctx, `<div class="msg" style="font-family:var(--font-title);font-size:1.2em">Watch...</div><div class="pads" style="display:grid;grid-template-columns:repeat(${pads > 4 ? 3 : 2},1fr);gap:.7em;width:78%"></div>`);
    const msg = c.querySelector(".msg"), grid = c.querySelector(".pads");
    const P = Array.from({ length: pads }, (_, i) => { const b = el("button", "pad"); b.style.cssText = `aspect-ratio:1;max-width:100%;background:${cols[i]};color:${cols[i]}`; grid.appendChild(b); return b; });
    const seq = Array.from({ length }, () => Math.floor(R() * pads));
    const light = async (i, d) => { P[i].classList.add("lit"); Audio2.sfx("note_" + (i + 1)); await ctx.wait(d); P[i].classList.remove("lit"); };
    let round = 0, ok = true;
    for (let len = Math.min(start, length); len <= length && ok; len++) {
      round = len; ctx.score(`${len - Math.min(start, length) + 1} / ${length - Math.min(start, length) + 1}`);
      msg.textContent = "Watch..."; P.forEach(p => p.disabled = true);
      await ctx.wait(0.5);
      for (let k = 0; k < len; k++) { await light(seq[k], 0.42 / Math.sqrt(ctx.diff)); await ctx.wait(0.15); }
      msg.textContent = "Your turn!"; P.forEach(p => p.disabled = false);
      ok = await new Promise(resolve => {
        let k = 0;
        P.forEach((p, i) => p.onclick = () => {
          light(i, 0.2);
          if (i !== seq[k]) { ctx.sfx("wrong"); resolve(false); return; }
          if (++k === len) { ctx.feedback("Yes!", "#7de3b4", 20); resolve(true); }
        });
      });
      P.forEach(p => p.onclick = null);
      await ctx.wait(0.5);
    }
    return { won: ok, score: ok ? length : round - 1, stars: ok ? 3 : 0, detail: ok ? null : `You remembered ${Math.max(0, round - 1)} steps` };
  });

  // ---------- action ----------
  reg("catch", { name: "Catch", icon: "basket", color: "#3cc88f", blurb: "Drag the basket; catch the good, dodge the bad.", how: "Drag the basket to catch the good things. Avoid the bad ones!" }, async ctx => {
    const need = ctx.num("need", 12), time = ctx.num("time", 25), speed = ctx.num("speed", 1) * ctx.diff, bad = ctx.num("bad", 0.3);
    const b = ctx.board, W = b.clientWidth, H = b.clientHeight;
    const basket = el("div", "", ICON.basket); basket.style.cssText = `position:absolute;bottom:4%;width:${W * 0.24}px;height:${W * 0.2}px;color:#fff;filter:drop-shadow(0 .2em 0 rgba(0,0,0,.3));left:${W / 2 - W * 0.12}px`;
    b.appendChild(basket);
    let bx = W / 2, score = 0, items = [], spawnT = 0;
    const move = e => { const r = b.getBoundingClientRect(); bx = Math.max(W * 0.12, Math.min(W * 0.88, e.clientX - r.left)); };
    b.addEventListener("pointermove", move); b.addEventListener("pointerdown", move);
    ctx.score(`0 / ${need}`);
    const T = ctx.timer(time);
    const res = await new Promise(resolve => {
      ctx.loop(dt => {
        basket.style.left = bx - W * 0.12 + "px";
        spawnT -= dt;
        if (spawnT <= 0) {
          spawnT = (0.55 + R() * 0.45) / speed;
          const isBad = R() < bad, f = isBad ? (R() < 0.5 ? ["bomb", "#3a3344"] : ["cloud", "#8f9bb3"]) : FACES[Math.floor(R() * 6)];
          const it = el("div", "", ICON[f[0]]); const s = W * 0.12;
          it.style.cssText = `position:absolute;left:0;top:0;width:${s}px;height:${s}px;color:${f[1]};filter:drop-shadow(0 .1em .1em rgba(0,0,0,.4))`;
          b.appendChild(it); items.push({ el: it, x: W * 0.08 + R() * W * 0.84, y: -s, v: H * (0.32 + R() * 0.2) * speed, bad: isBad, s, r: (R() - 0.5) * 3 });
        }
        for (const it of items) {
          it.y += it.v * dt;
          it.el.style.transform = `translate(${it.x - it.s / 2}px, ${it.y}px) rotate(${it.y * it.r * 0.01}rad)`;
          const by = H * 0.96 - W * 0.2;
          if (!it.done && it.y + it.s > by && it.y < by + W * 0.08 && Math.abs(it.x - bx) < W * 0.14) {
            it.done = true; it.el.remove();
            if (it.bad) { score = Math.max(0, score - 2); ctx.sfx("ouch"); ctx.feedback("-2", "#ff8a8a", 60); basket.animate([{ translate: "-.3em 0" }, { translate: ".3em 0" }, { translate: "0 0" }], { duration: 200 }); }
            else { score++; ctx.sfx("catch"); }
            ctx.score(`${score} / ${need}`);
            if (score >= need) resolve("win");
          }
          if (it.y > H) { it.done = true; it.el.remove(); }
        }
        items = items.filter(i => !i.done);
      });
      T.done.then(() => resolve("time"));
    });
    T.stop(); b.removeEventListener("pointermove", move);
    return { won: res === "win", score, stars: res === "win" ? starsFor(T.frac() + 0.5) : 0, detail: `${score} caught` };
  });

  reg("rhythm", { name: "Rhythm", icon: "note", color: "#ff5c8a", blurb: "Tap the notes as they cross the line.", how: "Tap each lane's button as the notes cross the line!" }, async ctx => {
    const bpm = ctx.num("bpm", 110), count = ctx.num("notes", 20), lanes = Math.max(2, Math.min(4, ctx.num("lanes", 3))), need = ctx.num("need", 70), travel = ctx.num("travel", 1.4) / ctx.diff;
    const b = ctx.board, H = b.clientHeight;
    const cols = ["#ff5c8a", "#5cc8ff", "#ffb547", "#3cc88f"];
    const wrap = el("div"); wrap.style.cssText = `position:absolute;inset:0 6%;display:grid;grid-template-columns:repeat(${lanes},1fr);gap:4%`;
    b.appendChild(wrap);
    const lineY = H * 0.78;
    const L = Array.from({ length: lanes }, (_, i) => {
      const lane = el("div"); lane.style.cssText = "position:relative;height:100%;background:linear-gradient(transparent,rgba(255,255,255,.08));border-radius:1em";
      const line = el("div"); line.style.cssText = `position:absolute;left:0;right:0;top:${lineY}px;height:.3em;margin-top:-.15em;background:${cols[i]};border-radius:999px;box-shadow:0 0 1em ${cols[i]}`;
      const btn = el("button"); btn.style.cssText = `position:absolute;left:8%;right:8%;bottom:2%;height:${H * 0.13}px;border-radius:1em;background:${cols[i]};opacity:.85;box-shadow:0 .3em 0 rgba(0,0,0,.3)`;
      lane.append(line, btn); wrap.appendChild(lane);
      return { lane, btn };
    });
    const beat = 60 / bpm, t0 = Clock.now + 1.0;
    const notes = []; let t = 0;
    for (let i = 0; i < count; i++) { t += R() < 0.25 ? beat / 2 : beat; notes.push({ time: t0 + travel + t, lane: Math.floor(R() * lanes), el: null, state: 0 }); }
    let hits = 0, judged = 0, lastBeat = -1;
    ctx.score("0%");
    const judge = (n, ok, perfect) => { n.state = ok ? 1 : 2; judged++; if (ok) hits++; if (n.el) { n.el.animate([{ transform: n.el.style.transform + " scale(1)", opacity: 1 }, { transform: n.el.style.transform + " scale(1.8)", opacity: 0 }], { duration: 250, fill: "forwards" }); } ctx.score(`${Math.round(hits / Math.max(1, judged) * 100)}%`); if (ok) ctx.feedback(perfect ? "Perfect!" : "Good", perfect ? "#ffe08a" : "#7de3b4", 55); };
    L.forEach((l, i) => l.btn.onpointerdown = e => {
      e.preventDefault(); l.btn.animate([{ filter: "brightness(1.6)" }, { filter: "none" }], { duration: 150 });
      const now = Clock.now;
      const n = notes.filter(n => n.lane === i && !n.state).sort((a, b) => Math.abs(a.time - now) - Math.abs(b.time - now))[0];
      if (n && Math.abs(n.time - now) < 0.2) { judge(n, true, Math.abs(n.time - now) < 0.08); Audio2.sfx("note_" + (i + 1)); } else Audio2.sfx("tick");
    });
    await new Promise(resolve => {
      ctx.loop(() => {
        const now = Clock.now;
        const bt = Math.floor((now - t0) / beat); if (bt > lastBeat && now > t0) { lastBeat = bt; if (!Audio2.musicName) Audio2.sfx("tick"); }
        for (const n of notes) {
          const k = 1 - (n.time - now) / travel;
          if (k < 0 || n.state === 1) continue;
          if (!n.el && !n.state) { n.el = el("div"); n.el.style.cssText = `position:absolute;left:12%;right:12%;top:0;height:${H * 0.05}px;margin-top:-${H * 0.025}px;border-radius:999px;background:#fff;box-shadow:0 0 .8em ${cols[n.lane]},inset 0 0 0 .25em ${cols[n.lane]}`; L[n.lane].lane.appendChild(n.el); }
          if (n.el) n.el.style.transform = `translateY(${k * lineY}px)`;
          if (!n.state && now - n.time > 0.2) { judge(n, false); ctx.feedback("Miss", "#ff8a8a", 55); }
          if (n.el && k > 1.25) { n.el.remove(); n.el = null; }
        }
        if (judged >= notes.length && now > notes[notes.length - 1].time + 0.5) resolve();
      });
    });
    const acc = Math.round(hits / notes.length * 100);
    return { won: acc >= need, score: acc, stars: acc >= 95 ? 3 : acc >= 80 ? 2 : 1, detail: `${acc}% accuracy` };
  });

  reg("dressup", { name: "Dress up", icon: "shirt", color: "#b07cff", blurb: "Pick an outfit; it's applied live and saved for later.", how: "Choose a look.", light: true, noCountdown: true, noIntro: true, noResult: true }, async ctx => {
    const env = ctx.env, st = env.stage, id = (ctx.str("character", "PLAYER")).toUpperCase();
    const c = st.character(id);
    const wasOn = c.onStage;
    if (!c.onStage) Object.assign(c, { x: 160, y: 0, scale: 1, onStage: true, alpha: 0, facing: 0 });
    const prevCam = { ...st.cam };
    Clock.tween(0.4, k => c.alpha = Math.max(c.alpha, k));
    st.camTo(1.12, c.x / 320, 0.42, 0.6);
    const slots = [];
    for (const l of ctx.data) { const m = l.trim().match(/^([A-Za-z_]+)\s*:\s*(.+)$/); if (m) slots.push({ slot: m[1].toLowerCase(), opts: m[2].split(",").map(s => s.trim().toLowerCase()).filter(Boolean) }); }
    if (!slots.length) slots.push({ slot: "outfit", opts: ["casual", "dress", "suit", "sweater"] }, { slot: "hair", opts: ["loose", "ponytail", "bun", "short"] });
    ctx.hud.querySelector(".title").textContent = ctx.str("title", "Choose your look");
    const panel = el("div"); panel.style.cssText = "position:absolute;left:5%;right:5%;bottom:3%;display:flex;flex-direction:column;gap:.55em";
    ctx.board.appendChild(panel);
    const idx = slots.map(s => Math.max(0, s.opts.indexOf(c.look.slots[s.slot])));
    const apply = (i, twirl) => {
      const s = slots[i]; c.look.slots[s.slot] = s.opts[idx[i]];
      if (twirl) { c.play(s.slot === "hair" ? "nod" : "spin"); Audio2.sfx("whoosh"); }
      panel.children[i].querySelector(".lbl").innerHTML = `<small>${esc(s.slot)}</small>${esc(s.opts[idx[i]].replace(/_/g, " ").replace(/\b\w/g, m => m.toUpperCase()))}`;
    };
    slots.forEach((s, i) => {
      const row = el("div", "slot-row", `<button class="arrow" aria-label="Previous ${esc(s.slot)}">${ICON.left}</button><div class="lbl"></div><button class="arrow" aria-label="Next ${esc(s.slot)}">${ICON.right}</button>`);
      const [prev, , next] = row.children;
      prev.onclick = () => { idx[i] = (idx[i] + s.opts.length - 1) % s.opts.length; apply(i, true); };
      next.onclick = () => { idx[i] = (idx[i] + 1) % s.opts.length; apply(i, true); };
      panel.appendChild(row); apply(i, false);
    });
    const ok = el("button", "btn", esc(ctx.str("confirm", "Looks great!"))); ok.style.alignSelf = "center"; ok.style.marginTop = ".3em";
    panel.appendChild(ok);
    c.emotion = "happy";
    await new Promise(r => ok.onclick = () => { Audio2.sfx("success"); r(); });
    slots.forEach((s, i) => env.vars.set(id + "_" + s.slot, s.opts[idx[i]]));
    c.play("cheer");
    await st.camTo(prevCam.z, prevCam.fx, prevCam.fy, 0.5);
    if (!wasOn && !env.story) c.onStage = false;
    return { won: true, score: 1 };
  });

  // demo setups for the arcade
  const demo = {
    qte: { params: { count: "6", need: "4" } },
    timing: { params: { rounds: "3", need: "2" } },
    mash: { params: { taps: "25", time: "7" } },
    memory: { params: { pairs: "6", time: "60" } },
    slide: { params: { size: "3", time: "180" } },
    hidden: { params: { items: "key,ring,letter,star,heart", time: "45" } },
    scramble: { params: { word: "COFFEE", hint: "Maya's favorite drink", time: "40" } },
    quiz: { data: ["Q: What's in a cortado?", "* Espresso and a little warm milk", "- Espresso and whipped cream", "- Cold brew and oat milk", "Q: Which drink has the most foam?", "* Cappuccino", "- Flat white", "- Americano"], params: { pass: "1" } },
    keypad: { params: { code: "0315", hint: "Grand opening: March 15", tries: "3" } },
    sequence: { params: { length: "5", start: "2" } },
    catch: { params: { need: "12", time: "25" } },
    rhythm: { params: { bpm: "110", notes: "18", need: "60" } },
    dressup: { data: ["outfit: casual, dress, suit, sweater", "hair: loose, ponytail, bun, short"], params: { character: "PLAYER" } },
  };
  list.forEach(g => Object.assign(g, demo[g.id] || {}));
  return { run, list };
})();
