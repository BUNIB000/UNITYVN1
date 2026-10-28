// ===== Clock, tweens, stage (backgrounds, characters, camera, weather) =====
const Clock = {
  now: 0, scale: 1, tweens: [],
  tick(dt) {
    this.now += dt * this.scale;
    const list = this.tweens; this.tweens = [];
    for (const t of list) {
      const k = t.dur <= 0 ? 1 : Math.min(1, (this.now - t.start) / t.dur);
      t.fn(t.ease(k));
      if (k >= 1) t.resolve(); else this.tweens.push(t);
    }
  },
  tween(dur, fn, ease) { return new Promise(resolve => { const t = { start: this.now, dur, fn, ease: ease || Ease.inOut, resolve }; fn(t.ease(0)); this.tweens.push(t); }); },
  wait(sec) { return this.tween(sec, () => {}); },
  clear() { this.tweens.forEach(t => t.resolve()); this.tweens = []; },
};
const Ease = {
  linear: k => k,
  out: k => 1 - Math.pow(1 - k, 3),
  in: k => k * k * k,
  inOut: k => k < 0.5 ? 4 * k * k * k : 1 - Math.pow(-2 * k + 2, 3) / 2,
  back: k => { const c = 1.70158; return 1 + (c + 1) * Math.pow(k - 1, 3) + c * Math.pow(k - 1, 2); },
  bounce: k => { const n = 7.5625, d = 2.75; if (k < 1 / d) return n * k * k; if (k < 2 / d) return n * (k -= 1.5 / d) * k + 0.75; if (k < 2.5 / d) return n * (k -= 2.25 / d) * k + 0.9375; return n * (k -= 2.625 / d) * k + 0.984375; },
};

// Body motion for named gestures (same curves as the Unity engine's ProceduralMotion).
const Motion = (() => {
  const S = Math.sin, PI = Math.PI, cl = v => Math.max(0, Math.min(1, v));
  const squash = k => k < 0.12 ? S(k / 0.12 * PI) : k > 0.88 ? S((k - 0.88) / 0.12 * PI) : (k < 0.5 ? -0.4 * S((k - 0.12) / 0.38 * PI) : 0);
  const P = (ox, oy, rot, sx, sy) => ({ ox: ox || 0, oy: oy || 0, rot: rot || 0, sx: sx == null ? 1 : sx, sy: sy == null ? 1 : sy });
  const holds = new Set(["think", "lean", "pout", "faint", "shy", "sulk"]);
  const curves = {
    jump: [0.55, k => P(0, 0.075 * S(PI * cl((k - 0.12) / 0.76)), 0, 1 + squash(k) * 0.05, 1 - squash(k) * 0.06)],
    bounce: [0.8, k => P(0, 0.04 * Math.abs(S(2 * PI * k)), 0, 1, 1 + 0.015 * S(4 * PI * k))],
    nod: [0.7, k => P(0, -0.012 * Math.abs(S(2 * PI * k)), 0, 1, 1 - 0.01 * Math.abs(S(2 * PI * k)))],
    shake_head: [0.7, k => P(0, 0, 3.5 * S(5 * PI * k) * (1 - k))],
    laugh: [1.1, k => P(0, 0.01 * Math.abs(S(9 * PI * k)) * (1 - k * 0.5), -1.5 * S(PI * k), 1, 1 + 0.01 * S(18 * PI * k))],
    gasp: [0.55, k => { const pop = k < 0.2 ? k / 0.2 : 1 - Ease.back((k - 0.2) / 0.8) * 0.85; return P(0, 0.025 * pop, 2.5 * pop, 1 - 0.02 * pop, 1 + 0.04 * pop); }],
    sigh: [1.5, k => P(0, -0.018 * S(PI * k), 0, 1 + 0.01 * S(PI * k), 1 - 0.022 * S(PI * k))],
    shiver: [1.1, (k, t) => P(0.006 * S(t * 70) * (1 - k * 0.6), -0.008 * S(PI * k))],
    cry: [1.6, (k, t) => P(0.003 * S(t * 40), -0.015 * S(PI * k) - 0.004 * Math.abs(S(t * 8)), 0, 1, 1 - 0.015 * S(PI * k))],
    angry: [0.6, (k, t) => { const hit = k < 0.25 ? S(k / 0.25 * PI) : 0; return P(0.007 * S(t * 55) * (1 - k), -0.025 * hit, 0, 1 + 0.03 * hit, 1 - 0.03 * hit); }],
    dance: [1.8, k => P(0.02 * S(4 * PI * k), 0.018 * Math.abs(S(4 * PI * k)), 5 * S(4 * PI * k))],
    wave: [1.2, k => P(0, 0.006 * S(PI * k), 1.5 * S(6 * PI * k) * S(PI * k))],
    spin: [0.6, k => P(0, 0.02 * S(PI * k), 0, Math.cos(2 * PI * Ease.inOut(k)), 1)],
    shrug: [0.7, k => P(0, 0.015 * S(PI * k), 0, 1 + 0.02 * S(PI * k), 1)],
    bow: [1.2, k => P(0, -0.035 * S(PI * k), -3 * S(PI * k), 1, 1 - 0.03 * S(PI * k))],
    think: [0.6, k => P(0, 0, 3 * Ease.back(k))],
    lean: [0.6, k => P(0, 0, -5 * Ease.back(k))],
    pout: [0.6, k => P(0, 0, 2 * Ease.back(k))],
    shy: [0.6, k => P(0, -0.01 * k, 2 * Ease.back(k))],
    faint: [1.2, k => { const f = Ease.bounce(k); return P(-0.15 * f, -0.35 * f, -80 * f); }],
    point: [0.5, k => P(0.01 * S(PI * k), 0, -2.5 * S(PI * k))],
    talk: [1.2, (k, t) => P(0, 0.004 * Math.abs(S(t * 7)), 0.8 * S(t * 2.3))],
    love: [1.0, k => P(0, 0.012 * S(PI * k), 2.5 * S(2 * PI * k), 1, 1 + 0.01 * S(PI * k))],
  };
  const alias = { hop: "jump", cheer: "bounce", excited: "bounce", yes: "nod", no: "shake_head", shake: "shake_head", giggle: "laugh", surprised: "gasp", flinch: "gasp", shocked: "gasp", sad: "sigh", scared: "shiver", tremble: "shiver", stomp: "angry", sulk: "shy", swoon: "love", blush: "shy" };
  return {
    get(g) { if (!g) return null; return curves[g] || curves[alias[g]] || null; },
    holds: g => holds.has(g),
  };
})();

