// ===== Built-in art: painted backgrounds, the animated character doll, item close-ups =====
const Art = (() => {
  const TAU = Math.PI * 2;
  const rnd = seed => () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  const hash = s => { let h = 2166136261; for (const c of s) h = Math.imul(h ^ c.charCodeAt(0), 16777619); return Math.abs(h) || 1; };
  function hex(c, fb) {
    if (!c) return fb;
    c = c.trim(); if (c[0] !== "#") c = "#" + c;
    return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(c) ? c : fb;
  }
  function rgb(h) { h = h.replace("#", ""); if (h.length === 3) h = h.split("").map(x => x + x).join(""); const n = parseInt(h, 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
  function shade(h, k) { const [r, g, b] = rgb(h); const f = v => Math.max(0, Math.min(255, Math.round(k < 0 ? v * (1 + k) : v + (255 - v) * k))); return `rgb(${f(r)},${f(g)},${f(b)})`; }
  function alpha(h, a) { const [r, g, b] = rgb(h); return `rgba(${r},${g},${b},${a})`; }
  function hsl(hue, s, l) { return `hsl(${hue},${s}%,${l}%)`; }
  const lin = (g, x0, y0, x1, y1, stops) => { const gr = g.createLinearGradient(x0, y0, x1, y1); stops.forEach(([o, c]) => gr.addColorStop(o, c)); return gr; };
  const rad = (g, x, y, r, stops) => { const gr = g.createRadialGradient(x, y, 0, x, y, r); stops.forEach(([o, c]) => gr.addColorStop(o, c)); return gr; };
  function rr(g, x, y, w, h, r) { g.beginPath(); g.moveTo(x + r, y); g.arcTo(x + w, y, x + w, y + h, r); g.arcTo(x + w, y + h, x, y + h, r); g.arcTo(x, y + h, x, y, r); g.arcTo(x, y, x + w, y, r); g.closePath(); }

  // ---------------- backgrounds ----------------
  const SKIES = {
    morning: ["#9fd3f5", "#ffe1c4", "#ffd0b0"],
    day: ["#6cc0f5", "#bfe6ff", "#e8f7ff"],
    evening: ["#5b4b9a", "#e9788c", "#ffb27a"],
    night: ["#0b1035", "#1d2462", "#3a3478"],
  };
  function timeOf(name) {
    const n = name.toLowerCase();
    if (/night|midnight/.test(n)) return "night";
    if (/evening|sunset|dusk/.test(n)) return "evening";
    if (/morning|dawn/.test(n)) return "morning";
    return "day";
  }
  function sky(g, w, h, tod, top = 0, bottom = h) {
    const c = SKIES[tod];
    g.fillStyle = lin(g, 0, top, 0, bottom, [[0, c[0]], [0.6, c[1]], [1, c[2]]]);
    g.fillRect(0, top, w, bottom - top);
    const R = rnd(7);
    if (tod === "night") {
      for (let i = 0; i < 90; i++) { g.fillStyle = `rgba(255,255,240,${0.3 + R() * 0.7})`; const s = R() * 1.8 + 0.4; g.fillRect(R() * w, top + R() * (bottom - top) * 0.7, s, s); }
      g.fillStyle = rad(g, w * 0.78, top + h * 0.1, h * 0.06, [[0, "#fffbe6"], [0.55, "#fff3c4"], [1, "rgba(255,243,196,0)"]]);
      g.beginPath(); g.arc(w * 0.78, top + h * 0.1, h * 0.06, 0, TAU); g.fill();
    } else {
      for (let i = 0; i < 4; i++) {
        const cx = R() * w, cy = top + (0.08 + R() * 0.25) * h, s = h * (0.03 + R() * 0.03);
        g.fillStyle = tod === "evening" ? "rgba(255,200,210,.55)" : "rgba(255,255,255,.8)";
        for (let k = 0; k < 5; k++) { g.beginPath(); g.ellipse(cx + (k - 2) * s * 0.9, cy + Math.sin(k * 2.1) * s * 0.25, s * (1 - Math.abs(k - 2) * 0.18), s * 0.6, 0, 0, TAU); g.fill(); }
      }
    }
  }
  function skyline(g, w, base, h, col, lit, seed) {
    const R = rnd(seed); let x = -10;
    while (x < w) {
      const bw = w * (0.08 + R() * 0.1), bh = h * (0.25 + R() * 0.75);
      g.fillStyle = col; g.fillRect(x, base - bh, bw, bh);
      if (R() > 0.6) g.fillRect(x + bw * 0.4, base - bh - h * 0.06, bw * 0.12, h * 0.06);
      if (lit) for (let yy = base - bh + 8; yy < base - 6; yy += 10) for (let xx = x + 5; xx < x + bw - 6; xx += 9) if (R() < lit) { g.fillStyle = R() > 0.5 ? "rgba(255,226,140,.9)" : "rgba(255,240,200,.75)"; g.fillRect(xx, yy, 4, 5); }
      x += bw + 2;
    }
  }
  function floorTiles(g, w, top, h, a, b) {
    g.fillStyle = a; g.fillRect(0, top, w, h);
    const rows = 7;
    for (let r = 0; r < rows; r++) {
      const y0 = top + h * Math.pow(r / rows, 1.6), y1 = top + h * Math.pow((r + 1) / rows, 1.6);
      const n = 6 + r * 2;
      for (let c = 0; c < n; c++) {
        if ((r + c) % 2) continue;
        const sx = (x, y) => w / 2 + (x - w / 2) * (1 + (y - top) / h * 0.9);
        g.fillStyle = b; g.beginPath();
        const xa = w * c / n, xb = w * (c + 1) / n;
        g.moveTo(sx(xa, y0), y0); g.lineTo(sx(xb, y0), y0); g.lineTo(sx(xb, y1), y1); g.lineTo(sx(xa, y1), y1); g.fill();
      }
    }
  }
  function woodFloor(g, w, top, h, col) {
    g.fillStyle = col; g.fillRect(0, top, w, h);
    g.strokeStyle = "rgba(60,30,10,.18)"; g.lineWidth = 1.5;
    for (let i = -12; i <= 12; i++) { g.beginPath(); g.moveTo(w / 2 + i * w * 0.06, top); g.lineTo(w / 2 + i * w * 0.16, top + h); g.stroke(); }
    for (let y = top + 6; y < top + h; y += h / 9) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.globalAlpha = 0.3; g.stroke(); g.globalAlpha = 1; }
  }
  function pendantLamp(g, x, y0, y, s, glow) {
    g.strokeStyle = "#3b2b25"; g.lineWidth = 2; g.beginPath(); g.moveTo(x, y0); g.lineTo(x, y); g.stroke();
    if (glow) { g.fillStyle = rad(g, x, y + s * 0.6, s * 4, [[0, "rgba(255,214,140,.55)"], [1, "rgba(255,214,140,0)"]]); g.fillRect(x - s * 4, y - s * 3.4, s * 8, s * 8); }
    g.fillStyle = "#2f3b3a"; g.beginPath(); g.moveTo(x - s, y + s * 0.7); g.quadraticCurveTo(x, y - s * 0.4, x + s, y + s * 0.7); g.closePath(); g.fill();
    g.fillStyle = glow ? "#ffe6a8" : "#f3e1b0"; g.beginPath(); g.ellipse(x, y + s * 0.72, s * 0.45, s * 0.2, 0, 0, TAU); g.fill();
  }

  const painters = {
    street(g, w, h, tod) {
      sky(g, w, h, tod, 0, h * 0.62);
      skyline(g, w, h * 0.6, h * 0.22, tod === "night" ? "#2a2f63" : "rgba(160,170,210,.6)", tod === "night" ? 0.3 : 0, 3);
      // shop fronts
      const R = rnd(11); const cols = ["#d98b6a", "#e7b977", "#c77a8b", "#8fb6a8", "#d7a35f"];
      let x = -w * 0.05;
      for (let i = 0; i < 4; i++) {
        const bw = w * (0.28 + R() * 0.06), top = h * (0.18 + R() * 0.12), col = cols[i % cols.length];
        g.fillStyle = col; g.fillRect(x, top, bw, h * 0.66 - top);
        g.fillStyle = shade(col, -0.18); g.fillRect(x, top, bw, h * 0.015);
        for (let yy = top + h * 0.04; yy < h * 0.42; yy += h * 0.075) for (let k = 0; k < 3; k++) {
          const wx = x + bw * (0.12 + k * 0.29), ww = bw * 0.18;
          g.fillStyle = "#f4efe6"; g.fillRect(wx - 2, yy - 2, ww + 4, h * 0.05 + 4);
          g.fillStyle = tod === "night" ? (R() > 0.5 ? "#ffd98a" : "#33386e") : lin(g, 0, yy, 0, yy + h * 0.05, [[0, "#cfe9ff"], [1, "#8db8db"]]);
          g.fillRect(wx, yy, ww, h * 0.05);
        }
        // awning + window
        const aw = h * 0.47;
        g.fillStyle = "#3a3340"; g.fillRect(x + bw * 0.08, aw, bw * 0.84, h * 0.17);
        g.fillStyle = tod === "night" ? "#ffcf86" : lin(g, 0, aw, 0, aw + h * 0.17, [[0, "#fff6dd"], [1, "#e8cfa5"]]);
        g.fillRect(x + bw * 0.12, aw + h * 0.012, bw * 0.76, h * 0.15);
        const st = i % 2 ? "#e66a7e" : "#3fa69a";
        for (let k = 0; k < 6; k++) { g.fillStyle = k % 2 ? "#fff8ef" : st; g.beginPath(); const ax = x + bw * 0.05 + k * bw * 0.15; g.moveTo(ax, aw - h * 0.05); g.lineTo(ax + bw * 0.15, aw - h * 0.05); g.lineTo(ax + bw * 0.15, aw); g.arc(ax + bw * 0.075, aw, bw * 0.075, 0, Math.PI); g.closePath(); g.fill(); }
        x += bw;
      }
      // sidewalk
      g.fillStyle = lin(g, 0, h * 0.64, 0, h, [[0, "#c9c2c4"], [1, "#9e959a"]]); g.fillRect(0, h * 0.64, w, h * 0.36);
      g.strokeStyle = "rgba(80,70,80,.25)"; g.lineWidth = 2;
      for (let i = 1; i < 6; i++) { const y = h * 0.64 + h * 0.36 * Math.pow(i / 6, 1.5); g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
      for (let i = -6; i <= 6; i++) { g.beginPath(); g.moveTo(w / 2 + i * w * 0.12, h * 0.64); g.lineTo(w / 2 + i * w * 0.3, h); g.stroke(); }
      // blossom trees
      for (const tx of [w * 0.08, w * 0.92]) {
        g.fillStyle = "#6b4a3c"; g.fillRect(tx - 6, h * 0.36, 12, h * 0.3);
        const R2 = rnd(hash("t" + tx));
        for (let k = 0; k < 26; k++) { g.fillStyle = ["#ffb7cf", "#ffc9d9", "#f59bb8", "#ffe0ea"][k % 4]; g.beginPath(); g.arc(tx + (R2() - 0.5) * w * 0.26, h * 0.3 + (R2() - 0.5) * h * 0.14, w * (0.04 + R2() * 0.04), 0, TAU); g.fill(); }
      }
      // lamp post
      g.fillStyle = "#2e3440"; g.fillRect(w * 0.5 - 4, h * 0.3, 8, h * 0.36);
      g.beginPath(); g.ellipse(w * 0.5, h * 0.3, 14, 10, 0, 0, TAU); g.fill();
      g.fillStyle = tod === "night" ? "#ffe39b" : "#f6f0d8"; g.beginPath(); g.ellipse(w * 0.5, h * 0.305, 8, 6, 0, 0, TAU); g.fill();
    },
    cafe(g, w, h, tod) {
      const night = tod === "night";
      // back wall
      g.fillStyle = lin(g, 0, 0, 0, h * 0.62, night ? [[0, "#5a3f3f"], [1, "#7d5a4f"]] : [[0, "#f5dcc0"], [1, "#efcfaa"]]);
      g.fillRect(0, 0, w, h * 0.62);
      // wallpaper stripes
      g.fillStyle = night ? "rgba(255,255,255,.03)" : "rgba(180,110,70,.08)";
      for (let x = 0; x < w; x += w / 14) g.fillRect(x, 0, w / 28, h * 0.62);
      // window (left)
      const wx = w * 0.05, wy = h * 0.12, ww = w * 0.36, wh = h * 0.3;
      g.fillStyle = "#6b4733"; rr(g, wx - 8, wy - 8, ww + 16, wh + 16, 10); g.fill();
      g.save(); rr(g, wx, wy, ww, wh, 6); g.clip();
      sky(g, w, h, tod === "night" ? "night" : "morning", wy, wy + wh);
      skyline(g, w, wy + wh, wh * 0.6, night ? "#232a5c" : "rgba(150,160,200,.7)", night ? 0.35 : 0, 5);
      g.restore();
      g.fillStyle = "#6b4733"; g.fillRect(wx + ww / 2 - 4, wy, 8, wh); g.fillRect(wx, wy + wh / 2 - 4, ww, 8);
      g.font = `${h * 0.014}px 'Lilita One', sans-serif`; g.fillStyle = night ? "rgba(255,230,190,.75)" : "rgba(120,60,40,.55)"; g.textAlign = "center";
      g.fillText("MOONBEAN", wx + ww / 4, wy + wh * 0.28); g.fillText("CAF\u00c9", wx + ww * 0.75, wy + wh * 0.28);
      // chalkboard menu
      const bx = w * 0.5, by = h * 0.1, bw = w * 0.42, bh = h * 0.2;
      g.fillStyle = "#7a5133"; rr(g, bx - 7, by - 7, bw + 14, bh + 14, 8); g.fill();
      g.fillStyle = "#2c3a35"; rr(g, bx, by, bw, bh, 5); g.fill();
      g.fillStyle = "#f6efe2"; g.font = `${h * 0.026}px 'Lilita One', sans-serif`; g.fillText("MENU", bx + bw / 2, by + bh * 0.2);
      g.font = `${h * 0.0145}px 'Varela Round', sans-serif`; g.textAlign = "left";
      [["Oat latte", "4.50"], ["Cortado", "3.80"], ["Cappuccino", "4.20"], ["Moon mocha", "5.00"]].forEach(([a, b], i) => {
        const yy = by + bh * (0.38 + i * 0.16); g.fillStyle = "rgba(246,239,226,.9)"; g.fillText(a, bx + bw * 0.08, yy); g.textAlign = "right"; g.fillText(b, bx + bw * 0.92, yy); g.textAlign = "left";
      });
      g.textAlign = "center";
      // back door (staff only, with keypad)
      const dx = w * 0.78, dy = h * 0.33, dw = w * 0.17, dh = h * 0.29;
      g.fillStyle = night ? "#3a2622" : "#6e4632"; g.fillRect(dx - 5, dy - 5, dw + 10, dh + 5);
      g.fillStyle = night ? "#4c302a" : "#8a5a40"; g.fillRect(dx, dy, dw, dh);
      g.strokeStyle = "rgba(0,0,0,.2)"; g.lineWidth = 2; g.strokeRect(dx + dw * 0.15, dy + dh * 0.08, dw * 0.7, dh * 0.38); g.strokeRect(dx + dw * 0.15, dy + dh * 0.54, dw * 0.7, dh * 0.38);
      g.fillStyle = "#d9b45a"; g.beginPath(); g.arc(dx + dw * 0.82, dy + dh * 0.55, 4, 0, TAU); g.fill();
      g.fillStyle = "#c8c8cc"; g.fillRect(dx + dw * 0.72, dy + dh * 0.4, dw * 0.16, dh * 0.1);
      g.fillStyle = night ? "#ff5a6a" : "#3a3a44"; g.fillRect(dx + dw * 0.75, dy + dh * 0.42, dw * 0.1, dh * 0.025);
      g.fillStyle = "#f2e6c8"; g.fillRect(dx + dw * 0.18, dy - h * 0.03, dw * 0.64, h * 0.022);
      g.fillStyle = "#7a3b2e"; g.font = `${h * 0.012}px 'Varela Round', sans-serif`; g.fillText("STAFF ONLY", dx + dw / 2, dy - h * 0.014);
      // shelves
      for (const sy of [h * 0.36, h * 0.46]) {
        g.fillStyle = "#7a5133"; g.fillRect(w * 0.44, sy, w * 0.3, h * 0.012);
        const R = rnd(hash("s" + sy));
        for (let k = 0; k < 6; k++) { const cx = w * 0.46 + k * w * 0.048; const c = ["#f0e4d0", "#d77a61", "#5f8f8a", "#e9c46a"][k % 4]; g.fillStyle = c; rr(g, cx, sy - h * (0.03 + R() * 0.02), w * 0.03, h * (0.03 + R() * 0.02), 3); g.fill(); }
      }
      // floor
      floorTiles(g, w, h * 0.62, h * 0.38, night ? "#4b3a3a" : "#e9dfd3", night ? "#3a2c2d" : "#c9b8a6");
      // counter (behind characters)
      const cy = h * 0.53;
      g.fillStyle = night ? "#3b2a26" : "#7b5137"; g.fillRect(-5, cy, w * 0.72, h * 0.14);
      g.fillStyle = night ? "#2b1e1b" : "#5f3d29"; for (let x = 0; x < w * 0.72; x += w * 0.06) g.fillRect(x, cy + h * 0.02, 3, h * 0.12);
      g.fillStyle = night ? "#6b5a52" : "#d8c7b3"; g.fillRect(-5, cy - h * 0.012, w * 0.74, h * 0.016);
      // espresso machine + register
      g.fillStyle = "#b9bec4"; rr(g, w * 0.08, cy - h * 0.085, w * 0.16, h * 0.075, 6); g.fill();
      g.fillStyle = "#8b9198"; g.fillRect(w * 0.1, cy - h * 0.03, w * 0.12, h * 0.012);
      g.fillStyle = "#c0392b"; g.beginPath(); g.arc(w * 0.2, cy - h * 0.06, w * 0.012, 0, TAU); g.fill();
      g.fillStyle = "#4a4f57"; rr(g, w * 0.5, cy - h * 0.05, w * 0.12, h * 0.04, 4); g.fill();
      g.fillStyle = "#9fd8b4"; g.fillRect(w * 0.52, cy - h * 0.045, w * 0.08, h * 0.012);
      // pastry dome
      g.fillStyle = "rgba(220,240,255,.55)"; g.beginPath(); g.ellipse(w * 0.36, cy - h * 0.012, w * 0.07, h * 0.04, 0, Math.PI, 0); g.fill();
      g.fillStyle = "#e0a458"; g.beginPath(); g.ellipse(w * 0.35, cy - h * 0.018, w * 0.03, h * 0.012, 0, 0, TAU); g.fill();
      // lamps
      for (const lx of [0.2, 0.5, 0.8]) pendantLamp(g, w * lx, 0, h * 0.06, w * 0.05, night);
      if (night) { g.fillStyle = "rgba(20,20,60,.25)"; g.fillRect(0, 0, w, h); }
    },
    bedroom(g, w, h, tod) {
      g.fillStyle = lin(g, 0, 0, 0, h * 0.64, [[0, "#cdb8e6"], [1, "#e7d8f2"]]); g.fillRect(0, 0, w, h * 0.64);
      // window with sunset
      const wx = w * 0.55, wy = h * 0.1, ww = w * 0.36, wh = h * 0.26;
      g.fillStyle = "#fff"; rr(g, wx - 8, wy - 8, ww + 16, wh + 16, 10); g.fill();
      g.save(); rr(g, wx, wy, ww, wh, 6); g.clip(); sky(g, w, h, tod, wy, wy + wh); skyline(g, w, wy + wh, wh * 0.5, tod === "night" ? "#232a5c" : "rgba(110,80,140,.7)", tod === "day" ? 0 : 0.3, 9); g.restore();
      g.fillStyle = "#fff"; g.fillRect(wx + ww / 2 - 3, wy, 6, wh);
      g.fillStyle = "#f7a8c4"; g.fillRect(wx - w * 0.06, wy - h * 0.02, w * 0.07, wh + h * 0.08); g.fillRect(wx + ww - w * 0.01, wy - h * 0.02, w * 0.07, wh + h * 0.08);
      // fairy lights
      g.strokeStyle = "#5b5070"; g.lineWidth = 1.5; g.beginPath();
      for (let x = 0; x <= w; x += 4) g.lineTo(x, h * 0.05 + Math.sin(x / w * Math.PI * 3) * h * 0.02 + h * 0.02); g.stroke();
      for (let i = 0; i < 14; i++) { const x = (i + 0.5) * w / 14, y = h * 0.07 + Math.sin(x / w * Math.PI * 3) * h * 0.02; g.fillStyle = rad(g, x, y, 12, [[0, "rgba(255,230,150,.9)"], [1, "rgba(255,230,150,0)"]]); g.fillRect(x - 12, y - 12, 24, 24); g.fillStyle = "#ffe9a8"; g.beginPath(); g.arc(x, y, 3, 0, TAU); g.fill(); }
      // mirror
      g.fillStyle = "#e3c27a"; g.beginPath(); g.ellipse(w * 0.2, h * 0.27, w * 0.11, h * 0.12, 0, 0, TAU); g.fill();
      g.fillStyle = lin(g, w * 0.1, h * 0.17, w * 0.3, h * 0.37, [[0, "#e9f4ff"], [0.5, "#b9d3ea"], [1, "#eaf3fb"]]); g.beginPath(); g.ellipse(w * 0.2, h * 0.27, w * 0.09, h * 0.105, 0, 0, TAU); g.fill();
      // wardrobe & bed
      woodFloor(g, w, h * 0.64, h * 0.36, "#c99a6b");
      g.fillStyle = "#f5eadf"; rr(g, w * 0.02, h * 0.44, w * 0.42, h * 0.16, 12); g.fill();
      g.fillStyle = "#8d79b8"; rr(g, w * 0.02, h * 0.5, w * 0.42, h * 0.14, 12); g.fill();
      g.fillStyle = "#fff"; rr(g, w * 0.05, h * 0.46, w * 0.13, h * 0.05, 14); g.fill();
      g.fillStyle = "#f7a8c4"; rr(g, w * 0.2, h * 0.465, w * 0.11, h * 0.045, 14); g.fill();
      g.fillStyle = "#b07a5a"; g.fillRect(w * 0.7, h * 0.4, w * 0.26, h * 0.24); g.fillStyle = "#c48e6c"; g.fillRect(w * 0.71, h * 0.41, w * 0.115, h * 0.22); g.fillRect(w * 0.835, h * 0.41, w * 0.115, h * 0.22);
      g.fillStyle = "#e7c27a"; g.fillRect(w * 0.815, h * 0.5, 3, h * 0.03); g.fillRect(w * 0.84, h * 0.5, 3, h * 0.03);
      g.fillStyle = "rgba(255,170,200,.35)"; g.beginPath(); g.ellipse(w * 0.5, h * 0.82, w * 0.36, h * 0.07, 0, 0, TAU); g.fill();
      if (tod === "evening") { g.fillStyle = "rgba(255,140,90,.12)"; g.fillRect(0, 0, w, h); }
    },
    rooftop(g, w, h, tod) {
      sky(g, w, h, tod, 0, h * 0.62);
      skyline(g, w, h * 0.62, h * 0.32, "#1a1f4a", 0.45, 21);
      skyline(g, w, h * 0.64, h * 0.18, "#11163a", 0.5, 33);
      // deck
      g.fillStyle = lin(g, 0, h * 0.62, 0, h, [[0, "#5a4250"], [1, "#3a2a35"]]); g.fillRect(0, h * 0.62, w, h * 0.38);
      g.strokeStyle = "rgba(0,0,0,.25)"; for (let i = 1; i < 10; i++) { const y = h * 0.62 + h * 0.38 * Math.pow(i / 10, 1.4); g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
      // railing
      g.fillStyle = "#2a2234"; g.fillRect(0, h * 0.585, w, h * 0.012);
      for (let x = 0; x < w; x += w / 16) g.fillRect(x, h * 0.585, 3, h * 0.04);
      // string lights
      for (let r = 0; r < 2; r++) {
        const y0 = h * (0.08 + r * 0.1);
        g.strokeStyle = "#1b1426"; g.lineWidth = 1.5; g.beginPath();
        for (let x = 0; x <= w; x += 4) g.lineTo(x, y0 + Math.abs(Math.sin(x / w * Math.PI * 2)) * h * 0.06); g.stroke();
        for (let i = 0; i < 12; i++) {
          const x = (i + 0.5 + r * 0.5) * w / 12, y = y0 + Math.abs(Math.sin(x / w * Math.PI * 2)) * h * 0.06 + 5;
          const c = ["#ffd36e", "#ff8fb1", "#8fe3ff", "#c8ff9e"][(i + r) % 4];
          g.fillStyle = rad(g, x, y, 18, [[0, alpha(c, 0.75)], [1, alpha(c, 0)]]); g.fillRect(x - 18, y - 18, 36, 36);
          g.fillStyle = c; g.beginPath(); g.ellipse(x, y, 3.5, 5, 0, 0, TAU); g.fill();
        }
      }
      // plants & lanterns
      for (const px of [w * 0.06, w * 0.94]) { g.fillStyle = "#8a5a44"; g.fillRect(px - 16, h * 0.56, 32, h * 0.06); g.fillStyle = "#2e6b4f"; for (let k = 0; k < 7; k++) { g.beginPath(); g.ellipse(px + (k - 3) * 6, h * 0.53 - Math.abs(k - 3) * -3, 6, h * 0.04, (k - 3) * 0.3, 0, TAU); g.fill(); } }
      g.fillStyle = "rgba(255,200,120,.12)"; g.fillRect(0, h * 0.62, w, h * 0.38);
    },
  };

  function paintPlaceholder(g, w, h, name) {
    const hue = hash(name) % 360;
    g.fillStyle = lin(g, 0, 0, 0, h, [[0, hsl(hue, 45, 78)], [0.62, hsl(hue, 40, 88)], [0.62, hsl(hue, 25, 70)], [1, hsl(hue, 25, 58)]]);
    g.fillRect(0, 0, w, h);
    g.fillStyle = "rgba(255,255,255,.5)"; g.font = `${h * 0.03}px 'Lilita One', sans-serif`; g.textAlign = "center";
    g.fillText(name.toUpperCase(), w / 2, h * 0.3);
  }

  function pickPainter(name) {
    const n = name.toLowerCase();
    if (/coffee|cafe|café|bakery|diner|restaurant/.test(n)) return "cafe";
    if (/bedroom|room|apartment|home|dorm/.test(n)) return "bedroom";
    if (/roof|party|terrace|club|balcony/.test(n)) return "rooftop";
    if (/street|city|park|road|town|outside|sidewalk/.test(n)) return "street";
    return null;
  }

  const bgCache = new Map();
  function background(name, w, h) {
    const key = name + "|" + w + "x" + h;
    if (bgCache.has(key)) return bgCache.get(key);
    const c = document.createElement("canvas"); c.width = w; c.height = h;
    const g = c.getContext("2d");
    const p = pickPainter(name);
    if (p) painters[p](g, w, h, timeOf(name)); else paintPlaceholder(g, w, h, name.replace(/^(INT|EXT)\.?\s*/i, "").replace(/\s*-\s*.*$/, ""));
    bgCache.set(key, c);
    return c;
  }
  function clearCache() { bgCache.clear(); }

  // ---------------- character doll ----------------
  // Drawn in a 1000-unit tall space, feet at (0,0), head up toward -1000.
  const SKINS = ["#f6d3b8", "#e8b892", "#c98e64", "#9a6643", "#f9dcc6", "#d9a37e"];
  const EYES = ["#5a3a26", "#2f6f8f", "#3c7a4a", "#6b4a8a", "#3a2a20"];
  const HAIRS = ["loose", "ponytail", "bun", "short"];
  const OUTFITS = { casual: 1, dress: 1, suit: 1, sweater: 1, apron: 1, jacket: 1 };

  function makeLook(id, opts) {
    opts = opts || {};
    const h = hash(id);
    const hairStyle = (opts.hair || HAIRS[h % 3]).toLowerCase();
    return {
      skin: hex(opts.skin, SKINS[h % SKINS.length]),
      hairColor: hex(opts.haircolor, hsl(h % 40 + 10, 40, 22 + h % 20)),
      eye: hex(opts.eyes, EYES[(h >> 3) % EYES.length]),
      color: hex(opts.color, hsl(h % 360, 60, 62)),
      slots: { hair: hairStyle, outfit: (opts.outfit || "casual").toLowerCase() },
      build: opts.build || (hairStyle === "short" ? "broad" : "slim"),
    };
  }

  const FACES = {
    neutral: { brow: 0, eye: "open", mouth: "soft" },
    happy: { brow: -3, eye: "smile", mouth: "smile" },
    laugh: { brow: -5, eye: "happy", mouth: "laugh" },
    sad: { brow: 0, inner: 8, eye: "sad", mouth: "frown" },
    cry: { brow: 0, inner: 10, eye: "closed", mouth: "frown", tears: 1 },
    angry: { brow: 4, inner: -10, eye: "narrow", mouth: "flat" },
    surprised: { brow: -10, eye: "wide", mouth: "o" },
    scared: { brow: -8, inner: 8, eye: "wide", mouth: "wavy", sweat: 1 },
    worried: { brow: -2, inner: 7, eye: "open", mouth: "wavy", sweat: 0.6 },
    embarrassed: { brow: -2, inner: 5, eye: "aside", mouth: "wavy", blush: 1 },
    love: { brow: -4, eye: "happy", mouth: "smile", blush: 0.8, hearts: 1 },
    smug: { brow: 0, raise: 1, eye: "half", mouth: "smirk" },
    think: { brow: 0, raise: 1, eye: "up", mouth: "hmm" },
    tired: { brow: 2, eye: "half", mouth: "flat" },
    pout: { brow: 2, inner: 3, eye: "open", mouth: "pout", blush: 0.4 },
    disgusted: { brow: 4, inner: -4, eye: "narrow", mouth: "wavy" },
    flirty: { brow: -2, raise: 1, eye: "half", mouth: "smirk", blush: 0.5 },
    giggle: { brow: -3, eye: "happy", mouth: "smile", blush: 0.3 },
  };

  // arm targets: [upper, lower] in degrees; 0 = hanging down, positive = away from the body
  const REST = [7, -4];
  function gesturePose(gesture, k, t, near) {
    const e = Math.min(1, k * 5, (1 - k) * 5);
    const S = Math.sin;
    switch (gesture) {
      case "wave": return near ? [[150, 18 + 28 * S(t * 13)], e] : null;
      case "cheer": case "excited": return [[152 + 6 * S(t * 10), 14], e];
      case "point": return near ? [[78, -4], e] : null;
      case "think": return near ? [[-14, -152], 1] : null;
      case "shrug": return [[16, 74], e];
      case "dance": return [[95 + 55 * S(t * 8 + (near ? 0 : Math.PI)), -45], e];
      case "cry": return [[-6, -160], e];
      case "love": case "swoon": case "blush": case "shy": return [[3, -138], e];
      case "gasp": case "surprised": case "flinch": case "shocked": return near ? [[-4, -142], e] : null;
      case "angry": case "stomp": return [[28, -62], 1];
      case "talk": return near ? [[22 + 6 * S(t * 2.7), 104 + 16 * S(t * 4.1)], 1] : null;
      case "laugh": case "giggle": return near ? [[-4, -96], e] : null;
      case "bow": return [[-6, -12], e];
      case "pout": return [[6, -110], 1];
    }
    return null;
  }
  function emotionPose(emotion, near) {
    switch (emotion) {
      case "angry": return [28, -62];
      case "smug": return near ? [28, -62] : null;
      case "think": return near ? [-14, -152] : null;
      case "worried": case "scared": case "embarrassed": return [-2, -112];
      case "love": return [3, -138];
      case "sad": case "cry": return [3, -4];
    }
    return null;
  }

  function stroke(g, w, col) { g.lineWidth = w; g.strokeStyle = col; g.stroke(); }
  const OUT = "rgba(70,35,45,.45)";

  function drawHairBack(g, L, sway) {
    const c = L.hairColor;
    g.fillStyle = c;
    const st = L.slots.hair;
    if (st === "loose") {
      g.beginPath(); g.moveTo(-74, -905);
      g.bezierCurveTo(-95, -820, -100 + sway * 0.4, -760, -92 + sway, -690);
      g.quadraticCurveTo(-40 + sway, -705, 0 + sway, -695); g.quadraticCurveTo(40 + sway, -705, 92 + sway, -690);
      g.bezierCurveTo(100 + sway * 0.4, -760, 95, -820, 74, -905); g.closePath(); g.fill(); stroke(g, 4, OUT);
    } else if (st === "ponytail") {
      g.save(); g.translate(58, -918); g.rotate((12 + sway * 0.5) * Math.PI / 180);
      g.beginPath(); g.moveTo(-10, 0); g.bezierCurveTo(50, 10, 62, 90, 30, 190); g.quadraticCurveTo(22, 120, -8, 40); g.closePath(); g.fill(); stroke(g, 4, OUT);
      g.fillStyle = L.color; g.beginPath(); g.ellipse(0, 4, 10, 13, 0.4, 0, TAU); g.fill();
      g.restore();
    } else if (st === "bun") {
      g.beginPath(); g.arc(0, -962, 40, 0, TAU); g.fill(); stroke(g, 4, OUT);
      g.strokeStyle = shade(c, 0.15); g.lineWidth = 4; g.beginPath(); g.arc(0, -962, 24, 3.6, 5.6); g.stroke();
    } else if (st === "short") {
      g.beginPath(); g.ellipse(0, -880, 76, 74, 0, Math.PI, 0); g.fill();
    } else if (st === "long" || st === "wavy") {
      g.beginPath(); g.moveTo(-74, -905); g.bezierCurveTo(-110, -780, -110 + sway, -640, -84 + sway, -600); g.lineTo(84 + sway, -600); g.bezierCurveTo(110 + sway, -640, 110, -780, 74, -905); g.closePath(); g.fill(); stroke(g, 4, OUT);
    }
  }
  function drawHairFront(g, L) {
    const c = L.hairColor, st = L.slots.hair;
    g.fillStyle = c;
    g.beginPath();
    if (st === "short") {
      g.moveTo(-74, -860); g.bezierCurveTo(-82, -935, -40, -972, 6, -968); g.bezierCurveTo(60, -965, 84, -930, 74, -858);
      g.lineTo(66, -884); g.quadraticCurveTo(40, -915, 18, -902); g.quadraticCurveTo(-2, -925, -24, -905); g.quadraticCurveTo(-46, -918, -62, -890); g.lineTo(-68, -850);
    } else {
      g.moveTo(-72, -840); g.bezierCurveTo(-82, -930, -40, -970, 2, -966); g.bezierCurveTo(52, -966, 86, -930, 72, -840);
      g.quadraticCurveTo(64, -882, 46, -900); g.quadraticCurveTo(20, -880, -6, -906); g.quadraticCurveTo(-30, -890, -56, -895); g.quadraticCurveTo(-66, -872, -72, -840);
    }
    g.closePath(); g.fill(); stroke(g, 4, OUT);
    g.strokeStyle = shade(c, 0.28); g.lineWidth = 5; g.lineCap = "round";
    g.beginPath(); g.arc(-8, -905, 48, 3.75, 4.6); g.stroke();
  }

  function drawFace(g, L, st) {
    const F = FACES[st.emotion] || FACES.neutral;
    const fx = st.look * 9;
    const talking = st.mouthOpen;
    // blush
    const blush = Math.max(F.blush || 0, st.blush || 0);
    if (blush > 0) { g.fillStyle = `rgba(255,110,130,${0.35 * blush})`; for (const s of [-1, 1]) { g.beginPath(); g.ellipse(s * 38 + fx, -842, 15, 8, 0, 0, TAU); g.fill(); } }
    // eyes
    const closed = st.blink || F.eye === "closed";
    for (const s of [-1, 1]) {
      const ex = s * 27 + fx, ey = -872;
      g.save(); g.translate(ex, ey);
      if (closed || F.eye === "happy") {
        g.strokeStyle = "#3a2228"; g.lineWidth = 5; g.lineCap = "round"; g.beginPath();
        if (F.eye === "happy" && !st.blink) g.arc(0, 6, 13, Math.PI * 1.15, Math.PI * 1.85); else g.arc(0, -6, 13, Math.PI * 0.2, Math.PI * 0.8);
        g.stroke();
      } else {
        let open = 1, look = st.look * 2.5, lookY = 0;
        if (F.eye === "wide") open = 1.25; if (F.eye === "half") open = 0.55; if (F.eye === "narrow") open = 0.65; if (F.eye === "sad") open = 0.8; if (F.eye === "smile") open = 0.85;
        if (F.eye === "aside") look = -s * 0 + 5 * (st.look || 1); if (F.eye === "up") { lookY = -4; look = 4; }
        g.fillStyle = "#fff";
        g.beginPath(); g.moveTo(-16, 0); g.quadraticCurveTo(0, -22 * open, 16, -1); g.quadraticCurveTo(0, 15 * open, -16, 0); g.closePath(); g.fill();
        g.save(); g.clip();
        g.fillStyle = L.eye; g.beginPath(); g.arc(look, lookY + 1, F.eye === "wide" ? 8 : 10, 0, TAU); g.fill();
        g.fillStyle = "#1d1416"; g.beginPath(); g.arc(look, lookY + 1, F.eye === "wide" ? 3.5 : 5, 0, TAU); g.fill();
        g.fillStyle = "#fff"; g.beginPath(); g.arc(look - 3, lookY - 3, 3, 0, TAU); g.fill();
        g.restore();
        if (F.eye === "smile") { g.fillStyle = L.skin; g.beginPath(); g.ellipse(0, 12, 18, 8, 0, 0, TAU); g.fill(); }
        // lash line
        g.strokeStyle = "#2a1a1e"; g.lineWidth = 4.5; g.lineCap = "round";
        g.beginPath(); g.moveTo(-17, 1); g.quadraticCurveTo(0, -22 * open, 17, -2); g.lineTo(21, -7 - 3 * open); g.stroke();
        if (F.eye === "sad") { g.strokeStyle = "rgba(60,30,40,.35)"; g.lineWidth = 2; g.beginPath(); g.moveTo(-14, -12); g.lineTo(12, -16); g.stroke(); }
      }
      g.restore();
      // brows
      const bh = -900 + (F.brow || 0) + (F.raise && s === 1 ? -9 : 0);
      const inner = F.inner || 0;
      g.strokeStyle = shade(L.hairColor, -0.2); g.lineWidth = 6; g.lineCap = "round";
      g.beginPath(); g.moveTo(s * 13 + fx, bh - inner); g.quadraticCurveTo(s * 27 + fx, bh - 6 - inner * 0.4, s * 40 + fx, bh + 2); g.stroke();
    }
    // nose
    g.strokeStyle = shade(L.skin, -0.28); g.lineWidth = 3.5; g.lineCap = "round";
    g.beginPath(); g.moveTo(fx * 1.3 + 2, -858); g.quadraticCurveTo(fx * 1.3 + 6, -848, fx * 1.3 - 2, -845); g.stroke();
    // mouth
    const mx = fx * 1.1, my = -822;
    const lip = "#7a2b3a";
    g.lineCap = "round"; g.lineJoin = "round";
    let m = F.mouth;
    const openFill = (w, hgt, curve) => {
      g.fillStyle = "#5a1f2b"; g.beginPath(); g.moveTo(mx - w, my); g.quadraticCurveTo(mx, my - curve, mx + w, my); g.quadraticCurveTo(mx, my + hgt * 2, mx - w, my); g.fill();
      g.save(); g.clip(); g.fillStyle = "#e86a7d"; g.beginPath(); g.ellipse(mx, my + hgt * 1.4, w * 0.6, hgt * 0.6, 0, 0, TAU); g.fill();
      g.fillStyle = "#fff"; g.fillRect(mx - w, my - curve - 2, w * 2, Math.max(3, hgt * 0.35)); g.restore();
    };
    if (m === "laugh") openFill(19, 12 + (talking ? 3 : 0), 4);
    else if (talking) {
      if (m === "frown" || m === "wavy") openFill(11, 7, -4);
      else if (m === "o") { g.fillStyle = "#5a1f2b"; g.beginPath(); g.ellipse(mx, my + 3, 8, 11, 0, 0, TAU); g.fill(); }
      else openFill(m === "smile" ? 15 : 12, m === "smile" ? 9 : 7, m === "smile" ? 3 : 1);
    } else {
      g.strokeStyle = lip; g.lineWidth = 4.5; g.beginPath();
      switch (m) {
        case "smile": g.moveTo(mx - 15, my - 3); g.quadraticCurveTo(mx, my + 11, mx + 15, my - 3); break;
        case "frown": g.moveTo(mx - 12, my + 5); g.quadraticCurveTo(mx, my - 6, mx + 12, my + 5); break;
        case "flat": g.moveTo(mx - 12, my + 1); g.lineTo(mx + 12, my + 1); break;
        case "o": g.stroke(); g.fillStyle = "#5a1f2b"; g.beginPath(); g.ellipse(mx, my + 3, 7, 9, 0, 0, TAU); g.fill(); g.beginPath(); break;
        case "wavy": g.moveTo(mx - 12, my + 2); g.quadraticCurveTo(mx - 6, my - 4, mx, my + 2); g.quadraticCurveTo(mx + 6, my + 7, mx + 12, my + 1); break;
        case "smirk": g.moveTo(mx - 10, my + 3); g.quadraticCurveTo(mx + 4, my + 6, mx + 14, my - 5); break;
        case "hmm": g.moveTo(mx - 4, my + 2); g.lineTo(mx + 10, my); break;
        case "pout": g.stroke(); g.fillStyle = "#c95a6c"; g.beginPath(); g.ellipse(mx, my + 2, 6, 5, 0, 0, TAU); g.fill(); g.beginPath(); break;
        default: g.moveTo(mx - 10, my); g.quadraticCurveTo(mx, my + 5, mx + 10, my);
      }
      g.stroke();
    }
    // tears, sweat
    if (F.tears) { g.fillStyle = "rgba(120,190,255,.85)"; for (const s of [-1, 1]) { const ph = (st.time * 0.9 + (s > 0 ? 0.5 : 0)) % 1; g.beginPath(); g.ellipse(s * 30 + fx, -858 + ph * 40, 4, 7, 0, 0, TAU); g.fill(); } }
    if (F.sweat) { g.fillStyle = "rgba(140,200,255,.9)"; g.beginPath(); g.moveTo(58, -930); g.quadraticCurveTo(68, -905, 58, -900); g.quadraticCurveTo(48, -905, 58, -930); g.fill(); }
  }

  function limb(g, x0, y0, x1, y1, w, col) {
    g.lineCap = "round";
    g.strokeStyle = OUT; g.lineWidth = w + 5; g.beginPath(); g.moveTo(x0, y0); g.lineTo(x1, y1); g.stroke();
    g.strokeStyle = col; g.lineWidth = w; g.beginPath(); g.moveTo(x0, y0); g.lineTo(x1, y1); g.stroke();
  }

  function outfitColors(L) {
    const o = L.slots.outfit, c = L.color;
    switch (o) {
      case "dress": return { top: c, sleeve: null, legs: L.skin, skirt: c, shoe: shade(c, -0.45) };
      case "suit": return { top: "#2b3550", sleeve: "#2b3550", sleeveLen: 1, legs: "#262f47", shoe: "#1b1b22" };
      case "sweater": return { top: "#efe3cf", sleeve: "#efe3cf", sleeveLen: 1, legs: "#4a3b35", shoe: "#5a3d2e" };
      case "jacket": return { top: c, sleeve: "#2a2a33", sleeveLen: 1, legs: "#2f3d5c", shoe: "#222" };
      case "apron": return { top: c, sleeve: c, sleeveLen: 0.5, legs: "#3d5a8a", shoe: "#f4f4f4" };
      default: return { top: c, sleeve: c, sleeveLen: 0.5, legs: "#3d5a8a", shoe: "#f4f4f4" };
    }
  }

  function drawBody(g, L, st) {
    const broad = L.build === "broad";
    const sw = broad ? 108 : 94, ww = broad ? 80 : 62, hw = broad ? 80 : 84;
    const O = outfitColors(L);
    // legs
    for (const s of [-1, 1]) {
      g.fillStyle = O.legs; g.beginPath();
      g.moveTo(s * (hw - 4), -480); g.lineTo(s * 6, -480); g.lineTo(s * 14, -260); g.lineTo(s * 18, -30); g.lineTo(s * 50, -30); g.lineTo(s * 60, -260); g.closePath(); g.fill(); stroke(g, 4, OUT);
      g.fillStyle = O.shoe; g.beginPath(); g.ellipse(s * 36, -18, 34, 20, 0, 0, TAU); g.fill(); stroke(g, 3, OUT);
    }
    if (O.skirt == null) { g.fillStyle = O.legs; g.beginPath(); g.moveTo(-hw, -500); g.lineTo(hw, -500); g.lineTo(hw - 6, -440); g.lineTo(-hw + 6, -440); g.fill(); }
    // neck
    g.fillStyle = shade(L.skin, -0.1); g.fillRect(-19, -820, 38, 70);
    // torso
    g.fillStyle = O.top;
    g.beginPath(); g.moveTo(-sw + 22, -770); g.quadraticCurveTo(-sw, -766, -sw + 2, -730);
    g.lineTo(-ww, -560); g.lineTo(-hw, -478); g.lineTo(hw, -478); g.lineTo(ww, -560); g.lineTo(sw - 2, -730); g.quadraticCurveTo(sw, -766, sw - 22, -770); g.closePath();
    g.fill(); stroke(g, 4, OUT);
    // side shading
    g.save(); g.clip(); g.fillStyle = "rgba(60,20,40,.12)"; g.fillRect(sw * 0.45, -780, sw, 320); g.restore();
    // neckline
    g.fillStyle = L.skin; g.beginPath(); g.moveTo(-26, -771); g.quadraticCurveTo(0, -735, 26, -771); g.closePath(); g.fill();
    const o = L.slots.outfit;
    if (o === "dress") {
      g.fillStyle = O.skirt; g.beginPath(); g.moveTo(-ww, -565); g.lineTo(ww, -565); g.quadraticCurveTo(110, -400, 132, -270); g.quadraticCurveTo(0, -245, -132, -270); g.quadraticCurveTo(-110, -400, -ww, -565); g.fill(); stroke(g, 4, OUT);
      g.strokeStyle = "rgba(60,20,40,.18)"; g.lineWidth = 3; for (const x of [-60, -20, 20, 60]) { g.beginPath(); g.moveTo(x * 0.4, -560); g.lineTo(x * 1.4, -262); g.stroke(); }
      g.fillStyle = shade(L.color, -0.25); g.fillRect(-ww - 2, -575, ww * 2 + 4, 16);
    } else if (o === "suit") {
      g.fillStyle = "#f7f7fa"; g.beginPath(); g.moveTo(-30, -770); g.lineTo(0, -610); g.lineTo(30, -770); g.fill();
      g.fillStyle = L.color; g.beginPath(); g.moveTo(-8, -755); g.lineTo(8, -755); g.lineTo(11, -640); g.lineTo(0, -620); g.lineTo(-11, -640); g.fill();
      g.strokeStyle = "#1c2338"; g.lineWidth = 5; g.beginPath(); g.moveTo(-32, -772); g.lineTo(-6, -600); g.lineTo(-50, -700); g.moveTo(32, -772); g.lineTo(6, -600); g.lineTo(50, -700); g.stroke();
      g.fillStyle = "#d9d9de"; for (const y of [-560, -520]) { g.beginPath(); g.arc(-4, y, 5, 0, TAU); g.fill(); }
    } else if (o === "sweater") {
      g.fillStyle = L.color; g.fillRect(-sw + 6, -680, (sw - 6) * 2, 26); g.fillRect(-ww - 4, -600, (ww + 4) * 2, 14);
      g.fillStyle = "rgba(120,90,60,.25)"; for (let x = -ww; x < ww; x += 12) g.fillRect(x, -500, 5, 22);
      g.fillStyle = "#efe3cf"; g.beginPath(); g.moveTo(-34, -775); g.quadraticCurveTo(0, -745, 34, -775); g.lineTo(26, -790); g.quadraticCurveTo(0, -765, -26, -790); g.fill();
    } else if (o === "jacket") {
      g.fillStyle = "#2a2a33";
      for (const s of [-1, 1]) { g.beginPath(); g.moveTo(s * 26, -772); g.lineTo(s * (sw - 22), -770); g.quadraticCurveTo(s * sw, -766, s * (sw - 2), -730); g.lineTo(s * ww, -560); g.lineTo(s * hw, -478); g.lineTo(s * 30, -478); g.lineTo(s * 36, -650); g.closePath(); g.fill(); }
      g.strokeStyle = "#45454f"; g.lineWidth = 4; g.beginPath(); g.moveTo(-26, -772); g.lineTo(-44, -690); g.moveTo(26, -772); g.lineTo(44, -690); g.stroke();
    } else if (o === "apron") {
      g.fillStyle = "#f3e7d3"; g.beginPath(); g.moveTo(-46, -700); g.lineTo(46, -700); g.lineTo(58, -560); g.lineTo(76, -300); g.quadraticCurveTo(0, -285, -76, -300); g.lineTo(-58, -560); g.closePath(); g.fill(); stroke(g, 3, OUT);
      g.strokeStyle = "#f3e7d3"; g.lineWidth = 7; g.beginPath(); g.moveTo(-40, -698); g.lineTo(-22, -772); g.moveTo(40, -698); g.lineTo(22, -772); g.stroke();
      g.strokeStyle = "rgba(140,90,60,.5)"; g.lineWidth = 3; rr(g, -30, -470, 60, 50, 6); g.stroke();
      g.fillStyle = "#8a5a3c"; g.font = "bold 22px 'Lilita One', sans-serif"; g.textAlign = "center"; g.fillText("☾", 0, -620);
    }
    return { sw, O };
  }

  function drawArm(g, L, O, s, sw, a) {
    const sx = s * (sw - 14), sy = -742;
    const r1 = a[0] * Math.PI / 180, r2 = (a[0] + a[1]) * Math.PI / 180;
    const ex = sx + s * Math.sin(r1) * 155, ey = sy + Math.cos(r1) * 155;
    const hx = ex + s * Math.sin(r2) * 145, hy = ey + Math.cos(r2) * 145;
    limb(g, sx, sy, ex, ey, 34, L.skin);
    limb(g, ex, ey, hx, hy, 30, L.skin);
    if (O.sleeve) {
      if (O.sleeveLen >= 1) { limb(g, sx, sy, ex, ey, 42, O.sleeve); limb(g, ex, ey, ex + (hx - ex) * 0.86, ey + (hy - ey) * 0.86, 38, O.sleeve); }
      else limb(g, sx, sy, sx + (ex - sx) * 0.45, sy + (ey - sy) * 0.45, 40, O.sleeve);
    }
    g.fillStyle = L.skin; g.beginPath(); g.arc(hx, hy, 20, 0, TAU); g.fill(); stroke(g, 3.5, OUT);
  }

  // st: { emotion, gesture, gk, time, blink, mouthOpen, look, arms:[[u,l],[u,l]], headTilt, sway }
  function drawCharacter(g, L, st) {
    drawHairBack(g, L, st.sway || 0);
    const { sw, O } = drawBody(g, L, st);
    // head
    g.save();
    g.translate(0, -790); g.rotate((st.headTilt || 0) * Math.PI / 180); g.translate(0, 790);
    if (L.slots.hair === "short") drawHairBack(g, L, 0);
    g.fillStyle = L.skin;
    for (const s of [-1, 1]) { g.beginPath(); g.ellipse(s * 64, -862, 11, 17, 0, 0, TAU); g.fill(); stroke(g, 3, OUT); }
    g.beginPath(); g.moveTo(-66, -885);
    g.bezierCurveTo(-68, -950, 68, -950, 66, -885);
    g.bezierCurveTo(66, -828, 34, -790, 0, -786);
    g.bezierCurveTo(-34, -790, -66, -828, -66, -885); g.closePath();
    g.fill(); stroke(g, 4, OUT);
    drawFace(g, L, st);
    drawHairFront(g, L);
    g.restore();
    drawArm(g, L, O, -1, sw, st.arms[0]);
    drawArm(g, L, O, 1, sw, st.arms[1]);
  }

  // ---------------- items for @show ----------------
  function item(name, size) {
    const c = document.createElement("canvas"); c.width = c.height = size;
    const g = c.getContext("2d"); const n = name.toLowerCase(), s = size;
    g.translate(s / 2, s / 2);
    if (/key/.test(n)) {
      g.rotate(-0.6);
      g.fillStyle = lin(g, -s * 0.3, 0, s * 0.3, 0, [[0, "#f1cf72"], [0.5, "#c8962e"], [1, "#a8741d"]]);
      g.beginPath(); g.arc(-s * 0.22, 0, s * 0.13, 0, TAU); g.fill();
      g.fillRect(-s * 0.12, -s * 0.035, s * 0.44, s * 0.07);
      g.fillRect(s * 0.2, 0, s * 0.04, s * 0.1); g.fillRect(s * 0.27, 0, s * 0.04, s * 0.07);
      g.globalCompositeOperation = "destination-out"; g.beginPath(); g.arc(-s * 0.22, 0, s * 0.06, 0, TAU); g.fill(); g.globalCompositeOperation = "source-over";
      g.strokeStyle = "rgba(90,50,10,.5)"; g.lineWidth = 3; g.beginPath(); g.arc(-s * 0.22, 0, s * 0.13, 0, TAU); g.stroke();
    } else if (/letter|note|envelope/.test(n)) {
      g.fillStyle = "#fff4dc"; g.fillRect(-s * 0.32, -s * 0.22, s * 0.64, s * 0.44);
      g.strokeStyle = "#d1b98f"; g.lineWidth = 3; g.beginPath(); g.moveTo(-s * 0.32, -s * 0.22); g.lineTo(0, s * 0.04); g.lineTo(s * 0.32, -s * 0.22); g.stroke();
      g.fillStyle = "#c0392b"; g.beginPath(); g.arc(0, s * 0.04, s * 0.05, 0, TAU); g.fill();
    } else if (/coffee|latte|cup|mug/.test(n)) {
      g.fillStyle = "#fff"; rr(g, -s * 0.22, -s * 0.12, s * 0.4, s * 0.34, s * 0.06); g.fill();
      g.strokeStyle = "#fff"; g.lineWidth = s * 0.05; g.beginPath(); g.arc(s * 0.2, s * 0.04, s * 0.08, -1.4, 1.4); g.stroke();
      g.fillStyle = "#b7834f"; g.beginPath(); g.ellipse(-s * 0.02, -s * 0.12, s * 0.2, s * 0.05, 0, 0, TAU); g.fill();
      g.strokeStyle = "#fff4e2"; g.lineWidth = 3; g.beginPath(); g.moveTo(-s * 0.08, -s * 0.12); g.quadraticCurveTo(-s * 0.02, -s * 0.16, s * 0.04, -s * 0.12); g.stroke();
    } else {
      const hue = hash(n) % 360;
      g.fillStyle = hsl(hue, 55, 65); rr(g, -s * 0.28, -s * 0.28, s * 0.56, s * 0.56, s * 0.08); g.fill();
      g.fillStyle = "#fff"; g.font = `${s * 0.09}px 'Lilita One', sans-serif`; g.textAlign = "center"; g.fillText(name.replace(/_/g, " "), 0, s * 0.03);
    }
    return c;
  }

  return { background, clearCache, drawCharacter, makeLook, FACES, gesturePose, emotionPose, REST, item, hex, shade, alpha, hash, rr, OUTFITS };
})();
