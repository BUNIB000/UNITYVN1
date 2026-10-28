// ===== Your own art: images imported on this device (IndexedDB), used in place of the built-in art =====
// Keys: char/<ID>/<outfit|base>/<name>, bg/<name>, img/<name>, cover/<name>, mg/<game>/<name>
const ArtStore = (() => {
  const mem = new Map();
  let db = null, version = 0;
  const listeners = new Set();
  const changed = () => { version++; Art.clearCache(); listeners.forEach(f => f()); };

  function open() {
    return new Promise(res => {
      try {
        const r = indexedDB.open("vn_art", 1);
        r.onupgradeneeded = () => r.result.createObjectStore("a");
        r.onsuccess = () => { db = r.result; res(); };
        r.onerror = () => res();
      } catch (e) { res(); }
    });
  }
  function toImage(blob) {
    return new Promise(res => {
      const url = URL.createObjectURL(blob), im = new Image();
      im.onload = () => res(im); im.onerror = () => res(null); im.src = url;
    });
  }
  async function loadAll() {
    await open(); if (!db) return;
    const entries = await new Promise(res => {
      try {
        const out = [], tx = db.transaction("a"), st = tx.objectStore("a"), cur = st.openCursor();
        cur.onsuccess = () => { const c = cur.result; if (c) { out.push([c.key, c.value]); c.continue(); } else res(out); };
        cur.onerror = () => res(out);
      } catch (e) { res([]); }
    });
    for (const [k, blob] of entries) { const im = await toImage(blob); if (im) mem.set(k, im); }
    if (entries.length) changed();
  }
  function idb(mode, fn) { return new Promise(res => { if (!db) return res(); try { const tx = db.transaction("a", mode); fn(tx.objectStore("a")); tx.oncomplete = () => res(); tx.onerror = () => res(); } catch (e) { res(); } }); }

  // Clears a plain background (white or any flat colour) by flood-filling from the edges.
  function removeBackground(g, w, h) {
    const d = g.getImageData(0, 0, w, h), p = d.data;
    const at = (x, y) => (y * w + x) * 4;
    const corners = [at(1, 1), at(w - 2, 1), at(1, h - 2), at(w - 2, h - 2)];
    if (corners.some(i => p[i + 3] < 200)) return; // already transparent
    const bg = [0, 1, 2].map(c => corners.reduce((s, i) => s + p[i + c], 0) / 4);
    const dist = i => Math.max(Math.abs(p[i] - bg[0]), Math.abs(p[i + 1] - bg[1]), Math.abs(p[i + 2] - bg[2]));
    const HARD = 34, SOFT = 64;
    const seen = new Uint8Array(w * h), q = new Int32Array(w * h);
    let qh = 0, qt = 0;
    const push = n => { if (!seen[n]) { seen[n] = 1; q[qt++] = n; } };
    for (let x = 0; x < w; x++) { push(x); push((h - 1) * w + x); }
    for (let y = 0; y < h; y++) { push(y * w); push(y * w + w - 1); }
    while (qh < qt) {
      const n = q[qh++], i = n * 4, dd = dist(i);
      if (dd > SOFT) continue;
      if (dd > HARD) { p[i + 3] = Math.round(255 * (dd - HARD) / (SOFT - HARD)); continue; }
      p[i + 3] = 0;
      const x = n % w;
      if (x > 0) push(n - 1); if (x < w - 1) push(n + 1);
      if (n >= w) push(n - w); if (n < w * (h - 1)) push(n + w);
    }
    g.putImageData(d, 0, 0);
  }

  async function importFile(key, file, opts) {
    opts = opts || {};
    const src = await toImage(file);
    if (!src) throw new Error("That file isn't an image this browser can open.");
    const maxH = opts.maxH || 1600, maxW = opts.maxW || 1600;
    const k = Math.min(1, maxH / src.naturalHeight, maxW / src.naturalWidth);
    const w = Math.round(src.naturalWidth * k), h = Math.round(src.naturalHeight * k);
    const c = document.createElement("canvas"); c.width = w; c.height = h;
    const g = c.getContext("2d", { willReadFrequently: !!opts.removeBg });
    g.drawImage(src, 0, 0, w, h);
    URL.revokeObjectURL(src.src);
    if (opts.removeBg) removeBackground(g, w, h);
    await storeCanvas(key, c, opts.removeBg || opts.alpha);
  }
  async function storeCanvas(key, c, png) {
    const blob = await new Promise(res => c.toBlob(res, png ? "image/png" : "image/jpeg", 0.9));
    await idb("readwrite", st => st.put(blob, key));
    mem.set(key, await toImage(blob));
    changed();
  }

  // ----- Sheets: one picture holding several poses, cut into separate images -----
  const SOLID = 40;
  // Finds each figure on a cleared sheet: connected shapes on a coarse grid, small bits merged into nearby figures.
  function findFigures(p, w, h) {
    const s = Math.max(1, Math.ceil(Math.max(w, h) / 420)), gw = Math.ceil(w / s), gh = Math.ceil(h / s);
    const raw = new Uint8Array(gw * gh);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (p[(y * w + x) * 4 + 3] > SOLID) raw[((y / s) | 0) * gw + ((x / s) | 0)] = 1;
    const m = new Uint8Array(gw * gh); // grown by one cell so hair strands and fingers stay attached
    for (let y = 0; y < gh; y++) for (let x = 0; x < gw; x++) if (raw[y * gw + x])
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const X = x + dx, Y = y + dy; if (X >= 0 && Y >= 0 && X < gw && Y < gh) m[Y * gw + X] = 1; }
    const lab = new Int32Array(gw * gh), q = new Int32Array(gw * gh), boxes = [];
    for (let i = 0; i < m.length; i++) {
      if (!m[i] || lab[i]) continue;
      const b = { x0: 1e9, y0: 1e9, x1: -1, y1: -1, n: 0 };
      let qh = 0, qt = 0; q[qt++] = i; lab[i] = boxes.length + 1;
      while (qh < qt) {
        const n = q[qh++], x = n % gw, y = (n / gw) | 0;
        b.x0 = Math.min(b.x0, x); b.y0 = Math.min(b.y0, y); b.x1 = Math.max(b.x1, x); b.y1 = Math.max(b.y1, y); b.n++;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= gw || Y >= gh) continue;
          const k = Y * gw + X; if (m[k] && !lab[k]) { lab[k] = boxes.length + 1; q[qt++] = k; }
        }
      }
      boxes.push(b);
    }
    if (!boxes.length) return [];
    const area = b => (b.x1 - b.x0 + 1) * (b.y1 - b.y0 + 1);
    const big = Math.max(...boxes.map(area));
    let main = boxes.filter(b => area(b) >= big * 0.08);
    const small = boxes.filter(b => area(b) < big * 0.08);
    for (const b of small) { // a tear or sweat drop next to a figure joins it
      const cx = (b.x0 + b.x1) / 2, cy = (b.y0 + b.y1) / 2;
      const t = main.find(o => { const mx = (o.x1 - o.x0) * 0.15, my = (o.y1 - o.y0) * 0.1; return cx >= o.x0 - mx && cx <= o.x1 + mx && cy >= o.y0 - my && cy <= o.y1 + my; });
      if (t) { t.x0 = Math.min(t.x0, b.x0); t.y0 = Math.min(t.y0, b.y0); t.x1 = Math.max(t.x1, b.x1); t.y1 = Math.max(t.y1, b.y1); }
    }
    for (let again = true; again;) { // overlapping boxes are one figure
      again = false;
      for (let i = 0; i < main.length && !again; i++) for (let j = i + 1; j < main.length && !again; j++) {
        const a = main[i], b = main[j];
        if (a.x0 <= b.x1 && b.x0 <= a.x1 && a.y0 <= b.y1 && b.y0 <= a.y1) {
          a.x0 = Math.min(a.x0, b.x0); a.y0 = Math.min(a.y0, b.y0); a.x1 = Math.max(a.x1, b.x1); a.y1 = Math.max(a.y1, b.y1);
          main.splice(j, 1); again = true;
        }
      }
    }
    const out = main.map(b => ({ x: b.x0 * s, y: b.y0 * s, w: Math.min(w, (b.x1 + 1) * s) - b.x0 * s, h: Math.min(h, (b.y1 + 1) * s) - b.y0 * s }));
    // reading order: rows top to bottom, then left to right
    out.sort((a, b) => (a.y + a.h / 2) - (b.y + b.h / 2));
    const medH = out.map(b => b.h).sort((a, b) => a - b)[out.length >> 1], rows = [];
    for (const b of out) { const r = rows[rows.length - 1]; if (r && (b.y + b.h / 2) - r.cy < medH * 0.5) r.items.push(b); else rows.push({ cy: b.y + b.h / 2, items: [b] }); }
    return rows.flatMap(r => r.items.sort((a, b) => a.x - b.x));
  }
  // Tightens a box to the solid pixels inside it.
  function tighten(p, w, b) {
    let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1;
    for (let y = b.y; y < b.y + b.h; y++) for (let x = b.x; x < b.x + b.w; x++) if (p[(y * w + x) * 4 + 3] > SOLID) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
    return x1 < 0 ? null : { x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 };
  }
  // Horizontal middle of the legs and feet, so a raised arm doesn't shift the body sideways.
  function footCenter(p, w, b) {
    let sum = 0, n = 0;
    for (let y = Math.round(b.y + b.h * 0.75); y < b.y + b.h; y++) for (let x = b.x; x < b.x + b.w; x++) if (p[(y * w + x) * 4 + 3] > SOLID) { sum += x; n++; }
    return n ? sum / n : b.x + b.w / 2;
  }
  // Returns the cut pictures as canvases. figures: true puts every figure on the same canvas size,
  // feet on the bottom edge and centred on the legs, so swapping between them doesn't jump.
  async function cutSheet(file, opts) {
    opts = opts || {};
    const src = await toImage(file);
    if (!src) throw new Error("That file isn't an image this browser can open.");
    const k = Math.min(1, 3400 / Math.max(src.naturalWidth, src.naturalHeight));
    const w = Math.round(src.naturalWidth * k), h = Math.round(src.naturalHeight * k);
    const c = document.createElement("canvas"); c.width = w; c.height = h;
    const g = c.getContext("2d", { willReadFrequently: true });
    g.drawImage(src, 0, 0, w, h);
    URL.revokeObjectURL(src.src);
    let boxes = [];
    if (opts.rows && opts.cols) {
      const cw = w / opts.cols, ch = h / opts.rows;
      for (let r = 0; r < opts.rows; r++) for (let q = 0; q < opts.cols; q++) {
        const x = Math.round(q * cw), y = Math.round(r * ch), bw = Math.round((q + 1) * cw) - x, bh = Math.round((r + 1) * ch) - y;
        if (opts.removeBg) { const cell = g.getImageData(x, y, bw, bh); const t = document.createElement("canvas"); t.width = bw; t.height = bh; const tg = t.getContext("2d", { willReadFrequently: true }); tg.putImageData(cell, 0, 0); removeBackground(tg, bw, bh); g.putImageData(tg.getImageData(0, 0, bw, bh), x, y); }
        boxes.push({ x, y, w: bw, h: bh });
      }
    } else if (opts.removeBg) removeBackground(g, w, h);
    const p = g.getImageData(0, 0, w, h).data;
    if (!(opts.rows && opts.cols)) boxes = findFigures(p, w, h);
    boxes = boxes.map(b => tighten(p, w, b)).filter(Boolean);
    if (!opts.figures) {
      return boxes.map(b => { const pad = Math.round(Math.max(b.w, b.h) * 0.04), o = document.createElement("canvas"); o.width = b.w + pad * 2; o.height = b.h + pad * 2; o.getContext("2d").drawImage(c, b.x, b.y, b.w, b.h, pad, pad, b.w, b.h); return o; });
    }
    // alignFirst: only the first n pieces (the heads of a parts sheet) share a canvas; the rest are cut tight.
    const n = opts.alignFirst || boxes.length;
    const group = boxes.slice(0, n);
    const centers = group.map(b => footCenter(p, w, b));
    const half = Math.max(1, ...group.map((b, i) => Math.max(centers[i] - b.x, b.x + b.w - centers[i])));
    const tall = Math.max(1, ...group.map(b => b.h));
    const top = Math.round(tall * 0.05), ow = Math.ceil(half * 2 + tall * 0.04), oh = tall + top;
    const aligned = group.map((b, i) => {
      const o = document.createElement("canvas"); o.width = ow; o.height = oh;
      o.getContext("2d").drawImage(c, b.x, b.y, b.w, b.h, Math.round(ow / 2 - (centers[i] - b.x)), oh - b.h, b.w, b.h);
      return o;
    });
    const rest = boxes.slice(n).map(b => { const o = document.createElement("canvas"); o.width = b.w; o.height = b.h; o.getContext("2d").drawImage(c, b.x, b.y, b.w, b.h, 0, 0, b.w, b.h); return o; });
    return aligned.concat(rest);
  }
  // Stores a ready-made image file as is. Call refresh() once after a batch.
  async function putBlob(key, blob) {
    const im = await toImage(blob); if (!im) return false;
    await idb("readwrite", st => st.put(blob, key));
    mem.set(key, im); return true;
  }
  function refresh() { changed(); }
  async function remove(key) { mem.delete(key); await idb("readwrite", st => st.delete(key)); changed(); }
  async function clear(prefix) {
    const ks = [...mem.keys()].filter(k => !prefix || k.startsWith(prefix));
    ks.forEach(k => mem.delete(k));
    await idb("readwrite", st => ks.forEach(k => st.delete(k)));
    changed();
  }

  const get = key => mem.get(key) || null;
  const hasChar = id => { for (const k of mem.keys()) if (k.startsWith("char/" + id + "/")) return true; return false; };

  // Picks the picture for a character the same way the Unity engine does for one-image-per-pose art.
  function charImage(id, outfit, names, talk, blink) {
    const folders = outfit && outfit !== "casual" && outfit !== "base" ? [outfit, "base"] : ["base"];
    for (const f of folders) {
      for (const n of names) {
        if (!n) continue;
        const base = `char/${id}/${f}/${n}`;
        if (!mem.has(base)) continue;
        if (talk && mem.has(base + "_talk")) return { img: mem.get(base + "_talk"), key: base };
        if (blink && mem.has(base + "_blink")) return { img: mem.get(base + "_blink"), key: base };
        return { img: mem.get(base), key: base };
      }
    }
    return null;
  }
  function background(name) {
    const full = Core.slug(name), noPrefix = full.replace(/^(int|ext|i_e|int_ext|ext_int)_/, "");
    const noTime = noPrefix.replace(/_(day|night|morning|evening|afternoon|dawn|dusk|sunset|midnight)$/, "");
    for (const n of [full, noPrefix, noTime]) if (mem.has("bg/" + n)) return mem.get("bg/" + n);
    // Same place at another time of day (one coffee shop picture covers day and night).
    for (const k of mem.keys()) if (k.startsWith("bg/" + noTime + "_")) return mem.get(k);
    return null;
  }
  const count = prefix => [...mem.keys()].filter(k => k.startsWith(prefix)).length;

  return { loadAll, importFile, putBlob, refresh, storeCanvas, cutSheet, remove, clear, get, hasChar, charImage, background, count, onChange: f => listeners.add(f), get version() { return version; } };
})();
