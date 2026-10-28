using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>Reports pointer drags in screen space.</summary>
    public sealed class DragHandler : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public System.Action<Vector2> Moved;
        public void OnDrag(PointerEventData e) => Moved?.Invoke(e.position);
        public void OnPointerDown(PointerEventData e) => Moved?.Invoke(e.position);
    }

    /// <summary>
    /// Catch: drag the basket to catch the good things and dodge the bad ones.
    /// minigame catch need=12 time=25
    /// Skin: basket, good (or good_1, good_2...), bad (or bad_1...), background
    /// </summary>
    [Minigame("catch", "collect", "falling")]
    public sealed class CatchGame : Minigame
    {
        public override string DefaultTitle => "Catch!";
        public override string DefaultInstructions => "Drag to move. Catch the good things, avoid the bad ones!";
        protected override float DefaultTime => 25f;

        sealed class Drop
        {
            public RectTransform Rt;
            public bool Good;
            public float Speed, Spin;
        }

        protected override IEnumerator Play()
        {
            int need = Ctx.GetInt("need", Mathf.RoundToInt(Ctx.Difficulty(8, 12, 16)));
            float speed = Ctx.GetFloat("speed", Ctx.Difficulty(0.8f, 1f, 1.3f));
            float badChance = Ctx.GetFloat("bad", Ctx.Difficulty(0.2f, 0.3f, 0.4f));
            var goods = Ctx.SpritesWithPrefix("good");
            var bads = Ctx.SpritesWithPrefix("bad");

            var basket = SkinImage(Board, "basket", ProceduralArt.RoundedRect(40), Theme.accentColor);
            var brt = basket.rectTransform;
            UIKit.Place(brt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(240, 120));
            var drag = TapArea.Create(Board, "Drag Area").gameObject.AddComponent<DragHandler>();
            drag.transform.SetAsFirstSibling();
            float targetX = 0;
            drag.Moved = screen =>
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Board, screen, null, out var local);
                targetX = Mathf.Clamp(local.x, -Width / 2 + 120, Width / 2 - 120);
            };

            var drops = new List<Drop>();
            int caught = 0;
            float spawn = 0;
            SetScore($"0 / {need}");
            while (!TimedOut && caught < need)
            {
                float dt = Time.deltaTime;
                brt.anchoredPosition = new Vector2(Mathf.Lerp(brt.anchoredPosition.x, targetX, 1 - Mathf.Exp(-dt * 18f)), 40);
                spawn -= dt;
                if (spawn <= 0)
                {
                    spawn = Ctx.Range(0.45f, 0.9f) / speed;
                    bool good = Ctx.Range(0, 1) > badChance;
                    var list = good ? goods : bads;
                    Sprite s = list.Count > 0 ? list[Ctx.Next(list.Count)] : good ? ProceduralArt.Heart() : ProceduralArt.Triangle("down");
                    var img = UIKit.Image(Board, good ? "Good" : "Bad", s, list.Count > 0 ? Color.white : good ? new Color(1f, 0.45f, 0.6f) : new Color(0.35f, 0.3f, 0.4f));
                    img.preserveAspect = true;
                    UIKit.Place(img.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(Ctx.Range(-Width / 2 + 80, Width / 2 - 80), 60), new Vector2(110, 110));
                    drops.Add(new Drop { Rt = img.rectTransform, Good = good, Speed = Ctx.Range(500, 800) * speed, Spin = Ctx.Range(-120, 120) });
                }
                for (int i = drops.Count - 1; i >= 0; i--)
                {
                    var d = drops[i];
                    d.Rt.anchoredPosition += new Vector2(0, -d.Speed * dt);
                    d.Rt.localRotation = Quaternion.Euler(0, 0, d.Rt.localEulerAngles.z + d.Spin * dt);
                    float y = d.Rt.anchoredPosition.y + Height; // distance from bottom
                    bool inBasket = y < 190 && y > 90 && Mathf.Abs(d.Rt.anchoredPosition.x - brt.anchoredPosition.x) < 150;
                    if (inBasket)
                    {
                        if (d.Good) { caught++; Ctx.PlaySound("catch", "pop"); FloatText(new Vector2(brt.anchoredPosition.x, -Height / 2 + 260), "+1", Theme.accentColor); StartCoroutine(Bounce(brt, 1.1f)); }
                        else { caught = Mathf.Max(0, caught - 2); Ctx.PlaySound("ouch", "fail", 0.6f); FloatText(new Vector2(brt.anchoredPosition.x, -Height / 2 + 260), "-2", new Color(1f, 0.4f, 0.4f)); StartCoroutine(Shake(brt)); }
                        SetScore($"{caught} / {need}");
                        Destroy(d.Rt.gameObject);
                        drops.RemoveAt(i);
                    }
                    else if (y < -100)
                    {
                        Destroy(d.Rt.gameObject);
                        drops.RemoveAt(i);
                    }
                }
                yield return null;
            }
            foreach (var d in drops) if (d.Rt != null) Destroy(d.Rt.gameObject);
            Ctx.Score = caught;
            Ctx.Won = caught >= need;
        }
    }

    /// <summary>
    /// Rhythm: notes fall down lanes; tap the lane as they reach the line. Dance-offs, concerts, karaoke.
    /// minigame rhythm bpm=110 notes=24 lanes=3 music=dance_song need=70
    /// Skin: note, lane, button_1..button_4
    /// </summary>
    [Minigame("rhythm", "dance", "music", "beat")]
    public sealed class RhythmGame : Minigame
    {
        public override string DefaultTitle => "Feel the Beat";
        public override string DefaultInstructions => "Tap each lane when a note reaches the line.";
        protected override bool UsesTimer => false;

        sealed class Note
        {
            public int Lane;
            public float Time;
            public RectTransform Rt;
            public bool Done;
        }

        protected override IEnumerator Play()
        {
            float bpm = Ctx.GetFloat("bpm", 110);
            int count = Ctx.GetInt("notes", 24);
            int lanes = Mathf.Clamp(Ctx.GetInt("lanes", 3), 2, 4);
            float needPct = Ctx.GetFloat("need", 70);
            float travel = Ctx.GetFloat("travel", Ctx.Difficulty(1.8f, 1.4f, 1.05f));
            float beat = 60f / bpm;
            float window = Ctx.Difficulty(0.2f, 0.15f, 0.11f);
            var noteSprite = Ctx.Sprite("note");

            float laneW = (Width - 40) / lanes;
            float lineY = -Height / 2 + 300;
            float topY = Height / 2;
            var hitLine = UIKit.Image(Board, "Line", ProceduralArt.RoundedRect(8), new Color(1, 1, 1, 0.6f));
            UIKit.Place(hitLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, lineY), new Vector2(Width - 40, 14));
            var buttons = new List<Button>();
            var notes = new List<Note>();
            int hits = 0, judged = 0, combo = 0;
            var laneTaps = new Queue<int>();
            for (int l = 0; l < lanes; l++)
            {
                int li = l;
                float x = (l - (lanes - 1) / 2f) * laneW;
                var lane = SkinImage(Board, "lane", ProceduralArt.RoundedRect(30), new Color(1, 1, 1, 0.06f));
                UIKit.Place(lane.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(laneW - 16, Height));
                lane.transform.SetAsFirstSibling();
                var b = SkinButton(Board, "button_" + (l + 1), null, Palette[l] * 0.85f, () => laneTaps.Enqueue(li), 40, 70);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, lineY - 150), new Vector2(laneW - 40, 180));
                buttons.Add(b);
            }

            // Build a chart: mostly on beats, some off-beats, lanes random but not too repetitive.
            float t0 = 1.0f + travel;
            float tt = t0;
            int lastLane = -1;
            for (int i = 0; i < count; i++)
            {
                int lane = Ctx.Next(lanes);
                if (lane == lastLane && Ctx.Next(2) == 0) lane = (lane + 1) % lanes;
                lastLane = lane;
                notes.Add(new Note { Lane = lane, Time = tt });
                tt += beat * (Ctx.Next(4) == 0 ? 0.5f : Ctx.Next(5) == 0 ? 2f : 1f);
            }
            string music = Ctx.Get("music");
            float clock = 0;
            if (music == null) StartCoroutine(Metronome(beat, tt + 1f));
            SetScore("0");

            var feedback = UIKit.Text(Board, "Feedback", "", 70, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(feedback.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, lineY + 200), new Vector2(Width, 120));

            void Judge(string text, Color c)
            {
                feedback.text = text;
                feedback.color = c;
                StartCoroutine(Bounce(feedback.rectTransform, 1.3f));
            }

            while (judged < notes.Count)
            {
                clock += Time.deltaTime;
                foreach (var n in notes)
                {
                    if (n.Done) continue;
                    float appear = n.Time - travel;
                    if (clock >= appear && n.Rt == null)
                    {
                        var img = UIKit.Image(Board, "Note", noteSprite != null ? noteSprite : ProceduralArt.RoundedRect(40), noteSprite != null ? Color.white : Palette[n.Lane]);
                        if (noteSprite != null) img.preserveAspect = true;
                        UIKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((n.Lane - (lanes - 1) / 2f) * laneW, topY), new Vector2(laneW - 50, 70));
                        n.Rt = img.rectTransform;
                    }
                    if (n.Rt != null)
                    {
                        float k = (clock - appear) / travel;
                        n.Rt.anchoredPosition = new Vector2(n.Rt.anchoredPosition.x, Mathf.LerpUnclamped(topY, lineY, k));
                    }
                    if (clock > n.Time + window)
                    {
                        n.Done = true;
                        judged++;
                        combo = 0;
                        Judge("Miss", new Color(0.8f, 0.8f, 0.9f));
                        if (n.Rt != null) StartCoroutine(FadeAndKill(n.Rt));
                    }
                }
                while (laneTaps.Count > 0)
                {
                    int lane = laneTaps.Dequeue();
                    StartCoroutine(Bounce((RectTransform)buttons[lane].transform, 1.08f));
                    var target = notes.Where(n => !n.Done && n.Lane == lane && Mathf.Abs(n.Time - clock) <= window).OrderBy(n => Mathf.Abs(n.Time - clock)).FirstOrDefault();
                    if (target != null)
                    {
                        target.Done = true;
                        judged++;
                        hits++;
                        combo++;
                        bool perfect = Mathf.Abs(target.Time - clock) < window * 0.4f;
                        Judge(perfect ? (combo > 4 ? $"Perfect x{combo}" : "Perfect!") : "Good", perfect ? Theme.accentColor : new Color(0.5f, 0.9f, 1f));
                        AudioDirector.Instance?.PlaySound("pop", 0.6f, 1f + lane * 0.15f);
                        if (target.Rt != null) Destroy(target.Rt.gameObject);
                        SetScore(Mathf.RoundToInt(hits * 100f / notes.Count) + "%");
                    }
                }
                yield return null;
            }
            float pct = hits * 100f / Mathf.Max(1, notes.Count);
            Ctx.Score = Mathf.RoundToInt(pct);
            Ctx.Won = pct >= needPct;
        }

        IEnumerator Metronome(float beat, float until)
        {
            float t = 0;
            while (t < until)
            {
                AudioDirector.Instance?.PlaySound("blip", 0.25f, 0.6f);
                yield return Tween.Wait(beat);
                t += beat;
            }
        }

        IEnumerator FadeAndKill(RectTransform rt)
        {
            var g = UIKit.Group(rt.gameObject);
            yield return Tween.Fade(g, 0, 0.2f);
            if (rt != null) Destroy(rt.gameObject);
        }

        protected override int StarsFor() => Ctx.Score >= 95 ? 3 : Ctx.Score >= 80 ? 2 : 1;
    }

    /// <summary>
    /// Dress up: pick outfit pieces for a character who stays on stage behind the panel.
    ///   minigame dressup character=PLAYER
    ///       outfit: casual, formal, party
    ///       hair: long, bob, ponytail
    /// Each pick is saved as a variable (PLAYER_outfit = "party") and applied to the character.
    /// Skin: preview images named slot_label (outfit_party) are shown on the option buttons.
    /// </summary>
    [Minigame("dressup", "outfit", "wardrobe", "makeover", "choose")]
    public sealed class DressUpGame : Minigame
    {
        public override string DefaultTitle => "Choose Your Look";
        public override string DefaultInstructions => "Tap the arrows to try things on, then confirm.";
        protected override bool UsesTimer => false;
        protected override bool ShowsResult => false;

        protected override IEnumerator Play()
        {
            string who = (Ctx.Get("character") ?? Ctx.Get("arg1") ?? "PLAYER").ToUpperInvariant();
            var slots = new List<(string slot, List<string> options)>();
            foreach (var raw in Ctx.Data)
            {
                var line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var opts = line.Substring(colon + 1).Split(',').Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0).ToList();
                if (opts.Count > 0) slots.Add((line.Substring(0, colon).Trim().ToLowerInvariant(), opts));
            }
            foreach (var kv in Ctx.Params.Where(p => p.Key.StartsWith("options_")))
                slots.Add((kv.Key.Substring(8), kv.Value.Split(',').Select(s => s.Trim()).ToList()));
            if (slots.Count == 0) slots.Add(("outfit", new List<string> { "casual", "formal", "party" }));

            var character = Ctx.Stage != null ? Ctx.Stage.Character(who) : null;
            bool shownHere = false;
            if (character != null && !character.OnStage)
            {
                character.PlaceAt(Ctx.Stage.Theme.centerX, 0, 1f, Ctx.Stage.CurrentZone);
                character.Show();
                character.SetAlpha(1f);
                shownHere = true;
            }
            if (character != null) character.PlayAnimation("idle_happy");

            // Panel at the bottom; the stage shows through the top half.
            var bg = Ctx.Area.Find("Background");
            if (bg != null) bg.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            var panel = UIKit.Image(Board, "Panel", ProceduralArt.RoundedRect(50), new Color(0.1f, 0.08f, 0.15f, 0.92f));
            float rowH = 170;
            float panelH = slots.Count * rowH + 240;
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(Width, panelH));

            var picks = new int[slots.Count];
            for (int s = 0; s < slots.Count; s++)
            {
                int si = s;
                var (slot, options) = slots[s];
                string current = Ctx.Variables != null ? Ctx.Variables.GetString(who + "_" + slot) : null;
                picks[s] = Mathf.Max(0, options.FindIndex(o => o == current));
                float y = panelH - 70 - s * rowH;
                var cap = UIKit.Text(panel.transform, "Slot", slot.ToUpperInvariant(), 30, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Center, true);
                UIKit.Place(cap.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(Width, 40));
                var value = UIKit.Text(panel.transform, "Value", "", 48, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Place(value.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, y - 70), new Vector2(Width - 360, 90));
                var preview = UIKit.Image(panel.transform, "Preview", null, Color.white);
                UIKit.Place(preview.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(-Width / 2 + 230, y - 70), new Vector2(90, 90));
                preview.preserveAspect = true;

                void Apply()
                {
                    string label = options[picks[si]];
                    value.text = label.Replace('_', ' ');
                    var p = Ctx.Sprite(slot + "_" + label);
                    preview.sprite = p;
                    preview.enabled = p != null;
                    if (character != null)
                    {
                        character.Wear(slot, label);
                        character.PlayAnimation("spin");
                    }
                    StartCoroutine(Bounce(value.rectTransform, 1.15f));
                }

                var left = UIKit.Button(panel.transform, "Left", null, Theme.accentColor, Color.white, () => { picks[si] = (picks[si] - 1 + options.Count) % options.Count; Apply(); }, 40, 50);
                UIKit.Place((RectTransform)left.transform, new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(40, y - 70), new Vector2(110, 110));
                UIKit.Stretch(UIKit.Image(left.transform, "Arrow", ProceduralArt.Triangle("left"), Color.white).rectTransform, 28);
                var right = UIKit.Button(panel.transform, "Right", null, Theme.accentColor, Color.white, () => { picks[si] = (picks[si] + 1) % options.Count; Apply(); }, 40, 50);
                UIKit.Place((RectTransform)right.transform, new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-40, y - 70), new Vector2(110, 110));
                UIKit.Stretch(UIKit.Image(right.transform, "Arrow", ProceduralArt.Triangle("right"), Color.white).rectTransform, 28);
                Apply();
            }

            bool done = false;
            var confirm = UIKit.Button(panel.transform, "Confirm", Ctx.Get("confirm", "THIS IS PERFECT"), Theme.accentColor, Color.white, () => done = true, 44, 60);
            UIKit.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(Width - 120, 130));
            while (!done) yield return null;

            for (int s = 0; s < slots.Count; s++)
                Ctx.Variables?.Set(who + "_" + slots[s].slot, slots[s].options[picks[s]]);
            if (character != null) character.PlayAnimation("cheer");
            Ctx.PlaySound("confirm", "success", 0.6f);
            if (shownHere) character.Hide();
            Ctx.Won = true;
            Ctx.Score = 1;
        }
    }
}
