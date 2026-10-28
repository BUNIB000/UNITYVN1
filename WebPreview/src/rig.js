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
  // Two-bone solve: the arm angles ([upper, lower], 0 = hanging down, positive = away from the body) that put the fingertips
  // on a named spot of the body. Spots are measured from the neck, in head and torso sizes, so they fit any character.
  function ik(id, s, goal) {
    const mt = meta(id); if (!mt) return null;
    const sgn = s === 0 ? -1 : 1, m = mt[s === 0 ? "arm_l" : "arm_r"];
    const fo = side(id, "forearm", sgn), hd = part(id, "head"); if (!fo || !hd) return null;
    const L1 = m.e - m.py, L2 = 0.93 * fo.im.naturalHeight, H1 = hd.naturalHeight - mt.head.dy, th = mt.torso.h, tw = mt.torso.w;
    const G = { chin: [0.10 * H1, 0.02 * H1], mouth: [0.07 * H1, -0.31 * H1], cheek: [0.33 * H1, -0.33 * H1], eyes: [0.24 * H1, -0.52 * H1],
      chest: [0.14 * tw, 0.22 * th], hip: [0.47 * tw, 0.55 * th], cross: [-0.36 * tw, 0.24 * th] }[goal];
    if (!G) return null;
    const tx = sgn * G[0], ty = G[1], dx = tx - m.x, dy = ty - m.y;
    let d = Math.hypot(dx, dy); d = Math.max(Math.abs(L1 - L2) + 1, Math.min(L1 + L2 - 1, d));
    const base = Math.atan2(dy, dx), A = Math.acos(Math.max(-1, Math.min(1, (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d))));
    const cand = [base + A, base - A].map(a => ({ x: m.x + L1 * Math.cos(a), y: m.y + L1 * Math.sin(a) }));
    const E = sgn * cand[0].x > sgn * cand[1].x ? cand[0] : cand[1]; // elbow out to the side, never across the body
    const hx = m.x + (tx - m.x) * d / Math.hypot(dx, dy), hy = m.y + (ty - m.y) * d / Math.hypot(dx, dy);
    const a0 = sgn * Math.atan2(E.x - m.x, E.y - m.y) / RAD, abs = sgn * Math.atan2(hx - E.x, hy - E.y) / RAD;
    let a1 = abs - a0; while (a1 > 180) a1 -= 360; while (a1 < -180) a1 += 360;
    return [a0, a1];
  }
  const hasExact = id => !!meta(id) && has(id);
  function setMeta(id, m) { try { if (m) localStorage.setItem("vn_rigmeta_" + id, JSON.stringify(m)); else localStorage.removeItem("vn_rigmeta_" + id); } catch (e) {} metas.delete(id); }
  function rigLayer(g, ch, t, mt, torso, head0) {
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

  // ----- Whole-body poses (arms folded, phone, hands on hips...) and the side-view walk -----
  // Poses are stored as char/<ID>/pose/<name>; the head is cut off and replaced by the puppet's own head, so faces still
  // blink, talk and change expression. The numbers in char/<ID>/pose/_meta place each pose on the puppet's neck.
  const pmetas = new Map();
  function poseMeta(id) {
    if (pmetas.has(id)) return pmetas.get(id);
    let m = null; try { m = JSON.parse(localStorage.getItem("vn_posemeta_" + id)); } catch (e) {}
    pmetas.set(id, m); return m;
  }
  function setPoseMeta(id, m) { try { if (m) localStorage.setItem("vn_posemeta_" + id, JSON.stringify(m)); else localStorage.removeItem("vn_posemeta_" + id); } catch (e) {} pmetas.delete(id); }
  const pimg = (id, n) => ArtStore.get(`char/${id}/pose/${n}`);
  const hasPose = (id, n) => { const m = poseMeta(id); return !!(m && m.poses && m.poses[n] && pimg(id, n) && hasExact(id)); };
  const hasWalk = id => { const m = poseMeta(id); return !!(m && m.walk && pimg(id, "walk0") && pimg(id, "walk3") && hasExact(id)); };
  const POSE_WORDS = {
    arms_crossed: ["arms_crossed", "arms_folded", "folded", "crossed", "fold_arms", "cross_arms", "folded_arms", "crossed_arms"],
    phone: ["phone", "using_phone", "use_phone", "texting", "text", "check_phone"],
    hands_hips: ["hands_hips", "hands_on_hips", "hips", "hands_on_hip", "hand_on_hips"],
    hands_back: ["hands_back", "hands_behind_back", "behind_back", "hands_behind", "hands_behind_back"],
    hands_clasped: ["hands_clasped", "clasped", "clasp", "hands_together", "hands_in_front"],
    hand_pocket: ["hand_pocket", "hand_in_pocket", "pocket", "in_pocket", "hands_pocket"],
  };
  // The pose a script word means for this character, or null.
  function poseName(id, word) {
    const w = String(word || "").toLowerCase().replace(/[\s-]+/g, "_").replace(/_loop$/, "");
    for (const k of Object.keys(POSE_WORDS)) if (POSE_WORDS[k].includes(w) || k === w) return hasPose(id, k) ? k : null;
    return null;
  }
  // The head with only the last 5% faded, so the new head blends into the neck of the pose.
  const fade2 = new Map();
  function fadedNeck(im) {
    let c = fade2.get(im); if (c) return c;
    c = document.createElement("canvas"); c.width = im.naturalWidth; c.height = im.naturalHeight;
    const x = c.getContext("2d"); x.drawImage(im, 0, 0);
    const gr = x.createLinearGradient(0, c.height * 0.95, 0, c.height);
    gr.addColorStop(0, "rgba(0,0,0,1)"); gr.addColorStop(1, "rgba(0,0,0,0)");
    x.globalCompositeOperation = "destination-in"; x.fillStyle = gr; x.fillRect(0, 0, c.width, c.height);
    fade2.set(im, c); return c;
  }
  function frame(g, ch, t, mt, head0, body) {
    const id = ch.id, head = pickHead(id, ch, t < ch.blinkUntil) || head0;
    const hW = head.naturalWidth, hH = head.naturalHeight, dy = mt.head.dy;
    const total = mt.feet + (hH - dy), k = 1000 / total;
    g.save(); g.scale(k, k); g.translate(0, -mt.feet);
    body(head, hW, hH, dy);
    g.restore();
  }
  function poseLayer(g, ch, t, mt, head0, name) {
    const id = ch.id, p = poseMeta(id).poses[name], im = pimg(id, name);
    frame(g, ch, t, mt, head0, (head, hW, hH, dy) => {
      g.drawImage(im, -p.cx, dy - p.yb);
      g.save(); g.translate(0, Math.sin(t * 2.2 + ch.order + 1) * hH * 0.004); g.rotate((ch.headTilt || 0) * RAD);
      g.drawImage(fadedNeck(head), -hW / 2, dy - hH, hW, hH); g.restore();
    });
  }
  function walkLayer(g, ch, t, mt, head0) {
    const id = ch.id, pm = poseMeta(id), i = ((Math.floor(ch.walkPhase / (Math.PI / 2)) % 4) + 4) % 4, im = pimg(id, "walk" + i) || pimg(id, "walk0");
    frame(g, ch, t, mt, head0, (head, hW, hH, dy) => {
      const w = im.naturalWidth, h = im.naturalHeight;
      g.save(); if (ch.facing < 0) g.scale(-1, 1);
      g.drawImage(im, -w / 2, dy + pm.ground - h, w, h); g.restore();
    });
  }
  // Draws whichever look the character should have now (a pose, the walk, or the puppet) and fades between them.
  function drawExact(g, ch, t, mt, torso, head0) {
    const id = ch.id;
    const want = ch.walk && hasWalk(id) ? "walk" : ch.pose && hasPose(id, ch.pose) ? ch.pose : null;
    if (ch._rl === undefined) { ch._rl = want; ch._rlPrev = undefined; ch._rlT = t; }
    else if (want !== ch._rl) { ch._rlPrev = ch._rl; ch._rl = want; ch._rlT = t; }
    const k = ch._rlPrev === undefined ? 1 : Math.min(1, (t - ch._rlT) / 0.22);
    if (k >= 1) ch._rlPrev = undefined;
    const one = (name, a) => {
      g.save(); g.globalAlpha *= a;
      if (name === null) rigLayer(g, ch, t, mt, torso, head0);
      else if (name === "walk") walkLayer(g, ch, t, mt, head0);
      else poseLayer(g, ch, t, mt, head0, name);
      g.restore();
    };
    if (ch._rlPrev !== undefined) one(ch._rlPrev, 1);
    one(ch._rl, k);
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
  return { has, hasExact, ik, draw, headMul, setHeadMul, setMeta, setPoseMeta, hasPose, hasWalk, poseName };
})();
