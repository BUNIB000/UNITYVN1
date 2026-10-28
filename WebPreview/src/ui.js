// ===== UI: icons, bubbles, choices, cards, toasts, fades =====
const ICON = (() => {
  const s = (body, vb) => `<svg viewBox="${vb || "0 0 24 24"}" fill="currentColor" aria-hidden="true">${body}</svg>`;
  const st = body => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${body}</svg>`;
  return {
    heart: s('<path d="M12 21s-7.5-4.6-9.6-9.2C.9 8.3 3 4.5 6.7 4.5c2 0 3.6 1.1 5.3 3 1.7-1.9 3.3-3 5.3-3 3.7 0 5.8 3.8 4.3 7.3C19.5 16.4 12 21 12 21z"/>'),
    star: s('<path d="M12 2.5l2.9 6.1 6.6.8-4.9 4.6 1.3 6.6L12 17.3l-5.9 3.3 1.3-6.6L2.5 9.4l6.6-.8z"/>'),
    lock: s('<path d="M7 10V7a5 5 0 0110 0v3h1a1 1 0 011 1v9a1 1 0 01-1 1H6a1 1 0 01-1-1v-9a1 1 0 011-1zm2 0h6V7a3 3 0 00-6 0z"/>'),
    menu: st('<path d="M4 7h16M4 12h16M4 17h16"/>'),
    play: s('<path d="M7 4.5v15a1 1 0 001.5.9l12-7.5a1 1 0 000-1.7l-12-7.6A1 1 0 007 4.5z"/>'),
    ff: s('<path d="M3 5.5v13a1 1 0 001.6.8l8-6.5a1 1 0 000-1.6l-8-6.5A1 1 0 003 5.5zm9 0v13a1 1 0 001.6.8l8-6.5a1 1 0 000-1.6l-8-6.5a1 1 0 00-1.6.8z"/>'),
    auto: st('<path d="M5 12a7 7 0 0112-5l2 2M19 12a7 7 0 01-12 5l-2-2"/><path d="M19 4v5h-5M5 20v-5h5"/>'),
    close: st('<path d="M6 6l12 12M18 6L6 18"/>'),
    spark: s('<path d="M12 1.5l2.2 7.3 7.3 2.2-7.3 2.2L12 20.5l-2.2-7.3L2.5 11l7.3-2.2z"/>'),
    book: st('<path d="M4 5a2 2 0 012-2h13v15H6a2 2 0 00-2 2zM4 20a2 2 0 002 2h13v-4"/>'),
    pad: st('<rect x="2.5" y="7" width="19" height="11" rx="5"/><path d="M7 11v3M5.5 12.5h3M15.5 11.5h.01M18 13.5h.01"/>'),
    code: st('<path d="M8 7l-5 5 5 5M16 7l5 5-5 5M14 4l-4 16"/>'),
    left: st('<path d="M15 5l-7 7 7 7"/>'),
    right: st('<path d="M9 5l7 7-7 7"/>'),
    check: st('<path d="M5 12.5l4.5 4.5L19 7.5"/>'),
    phone: st('<rect x="6" y="2.5" width="12" height="19" rx="3"/><path d="M11 18h2"/>'),
    bolt: s('<path d="M13 2L4 14h6l-1 8 9-12h-6z"/>'),
    target: st('<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1.5" fill="currentColor"/>'),
    hand: st('<path d="M8 13V5.5a1.5 1.5 0 013 0V11m0-1.5v-6a1.5 1.5 0 013 0V11m0-4.5a1.5 1.5 0 013 0V12m0-3a1.5 1.5 0 013 0v5a8 8 0 01-8 8h-1a7 7 0 01-6-3.5L4.5 14a1.6 1.6 0 012.6-1.8L8 13.5"/>'),
    cards: st('<rect x="3" y="5" width="11" height="15" rx="2"/><path d="M10 3.5h9a2 2 0 012 2v13"/>'),
    puzzle: st('<path d="M4 8h3.5a2 2 0 114 0H15v3.5a2 2 0 110 4V19H11.5a2 2 0 10-4 0H4v-3.5a2 2 0 100-4z"/>'),
    search: st('<circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5L21 21"/>'),
    abc: st('<path d="M3 17l3-9 3 9M4 14h4M12 8h3a2 2 0 010 4h-3zm0 4h3.5a2.5 2.5 0 010 5H12zM21 9.5a3.5 3.5 0 100 6"/>'),
    quiz: st('<circle cx="12" cy="12" r="9.5"/><path d="M9.5 9.5a2.5 2.5 0 114 2c-1 .7-1.5 1.2-1.5 2.5M12 17h.01"/>'),
    keypad: st('<rect x="5" y="2.5" width="14" height="19" rx="2.5"/><path d="M9 7h.01M12 7h.01M15 7h.01M9 11h.01M12 11h.01M15 11h.01M9 15h.01M12 15h.01M15 15h.01"/>'),
    simon: st('<path d="M12 3a9 9 0 019 9h-9zM3 12a9 9 0 019-9v9zM12 21a9 9 0 01-9-9h9zM21 12a9 9 0 01-9 9v-9z"/>'),
    basket: st('<path d="M3 10h18l-2 10H5zM8 10l3-6M16 10l-3-6"/>'),
    note: s('<path d="M9 17.5A3.5 3.5 0 115.5 14H7V4l12-2v13.5a3.5 3.5 0 11-3.5-3.5H17V6.3L9 7.6z"/>'),
    shirt: st('<path d="M8 3l4 3 4-3 5 3-2.5 4.5L16 9v12H8V9l-2.5 1.5L3 6z"/>'),
    moon: s('<path d="M20 14.5A8.5 8.5 0 019.5 4a8.5 8.5 0 1010.5 10.5z"/>'),
    sun: s('<circle cx="12" cy="12" r="5"/><path d="M12 1v3M12 20v3M1 12h3M20 12h3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1" stroke="currentColor" stroke-width="2"/>'),
    coffee: s('<path d="M3 8h14v6a6 6 0 01-6 6H9a6 6 0 01-6-6zm14 1h1.5a3 3 0 010 6H17v-2h1.5a1 1 0 000-2H17zM7 2.5c0 1.5-1 1.5-1 3M10.5 2.5c0 1.5-1 1.5-1 3M14 2.5c0 1.5-1 1.5-1 3" />'),
    leaf: s('<path d="M20 3C9 3 4 8 4 15c0 2 .6 3.8 1.4 5L4 21.5 5.5 23l1.6-1.6C8.3 22 10 22.5 12 22.5 19 22.5 21 15 20 3z"/>'),
    diamond: s('<path d="M6 3h12l4 6-10 12L2 9z"/>'),
    flower: s('<circle cx="12" cy="6" r="3.5"/><circle cx="18" cy="11" r="3.5"/><circle cx="15.5" cy="18" r="3.5"/><circle cx="8.5" cy="18" r="3.5"/><circle cx="6" cy="11" r="3.5"/><circle cx="12" cy="13" r="2.8" fill="#ffd166"/>'),
    cake: s('<path d="M4 12h16v9H4zM6 8h12v4H6zM12 3c1 1 1 2 0 3-1-1-1-2 0-3z"/>'),
    music2: s('<path d="M20 3v12.5a3.5 3.5 0 11-2-3.2V7l-9 2v8.5a3.5 3.5 0 11-2-3.2V5z"/>'),
    gift: s('<path d="M3 8h18v4H3zM5 12h14v9H5zM11 8h2v13h-2zM12 8C10 4 6 4 7 7M12 8c2-4 6-4 5-1" />'),
    ring: st('<circle cx="12" cy="15" r="6"/><path d="M9 4h6l-3 5z"/>'),
    letter: st('<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/>'),
    key: st('<circle cx="7.5" cy="12" r="4"/><path d="M11.5 12H21M18 12v3M15 12v2"/>'),
    bomb: s('<circle cx="10" cy="14" r="7"/><path d="M14 8l3-3M17 5l2-2M18 6l1.5.5" stroke="currentColor" stroke-width="2"/>'),
    cloud: s('<path d="M7 19a5 5 0 01-.6-10A6.5 6.5 0 0119 9.5 4.8 4.8 0 0118 19z"/>'),
    sound: st('<path d="M4 9v6h4l5 4V5L8 9zM16.5 8.5a5 5 0 010 7M19 6a8.5 8.5 0 010 12"/>'),
    mute: st('<path d="M4 9v6h4l5 4V5L8 9zM17 9l5 6M22 9l-5 6"/>'),
    history: st('<path d="M3 12a9 9 0 103-6.7M3 4v4h4M12 7v5l3 2"/>'),
    restart: st('<path d="M4 12a8 8 0 108-8 8 8 0 00-6 2.7M4 4v4.5h4.5"/>'),
    home: st('<path d="M3 11l9-7 9 7v9a1 1 0 01-1 1h-5v-6H9v6H4a1 1 0 01-1-1z"/>'),
  };
})();

const UI = (() => {
  let frame, ui, fx, mgLayer, screens, hud;
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
  const updaters = new Set();
  let tapWaiters = [];
  const mode = { auto: false, skip: false, speed: 40 };
  try { const s = JSON.parse(localStorage.getItem("vn_ui") || "null"); if (s && s.speed) mode.speed = s.speed; } catch (e) {}

  function init(f) {
    frame = f;
    ui = f.querySelector("#ui"); fx = f.querySelector("#fx"); mgLayer = f.querySelector("#mg"); screens = f.querySelector("#screens"); hud = f.querySelector("#hud");
    f.addEventListener("pointerdown", e => {
      if (e.target.closest("button, input, textarea, select, .mg, .sheet, .lib, .card")) return;
      const w = tapWaiters; tapWaiters = [];
      w.forEach(fn => fn());
    });
  }
  function onTap() { return new Promise(r => tapWaiters.push(r)); }
  // pulse(): resolves on the next mode change (auto/skip toggled) or when typing finishes
  let pulseWaiters = [];
  function pulse() { return new Promise(r => pulseWaiters.push(() => r("pulse"))); }
  function kick() { const w = pulseWaiters; pulseWaiters = []; w.forEach(f => f()); }
  function cancelTaps() { tapWaiters = []; }

  // ---------- dialogue ----------
  let bubble = null;
  function placeBubble(b) {
    if (!b || !b.anchor) return;
    const a = b.anchor();
    const W = frame.clientWidth, H = frame.clientHeight;
    const bw = b.el.offsetWidth, bh = b.el.offsetHeight;
    if (!a) { b.el.style.left = (W - bw) / 2 + "px"; b.el.style.top = H * 0.06 + "px"; if (b.tail) b.tail.style.display = "none"; return; }
    const x = Math.max(W * 0.05, Math.min(W * 0.95 - bw, a.x - bw / 2));
    const y = Math.max(H * 0.07, Math.min(H * 0.5, a.y - bh - H * 0.035));
    b.el.style.left = x + "px"; b.el.style.top = y + "px";
    const tx = Math.max(16, Math.min(bw - 34, a.x - x - 10));
    if (b.tail) { b.tail.style.display = ""; b.tail.style.left = tx + "px"; }
    if (b.dots) { b.dots.style.left = tx + "px"; }
  }
  // opts: { name, color, style, text, anchor(), onLetter(ch), onDone() }
  function say(o) {
    return new Promise(resolve => {
      clearBubble();
      const style = o.style || "speech";
      let root, textEl;
      if (style === "phone") {
        root = el("div", "phone", `<div class="bar"><div class="avatar" style="background:${o.color || "#3a7bff"}">${esc((o.name || "?")[0])}</div><span>${esc(o.name || "Unknown")}</span></div><div class="msg"></div>`);
        textEl = root.querySelector(".msg");
        Audio2.sfx("phone");
      } else {
        root = el("div", "bubble " + style);
        if (o.name && style !== "narration") { const n = el("div", "name", esc(o.name)); n.style.background = o.color || "#ff5c8a"; root.appendChild(n); }
        textEl = el("div", "text"); root.appendChild(textEl);
        root.appendChild(el("div", "more"));
      }
      const b = { el: root, anchor: style === "narration" || style === "phone" ? null : o.anchor };
      if (style === "narration") { root.style.top = "40%"; }
      if (b.anchor) {
        if (style === "thought") { b.dots = el("div", "dots", '<i style="width:.9em;height:.9em;left:.2em;top:0"></i><i style="width:.5em;height:.5em;left:.5em;top:1.05em"></i>'); root.appendChild(b.dots); }
        else { b.tail = el("div", "tail"); root.appendChild(b.tail); }
      } else if (style !== "narration" && style !== "phone") {
        b.anchor = () => null;
      }
      ui.appendChild(root);
      bubble = b;
      placeBubble(b);
      const text = o.text;
      let shown = 0, acc = 0, done = false;
      const finish = () => { if (done) return; done = true; shown = text.length; textEl.textContent = text; root.classList.add("done"); updaters.delete(typer); o.onDone && o.onDone(); placeBubble(b); kick(); };
      const typer = dt => {
        if (mode.skip) { finish(); return; }
        acc += dt * mode.speed * (style === "shout" ? 1.3 : 1);
        while (acc >= 1 && shown < text.length) {
          acc -= 1; const ch = text[shown++];
          o.onLetter && o.onLetter(ch, shown);
          if (/[.,!?…]/.test(ch)) acc -= ch === "," ? 2 : 5;
        }
        textEl.innerHTML = esc(text.slice(0, shown)) + '<span style="visibility:hidden">' + esc(text.slice(shown)) + '</span>';
        if (shown >= text.length) finish();
      };
      b.update = () => placeBubble(b);
      updaters.add(typer);
      const advance = async () => {
        for (;;) {
          if (mode.skip) { finish(); await Clock.wait(0.1); break; }
          if (mode.auto && done) {
            const r = await Promise.race([onTap().then(() => "tap"), Clock.wait(1.0 + text.length * 0.03).then(() => "auto"), pulse()]);
            if (r === "tap" || r === "auto") break;
            continue;
          }
          const r = await Promise.race([onTap().then(() => "tap"), pulse()]);
          if (r !== "tap") continue;
          if (!done) { finish(); continue; }
          Audio2.sfx("click");
          break;
        }
        resolve();
      };
      advance();
    });
  }
  function clearBubble() { if (bubble) { bubble.el.remove(); bubble = null; } }

  // ---------- choices ----------
  function choice(prompt, options, timer) {
    return new Promise(resolve => {
      clearBubble();
      const box = el("div"); box.id = "choices";
      if (prompt) box.appendChild(el("div", "prompt", esc(prompt)));
      let bar = null;
      if (timer) { const t = el("div", "timer", "<i></i>"); box.appendChild(t); bar = t.firstChild; }
      let chosen = false;
      const pick = (i, btn) => {
        if (chosen) return; chosen = true;
        Audio2.sfx("pop");
        if (btn) btn.classList.add("chosen");
        setTimeout(() => { box.remove(); resolve(i); }, 260);
      };
      options.forEach((o, i) => {
        const icon = !o.enabled ? ICON.lock : o.tag ? (ICON[o.tag.toLowerCase()] || ICON.star) : null;
        const b = el("button", "opt" + (o.enabled ? "" : " locked"), (icon ? `<span class="ic">${icon}</span>` : "") + `<span>${esc(o.text)}</span>`);
        b.style.animationDelay = (0.06 + i * 0.07) + "s";
        b.onclick = () => { if (o.enabled) pick(i, b); else { b.animate([{ translate: "-.3em 0" }, { translate: ".3em 0" }, { translate: "0 0" }], { duration: 200 }); Audio2.sfx("wrong"); } };
        box.appendChild(b);
      });
      ui.appendChild(box);
      if (timer) {
        const start = Clock.now;
        const tick = () => {
          if (chosen) { updaters.delete(tick); return; }
          const k = Math.min(1, (Clock.now - start) / timer);
          bar.style.transform = `scaleX(${1 - k})`;
          if (k >= 1) { updaters.delete(tick); const d = options.findIndex(o => o.isDefault && o.enabled); pick(d >= 0 ? d : options.findIndex(o => o.enabled), box.querySelectorAll(".opt")[d >= 0 ? d : 0]); }
        };
        updaters.add(tick);
      }
    });
  }

  function ask(prompt, def) {
    return new Promise(resolve => {
      clearBubble();
      const c = el("form", "card", `<h2>${esc(prompt)}</h2><input type="text" id="name-input" maxlength="20" autocomplete="off" placeholder="${esc(def || "")}"><div class="row"><button class="btn" type="submit">Confirm</button></div>`);
      c.onsubmit = e => { e.preventDefault(); Audio2.sfx("pop"); c.remove(); resolve(c.querySelector("input").value); };
      ui.appendChild(c);
      setTimeout(() => { try { c.querySelector("input").focus(); } catch (e) {} }, 350);
    });
  }

  function toast(text, icon) {
    const t = el("div", "toast", `<span class="ic">${ICON[icon] || ICON.spark}</span><span>${esc(text)}</span>`);
    ui.appendChild(t); Audio2.sfx("notify");
    setTimeout(() => t.remove(), 2900);
  }
  function location(title, sub) {
    const t = el("div", "location", `<b>${esc(title)}</b>${sub ? `<small>${esc(sub)}</small>` : ""}`);
    ui.appendChild(t); setTimeout(() => t.remove(), 2300);
  }
  let shown = new Map();
  function showItem(name) {
    hideItem(name, true);
    const box = el("div", "showitem");
    const c = Art.item(name, 360); box.appendChild(c);
    ui.appendChild(box); shown.set(name, box); Audio2.sfx("whoosh");
  }
  function hideItem(name, instant) {
    const keys = name ? [name] : [...shown.keys()];
    for (const k of keys) { const b = shown.get(k); if (!b) continue; shown.delete(k); if (instant) b.remove(); else { b.animate([{ opacity: 1 }, { opacity: 0, transform: "translate(-50%,-50%) scale(.6)" }], { duration: 250, fill: "forwards" }); setTimeout(() => b.remove(), 260); } }
  }
  function fade(to, dur, color) {
    const f = fx.querySelector("#fade");
    if (color) f.style.background = color;
    const from = parseFloat(f.style.opacity || 0);
    return Clock.tween(dur, k => f.style.opacity = from + (to - from) * k);
  }
  async function flash(color, dur) {
    const f = fx.querySelector("#flash");
    f.style.background = color || "#fff";
    await Clock.tween(dur || 0.45, k => f.style.opacity = k < 0.15 ? k / 0.15 : 1 - (k - 0.15) / 0.85);
  }
  function clearAll() { clearBubble(); ui.innerHTML = ""; shown.clear(); cancelTaps(); }

  function tick(dt) { for (const u of [...updaters]) u(dt); if (bubble && bubble.update) bubble.update(); }

  function saveMode() { try { localStorage.setItem("vn_ui", JSON.stringify({ speed: mode.speed })); } catch (e) {} }

  return { init, el, esc, say, clearBubble, choice, ask, toast, location, showItem, hideItem, fade, flash, clearAll, tick, onTap, cancelTaps, kick, mode, saveMode, updaters, get frame() { return frame; }, get ui() { return ui; }, get mgLayer() { return mgLayer; }, get screens() { return screens; }, get hud() { return hud; } };
})();
