// ===== Puppet characters built from your own separate parts (head, torso, arms, legs) =====
// Parts are stored as char/<ID>/rig/<name>. The same arm angles, head tilt, walk phase and hair sway
// that drive the built-in doll drive these parts, so every gesture in the stories works.
const Rig = (() => {
  const RAD = Math.PI / 180;
  const part = (id, n) => ArtStore.get(`char/${id}/rig/${n}`);
  const has = id => !!(part(id, "head") && part(id, "torso"));
  // The piece for the left (-1) or right (+1) of the screen, mirroring the other side when only one was made.
  function side(id, base, s) {
    const own = part(id, base + (s < 0 ? "_l" : "_r"));
    if (own) return { im: own, flip: false };
    const other = part(id, base + (s < 0 ? "_r" : "_l"));
    return other ? { im: other, flip: true } : null;
  }
  // Draws a piece hanging from its top-centre pivot at the origin.
  function hang(g, p, mul) {
    const im = p.im, w = im.naturalWidth, h = im.naturalHeight;
    g.save(); if (p.flip) g.scale(-1, 1);
    g.drawImage(im, -w / 2, 0, w, h); g.restore();
  }
  // Heads come from a separate drawing area on the sheet, so their size is matched to the torso (a head is about
  // 0.72 of the torso's height), then nudged by the Head size slider.
  const headMul = id => { try { return +localStorage.getItem("vn_headk_" + id) || 1; } catch (e) { return 1; } };
  const setHeadMul = (id, v) => { try { localStorage.setItem("vn_headk_" + id, v); } catch (e) {} };
  function headScale(id, head, torso) { return Math.max(0.3, Math.min(3, 0.72 * torso.naturalHeight / head.naturalHeight)) * headMul(id); }
  function pickHead(id, ch, blink) {
    const names = [ch.rawEmotion && (EMO_NORMAL[ch.rawEmotion] || ch.rawEmotion) === ch.emotion ? ch.rawEmotion : null, ch.emotion, "neutral"];
    const em = names.find(n => n && (n === "neutral" ? part(id, "head") : part(id, "head_" + n))) || "neutral";
    const key = em === "neutral" ? "head" : "head_" + em;
    if (ch.mouthOpen) {
      const t = part(id, key + "_talk") || (em === "neutral" ? part(id, "head_talk") : null);
      if (t) return t;
    }
    if (blink) {
      const b = part(id, key + "_blink") || (em === "neutral" ? part(id, "head_blink") : null);
      if (b) return b;
    }
    return part(id, key);
  }
  // The head with its bottom 22% faded out. Drawn over the torso it blends the neck into the torso's neck stub.
  const fadeCache = new Map();
  function fadedHead(im) {
    let c = fadeCache.get(im); if (c) return c;
    c = document.createElement("canvas"); c.width = im.naturalWidth; c.height = im.naturalHeight;
    const x = c.getContext("2d"); x.drawImage(im, 0, 0);
    const gr = x.createLinearGradient(0, c.height * 0.78, 0, c.height);
    gr.addColorStop(0, "rgba(0,0,0,1)"); gr.addColorStop(1, "rgba(0,0,0,0)");
    x.globalCompositeOperation = "destination-in"; x.fillStyle = gr; x.fillRect(0, 0, c.width, c.height);
    fadeCache.set(im, c); return c;
  }
  // A limb that bends smoothly: the lower part of the upper arm and the top of the forearm are cut into thin strips, each
  // turned a little more than the last, so the elbow curves like skin instead of folding like two cardboard tubes.
  function strips(g, p, y0, y1, n, step, s) {
    const im = p.im, w = im.naturalWidth, h = im.naturalHeight, sy = y0 * h, sh = (y1 - y0) * h / n;
    for (let i = 0; i < n; i++) {
      g.save(); if (p.flip) g.scale(-1, 1);
      const ov = Math.min(sh * 4, h - (sy + i * sh)); // draw taller than the strip: the next strip covers the extra, so no gaps open on the outside of the bend
      g.drawImage(im, 0, sy + i * sh, w, ov, -w / 2, 0, w, ov);
      g.restore();
      g.translate(0, sh * 0.97); g.rotate(-s * step * RAD);
    }
    return sh * n;
  }
  // e = distance from the top of the upper-arm piece to the elbow
  function bentArm(g, up, fore, a1, s, e) {
    const uw = up.im.naturalWidth, uh = up.im.naturalHeight, fh = fore.im.naturalHeight, step = a1 / 20;
    g.save(); if (up.flip) g.scale(-1, 1);
    g.drawImage(up.im, 0, 0, uw, e * 0.85, -uw / 2, 0, uw, e * 0.85);
    g.restore();
    g.translate(0, e * 0.85 * 0.99);
    strips(g, up, 0.85 * e / uh, e / uh, 10, step, s);
    strips(g, fore, 0, 0.12, 10, step, s);
    g.save(); if (fore.flip) g.scale(-1, 1);
    g.drawImage(fore.im, 0, fh * 0.12, fore.im.naturalWidth, fh * 0.88 + 1, -fore.im.naturalWidth / 2, 0, fore.im.naturalWidth, fh * 0.88 + 1);
    g.restore();
  }
  // Pieces cut from one painted figure (rig.json next to the pieces): everything is already at one scale, the joints
  // are recorded in the file, and the arms were straightened to hang down, so no proportion fixing is needed.
  const metas = new Map();
  function meta(id) {
    if (metas.has(id)) return metas.get(id);
    let m = null; try { m = JSON.parse(localStorage.getItem("vn_rigmeta_" + id) || "null"); } catch (e) {}
    if (m && m.v !== 2) m = null;
    metas.set(id, m); return m;
  }
  function setMeta(id, m) { try { if (m) localStorage.setItem("vn_rigmeta_" + id, JSON.stringify(m)); else localStorage.removeItem("vn_rigmeta_" + id); } catch (e) {} metas.delete(id); }
  function drawExact(g, ch, t, mt, torso, head0) {
    const id = ch.id, head = pickHead(id, ch, t < ch.blinkUntil) || head0;
    const hW = head.naturalWidth, hH = head.naturalHeight, tw = torso.naturalWidth, dy = mt.head.dy;
    const total = mt.feet + (hH - dy), k = 1000 / total;
    g.save(); g.scale(k, k); g.translate(0, -mt.feet); // origin: centre of the top of the torso (the neck), feet on y = 0
    const swing = ch.walk ? (ch.walk > 1 ? 30 : 20) : 0;
    for (const s of [-1, 1]) {
      const l = side(id, "leg", s), m = mt[s < 0 ? "leg_l" : "leg_r"]; if (!l) continue;
      g.save(); g.translate(m.x, m.y); g.rotate(swing * Math.sin(ch.walkPhase + (s < 0 ? 0 : Math.PI)) * RAD); hang(g, l); g.restore();
    }
    const arm = s => {
      const up = side(id, "upper_arm", s), fore = side(id, "forearm", s), m = mt[s < 0 ? "arm_l" : "arm_r"], a = ch.arms[s < 0 ? 0 : 1];
      if (!up) return;
      g.save(); g.translate(m.x, m.y); g.rotate(-s * (fore ? a[0] : a[0] + a[1] * 0.5) * RAD); g.translate(0, -m.py);
      if (fore && Math.abs(a[1]) > 3) bentArm(g, up, fore, a[1], s, m.e);
      else { hang(g, up); if (fore) { g.translate(0, m.e); hang(g, fore); } }
      g.restore();
    };
    const low = s => { const a = ch.arms[s < 0 ? 0 : 1]; return Math.abs(a[0]) < 40 && Math.abs(a[0] + a[1]) < 70; };
    for (const s of [-1, 1]) if (low(s)) arm(s);
    const headAt = () => {
      g.translate(0, Math.sin(t * 2.2 + ch.order + 1) * hH * 0.004); g.rotate((ch.headTilt || 0) * RAD);
    };
    g.save(); headAt(); g.drawImage(head, -hW / 2, dy - hH, hW, hH); g.restore();
    g.drawImage(torso, -tw / 2, 0, tw, torso.naturalHeight);
    g.save(); headAt(); g.drawImage(fadedHead(head), -hW / 2, dy - hH, hW, hH); g.restore();
    for (const s of [-1, 1]) if (!low(s)) arm(s);
    g.restore();
    return true;
  }
  function draw(g, ch, t) {
    const id = ch.id, torso = part(id, "torso"), head0 = part(id, "head");
    if (!torso || !head0) return false;
    const mt = meta(id);
    if (mt) return drawExact(g, ch, t, mt, torso, head0);
    const head = pickHead(id, ch, t < ch.blinkUntil) || head0;
    const legL = side(id, "leg", -1), legR = side(id, "leg", 1);
    const th = torso.naturalHeight, tw = torso.naturalWidth;
    // the sheet pieces are drawn at whatever size filled their cell, so bring arms and legs to normal proportions
    const legH = Math.max(legL ? legL.im.naturalHeight : 0, legR ? legR.im.naturalHeight : 0);
    const ls = legH ? Math.max(0.7, Math.min(1.7, 1.3 * th / legH)) : 1;
    const uaL = side(id, "upper_arm", -1), foL = side(id, "forearm", -1);
    const armLen = (uaL ? uaL.im.naturalHeight : 0) + (foL ? foL.im.naturalHeight : 0);
    const as = armLen ? Math.max(0.6, Math.min(1.7, 1.12 * th / armLen)) : 1;
    const hk = headScale(id, head0, torso), hH = head.naturalHeight * hk, hW = head.naturalWidth * hk;
    const total = 0.92 * hH + th + 0.88 * legH * ls, k = 1000 / total;
    g.save();
    g.scale(k, k);
    g.translate(0, -(th + 0.88 * legH * ls)); // origin: centre of the top of the torso
    // legs swing while walking
    const swing = ch.walk ? (ch.walk > 1 ? 30 : 20) : 0;
    [[legL, -1], [legR, 1]].forEach(([l, s]) => {
      if (!l) return;
      g.save(); g.translate(s * tw * 0.2, th - l.im.naturalHeight * ls * 0.12);
      g.rotate(swing * Math.sin(ch.walkPhase + (s < 0 ? 0 : Math.PI)) * RAD);
      g.scale(ls, ls); hang(g, l); g.restore();
    });
    // arms hanging close to the body tuck behind the torso so its sleeves and neckline hide the joins;
    // raised arms are drawn in front
    const arm = s => {
      const up = side(id, "upper_arm", s); if (!up) return;
      const fore = side(id, "forearm", s), a = ch.arms[s < 0 ? 0 : 1];
      const uw = up.im.naturalWidth * as, uh = up.im.naturalHeight;
      g.save(); g.translate(s * (tw / 2 - uw * 0.6), uw * 0.12);
      g.rotate(-s * (fore ? a[0] : a[0] + a[1] * 0.5) * RAD);
      g.scale(as, as);
      if (fore && Math.abs(a[1]) > 3) bentArm(g, up, fore, a[1], s, 0.94 * uh);
      else { hang(g, up); if (fore) { g.translate(0, uh * 0.94); hang(g, fore); } }
      g.restore();
    };
    const low = s => { const a = ch.arms[s < 0 ? 0 : 1]; return Math.abs(a[0]) < 40 && Math.abs(a[0] + a[1]) < 70; };
    for (const s of [-1, 1]) if (low(s)) arm(s);
    // head behind the torso: the torso's neck stub covers the bottom of the neck, so there is no seam or gap
    g.save();
    g.translate(0, 0.16 * hH + Math.sin(t * 2.2 + ch.order + 1) * hH * 0.004);
    g.rotate((ch.headTilt || 0) * RAD);
    const hair = part(id, "hair_back");
    if (hair) { g.save(); g.translate(0, -hH * 0.92); g.rotate(-(ch.sway || 0) * 0.35 * RAD); g.scale(hk, hk); hang(g, { im: hair }); g.restore(); }
    g.drawImage(head, -hW / 2, -hH, hW, hH);
    g.restore();
    g.drawImage(torso, -tw / 2, 0, tw, th);
    g.save();
    g.translate(0, 0.16 * hH + Math.sin(t * 2.2 + ch.order + 1) * hH * 0.004);
    g.rotate((ch.headTilt || 0) * RAD);
    g.drawImage(fadedHead(head), -hW / 2, -hH, hW, hH);
    g.restore();
    for (const s of [-1, 1]) if (!low(s)) arm(s);
    g.restore();
    return true;
  }
  return { has, draw, headMul, setHeadMul, setMeta };
})();
