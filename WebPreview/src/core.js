// ===== Story language: parser, expressions, variables, runner (port of the Unity core) =====
const Core = (() => {
  const COMMAND_VERBS = new Set("wait pause zoom pan shake flash fade transition music sound sfx ambience ambient notify weather overlay show hide tint clear vibrate autosave voice textbox bubble camera stop".split(" "));
  const META_KEYS = new Set("title series episode author description cover genre start".split(" "));
  const REACTIONS = { jumps: "jump", nods: "nod", shakes: "shake_head", laughs: "laugh", bounces: "bounce", gasps: "gasp", sighs: "sigh", shivers: "shiver", trembles: "shiver", waves: "wave", cries: "cry", dances: "dance", spins: "spin", shrugs: "shrug", bows: "bow", cheers: "cheer", flinches: "flinch", giggles: "giggle", pouts: "pout", thinks: "think", leans: "lean", stomps: "stomp", hops: "hop", faints: "faint", blushes: "blush" };
  const SCENE = /^(INT\.?\/EXT|EXT\.?\/INT|I\/E|INT|EXT)[.\s]/;
  const SPEAKER = /^([A-Z][A-Z0-9_'.\- ]*?)\s*(?:\(([^)]*)\))?\s*(?:\[([^\]]*)\])?\s*(?::\s*(.*))?$/;
  const SET = /^set\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(\+=|-=|\*=|\/=|=|\+|-|to\b)?\s*(.*)$/i;
  const BRACKET = /\[([^\]]*)\]/g;

  class StoryError extends Error { constructor(line, msg) { super(msg); this.line = line; } }

  function tryFloat(s) {
    if (s == null) return null;
    s = String(s);
    if (s.length > 1 && /s$/i.test(s) && /\d/.test(s[s.length - 2])) s = s.slice(0, -1);
    if (!/^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/.test(s)) return null;
    return parseFloat(s);
  }
  const isNum = s => tryFloat(s) !== null;
  const isIdent = s => /^[A-Za-z_][A-Za-z0-9_.]*$/.test(s);

  function tokenize(line, opts) {
    const words = []; let sb = "", quoted = false, had = false;
    for (let i = 0; i <= line.length; i++) {
      const c = i < line.length ? line[i] : " ";
      if (c === '"' || (quoted && c === "”") || (!quoted && c === "“")) { quoted = !quoted; had = true; continue; }
      if (!quoted && /\s/.test(c)) {
        if (sb.length || had) {
          const eq = sb.indexOf("=");
          if (opts && eq > 0 && eq < sb.length - 1 && !sb.includes("==") && isIdent(sb.slice(0, eq))) opts[sb.slice(0, eq).toLowerCase()] = sb.slice(eq + 1).replace(/^"|"$/g, "");
          else words.push(sb);
        }
        sb = ""; had = false; continue;
      }
      sb += c;
    }
    return words;
  }

  function slug(s) { return s.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, ""); }
  function fmtNum(d) { return Math.abs(d - Math.round(d)) < 1e-9 ? String(Math.round(d)) : String(Math.round(d * 100) / 100); }

  // ---------- variables & expressions ----------
  const toNumber = v => v == null ? 0 : typeof v === "number" ? v : typeof v === "boolean" ? (v ? 1 : 0) : (isNum(v) ? parseFloat(v) : 0);
  const toBool = v => v == null ? false : typeof v === "boolean" ? v : typeof v === "number" ? Math.abs(v) > 1e-9 : (v.length > 0 && v.toLowerCase() !== "false" && v !== "0");
  const toText = v => v == null ? "" : typeof v === "number" ? fmtNum(v) : typeof v === "boolean" ? (v ? "true" : "false") : String(v);

  class Vars {
    constructor() { this.v = {}; }
    get(n) { const k = n.toLowerCase(); return k in this.v ? this.v[k] : 0; }
    has(n) { return n.toLowerCase() in this.v; }
    set(n, val) { this.v[n.toLowerCase()] = val; }
    interpolate(text, resolver) {
      if (!text || text.indexOf("{") < 0) return text;
      let out = "";
      for (let i = 0; i < text.length; i++) {
        const c = text[i];
        if (c === "{" && text[i + 1] === "{") { out += "{"; i++; continue; }
        if (c === "{") {
          const close = text.indexOf("}", i + 1);
          if (close > i) { const name = text.slice(i + 1, close).trim(); const r = resolver && resolver(name); out += r != null ? r : toText(this.get(name)); i = close; continue; }
        }
        out += c;
      }
      return out;
    }
  }

  function parseExpr(src) {
    let p = 0;
    const s = src;
    const space = () => { while (p < s.length && /\s/.test(s[p])) p++; };
    const match = (tok, word) => {
      space();
      if (s.substr(p, tok.length).toLowerCase() !== tok) return false;
      if (word && p + tok.length < s.length && /[A-Za-z0-9_]/.test(s[p + tok.length])) return false;
      p += tok.length; return true;
    };
    const eq = (a, b) => {
      if (typeof a === "string" || typeof b === "string") return toText(a).toLowerCase() === toText(b).toLowerCase();
      if (typeof a === "boolean" || typeof b === "boolean") return toBool(a) === toBool(b);
      return Math.abs(toNumber(a) - toNumber(b)) < 1e-9;
    };
    const bin = (op, l, r) => vars => {
      if (op === "and") return toBool(l(vars)) && toBool(r(vars));
      if (op === "or") return toBool(l(vars)) || toBool(r(vars));
      const a = l(vars), b = r(vars);
      switch (op) {
        case "+": return (typeof a === "string" || typeof b === "string") ? toText(a) + toText(b) : toNumber(a) + toNumber(b);
        case "-": return toNumber(a) - toNumber(b);
        case "*": return toNumber(a) * toNumber(b);
        case "/": { const d = toNumber(b); return Math.abs(d) < 1e-12 ? 0 : toNumber(a) / d; }
        case "%": { const d = toNumber(b); return Math.abs(d) < 1e-12 ? 0 : toNumber(a) % d; }
        case "==": return eq(a, b);
        case "!=": return !eq(a, b);
        case "<": return toNumber(a) < toNumber(b);
        case "<=": return toNumber(a) <= toNumber(b);
        case ">": return toNumber(a) > toNumber(b);
        case ">=": return toNumber(a) >= toNumber(b);
      }
    };
    function or() { let l = and(); while (match("or", true) || match("||")) l = bin("or", l, and()); return l; }
    function and() { let l = not(); while (match("and", true) || match("&&")) l = bin("and", l, not()); return l; }
    function not() {
      if (match("not", true)) { const o = not(); return v => !toBool(o(v)); }
      space();
      if (s[p] === "!" && s[p + 1] !== "=") { p++; const o = not(); return v => !toBool(o(v)); }
      return cmp();
    }
    function cmp() {
      const l = add();
      for (const op of ["==", "!=", "<=", ">=", "<", ">", "="]) if (match(op)) return bin(op === "=" ? "==" : op, l, add());
      if (match("is not", true)) return bin("!=", l, add());
      if (match("is", true)) return bin("==", l, add());
      return l;
    }
    function add() { let l = mul(); for (;;) { if (match("+")) l = bin("+", l, mul()); else if (match("-")) l = bin("-", l, mul()); else return l; } }
    function mul() { let l = un(); for (;;) { if (match("*")) l = bin("*", l, un()); else if (match("/")) l = bin("/", l, un()); else if (match("%")) l = bin("%", l, un()); else return l; } }
    function un() { if (match("-")) { const o = un(); return v => -toNumber(o(v)); } return atom(); }
    function atom() {
      space();
      if (p >= s.length) throw new Error(`Expression '${s}' ends too early`);
      const c = s[p];
      if (c === "(") { p++; const e = or(); if (!match(")")) throw new Error(`Missing ')' in '${s}'`); return e; }
      if (c === '"' || c === "'") { const end = s.indexOf(c, p + 1); if (end < 0) throw new Error(`Missing closing quote in '${s}'`); const lit = s.slice(p + 1, end); p = end + 1; return () => lit; }
      if (/[0-9.]/.test(c)) { const st = p; while (p < s.length && /[0-9.]/.test(s[p])) p++; const n = parseFloat(s.slice(st, p)); return () => n; }
      if (/[A-Za-z_]/.test(c)) {
        const st = p; while (p < s.length && /[A-Za-z0-9_.]/.test(s[p])) p++;
        const name = s.slice(st, p);
        if (name.toLowerCase() === "true") return () => true;
        if (name.toLowerCase() === "false") return () => false;
        return vars => vars.get(name);
      }
      throw new Error(`Unexpected '${c}' in '${s}'`);
    }
    const e = or(); space();
    if (p < s.length) throw new Error(`Unexpected '${s.slice(p)}' in '${s}'`);
    e.source = src;
    return e;
  }

  // ---------- parser ----------
  function firstWord(t) { let k = 0; while (k < t.length && !/\s/.test(t[k]) && !":([".includes(t[k])) k++; return t.slice(0, k); }

  function parse(source, name) {
    const prog = { name: name || "story", ins: [], labels: {}, meta: {}, chars: {}, errors: [] };
    const out = prog.ins;
    const pending = [];
    const err = (n, msg, warn) => prog.errors.push({ line: n.line, msg, src: n.text, warn: !!warn });
    const emit = (ins, n, par) => { ins.line = n.line; ins.parallel = !!par; out.push(ins); return ins; };

    // indentation tree
    const roots = [], stack = [];
    let blank = false;
    source.replace(/\r\n?/g, "\n").split("\n").forEach((raw0, i) => {
      const raw = raw0.replace(/\t/g, "    "), tr = raw.trim();
      if (!tr) { blank = true; return; }
      if (tr.startsWith("#") || tr.startsWith("//") || tr === "{" || tr === "}") return;
      const indent = raw.length - raw.replace(/^\s+/, "").length;
      const node = { line: i + 1, indent, text: tr.replace(/\{$/, "").trim(), blank, children: [] };
      blank = false;
      while (stack.length && stack[stack.length - 1].indent >= indent) stack.pop();
      (stack.length ? stack[stack.length - 1].children : roots).push(node);
      stack.push(node);
    });

    const isSpeakerName = n => n.length > 0 && n.length <= 40 && (n in prog.chars || (n === n.toUpperCase() && /[A-Za-z]/.test(n)));
    const isPlain = t => {
      if (t.startsWith("@") || t.startsWith("&") || SCENE.test(t)) return false;
      const w = firstWord(t).toLowerCase();
      if (w === t.split(" ")[0] && (COMMAND_VERBS.has(w) || ["choice", "if", "set", "goto", "label", "minigame", "input", "next", "end", "call", "return"].includes(w))) return false;
      const m = t.match(SPEAKER);
      if (m && isSpeakerName(m[1].trim()) && (m[2] !== undefined || m[4] !== undefined)) return false;
      return true;
    };
    const X = (n, f) => { try { return f(); } catch (e) { throw new StoryError(n.line, "Couldn't read the condition: " + e.message); } };
    const expr = (n, s) => X(n, () => parseExpr(s));

    function block(nodes) {
      const st = { speaker: null, anim: null, style: null };
      for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].blank) { st.speaker = st.anim = st.style = null; }
        try { i = statement(nodes, i, st); }
        catch (e) { prog.errors.push({ line: e.line || nodes[i].line, msg: e.message, src: nodes[i].text }); }
      }
    }
    function say(n, speaker, anim, style, text) {
      if (text.length >= 2 && text[0] === '"' && text[text.length - 1] === '"') text = text.slice(1, -1);
      emit({ type: "say", speaker, anim: anim || null, style: speaker == null ? (style || "narration") : (style || "speech"), text }, n);
    }
    function statement(nodes, i, st) {
      const n = nodes[i];
      let t = n.text, par = false;
      if (t.startsWith("&")) { par = true; t = t.slice(1).trimStart(); const w = firstWord(t).toLowerCase(); if (!t.startsWith("@") && !["set", "goto", "label"].includes(w)) t = "@" + t; }
      const colon = t.indexOf(":");
      if (colon > 0 && n.indent === 0 && META_KEYS.has(t.slice(0, colon).trim().toLowerCase())) { prog.meta[t.slice(0, colon).trim().toLowerCase()] = t.slice(colon + 1).trim(); return i; }
      if (SCENE.test(t)) { background(n, t, t, par); st.speaker = null; return i; }
      const first = firstWord(t), fl = first.toLowerCase();
      if (t.startsWith("@")) {
        const body = t.slice(1).trimStart(), verb = firstWord(body), rest = body.slice(verb.length).trim(), vk = verb.toLowerCase();
        if (["background", "bg", "scene"].includes(vk)) { background(n, rest, null, par); return i; }
        if (COMMAND_VERBS.has(vk)) { command(n, vk, rest, par); return i; }
        if (vk === "end") { emit({ type: "end" }, n); return i; }
        if (!verb) { err(n, "'@' must be followed by a character name or a command."); return i; }
        if (verb !== verb.toUpperCase() && !(verb in prog.chars)) { err(n, `'${verb}' is not a command I know. Commands are things like @zoom, @music, @pause. Character names are written in CAPITALS, like @${verb.toUpperCase()}.`); return i; }
        character(n, verb.toUpperCase(), rest, par); return i;
      }
      switch (fl) {
        case "character": {
          const opts = {}, w = tokenize(t.slice(first.length), opts);
          if (!w.length) { err(n, "Write the character's name, like: character MAYA name=\"Maya\" color=#ff6699"); return i; }
          const id = w[0].toUpperCase();
          prog.chars[id] = { id, name: opts.name || (w.length > 1 ? w.slice(1).join(" ") : null), opts };
          return i;
        }
        case "label": {
          const l = t.slice(first.length).trim().replace(/:$/, "");
          if (!l) { err(n, "A label needs a name, like 'label coffee_date'."); return i; }
          if (l.toLowerCase() in prog.labels) err(n, `The label '${l}' is used twice.`);
          prog.labels[l.toLowerCase()] = out.length; emit({ type: "label", name: l }, n); return i;
        }
        case "goto": case "jump": { const j = emit({ type: "jump", label: t.slice(first.length).trim() }, n); pending.push(j); return i; }
        case "call": { const c = emit({ type: "call", label: t.slice(first.length).trim() }, n); pending.push(c); return i; }
        case "return": emit({ type: "return" }, n); return i;
        case "end": emit({ type: "end" }, n); return i;
        case "next": {
          const w = tokenize(t.slice(first.length));
          if (!w.length) { err(n, "'next' needs the name of the story to continue with."); return i; }
          emit({ type: "next", story: w[0], label: w.length > 1 ? w[w.length - 1] : null }, n); return i;
        }
        case "set": {
          const m = t.match(SET);
          if (!m) { err(n, "Write 'set' like: set trust + 1, set met_maya = true, set name = \"Alex\""); return i; }
          let op = (m[2] || "").toLowerCase(), ex = m[3].trim();
          if (op === "") { op = "="; if (!ex) ex = "true"; } else if (op === "to") op = "="; else if (op === "+") op = "+="; else if (op === "-") op = "-=";
          if (!ex) { err(n, `'set ${m[1]} ${op}' needs a value.`); return i; }
          emit({ type: "set", variable: m[1], op, value: expr(n, ex) }, n); return i;
        }
        case "input": {
          const opts = {}, w = tokenize(t.slice(first.length), opts);
          if (!w.length) { err(n, "input needs a variable name, like: input player_name \"What's your name?\""); return i; }
          emit({ type: "input", variable: w[0], prompt: w.length > 1 ? w[1] : "Enter a name", def: opts.default || null }, n); return i;
        }
        case "choice": choice(n, t.slice(first.length)); st.speaker = null; return i;
        case "if": return ifBlock(nodes, i);
        case "elif": case "else": err(n, `'${fl}' must come right after an 'if' block at the same indentation.`); return i;
        case "minigame": minigame(n, t.slice(first.length)); return i;
      }
      if (COMMAND_VERBS.has(fl) && first === fl) { command(n, fl, t.slice(first.length).trim(), par); return i; }
      if (n.children.length) err(n, "This line has indented lines under it, but it isn't a choice, if, or minigame. The indented lines will play normally.", true);
      const m = t.match(SPEAKER);
      if (m && isSpeakerName(m[1].trim())) {
        const nm = m[1].trim(), a = m[2] !== undefined ? m[2].trim() : null, sty = m[3] !== undefined ? m[3].trim().toLowerCase() : null, inline = m[4] !== undefined ? m[4].trim() : null;
        const nextIsText = i + 1 < nodes.length && !nodes[i + 1].blank && isPlain(nodes[i + 1].text);
        if (inline || nextIsText || nm === "NARRATOR" || nm in prog.chars) {
          st.speaker = nm === "NARRATOR" ? null : nm; st.anim = a; st.style = sty;
          if (inline) say(n, st.speaker, st.anim, st.style, inline);
          if (n.children.length) block(n.children);
          return i;
        }
      }
      say(n, st.speaker, st.anim, st.style, t);
      st.anim = null;
      if (n.children.length) block(n.children);
      return i;
    }
    function background(n, text, heading, par) {
      const ins = { type: "background", transition: null, duration: null, zone: null };
      const apply = words => {
        for (let k = 0; k < words.length; k++) {
          const w = words[k].toLowerCase();
          if (w === "in" || w === "for") continue;
          if (w === "zone" && k + 1 < words.length && /^\d+$/.test(words[k + 1])) { ins.zone = +words[++k]; continue; }
          const d = tryFloat(w); if (d !== null) { ins.duration = d; continue; }
          ins.transition = w;
        }
      };
      text.replace(BRACKET, (_, tag) => { apply(tokenize(tag)); return ""; });
      text = text.replace(BRACKET, "").trim();
      const wm = text.match(/\swith\s/i);
      if (wm) { apply(tokenize(text.slice(wm.index + 5))); text = text.slice(0, wm.index).trim(); }
      else if (heading == null) { const w = tokenize(text); if (w.length > 1) { text = w[0]; apply(w.slice(1)); } }
      ins.name = text.trim();
      ins.heading = heading != null;
      if (!ins.name) { err(n, "Which background? For example: @background coffee_shop"); return; }
      emit(ins, n, par);
    }
    function command(n, verb, rest, par) {
      const ins = { type: "command", verb: verb === "pause" ? "wait" : verb === "sfx" ? "sound" : verb === "ambient" ? "ambience" : verb, opts: {} };
      ins.args = tokenize(rest, ins.opts);
      for (let k = 0; k < ins.args.length; k++) {
        const w = ins.args[k].toLowerCase();
        if ((w === "in" || w === "for" || w === "over") && k + 1 < ins.args.length && isNum(ins.args[k + 1])) { ins.opts.time = ins.args[k + 1]; ins.args.splice(k, 2); k--; }
      }
      emit(ins, n, par);
    }
    function character(n, name, rest, par) {
      if (!rest) { err(n, `What should ${name} do? For example: @${name} enters from left`); return; }
      const ins = { type: "character", character: name, actions: [] };
      for (const part of rest.split(/\s+(?:and|then)\s+/i)) { const w = tokenize(part); if (w.length) ins.actions.push(action(n, name, w)); }
      emit(ins, n, par);
    }
    const peek = (w, k) => k < w.length ? w[k].toLowerCase() : null;
    const need = (n, w, k, what) => { if (k < w.length) return w[k]; throw new StoryError(n.line, `'${w.join(" ")}' needs ${what}.`); };
    const pint = (n, s) => { if (s != null && /^-?\d+$/.test(s)) return +s; throw new StoryError(n.line, `'${s}' should be a whole number.`); };
    const pfloat = (n, s, help) => { const f = tryFloat(s); if (f !== null) return f; throw new StoryError(n.line, help || `'${s}' should be a number.`); };
    function position(n, w, ref) {
      const pos = {};
      let word = peek(w, ref.k);
      if (word == null) throw new StoryError(n.line, "Where to? For example: left, center, right, or spot 1 160 0");
      if (word === "screen" || word === "the") { ref.k++; word = peek(w, ref.k); }
      if (word === "spot") {
        ref.k++;
        pos.scale = pfloat(n, peek(w, ref.k++), "spot needs: scale x y, like spot 1.0 160 0");
        pos.x = pfloat(n, peek(w, ref.k++), "spot needs: scale x y, like spot 1.0 160 0");
        pos.y = isNum(peek(w, ref.k)) ? pfloat(n, peek(w, ref.k++)) : 0;
        return pos;
      }
      if (isNum(word)) { pos.x = pfloat(n, word); ref.k++; if (isNum(peek(w, ref.k)) && peek(w, ref.k - 2) !== "in") { pos.y = pfloat(n, peek(w, ref.k)); ref.k++; } return pos; }
      if (word === "off" || word === "offscreen" || word === "far") { pos.named = (word === "far" ? "far_" : "offscreen_") + peek(w, ref.k + 1); ref.k += 2; return pos; }
      if (word === "next" && peek(w, ref.k + 1) === "to") { pos.nextTo = w[ref.k + 2].toUpperCase(); ref.k += 3; return pos; }
      if (word === "middle" || word === "centre") word = "center";
      if (["left", "right", "center", "top", "bottom"].includes(word) || word.startsWith("offscreen_") || word.startsWith("far_")) { pos.named = word; ref.k++; return pos; }
      if (w[ref.k] === w[ref.k].toUpperCase() && /[A-Za-z]/.test(w[ref.k])) { pos.nextTo = w[ref.k]; ref.k++; return pos; }
      throw new StoryError(n.line, `'${w[ref.k]}' isn't a place on screen. Use left, center, right, far left, offscreen right, a character's name, or spot <scale> <x> <y>.`);
    }
    function action(n, name, w) {
      const verb = w[0].toLowerCase(), a = { kind: null }, ref = { k: 1 };
      const P = o => peek(w, ref.k + (o || 0));
      switch (verb) {
        case "enters": case "enter": case "comes":
          a.kind = "enter";
          if (P() === "in") ref.k++;
          if (P() === "from") { a.value = P(1) || "left"; ref.k += 2; } else a.value = "fade";
          if (P() === "screen") { a.value = P(1); ref.k += 2; }
          if (P() === "to") { ref.k++; a.pos = position(n, w, ref); }
          break;
        case "exits": case "exit": case "leaves": case "leave":
          a.kind = "exit";
          if (["to", "toward", "towards"].includes(P())) ref.k++;
          if (P() === "screen") ref.k++;
          if (P() != null && P() !== "in") { a.value = P(); ref.k++; }
          break;
        case "walks": case "walk": case "runs": case "run": case "moves": case "move": case "slides": case "goes":
          a.kind = verb.startsWith("walk") || verb === "goes" ? "walk" : verb.startsWith("run") ? "run" : "move";
          if (P() === "to" || P() === "over") ref.k++;
          if (P() === "layer") { a.kind = "layer"; a.layer = pint(n, P(1)); ref.k += 2; break; }
          a.pos = position(n, w, ref);
          break;
        case "spot": a.kind = "spot"; ref.k = 0; a.pos = position(n, w, ref); break;
        case "stands": case "stand": case "sits": case "waits":
          a.kind = "spot";
          if (P() === "at" || (P() === "in" && P(1) !== "zone")) ref.k++;
          a.pos = P() == null ? { named: "center" } : position(n, w, ref);
          break;
        case "is": case "does": case "plays": case "starts": a.kind = "animate"; a.value = need(n, w, ref.k++, "an animation name, like talk_happy"); break;
        case "looks": case "feels":
          a.kind = "look";
          if (P() === "at") { a.kind = "face"; ref.k++; }
          a.value = need(n, w, ref.k++, "an expression, like happy"); break;
        case "faces": case "turns": case "face":
          a.kind = "face";
          if (["to", "toward", "towards"].includes(P())) ref.k++;
          a.value = need(n, w, ref.k++, "left, right, or another character"); break;
        case "wears": case "wear": case "changes": case "puts":
          a.kind = "wear";
          if (P() === "into" || P() === "on") ref.k++;
          if (w.length - ref.k >= 2 && P(1) !== "in") { a.slot = w[ref.k]; a.value = w[ref.k + 1]; ref.k += 2; }
          else a.value = need(n, w, ref.k++, "an outfit name");
          break;
        case "appears": case "appear":
          a.kind = "appear";
          if (P() === "at" || P() === "in") { if (P() === "at") ref.k++; if (P() !== "in" || !isNum(P(1))) a.pos = position(n, w, ref); }
          break;
        case "disappears": case "disappear": case "vanishes": a.kind = "disappear"; break;
        case "layer": a.kind = "layer"; a.layer = pint(n, need(n, w, ref.k++, "a layer number")); break;
        default:
          if (REACTIONS[verb]) { a.kind = "animate"; a.value = REACTIONS[verb]; break; }
          throw new StoryError(n.line, `I don't know how to make ${name} '${w[0]}'. Try enters, exits, walks to, moves to, stands, spot, is <animation>, looks <expression>, faces, wears, appears, or disappears.`);
      }
      for (let k = ref.k; k < w.length; k++) {
        const word = w[k].toLowerCase();
        if ((word === "in" || word === "over" || word === "for") && isNum(peek(w, k + 1))) { a.duration = tryFloat(w[k + 1]); k++; }
        else if (word === "in" && peek(w, k + 1) === "zone") { (a.pos = a.pos || {}).zone = pint(n, peek(w, k + 2)); k += 2; }
        else if (word === "zone") { (a.pos = a.pos || {}).zone = pint(n, peek(w, k + 1)); k++; }
        else if (word === "at" && peek(w, k + 1) === "layer") { a.layer = pint(n, peek(w, k + 2)); k += 2; }
        else if (word === "layer") { a.layer = pint(n, peek(w, k + 1)); k++; }
        else if (word === "slowly") a.duration = (a.duration || 1) * 1.6;
        else if (word === "quickly") a.duration = (a.duration || 1) * 0.5;
        else if (word === "loop" || word === "looping") a.value += "_loop";
        else throw new StoryError(n.line, `I don't understand '${w[k]}' in '${w.join(" ")}'.`);
      }
      return a;
    }
    function choice(n, rest) {
      const ins = emit({ type: "choice", prompt: null, timer: null, options: [] }, n);
      rest.replace(BRACKET, (_, tag) => { const tw = tokenize(tag); if (tw.length > 1 && (tw[0] === "timer" || tw[0] === "timed") && isNum(tw[1])) ins.timer = tryFloat(tw[1]); return ""; });
      rest = rest.replace(BRACKET, "");
      const opts = {}, words = tokenize(rest, opts);
      for (let k = 0; k < words.length; k++) {
        if ((words[k] === "timed" || words[k] === "timer") && k + 1 < words.length && isNum(words[k + 1])) { ins.timer = tryFloat(words[k + 1]); k++; }
        else ins.prompt = ins.prompt == null ? words[k] : ins.prompt + " " + words[k];
      }
      if (opts.timer && isNum(opts.timer)) ins.timer = tryFloat(opts.timer);
      if (!n.children.length) err(n, "A choice needs options under it, each in quotes and indented.");
      const ends = [];
      for (const c of n.children) {
        const ct = c.text;
        if (!ct.startsWith('"') && !ct.startsWith("“")) { prog.errors.push({ line: c.line, msg: "Each choice option must start with a quote, like: \"Order a latte\"", src: ct }); continue; }
        let close = -1; for (let q = 1; q < ct.length; q++) if (ct[q] === '"' || ct[q] === "”") { close = q; break; }
        if (close < 0) { prog.errors.push({ line: c.line, msg: "This option is missing its closing quote.", src: ct }); continue; }
        const o = { text: ct.slice(1, close), line: c.line, cond: null, locked: false, isDefault: false, tag: null };
        const tail = ct.slice(close + 1);
        tail.replace(BRACKET, (_, tag0) => {
          const tag = tag0.trim(), tl = tag.toLowerCase();
          if (tl.startsWith("if ")) o.cond = expr(c, tag.slice(3));
          else if (tl.startsWith("locked if ") || tl.startsWith("requires ")) { o.cond = expr(c, tag.slice(tag.indexOf(" ", tl.startsWith("locked") ? 7 : 0) + 1)); o.locked = true; }
          else if (tl === "default") o.isDefault = true;
          else o.tag = tag;
          return "";
        });
        const arrow = tail.indexOf("->");
        o.target = out.length;
        ins.options.push(o);
        if (c.children.length) block(c.children);
        if (arrow >= 0) pending.push(emit({ type: "jump", label: tail.slice(arrow + 2).replace(BRACKET, "").trim() }, c));
        else ends.push(emit({ type: "jump", target: -1 }, c));
      }
      ins.end = out.length;
      ends.forEach(j => j.target = ins.end);
    }
    function ifBlock(nodes, i) {
      const ends = []; let pend = null, last = i;
      for (let k = i; k < nodes.length; k++) {
        const n = nodes[k], fw = firstWord(n.text), word = fw.toLowerCase(), rest = n.text.slice(fw.length).trim();
        let cond;
        if (k === i || word === "elif") cond = rest;
        else if (word === "else" && /^if /i.test(rest)) cond = rest.slice(3);
        else if (word === "else") cond = null;
        else break;
        last = k;
        if (pend) pend.falseTarget = out.length;
        pend = null;
        if (cond == null) { block(n.children); break; }
        cond = cond.trim().replace(/:$/, "").trim();
        if (!cond) { err(n, "'if' needs a condition, like: if trust >= 3"); continue; }
        pend = emit({ type: "branch", cond: expr(n, cond) }, n);
        if (!n.children.length) err(n, "This 'if' has nothing indented under it.", true);
        block(n.children);
        ends.push(emit({ type: "jump", target: -1 }, n));
      }
      if (pend) pend.falseTarget = out.length;
      ends.forEach(j => j.target = out.length);
      return last;
    }
    function minigame(n, rest) {
      const ins = { type: "minigame", params: {}, data: [], winTarget: -1, loseTarget: -1 };
      const w = tokenize(rest, ins.params);
      if (!w.length) { err(n, "Which minigame? For example: minigame memory pairs=6"); return; }
      ins.game = w[0].toLowerCase();
      for (let k = 1; k < w.length; k++) ins.params["arg" + k] = w[k];
      emit(ins, n);
      const ends = [];
      const flat = (node, d) => { ins.data.push(" ".repeat(d * 4) + node.text); node.children.forEach(c => flat(c, d + 1)); };
      for (const c of n.children) {
        const wd = firstWord(c.text).toLowerCase().replace(/:$/, "");
        if (["win", "won", "success", "lose", "lost", "fail"].includes(wd) && c.text.replace(/:$/, "").trim().length === wd.length) {
          if (wd[0] === "w" || wd === "success") ins.winTarget = out.length; else ins.loseTarget = out.length;
          block(c.children);
          ends.push(emit({ type: "jump", target: -1 }, c));
        } else flat(c, 0);
      }
      ins.end = out.length;
      ends.forEach(j => j.target = ins.end);
    }

    block(roots);
    out.push({ type: "end", line: roots.length ? roots[roots.length - 1].line : 1 });
    for (const j of pending) {
      const t = prog.labels[j.label.toLowerCase()];
      if (t === undefined) prog.errors.push({ line: j.line, msg: `There is no label called '${j.label}'. Add a line 'label ${j.label}' somewhere in this story.` });
      else j.target = t;
    }
    prog.errors.sort((a, b) => a.line - b.line);
    return prog;
  }

  // ---------- runner ----------
  class Runner {
    constructor(prog, vars) { this.vars = vars || new Vars(); this.load(prog); }
    load(prog, label) {
      this.prog = prog; this.stack = []; this.pc = 0; this.cur = 0; this.finished = false;
      label = label || prog.meta.start;
      if (label) this.goto(label);
    }
    goto(label) {
      const t = this.prog.labels[label.toLowerCase()];
      if (t === undefined) throw new StoryError(0, `There is no label called '${label}' in ${this.prog.name}.`);
      this.pc = t;
    }
    next() {
      const I = this.prog.ins;
      for (let guard = 0; guard < 100000; guard++) {
        if (this.pc < 0 || this.pc >= I.length) { this.finished = true; return { type: "end" }; }
        const ins = I[this.pc];
        switch (ins.type) {
          case "label": this.pc++; continue;
          case "jump": this.pc = ins.target; continue;
          case "branch": this.pc = toBool(ins.cond(this.vars)) ? this.pc + 1 : ins.falseTarget; continue;
          case "call": this.stack.push(this.pc + 1); this.pc = ins.target; continue;
          case "return": if (!this.stack.length) { this.finished = true; this.cur = this.pc; return { type: "end", line: ins.line }; } this.pc = this.stack.pop(); continue;
          case "set": this.applySet(ins); this.pc++; continue;
          case "end": this.finished = true; this.cur = this.pc; return ins;
          case "choice": case "minigame": case "input": this.cur = this.pc; return ins;
          default: this.cur = this.pc; this.pc++; return ins;
        }
      }
      throw new StoryError(I[Math.min(this.pc, I.length - 1)].line, "The story is stuck in a loop that never shows anything. Check your goto lines.");
    }
    applySet(s) {
      const value = s.value(this.vars), V = this.vars;
      if (s.op === "=") { V.set(s.variable, value); return; }
      const cur = toNumber(V.get(s.variable)), v = toNumber(value);
      switch (s.op) {
        case "+=": if (typeof value === "string" && typeof V.get(s.variable) !== "number") V.set(s.variable, toText(V.get(s.variable)) + value); else V.set(s.variable, cur + v); break;
        case "-=": V.set(s.variable, cur - v); break;
        case "*=": V.set(s.variable, cur * v); break;
        case "/=": V.set(s.variable, Math.abs(v) < 1e-12 ? 0 : cur / v); break;
      }
    }
    visible(ch) {
      const list = [];
      for (const o of ch.options) {
        const ok = !o.cond || toBool(o.cond(this.vars));
        if (ok) list.push({ o, enabled: true }); else if (o.locked) list.push({ o, enabled: false });
      }
      return list;
    }
    choose(ch, o) { this.vars.set("last_choice", o.text); this.pc = o.target; }
    resolveMinigame(g, won, score) {
      this.vars.set("minigame_won", won); this.vars.set("minigame_score", score);
      if (g.params.as) { this.vars.set(g.params.as + "_won", won); this.vars.set(g.params.as + "_score", score); }
      const t = won ? g.winTarget : g.loseTarget;
      this.pc = t >= 0 ? t : g.end;
    }
    submitInput(inp, value) {
      if (!value || !value.trim()) value = inp.def || "";
      this.vars.set(inp.variable, value.trim());
      this.pc = this.cur + 1;
    }
  }

  return { parse, Runner, Vars, tokenize, tryFloat, isNum, slug, toNumber, toBool, toText, StoryError, parseExpr };
})();