const EMO_KNOWN = new Set("neutral happy sad angry surprised shocked scared afraid think thinking confused love laugh laughing embarrassed blush smug smirk cry crying tired sleepy disgusted determined excited flirty worried nervous annoyed smile serious gasp proud hopeful shy upset furious joyful content bored pout giggle".split(" "));
const EMO_NORMAL = { shocked: "surprised", gasp: "surprised", afraid: "scared", thinking: "think", confused: "think", laughing: "laugh", joyful: "laugh", blush: "embarrassed", shy: "embarrassed", smirk: "smug", crying: "cry", upset: "cry", sleepy: "tired", bored: "tired", nervous: "worried", furious: "angry", annoyed: "angry", smile: "happy", content: "happy", proud: "happy", hopeful: "happy", excited: "happy", serious: "neutral", determined: "neutral" };
function splitAnim(anim) {
  const r = { gesture: null, emotion: null, loop: false, raw: null };
  if (!anim) return r;
  const parts = anim.toLowerCase().split(/[_\s-]+/).filter(Boolean);
  const li = parts.indexOf("loop"); if (li >= 0) { r.loop = true; parts.splice(li, 1); }
  const g = [];
  for (const p of parts) { if (r.emotion == null && EMO_KNOWN.has(p)) { r.raw = p; r.emotion = EMO_NORMAL[p] || p; } else g.push(p); }
  r.gesture = g.length ? g.join("_") : null;
  if (!r.gesture && r.emotion && ["think", "laugh", "cry", "giggle", "pout"].includes(r.emotion)) r.gesture = r.emotion;
  if (!r.gesture && r.raw && ["gasp", "excited", "blush", "shy", "shocked", "surprised"].includes(r.raw)) r.gesture = r.raw;
  if (!r.gesture && r.emotion === "angry") r.gesture = "angry";
  return r;
}

