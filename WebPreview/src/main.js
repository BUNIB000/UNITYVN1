// ===== Boot: frame sizing and the frame loop =====
(() => {
  const frame = document.getElementById("frame");
  const stage = new Stage(document.getElementById("stage"));
  const cover = new Stage(document.createElement("canvas"));
  UI.init(frame);
  Player.init(stage, cover);

  function fit() {
    const vw = window.innerWidth, vh = window.innerHeight;
    let w, h;
    if (vw / vh < 9 / 16) { w = vw; h = Math.min(vh, vw * 20 / 9); }
    else { h = vh - (vw > 520 ? 24 : 0); w = h * 9 / 16; if (w > vw) { w = vw; h = w * 16 / 9; } }
    w = Math.floor(w); h = Math.floor(h);
    frame.style.width = w + "px"; frame.style.height = h + "px";
    frame.style.fontSize = Math.min(w / 360, h / 640) * 16 + "px";
    stage.resize(w, h);
  }
  fit();
  window.addEventListener("resize", () => { fit(); });
  document.addEventListener("pointerdown", () => Audio2.init(), { once: true });

  let last = performance.now(), coverT = 0;
  function frameLoop(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now;
    Clock.tick(dt);
    const t = now / 1000;
    stage.frame(dt * Clock.scale, t);
    UI.tick(dt * Clock.scale);
    if (cover.active && cover.canvas.isConnected) {
      cover.frame(dt, t);
      coverT -= dt; if (coverT <= 0) { coverT = 2.5 + Math.random() * 2; cover.loopGesture && cover.loopGesture(); }
    }
    requestAnimationFrame(frameLoop);
  }
  requestAnimationFrame(frameLoop);
  // fonts affect painted backgrounds: repaint once they arrive
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(() => { Art.clearCache(); fit(); if (!stage.bgName) Player.openLibrary(); });
  Player.openLibrary();
  ArtStore.loadAll().then(() => { stage.resize(stage.W, stage.H); if (document.querySelector(".lib")) Player.openLibrary(); });
})();
