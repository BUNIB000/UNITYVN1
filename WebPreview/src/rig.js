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
  function draw(g, ch, t) {
    const id = ch.id, torso = part(id, "torso"), head0 = part(id, "head");
    if (!torso || !head0) return false;
    const head = pickHead(id, ch, t < ch.blinkUntil) || head0;
    const legL = side(id, "leg", -1), legR = side(id, "leg", 1);
    const legH = Math.max(legL ? legL.im.naturalHeight : 0, legR ? legR.im.naturalHeight : 0);
    const hk = headScale(id, head0, torso), hH = head0.naturalHeight * hk, th = torso.naturalHeight, tw = torso.naturalWidth;
    const total = 0.92 * hH + th + 0.92 * legH, k = 1000 / total;
    g.save();
    g.scale(k, k);
    g.translate(0, -(th + 0.92 * legH)); // origin: centre of the top of the torso
    // legs swing while walking
    const swing = ch.walk ? (ch.walk > 1 ? 30 : 20) : 0;
    [[legL, -1], [legR, 1]].forEach(([l, s]) => {
      if (!l) return;
      g.save(); g.translate(s * tw * 0.2, th - l.im.naturalHeight * 0.08);
      g.rotate(swing * Math.sin(ch.walkPhase + (s < 0 ? 0 : Math.PI)) * RAD);
      hang(g, l); g.restore();
    });
    g.drawImage(torso, -tw / 2, 0, tw, th);
    // head, with hair that trails behind it
    g.save();
    g.translate(0, 0.08 * hH + Math.sin(t * 2.2 + ch.order + 1) * hH * 0.004);
    g.rotate((ch.headTilt || 0) * RAD);
    const hair = part(id, "hair_back");
    if (hair) { g.save(); g.translate(0, -hH * 0.92); g.rotate(-(ch.sway || 0) * 0.35 * RAD); g.scale(hk, hk); hang(g, { im: hair }); g.restore(); }
    g.drawImage(head, -head.naturalWidth * hk / 2, -head.naturalHeight * hk, head.naturalWidth * hk, head.naturalHeight * hk);
    g.restore();
    // arms: upper arm from the shoulder, forearm from the elbow
    for (const s of [-1, 1]) {
      const up = side(id, "upper_arm", s); if (!up) continue;
      const fore = side(id, "forearm", s), a = ch.arms[s < 0 ? 0 : 1];
      const uw = up.im.naturalWidth, uh = up.im.naturalHeight;
      g.save(); g.translate(s * (tw / 2 - uw * 0.55), uw * 0.35);
      g.rotate(-s * (fore ? a[0] : a[0] + a[1] * 0.5) * RAD);
      hang(g, up);
      if (fore) { g.translate(0, uh * 0.94); g.rotate(-s * a[1] * RAD); hang(g, fore); }
      g.restore();
    }
    g.restore();
    return true;
  }
  return { has, draw, headMul, setHeadMul };
})();