class StageChar {
  constructor(stage, id, def) {
    this.stage = stage; this.id = id; this.def = def;
    this.look = Art.makeLook(id, def && def.opts);
    this.x = 160; this.y = 0; this.scale = 1; this.facing = 0; this.alpha = 0; this.onStage = false;
    this.layer = 0; this.order = ++stage.order;
    this.emotion = (def && def.opts && def.opts.expression) || "neutral";
    this.gesture = null; this.gStart = 0; this.gDur = 0; this.gLoop = false; this.hold = null;
    this.arms = [Art.REST.slice(), Art.REST.slice()];
    this.nextBlink = 1 + Math.random() * 3; this.blinkUntil = 0;
    this.mouthOpen = false; this.talking = false;
    this.walk = 0; this.walkPhase = 0; this.dyOff = 0;
    this.sway = 0; this.swayV = 0; this.lastRot = 0;
    this.headTilt = 0;
    this.blush = 0;
  }
  get name() { return (this.def && this.def.name) || this.id.charAt(0) + this.id.slice(1).toLowerCase(); }
  get color() { return this.look.color; }
  play(anim) {
    const a = splitAnim(anim);
    if (a.emotion) { this.emotion = a.emotion; this.rawEmotion = a.raw; }
    this.animName = anim.toLowerCase().replace(/_loop$/, "");
    let g = a.gesture;
    const pn = typeof Rig !== "undefined" && Rig.poseName(this.id, anim.toLowerCase().replace(/^is_/, ""));
    if (pn) { this.pose = pn; this.gesture = null; this.hold = null; return 0; }
    if (g === "idle") { this.gesture = null; this.hold = null; this.pose = null; return 0; }
    if (g && g !== "talk") this.pose = null;
    if (!g) return 0;
    const m = Motion.get(g);
    const armOnly = Art.gesturePose(g, 0.5, 0, true) || Art.gesturePose(g, 0.5, 0, false);
    if (!m && !armOnly) return 0;
    this.gesture = g; this.gStart = Clock.now; this.gDur = m ? m[0] : 1.2; this.gLoop = a.loop || g === "talk";
    if (g === "talk") this.gDur = 1.6;
    this.hold = Motion.holds(g) ? g : null;
    return a.loop ? 0 : this.gDur;
  }
  feetPx() {
    const S = this.stage;
    return { x: this.x * S.u, y: S.H + S.CH * 0.13 - (this.y + this.dyOff) * S.u, h: S.CH * 0.9 * this.scale };
  }
  update(dt, t) {
    // blink
    if (t > this.nextBlink) { this.blinkUntil = t + 0.12; this.nextBlink = t + 2 + Math.random() * 3.5; if (Math.random() < 0.2) this.nextBlink = t + 0.3; }
    // gesture progress
    let k = 0, pose = null;
    if (this.gesture) {
      k = (Clock.now - this.gStart) / this.gDur;
      if (k >= 1) {
        if (this.gLoop && (this.gesture !== "talk" || this.talking)) { this.gStart = Clock.now; k = 0; }
        else { if (this.hold) k = 1; else { this.gesture = null; k = 0; } }
      }
    }
    const near = this.facing < 0 ? 0 : 1;
    for (let s = 0; s < 2; s++) {
      let target = Art.REST.slice();
      const ep = Art.emotionPose(this.emotion, s === near);
      if (ep) target = ep;
      if (this.gesture) {
        const gp = Art.gesturePose(this.gesture, Math.min(k, 0.999), Clock.now, s === near);
        if (gp) { const [p, e] = gp; target = [target[0] + (p[0] - target[0]) * e, target[1] + (p[1] - target[1]) * e]; }
      }
      // poses that touch the body: let a figure-cut puppet solve its own arm angles so the hand really gets there
      if (typeof Rig !== "undefined" && Rig.hasExact(this.id)) {
        let goal = Art.emotionGoal(this.emotion, s === near), gw = goal ? 1 : 0;
        if (this.gesture) { const gg = Art.gestureGoal(this.gesture, s === near), gp2 = Art.gesturePose(this.gesture, Math.min(k, 0.999), Clock.now, s === near); if (gg && gp2) { goal = gg; gw = gp2[1]; } }
        const ik = goal && Rig.ik(this.id, s, goal);
        if (ik) target = [target[0] + (ik[0] - target[0]) * gw, target[1] + (ik[1] - target[1]) * gw];
      }
      if (this.walk) { const sw = Math.sin(this.walkPhase + (s ? Math.PI : 0)) * 10; target = [target[0] + sw, target[1] - 8]; }
      target[0] += Math.sin(t * 1.7 + s) * 1.2;
      const r = 1 - Math.exp(-dt * 11);
      this.arms[s][0] += (target[0] - this.arms[s][0]) * r;
      this.arms[s][1] += (target[1] - this.arms[s][1]) * r;
    }
    const tiltTarget = { think: 6, sad: -3, cry: -4, embarrassed: -4, love: 5, happy: 2, smug: -3, worried: -2, surprised: 0, angry: 0 }[this.emotion] || 0;
    this.headTilt += ((tiltTarget * (this.facing || 1)) + Math.sin(t * 0.9 + this.order) * 1.2 - this.headTilt) * (1 - Math.exp(-dt * 4));
    this.motion = pose;
    this.gk = k;
  }
  draw(g, t) {
    if (!this.onStage || this.alpha <= 0.001) return;
    const f = this.feetPx();
    let P = { ox: 0, oy: 0, rot: 0, sx: 1, sy: 1 };
    if (this.gesture) { const m = Motion.get(this.gesture); if (m) P = m[1](Math.min(1, this.gk), Clock.now); }
    // breathing and walk bob
    P.sy *= 1 + 0.006 * Math.sin(t * 2.2 + this.order);
    if (this.walk) { P.oy += 0.012 * Math.abs(Math.sin(this.walkPhase)); P.rot += 1.4 * Math.sin(this.walkPhase) * (this.walk > 1 ? 1.5 : 1); }
    // hair follows body rotation like a spring
    const rotV = (P.rot - this.lastRot); this.lastRot = P.rot;
    this.swayV += (-rotV * 6 - this.sway * 0.25) ; this.swayV *= 0.8; this.sway += this.swayV * 0.5;
    this.sway = Math.max(-25, Math.min(25, this.sway + (this.walk ? Math.sin(this.walkPhase * 2) * 0.6 : 0)));
    g.save();
    g.globalAlpha = this.alpha;
    g.translate(f.x + P.ox * f.h * (this.facing || 1), f.y - P.oy * f.h);
    g.rotate(P.rot * (this.facing || 1) * Math.PI / 180);
    g.scale(P.sx, P.sy);
    g.scale(f.h / 1000, f.h / 1000);
    if (Rig.has(this.id) && Rig.draw(g, this, t)) { g.restore(); return; }
    if (ArtStore.hasChar(this.id) && this.drawImage(g, t)) { g.restore(); return; }
    Art.drawCharacter(g, this.look, {
      emotion: this.emotion, time: t, blink: t < this.blinkUntil, mouthOpen: this.mouthOpen,
      look: this.facing, arms: this.arms, headTilt: this.headTilt, sway: this.sway, blush: this.blush,
    });
    g.restore();
  }
  drawImage(g, t) {
    const active = this.gesture && (this.gk < 1 || this.hold);
    const names = active ? [this.animName, this.gesture] : [];
    names.push(this.rawEmotion && (EMO_NORMAL[this.rawEmotion] || this.rawEmotion) === this.emotion ? this.rawEmotion : null, this.emotion, "neutral", "idle");
    const pick = ArtStore.charImage(this.id, this.look.slots.outfit, names, this.mouthOpen, t < this.blinkUntil);
    if (!pick) return false;
    if (pick.key !== this.imgKey) { this.prevImg = this.curImg; this.curImg = pick.img; this.imgKey = pick.key; this.fadeT = t; }
    else this.curImg = pick.img;
    const opts = (this.def && this.def.opts) || {};
    const artFaces = (opts.faces || "").toLowerCase();
    if ((artFaces === "right" && this.facing < 0) || (artFaces === "left" && this.facing > 0)) g.scale(-1, 1);
    const draw = (im, a) => { if (!im || a <= 0) return; const w = 1000 * im.naturalWidth / im.naturalHeight; const ga = g.globalAlpha; g.globalAlpha = ga * a; g.drawImage(im, -w / 2, -1000, w, 1000); g.globalAlpha = ga; };
    const k = Math.min(1, (t - (this.fadeT || 0)) / 0.16);
    if (k < 1 && this.prevImg) draw(this.prevImg, 1);
    draw(this.curImg, k < 1 && this.prevImg ? k : 1);
    return true;
  }
  headPx() { const f = this.feetPx(); return { x: f.x, y: f.y - f.h * 0.975, h: f.h }; }
}

