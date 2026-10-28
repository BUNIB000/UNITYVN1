using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>
    /// Memory: flip cards to find matching pairs.
    /// minigame memory pairs=6 time=60
    /// Skin: card_back, face_1 ... face_N (any names starting with "face"), background
    /// </summary>
    [Minigame("memory", "pairs", "match", "cards")]
    public sealed class MemoryGame : Minigame
    {
        public override string DefaultTitle => "Memory";
        public override string DefaultInstructions => "Flip two cards at a time and find all the pairs.";
        protected override float DefaultTime => 60f;

        sealed class Card
        {
            public int Kind;
            public RectTransform Rt;
            public Image Face, Back;
            public bool Open, Matched;
        }

        protected override IEnumerator Play()
        {
            int pairs = Mathf.Clamp(Ctx.GetInt("pairs", Mathf.RoundToInt(Ctx.Difficulty(4, 6, 8))), 2, 12);
            var faces = Ctx.SpritesWithPrefix("face");
            var back = Ctx.Sprite("card_back");
            var kinds = Enumerable.Range(0, pairs).SelectMany(k => new[] { k, k }).OrderBy(_ => Ctx.Next(1000)).ToList();
            int cols = pairs * 2 <= 8 ? 3 : 4;
            if (pairs * 2 == 8) cols = 4;
            int rows = Mathf.CeilToInt(kinds.Count / (float)cols);
            float gap = 24;
            float cw = Mathf.Min((Width - gap * (cols + 1)) / cols, (Height - gap * (rows + 1)) / rows / 1.3f);
            float ch = cw * 1.3f;

            var cards = new List<Card>();
            Card first = null;
            bool busy = false;
            int matched = 0, moves = 0;
            for (int i = 0; i < kinds.Count; i++)
            {
                int r = i / cols, c = i % cols;
                int inRow = Mathf.Min(cols, kinds.Count - r * cols);
                float x = (c - (inRow - 1) / 2f) * (cw + gap);
                float y = ((rows - 1) / 2f - r) * (ch + gap);
                var card = new Card { Kind = kinds[i] };
                var holder = UIKit.Rect("Card " + i, Board, false);
                UIKit.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(cw, ch));
                card.Rt = holder;
                var faceBg = UIKit.Image(holder, "FaceBg", ProceduralArt.RoundedRect(26), Color.white);
                card.Face = UIKit.Image(faceBg.transform, "Face", faces.Count > 0 ? faces[card.Kind % faces.Count] : Symbol(card.Kind), faces.Count > 0 ? Color.white : Palette[card.Kind % Palette.Length]);
                card.Face.preserveAspect = true;
                UIKit.Stretch(card.Face.rectTransform, cw * 0.14f);
                var btn = UIKit.Button(holder, "Back", null, Theme.accentColor, Color.white, null, 30, 26);
                card.Back = btn.image;
                if (back != null) { btn.image.sprite = back; btn.image.color = Color.white; btn.image.type = Image.Type.Simple; }
                else
                {
                    var mark = UIKit.Image(btn.transform, "Mark", ProceduralArt.Star(), new Color(1, 1, 1, 0.35f));
                    UIKit.Stretch(mark.rectTransform, cw * 0.25f);
                }
                faceBg.gameObject.SetActive(false);
                var cc = card;
                btn.onClick.AddListener(() =>
                {
                    if (busy || cc.Open || cc.Matched || TimedOut) return;
                    StartCoroutine(Flip(cc, true));
                    if (first == null) { first = cc; return; }
                    moves++;
                    var a = first;
                    first = null;
                    StartCoroutine(Resolve(a, cc));
                });
                cards.Add(card);
                holder.localScale = Vector3.zero;
                StartCoroutine(DelayedPop(holder, i * 0.03f));
            }

            IEnumerator Resolve(Card a, Card b)
            {
                busy = true;
                yield return Tween.Wait(0.45f);
                if (a.Kind == b.Kind)
                {
                    a.Matched = b.Matched = true;
                    matched++;
                    Ctx.PlaySound("match", "success", 0.5f);
                    StartCoroutine(Bounce(a.Rt));
                    StartCoroutine(Bounce(b.Rt));
                    SetScore($"{matched} / {pairs}");
                }
                else
                {
                    Ctx.PlaySound("miss", "click", 0.6f);
                    StartCoroutine(Shake(a.Rt, 10));
                    yield return Shake(b.Rt, 10);
                    StartCoroutine(Flip(a, false));
                    yield return Flip(b, false);
                }
                busy = false;
            }

            SetScore($"0 / {pairs}");
            while (matched < pairs && !TimedOut) yield return null;
            Ctx.Won = matched == pairs;
            Ctx.Score = Ctx.Won ? Mathf.Max(1, Mathf.RoundToInt(TimeLeft)) : matched;
            yield return Tween.Wait(0.3f);
        }

        IEnumerator DelayedPop(RectTransform rt, float d)
        {
            yield return Tween.Wait(d);
            yield return Tween.Scale(rt, Vector3.one, 0.3f, Ease.OutBack);
        }

        IEnumerator Flip(Card c, bool open)
        {
            c.Open = open;
            AudioDirector.Instance?.PlaySound("click", 0.5f, open ? 1.2f : 0.9f);
            yield return Tween.Run(0.12f, Ease.InQuad, k => c.Rt.localScale = new Vector3(1 - k, 1, 1));
            c.Face.transform.parent.gameObject.SetActive(open);
            c.Back.gameObject.SetActive(!open);
            yield return Tween.Run(0.15f, Ease.OutBack, k => c.Rt.localScale = new Vector3(k, 1, 1));
        }
    }

    /// <summary>
    /// Sliding picture puzzle.
    /// minigame slide size=3 image=old_photo time=120
    /// Skin: image (or image= any picture in Images/Backgrounds), background
    /// </summary>
    [Minigame("slide", "sliding", "puzzle", "jigsaw")]
    public sealed class SlidePuzzleGame : Minigame
    {
        public override string DefaultTitle => "Picture Puzzle";
        public override string DefaultInstructions => "Slide the tiles to put the picture back together.";
        protected override float DefaultTime => 150f;

        protected override IEnumerator Play()
        {
            int n = Mathf.Clamp(Ctx.GetInt("size", Mathf.RoundToInt(Ctx.Difficulty(3, 3, 4))), 2, 6);
            var picture = Ctx.Sprite("image");
            if (picture == null && Ctx.Get("image") != null) picture = AssetLibrary.Image(Ctx.Get("image"));
            float size = Mathf.Min(Width - 40, Height - 300);
            float tile = size / n;
            var frame = UIKit.Image(Board, "Frame", ProceduralArt.RoundedRect(30), new Color(1, 1, 1, 0.12f));
            UIKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(size + 30, size + 30));

            // preview
            if (picture != null)
            {
                var prev = UIKit.Image(Board, "Preview", picture, new Color(1, 1, 1, 0.9f));
                prev.preserveAspect = true;
                UIKit.Place(prev.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(220, 220));
            }

            int empty = n * n - 1;
            var positions = Enumerable.Range(0, n * n).ToArray(); // positions[tile] = cell
            var tiles = new RectTransform[n * n];
            int moves = 0;
            bool solved = false;

            Vector2 CellPos(int cell) => new Vector2((cell % n - (n - 1) / 2f) * tile, ((n - 1) / 2f - cell / n) * tile + 60);

            for (int t = 0; t < n * n - 1; t++)
            {
                int tt = t;
                var btn = UIKit.Button(Board, "Tile " + t, null, Color.white, Color.white, () => TryMove(tt), 40, 12);
                var rt = (RectTransform)btn.transform;
                UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), CellPos(t), new Vector2(tile - 8, tile - 8));
                if (picture != null)
                {
                    var tex = picture.texture;
                    var r = picture.textureRect;
                    float side = Mathf.Min(r.width, r.height);
                    float ox = r.x + (r.width - side) / 2, oy = r.y + (r.height - side) / 2;
                    float step = side / n;
                    int col = t % n, row = t / n;
                    var sub = Sprite.Create(tex, new Rect(ox + col * step, oy + (n - 1 - row) * step, step, step), new Vector2(0.5f, 0.5f), 100f);
                    var img = UIKit.Image(rt, "Piece", sub, Color.white);
                    UIKit.Stretch(img.rectTransform, 4);
                    btn.image.color = new Color(1, 1, 1, 0.9f);
                }
                else
                {
                    btn.image.color = Color.Lerp(Theme.accentColor, Palette[(t / n) % Palette.Length], 0.35f);
                    var label = UIKit.Text(rt, "Num", (t + 1).ToString(), tile * 0.42f, Color.white, TextAlignmentOptions.Center, true);
                }
                tiles[t] = rt;
            }

            void TryMove(int t)
            {
                if (solved || TimedOut) return;
                int cell = positions[t];
                int er = empty / n, ec = empty % n, r = cell / n, c = cell % n;
                if (Mathf.Abs(er - r) + Mathf.Abs(ec - c) != 1) { StartCoroutine(Shake(tiles[t], 8, 0.2f)); return; }
                positions[t] = empty;
                empty = cell;
                moves++;
                AudioDirector.Instance?.PlaySound("click", 0.5f);
                StartCoroutine(Tween.Anchored(tiles[t], CellPos(positions[t]), 0.12f, Ease.OutCubic));
                SetScore(moves + " moves");
                bool ok = true;
                for (int i = 0; i < n * n - 1; i++) if (positions[i] != i) { ok = false; break; }
                solved = ok;
            }

            // Shuffle with random legal moves so it's always solvable.
            int last = -1;
            for (int s = 0; s < n * n * 25; s++)
            {
                var options = new List<int>();
                for (int t = 0; t < n * n - 1; t++)
                {
                    int cell = positions[t];
                    if (Mathf.Abs(cell / n - empty / n) + Mathf.Abs(cell % n - empty % n) == 1 && t != last) options.Add(t);
                }
                int pick = options[Ctx.Next(options.Count)];
                int pc = positions[pick];
                positions[pick] = empty;
                empty = pc;
                last = pick;
            }
            for (int t = 0; t < n * n - 1; t++) StartCoroutine(Tween.Anchored(tiles[t], CellPos(positions[t]), 0.4f, Ease.InOutCubic));
            SetScore("0 moves");
            yield return Tween.Wait(0.45f);

            while (!solved && !TimedOut) yield return null;
            Ctx.Won = solved;
            Ctx.Score = solved ? Mathf.Max(1, 500 - moves) : 0;
            if (solved) foreach (var t in tiles) if (t != null) StartCoroutine(Bounce(t, 1.05f));
        }
    }

    /// <summary>
    /// Hidden object: find every item in the scene.
    /// minigame hidden items=key,ring,letter time=45 background=bedroom
    /// Place items yourself with key@0.2,0.7 (x,y from 0 to 1), or leave them random.
    /// Skin: one sprite per item name, background
    /// </summary>
    [Minigame("hidden", "find", "search", "hiddenobject")]
    public sealed class HiddenObjectGame : Minigame
    {
        public override string DefaultTitle => "Search the Room";
        public override string DefaultInstructions => "Find all the hidden items before time runs out.";
        protected override float DefaultTime => 45f;

        protected override IEnumerator Play()
        {
            var specs = Ctx.GetList("items");
            foreach (var line in Ctx.Data) if (line.Trim().Length > 0) specs.Add(line.Trim());
            if (specs.Count == 0) specs.AddRange(new[] { "key", "ring", "letter" });
            float itemSize = Ctx.GetFloat("size", Ctx.Difficulty(170, 130, 95));

            var field = UIKit.Rect("Field", Board);
            field.offsetMin = new Vector2(0, 200);
            var list = UIKit.Rect("List", Board, false);
            UIKit.Place(list, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(Width, 170));
            UIKit.Image(list, "Bg", ProceduralArt.RoundedRect(40), new Color(0, 0, 0, 0.45f));

            int found = 0;
            var labels = new Dictionary<string, TextMeshProUGUI>();
            float slotW = Width / specs.Count;
            for (int i = 0; i < specs.Count; i++)
            {
                string spec = specs[i];
                string name = spec;
                Vector2? at = null;
                int atIdx = spec.IndexOf('@');
                if (atIdx > 0)
                {
                    name = spec.Substring(0, atIdx).Trim();
                    var parts = spec.Substring(atIdx + 1).Split(',');
                    if (parts.Length == 2 && Util.TryFloat(parts[0], out var px) && Util.TryFloat(parts[1], out var py)) at = new Vector2(px, py);
                }
                string pretty = name.Replace('_', ' ');
                var label = UIKit.Text(list, "Item " + name, pretty, 36, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(slotW * (i + 0.5f), 0), new Vector2(slotW - 10, 150));
                label.enableAutoSizing = true;
                label.fontSizeMin = 20;
                label.fontSizeMax = 38;
                labels[name] = label;

                var sprite = Ctx.Sprite(name);
                var fw = field.rect.width; var fh = field.rect.height;
                Vector2 pos = at.HasValue
                    ? new Vector2((at.Value.x - 0.5f) * fw, (at.Value.y - 0.5f) * fh)
                    : new Vector2(Ctx.Range(-fw / 2 + itemSize, fw / 2 - itemSize), Ctx.Range(-fh / 2 + itemSize, fh / 2 - itemSize));
                var btn = UIKit.Button(field, name, null, Color.white, Color.white, null, 30, 30);
                var rt = (RectTransform)btn.transform;
                UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(itemSize, itemSize));
                if (sprite != null) { btn.image.sprite = sprite; btn.image.type = Image.Type.Simple; btn.image.preserveAspect = true; btn.image.color = new Color(1, 1, 1, Ctx.GetFloat("visibility", 1f)); }
                else
                {
                    btn.image.sprite = Symbol(i);
                    btn.image.type = Image.Type.Simple;
                    btn.image.color = Color.Lerp(Palette[i % Palette.Length], new Color(0.5f, 0.5f, 0.55f), 0.55f);
                    rt.localRotation = Quaternion.Euler(0, 0, Ctx.Range(-40, 40));
                }
                // Decoys make it a search, not a tap-the-only-thing game.
                if (sprite == null)
                    for (int d = 0; d < 3; d++)
                    {
                        var decoy = UIKit.Image(field, "Decoy", Symbol(i + d + 3), Color.Lerp(Palette[(i + d + 5) % Palette.Length], new Color(0.5f, 0.5f, 0.55f), 0.6f));
                        UIKit.Place(decoy.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Ctx.Range(-fw / 2 + 80, fw / 2 - 80), Ctx.Range(-fh / 2 + 80, fh / 2 - 80)), Vector2.one * itemSize * Ctx.Range(0.6f, 1.1f));
                        decoy.rectTransform.SetAsFirstSibling();
                    }
                string n2 = name;
                btn.onClick.AddListener(() =>
                {
                    if (TimedOut || !btn.interactable) return;
                    btn.interactable = false;
                    found++;
                    Ctx.PlaySound("found", "success", 0.5f);
                    FloatText(rt.anchoredPosition + new Vector2(0, 200), "Found!", Theme.accentColor);
                    labels[n2].text = "<s>" + labels[n2].text + "</s>";
                    labels[n2].color = Theme.accentColor;
                    StartCoroutine(Collect(rt));
                    SetScore($"{found} / {specs.Count}");
                });
            }
            // Missed taps on the scene cost a little time.
            var miss = TapArea.Create(field, "Miss Area");
            miss.transform.SetAsFirstSibling();
            miss.Tapped += () => { TimeLeft -= 1.5f; Ctx.PlaySound("miss", "click", 0.4f); };

            SetScore($"0 / {specs.Count}");
            while (found < specs.Count && !TimedOut) yield return null;
            Ctx.Won = found == specs.Count;
            Ctx.Score = found;
        }

        IEnumerator Collect(RectTransform rt)
        {
            yield return Tween.Scale(rt, Vector3.one * 1.4f, 0.15f, Ease.OutQuad);
            var g = UIKit.Group(rt.gameObject);
            yield return Tween.All(this, Tween.Fade(g, 0, 0.3f), Tween.Scale(rt, Vector3.one * 0.2f, 0.3f, Ease.InBack));
        }
    }

    /// <summary>
    /// Word scramble: tap the letters in the right order.
    /// minigame scramble word=COFFEE hint="Maya's favorite drink" time=40
    /// Skin: tile, slot
    /// </summary>
    [Minigame("scramble", "word", "anagram", "unscramble")]
    public sealed class WordScrambleGame : Minigame
    {
        public override string DefaultTitle => "Unscramble";
        public override string DefaultInstructions => "Tap the letters in order to spell the word.";
        protected override float DefaultTime => 40f;

        protected override IEnumerator Play()
        {
            string word = (Ctx.Get("word") ?? Ctx.Get("arg1") ?? "STORY").ToUpperInvariant().Replace(" ", "");
            string hint = Ctx.Get("hint");
            var letters = word.ToCharArray().ToList();
            var order = Enumerable.Range(0, letters.Count).OrderBy(_ => Ctx.Next(1000)).ToList();
            if (letters.Count > 1 && order.SequenceEqual(Enumerable.Range(0, letters.Count))) order.Reverse();

            if (!string.IsNullOrEmpty(hint))
            {
                var h = UIKit.Text(Board, "Hint", hint, 40, new Color(1, 1, 1, 0.85f));
                UIKit.Place(h.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(Width - 60, 160));
            }

            float size = Mathf.Min(140, (Width - 40) / letters.Count - 12);
            var slots = new List<TextMeshProUGUI>();
            for (int i = 0; i < letters.Count; i++)
            {
                var slot = SkinImage(Board, "slot", ProceduralArt.RoundedRect(20), new Color(1, 1, 1, 0.15f));
                UIKit.Place(slot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (letters.Count - 1) / 2f) * (size + 12), 140), new Vector2(size, size));
                var t = UIKit.Text(slot.transform, "Letter", "", size * 0.6f, Color.white, TextAlignmentOptions.Center, true);
                slots.Add(t);
            }

            string typed = "";
            var buttons = new List<Button>();
            bool solved = false;
            for (int k = 0; k < order.Count; k++)
            {
                char ch = letters[order[k]];
                var b = SkinButton(Board, "tile", ch.ToString(), Theme.accentColor, null, size * 0.6f, 24);
                var rt = (RectTransform)b.transform;
                int perRow = Mathf.Min(order.Count, 6);
                int row = k / perRow, col = k % perRow;
                int inRow = Mathf.Min(perRow, order.Count - row * perRow);
                UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((col - (inRow - 1) / 2f) * (size + 24), -120 - row * (size + 24)), new Vector2(size + 8, size + 8));
                b.GetComponentInChildren<TextMeshProUGUI>().font = UIKit.TitleFont;
                buttons.Add(b);
                b.onClick.AddListener(() =>
                {
                    if (solved || TimedOut || !b.interactable) return;
                    typed += ch;
                    slots[typed.Length - 1].text = ch.ToString();
                    StartCoroutine(Bounce((RectTransform)slots[typed.Length - 1].transform.parent));
                    b.interactable = false;
                    UIKit.Group(b.gameObject).alpha = 0.25f;
                    if (typed.Length == word.Length)
                    {
                        if (typed == word) solved = true;
                        else StartCoroutine(Wrong());
                    }
                });
            }

            IEnumerator Wrong()
            {
                Ctx.PlaySound("wrong", "fail", 0.5f);
                foreach (var s in slots) StartCoroutine(Shake((RectTransform)s.transform.parent, 12));
                yield return Tween.Wait(0.45f);
                typed = "";
                foreach (var s in slots) s.text = "";
                foreach (var b in buttons) { b.interactable = true; UIKit.Group(b.gameObject).alpha = 1f; }
            }

            var clear = UIKit.Button(Board, "Clear", "CLEAR", new Color(1, 1, 1, 0.15f), Color.white, () =>
            {
                if (solved) return;
                typed = "";
                foreach (var s in slots) s.text = "";
                foreach (var b in buttons) { b.interactable = true; UIKit.Group(b.gameObject).alpha = 1f; }
            }, 36, 36);
            UIKit.Place((RectTransform)clear.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(300, 100));

            while (!solved && !TimedOut) yield return null;
            Ctx.Won = solved;
            Ctx.Score = solved ? Mathf.RoundToInt(TimeLeft) : 0;
            if (solved) foreach (var s in slots) StartCoroutine(Bounce((RectTransform)s.transform.parent, 1.2f));
        }
    }
}