class Stage {
  constructor(canvas) {
    this.canvas = canvas; this.g = canvas.getContext("2d");
    this.chars = new Map(); this.order = 0; this.defs = {};
    this.bgName = null; this.bg = null; this.prevBg = null; this.bgMix = 1;
    this.cam = { z: 1, fx: 0.5, fy: 0.5 }; this.shakeAmp = 0;
    this.tintColor = null; this.tintA = 0;
    this.weather = "none"; this.particles = [];
    this.W = 360; this.H = 640; this.CH = 640; this.dpr = 1;
  }
  get u() { return this.W / 320; }
  resize(w, h) {
    this.dpr = Math.min(2, window.devicePixelRatio || 1);
    this.W = w; this.H = h; this.CH = Math.min(h, w * 16 / 9);
    this.canvas.width = Math.round(w * this.dpr); this.canvas.height = Math.round(h * this.dpr);
    if (this.bgName) { this.bg = Art.background(this.bgName, this.canvas.width, this.canvas.height); this.prevBg = null; this.bgMix = 1; }
  }
  reset() {
    this.chars.clear(); this.bgName = null; this.bg = null; this.prevBg = null; this.bgMix = 1;
    this.cam = { z: 1, fx: 0.5, fy: 0.5 }; this.tintA = 0; this.tintColor = null; this.weather = "none"; this.particles = [];
  }
  character(id) {
    id = id.toUpperCase();
    let c = this.chars.get(id);
    if (!c) { c = new StageChar(this, id, this.defs[id]); this.chars.set(id, c); }
    return c;
  }
  async setBackground(name, transition, dur, fadeFn) {
    const img = Art.background(name, this.canvas.width, this.canvas.height);
    transition = transition || "cut";
    if (transition === "fade" && fadeFn) {
      await fadeFn(1, (dur || 1) / 2);
      this.bg = img; this.bgName = name; this.prevBg = null; this.bgMix = 1;
      this.clearCharacters();
      await fadeFn(0, (dur || 1) / 2);
      return;
    }
    this.clearCharacters();
    if (transition === "cut" || !this.bg) { this.bg = img; this.bgName = name; this.bgMix = 1; this.prevBg = null; return; }
    this.prevBg = this.bg; this.bg = img; this.bgName = name; this.bgMix = 0;
    await Clock.tween(dur || 0.8, k => this.bgMix = k);
    this.prevBg = null;
  }
  clearCharacters() { for (const c of this.chars.values()) { c.onStage = false; c.alpha = 0; c.gesture = null; } this.cam = { z: 1, fx: 0.5, fy: 0.5 }; }
  namedX(n) {
    return { left: 72, center: 160, right: 248, far_left: 30, far_right: 290, offscreen_left: -90, offscreen_right: 410, offscreen_top: 160, offscreen_bottom: 160 }[n] ?? 160;
  }
  resolve(pos, c) {
    let x = c.onStage ? c.x : 160, y = c.onStage ? c.y : 0, scale = c.onStage ? c.scale : 1;
    if (!pos) return { x, y, scale };
    if (pos.nextTo) { const o = this.chars.get(pos.nextTo.toUpperCase()); if (o && o.onStage) { const side = o.x > 160 ? -1 : 1; return { x: o.x + side * 85, y: o.y, scale: o.scale }; } }
    if (pos.named && pos.named !== "top" && pos.named !== "bottom") x = this.namedX(pos.named);
    if (pos.x != null) x = pos.x;
    if (pos.y != null) y = pos.y;
    if (pos.scale != null) scale = pos.scale;
    return { x, y, scale };
  }
  async move(c, to, mode, dur) {
    const from = { x: c.x, y: c.y, s: c.scale };
    const dist = Math.hypot(to.x - from.x, to.y - from.y);
    if (dist < 0.5 && Math.abs(to.scale - from.s) < 0.001) return;
    const speed = mode === "run" ? 420 : mode === "walk" ? 190 : 300;
    const d = dur != null ? dur : Math.max(0.35, dist / speed);
    if (Math.abs(to.x - from.x) > 2) c.facing = to.x > from.x ? 1 : -1;
    if (mode !== "move") c.walk = mode === "run" ? 2 : 1;
    const stepRate = mode === "run" ? 16 : 10;
    const t0 = Clock.now;
    await Clock.tween(d, k => {
      c.x = from.x + (to.x - from.x) * k; c.y = from.y + (to.y - from.y) * k; c.scale = from.s + (to.scale - from.s) * k;
      c.walkPhase = (Clock.now - t0) * stepRate;
    }, mode === "move" ? Ease.inOut : k => k < 0.12 ? (k / 0.12) * (k / 0.12) * 0.06 : k > 0.88 ? 1 - Math.pow((1 - k) / 0.12, 2) * 0.06 : 0.06 + (k - 0.12) / 0.76 * 0.88);
    c.walk = 0;
  }
  focusOn(c, z, dur) {
    const h = c.headPx();
    const fx = h.x / this.W, fy = (h.y + h.h * 0.17) / this.H;
    return this.camTo(z, fx, fy, dur);
  }
  camTo(z, fx, fy, dur) {
    const a = { ...this.cam };
    return Clock.tween(dur ?? 0.6, k => { this.cam.z = a.z + (z - a.z) * k; this.cam.fx = a.fx + (fx - a.fx) * k; this.cam.fy = a.fy + (fy - a.fy) * k; });
  }
  shake(dur, amp) { this.shakeAmp = amp || 10; return Clock.tween(dur || 0.5, k => this.shakeAmp = (amp || 10) * (1 - k), Ease.linear); }
  setTint(name, dur) {
    const map = { night: "#7d8fd6", sepia: "#e6c99a", sunset: "#ffb38a", dark: "#5a5a78", red: "#ff8a8a", dream: "#e2b8ff" };
    const from = this.tintA;
    if (!name || name === "none" || name === "off") return Clock.tween(dur || 0.8, k => this.tintA = from * (1 - k));
    this.tintColor = map[name] || Art.hex(name, "#7d8fd6");
    return Clock.tween(dur || 0.8, k => this.tintA = from + (1 - from) * k);
  }
  setWeather(w) {
    this.weather = (w || "none").toLowerCase();
    if (this.weather === "none" || this.weather === "off" || this.weather === "clear") { this.weather = "none"; }
  }
  camTransform() {
    const z = this.cam.z, W = this.W, H = this.H;
    const hw = W / (2 * z), hh = H / (2 * z);
    const cx = Math.max(hw, Math.min(W - hw, this.cam.fx * W)), cy = Math.max(hh, Math.min(H - hh, this.cam.fy * H));
    return { z, tx: W / 2 - cx * z, ty: H / 2 - cy * z };
  }
  toScreen(x, y) { const T = this.camTransform(); return { x: x * T.z + T.tx, y: y * T.z + T.ty, z: T.z }; }
  updateWeather(dt) {
    const W = this.W, H = this.H, w = this.weather;
    const want = { rain: 140, storm: 220, snow: 90, petals: 34, leaves: 26, sparkles: 40, dust: 30 }[w] || 0;
    while (this.particles.length < want) this.particles.push(this.spawn(w, true));
    if (this.particles.length > want) this.particles.length = want;
    for (const p of this.particles) {
      p.x += p.vx * dt; p.y += p.vy * dt; p.r += p.vr * dt; p.life += dt;
      if (w === "petals" || w === "leaves" || w === "snow") p.x += Math.sin(p.life * p.wob) * 20 * dt;
      if (p.y > H + 20 || p.x < -40 || p.x > W + 40 || (w === "sparkles" && p.life > p.max)) Object.assign(p, this.spawn(w, false));
    }
  }
  spawn(w, initial) {
    const W = this.W, H = this.H, R = Math.random;
    const p = { x: R() * W, y: initial ? R() * H : -20, vx: 0, vy: 0, r: R() * 6, vr: (R() - 0.5) * 3, life: 0, max: 1, s: 1, wob: 1 + R() * 2 };
    if (w === "rain" || w === "storm") { p.vy = H * (1.4 + R() * 0.5); p.vx = -H * 0.15; p.s = 0.6 + R() * 0.6; }
    else if (w === "snow") { p.vy = H * (0.06 + R() * 0.08); p.s = 1 + R() * 2.5; }
    else if (w === "petals" || w === "leaves") { p.vy = H * (0.07 + R() * 0.08); p.vx = W * 0.05; p.s = 0.7 + R() * 0.7; }
    else if (w === "sparkles") { p.y = R() * H; p.max = 1 + R() * 2; p.s = 0.5 + R(); }
    else if (w === "dust") { p.vy = -H * 0.01; p.vx = W * 0.01; p.s = 0.5 + R() * 1.5; p.y = R() * H; }
    return p;
  }
  drawWeather(g) {
    const w = this.weather; if (w === "none") return;
    for (const p of this.particles) {
      if (w === "rain" || w === "storm") { g.strokeStyle = "rgba(200,220,255,.55)"; g.lineWidth = 1.3 * p.s; g.beginPath(); g.moveTo(p.x, p.y); g.lineTo(p.x + 3 * p.s, p.y - 16 * p.s); g.stroke(); }
      else if (w === "snow") { g.fillStyle = "rgba(255,255,255,.9)"; g.beginPath(); g.arc(p.x, p.y, p.s, 0, 7); g.fill(); }
      else if (w === "petals" || w === "leaves") {
        g.save(); g.translate(p.x, p.y); g.rotate(p.r); g.scale(1, Math.abs(Math.sin(p.life * 3 + p.wob)) * 0.6 + 0.4);
        g.fillStyle = w === "petals" ? "rgba(255,170,200,.95)" : "rgba(214,140,60,.95)"; g.beginPath(); g.ellipse(0, 0, 6 * p.s, 3.5 * p.s, 0, 0, 7); g.fill(); g.restore();
      } else if (w === "sparkles") {
        const a = Math.sin(p.life / p.max * Math.PI); g.fillStyle = `rgba(255,240,180,${a})`;
        g.save(); g.translate(p.x, p.y); g.rotate(p.life); const s = 5 * p.s * a;
        g.beginPath(); for (let i = 0; i < 8; i++) { const r = i % 2 ? s * 0.3 : s; g.lineTo(Math.cos(i * Math.PI / 4) * r, Math.sin(i * Math.PI / 4) * r); } g.fill(); g.restore();
      } else if (w === "dust") { g.fillStyle = "rgba(255,240,210,.35)"; g.beginPath(); g.arc(p.x, p.y, p.s, 0, 7); g.fill(); }
    }
    if (w === "storm" && Math.random() < 0.004) this.lightning = 1;
    if (this.lightning > 0) { g.fillStyle = `rgba(255,255,255,${this.lightning * 0.5})`; g.fillRect(0, 0, this.W, this.H); this.lightning -= 0.08; }
  }
  frame(dt, t) {
    const g = this.g, W = this.W, H = this.H;
    g.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    g.fillStyle = "#120c18"; g.fillRect(0, 0, W, H);
    const T = this.camTransform();
    const sx = (Math.random() - 0.5) * this.shakeAmp, sy = (Math.random() - 0.5) * this.shakeAmp;
    g.save();
    g.translate(T.tx + sx, T.ty + sy); g.scale(T.z, T.z);
    if (this.prevBg) g.drawImage(this.prevBg, 0, 0, W, H);
    if (this.bg) { g.globalAlpha = this.bgMix; g.drawImage(this.bg, 0, 0, W, H); g.globalAlpha = 1; }
    const list = [...this.chars.values()].sort((a, b) => a.layer - b.layer || a.order - b.order);
    for (const c of list) { c.update(dt, t); c.draw(g, t); }
    g.restore();
    if (this.tintA > 0.01 && this.tintColor) {
      g.globalCompositeOperation = "multiply"; g.globalAlpha = this.tintA; g.fillStyle = this.tintColor; g.fillRect(0, 0, W, H);
      g.globalCompositeOperation = "source-over"; g.globalAlpha = 1;
    }
    this.updateWeather(dt);
    this.drawWeather(g);
  }
  snapshot() {
    return {
      bg: this.bgName, tint: this.tintA > 0.5 ? this.tintColor : null, weather: this.weather,
      chars: [...this.chars.values()].filter(c => c.onStage).map(c => ({ id: c.id, x: c.x, y: c.y, s: c.scale, f: c.facing, e: c.emotion, l: c.layer, slots: { ...c.look.slots }, hold: c.hold })),
    };
  }
  restore(s) {
    this.reset();
    if (s.bg) { this.bgName = s.bg; this.bg = Art.background(s.bg, this.canvas.width, this.canvas.height); }
    if (s.tint) { this.tintColor = s.tint; this.tintA = 1; }
    this.setWeather(s.weather);
    for (const d of s.chars || []) {
      const c = this.character(d.id);
      Object.assign(c, { x: d.x, y: d.y, scale: d.s, facing: d.f, emotion: d.e, layer: d.l, onStage: true, alpha: 1 });
      Object.assign(c.look.slots, d.slots);
      if (d.hold) { c.gesture = d.hold; c.hold = d.hold; c.gStart = -10; c.gDur = 0.6; }
    }
  }
}
