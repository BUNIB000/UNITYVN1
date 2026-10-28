using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>Marks a Minigame class and gives the names stories use for it: minigame memory ...</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MinigameAttribute : Attribute
    {
        public readonly string[] Names;
        public MinigameAttribute(params string[] names) { Names = names; }
    }

    /// <summary>Everything a minigame gets from the story: parameters, data lines, art and sound lookup.</summary>
    public sealed class MinigameContext
    {
        public string Game;
        public string Skin = "default";
        public Dictionary<string, string> Params = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> Data = new List<string>();
        public RectTransform Area;
        public VNTheme Theme;
        public VariableStore Variables;
        public StageView Stage;
        public System.Random Random = new System.Random();

        public bool Won;
        public float Score;

        public string Get(string key, string fallback = null) => Params.TryGetValue(key, out var v) && v.Length > 0 ? Variables != null ? Variables.Interpolate(v) : v : fallback;
        public int GetInt(string key, int fallback) => Params.TryGetValue(key, out var v) && Util.TryFloat(v, out var f) ? Mathf.RoundToInt(f) : fallback;
        public float GetFloat(string key, float fallback) => Params.TryGetValue(key, out var v) && Util.TryFloat(v, out var f) ? f : fallback;
        public bool GetBool(string key, bool fallback)
        {
            if (!Params.TryGetValue(key, out var v)) return fallback;
            v = v.ToLowerInvariant();
            return v == "true" || v == "yes" || v == "on" || v == "1";
        }

        public List<string> GetList(string key)
        {
            var v = Get(key);
            if (string.IsNullOrEmpty(v)) return new List<string>();
            return v.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        /// <summary>"easy", "normal" or "hard" scales a value.</summary>
        public float Difficulty(float easy, float normal, float hard)
        {
            switch ((Get("difficulty", "normal") ?? "normal").ToLowerInvariant())
            {
                case "easy": return easy;
                case "hard": return hard;
                default: return normal;
            }
        }

        string[] SearchFolders => new[]
        {
            "Minigames/" + Game + "/" + Skin,
            "Minigames/" + Skin + "/" + Game,
            "Minigames/" + Skin,
            "Minigames/" + Game,
            "Minigames/Shared",
        };

        /// <summary>
        /// Finds a skin sprite. Looks in Resources/Minigames/GAME/SKIN, then Minigames/GAME, then Minigames/Shared.
        /// Returns null when there's no custom art, so the game draws its built-in look.
        /// </summary>
        public Sprite Sprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var direct = Get(name); // a param can point at any image: card_back=MyCardBack
            if (direct != null)
            {
                var s = AssetLibrary.Image(direct);
                if (s != null) return s;
            }
            foreach (var f in SearchFolders)
            {
                var s = AssetLibrary.LoadSprite(f, name);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>All skin sprites whose names start with prefix (face_1, face_2...).</summary>
        public List<Sprite> SpritesWithPrefix(string prefix)
        {
            foreach (var f in SearchFolders)
            {
                var all = AssetLibrary.LoadAllSprites(f).Where(s => s.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.name).ToList();
                if (all.Count > 0) return all;
            }
            return new List<Sprite>();
        }

        public AudioClip Sound(string name)
        {
            foreach (var f in SearchFolders)
            {
                var c = AssetLibrary.Load<AudioClip>(f, name);
                if (c != null) return c;
            }
            return null;
        }

        public void PlaySound(string name, string builtIn = null, float volume = 1f)
        {
            var clip = Sound(name);
            if (clip != null) AudioDirector.Instance?.PlayClip(clip, volume);
            else if (builtIn != null) AudioDirector.Instance?.PlaySound(builtIn, volume);
        }

        public int Next(int max) => Random.Next(max);
        public float Range(float a, float b) => a + (float)Random.NextDouble() * (b - a);
    }

    /// <summary>
    /// Base class for minigames. A minigame builds its own UI inside Area, runs Play(),
    /// and sets Won / Score. Intro, countdown, timer, results and retries are handled here.
    /// To add your own: subclass this, add [Minigame("mygame")], and use "minigame mygame" in a story.
    /// </summary>
    public abstract class Minigame : MonoBehaviour
    {
        protected MinigameContext Ctx;
        protected RectTransform Board;
        protected VNTheme Theme => Ctx.Theme;
        protected float Width => Board.rect.width;
        protected float Height => Board.rect.height;

        TextMeshProUGUI scoreText, timerText;
        Image timerFill;
        RectTransform hudRoot;

        public abstract string DefaultTitle { get; }
        public abstract string DefaultInstructions { get; }
        /// <summary>Show the time bar for this game.</summary>
        protected virtual bool UsesTimer => true;
        protected virtual bool ShowsResult => true;
        protected virtual float DefaultTime => 30f;

        protected float TimeLimit => Ctx.GetFloat("time", DefaultTime);
        protected float TimeLeft;
        protected bool TimedOut => UsesTimer && TimeLimit > 0 && TimeLeft <= 0;

        /// <summary>The game itself. Set Ctx.Won and Ctx.Score before finishing.</summary>
        protected abstract IEnumerator Play();

        public IEnumerator Run(MinigameContext ctx)
        {
            Ctx = ctx;
            int retries = ctx.GetInt("retries", ctx.GetBool("retry", false) ? 99 : 0);
            string title = ctx.Get("title", DefaultTitle);
            string instructions = ctx.Get("instructions", DefaultInstructions);

            var bg = BuildBackground();
            var group = UIKit.Group(ctx.Area.gameObject);
            group.alpha = 0;
            yield return Tween.Fade(group, 1, 0.35f);

            if (!ctx.GetBool("skipintro", false)) yield return Intro(title, instructions);
            string music = ctx.Get("music");
            string previousMusic = AudioDirector.Instance != null ? AudioDirector.Instance.CurrentMusic : null;
            if (music != null) AudioDirector.Instance?.PlayMusic(music, 0.6f);

            while (true)
            {
                Board = UIKit.Rect("Board", ctx.Area);
                Board.offsetMin = new Vector2(30, 60);
                Board.offsetMax = new Vector2(-30, -260);
                BuildHud(title);
                yield return Countdown();
                TimeLeft = TimeLimit;
                Ctx.Won = false;
                Ctx.Score = 0;
                bool running = true;
                if (UsesTimer && TimeLimit > 0) StartCoroutine(TimerRoutine(() => running));
                yield return Play();
                running = false;
                yield return Tween.Wait(0.35f);

                bool again = false;
                if (ShowsResult)
                {
                    bool canRetry = !Ctx.Won && retries > 0;
                    yield return Result(Ctx.Won, canRetry, r => again = r);
                    if (again) retries--;
                }
                Destroy(Board.gameObject);
                if (hudRoot != null) Destroy(hudRoot.gameObject);
                if (!again) break;
            }
            if (music != null)
            {
                if (previousMusic != null) AudioDirector.Instance?.PlayMusic(previousMusic, 0.8f);
                else AudioDirector.Instance?.StopMusic(0.8f);
            }
            yield return Tween.Fade(group, 0, 0.3f);
        }

        // ---------- chrome ----------

        Image BuildBackground()
        {
            var custom = Ctx.Sprite("background");
            if (custom == null && Ctx.Get("background") != null) custom = AssetLibrary.Background(Ctx.Get("background"));
            Image img;
            if (custom != null)
            {
                img = UIKit.Image(Ctx.Area, "Background", custom, Color.white, true);
                var f = img.gameObject.AddComponent<AspectRatioFitter>();
                f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                f.aspectRatio = custom.rect.width / custom.rect.height;
                var veil = UIKit.Image(Ctx.Area, "Veil", ProceduralArt.White(), new Color(0, 0, 0, Ctx.GetFloat("dim", 0.25f)));
            }
            else
            {
                // Keep the story scene visible behind a soft veil, so the game feels part of the scene.
                img = UIKit.Image(Ctx.Area, "Background", ProceduralArt.Gradient("mg_bg", new Color(0.06f, 0.05f, 0.12f, 0.88f), new Color(0.2f, 0.12f, 0.3f, 0.72f)), Color.white, true);
            }
            return img;
        }

        void BuildHud(string title)
        {
            hudRoot = UIKit.Rect("HUD", Ctx.Area);
            var t = UIKit.Text(hudRoot, "Title", title, 64, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(Ctx.Area.rect.width - 300, 100));
            t.enableAutoSizing = true;
            t.fontSizeMin = 36;
            t.fontSizeMax = 64;

            scoreText = UIKit.Text(hudRoot, "Score", "", 44, Color.white, TextAlignmentOptions.Right, true);
            UIKit.Place(scoreText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -175), new Vector2(400, 70));

            if (UsesTimer && TimeLimit > 0)
            {
                var track = UIKit.Image(hudRoot, "Timer Track", ProceduralArt.RoundedRect(14), new Color(1, 1, 1, 0.18f));
                UIKit.Place(track.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-60, -200), new Vector2(Ctx.Area.rect.width - 300, 28));
                timerFill = UIKit.Image(track.transform, "Fill", ProceduralArt.RoundedRect(14), Theme.accentColor);
                timerFill.type = Image.Type.Filled;
                timerFill.fillMethod = Image.FillMethod.Horizontal;
                timerFill.fillAmount = 1;
                timerText = UIKit.Text(hudRoot, "Time", "", 40, Color.white, TextAlignmentOptions.Left, true);
                UIKit.Place(timerText.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-40, -200), new Vector2(110, 60));
                scoreText.rectTransform.anchoredPosition = new Vector2(-40, -110);
                scoreText.fontSize = 36;
            }
        }

        IEnumerator TimerRoutine(Func<bool> running)
        {
            while (running() && TimeLeft > 0)
            {
                TimeLeft -= Time.deltaTime;
                if (timerFill != null)
                {
                    float k = Mathf.Clamp01(TimeLeft / TimeLimit);
                    timerFill.fillAmount = k;
                    timerFill.color = k < 0.25f ? Color.Lerp(new Color(1f, 0.25f, 0.3f), Theme.accentColor, Mathf.PingPong(Time.time * 4, 1)) : Theme.accentColor;
                    timerText.text = Mathf.CeilToInt(Mathf.Max(0, TimeLeft)).ToString();
                }
                yield return null;
            }
        }

        protected void SetScore(string text)
        {
            if (scoreText != null) scoreText.text = text;
        }

        IEnumerator Intro(string title, string instructions)
        {
            var panel = Card(out var g, 760);
            var t = UIKit.Text(panel, "Title", title, 76, Theme.accentColor, TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(panel.sizeDelta.x - 80, 120));
            var ins = UIKit.Text(panel, "Instructions", instructions, 42, Color.white);
            UIKit.Place(ins.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(panel.sizeDelta.x - 100, 320));
            bool go = false;
            var b = UIKit.Button(panel, "Start", "START", Theme.accentColor, Color.white, () => go = true, 52, 60);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(420, 140));
            while (!go) yield return null;
            yield return Tween.All(this, Tween.Fade(g, 0, 0.2f), Tween.Scale(panel, Vector3.one * 0.9f, 0.2f, Ease.InBack));
            Destroy(panel.parent.gameObject);
        }

        RectTransform Card(out CanvasGroup group, float height)
        {
            var holder = UIKit.Rect("Card Holder", Ctx.Area);
            var panel = UIKit.Rect("Card", holder, false);
            float w = Mathf.Min(880, Ctx.Area.rect.width - 80);
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, height));
            var sh = UIKit.Image(panel, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.5f));
            UIKit.Stretch(sh.rectTransform, -26);
            UIKit.Image(panel, "Bg", ProceduralArt.RoundedRect(56), Theme.panelColor);
            group = UIKit.Group(holder.gameObject);
            panel.localScale = Vector3.one * 0.8f;
            StartCoroutine(Tween.Scale(panel, Vector3.one, 0.35f, Ease.OutBack));
            return panel;
        }

        IEnumerator Countdown()
        {
            var t = UIKit.Text(Ctx.Area, "Countdown", "", 220, Color.white, TextAlignmentOptions.Center, true);
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 160);
            foreach (var s in new[] { "3", "2", "1", "GO!" })
            {
                t.text = s;
                t.color = s == "GO!" ? Theme.accentColor : Color.white;
                t.rectTransform.localScale = Vector3.one * 1.6f;
                AudioDirector.Instance?.PlaySound(s == "GO!" ? "pop" : "click", 0.8f, s == "GO!" ? 1.3f : 1f);
                yield return Tween.Scale(t.rectTransform, Vector3.one, 0.3f, Ease.OutBack);
                yield return Tween.Wait(s == "GO!" ? 0.2f : 0.35f);
            }
            var g = UIKit.Group(t.gameObject);
            yield return Tween.Fade(g, 0, 0.15f);
            Destroy(t.gameObject);
        }

        IEnumerator Result(bool won, bool canRetry, Action<bool> retry)
        {
            if (won) { Ctx.PlaySound("win", "success"); StartCoroutine(Confetti()); }
            else Ctx.PlaySound("lose", "fail");
            var panel = Card(out var g, 640);
            string head = won ? Ctx.Get("wintext", "Success!") : Ctx.Get("losetext", "Not quite...");
            var t = UIKit.Text(panel, "Title", head, 92, won ? Theme.accentColor : new Color(0.75f, 0.75f, 0.85f), TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(panel.sizeDelta.x - 60, 140));
            var stars = UIKit.Rect("Stars", panel, false);
            UIKit.Place(stars, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(400, 120));
            int starCount = won ? StarsFor() : 0;
            for (int i = 0; i < 3; i++)
            {
                var s = UIKit.Image(stars, "Star" + i, ProceduralArt.Star(), i < starCount ? new Color(1f, 0.82f, 0.25f) : new Color(1, 1, 1, 0.15f));
                UIKit.Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 130, i == 1 ? 14 : 0), new Vector2(i == 1 ? 120 : 100, i == 1 ? 120 : 100));
                s.rectTransform.localScale = Vector3.zero;
                StartCoroutine(PopStar(s.rectTransform, 0.25f + i * 0.15f));
            }
            bool done = false, again = false;
            if (canRetry)
            {
                var r = UIKit.Button(panel, "Retry", "TRY AGAIN", new Color(1, 1, 1, 0.12f), Color.white, () => { again = true; done = true; }, 42, 50);
                UIKit.Place((RectTransform)r.transform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-15, 60), new Vector2(330, 120));
                var c = UIKit.Button(panel, "Continue", "CONTINUE", Theme.accentColor, Color.white, () => done = true, 42, 50);
                UIKit.Place((RectTransform)c.transform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(15, 60), new Vector2(330, 120));
            }
            else
            {
                var c = UIKit.Button(panel, "Continue", "CONTINUE", Theme.accentColor, Color.white, () => done = true, 48, 60);
                UIKit.Place((RectTransform)c.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(420, 130));
            }
            while (!done) yield return null;
            retry(again);
            yield return Tween.Fade(g, 0, 0.2f);
            Destroy(panel.parent.gameObject);
        }

        /// <summary>Override to award 1-3 stars from the score.</summary>
        protected virtual int StarsFor()
        {
            if (UsesTimer && TimeLimit > 0)
            {
                float k = TimeLeft / TimeLimit;
                return k > 0.5f ? 3 : k > 0.2f ? 2 : 1;
            }
            return 3;
        }

        static IEnumerator PopStar(RectTransform rt, float delay)
        {
            yield return Tween.Wait(delay);
            AudioDirector.Instance?.Pop();
            yield return Tween.Scale(rt, Vector3.one, 0.4f, Ease.OutBack);
        }

        IEnumerator Confetti()
        {
            var layer = UIKit.Rect("Confetti", Ctx.Area);
            var pieces = new List<(RectTransform rt, Vector2 v, float spin)>();
            var colors = new[] { Theme.accentColor, new Color(1f, 0.82f, 0.25f), new Color(0.35f, 0.8f, 1f), new Color(0.5f, 0.95f, 0.55f), Color.white };
            for (int i = 0; i < 70; i++)
            {
                var img = UIKit.Image(layer, "c", ProceduralArt.RoundedRect(4), colors[i % colors.Length]);
                var rt = img.rectTransform;
                UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Ctx.Range(-100, 100), 100), new Vector2(Ctx.Range(16, 26), Ctx.Range(26, 40)));
                pieces.Add((rt, new Vector2(Ctx.Range(-900, 900), Ctx.Range(900, 1900)), Ctx.Range(-600, 600)));
            }
            float t = 0;
            while (t < 2.2f && layer != null)
            {
                t += Time.deltaTime;
                for (int i = 0; i < pieces.Count; i++)
                {
                    var p = pieces[i];
                    p.v += new Vector2(-p.v.x * 1.2f * Time.deltaTime, -2600 * Time.deltaTime);
                    p.rt.anchoredPosition += p.v * Time.deltaTime;
                    p.rt.localRotation = Quaternion.Euler(0, 0, p.spin * t);
                    pieces[i] = p;
                }
                yield return null;
            }
            if (layer != null) Destroy(layer.gameObject);
        }

        // ---------- helpers for games ----------

        /// <summary>An image from the skin, or a built-in fallback tinted with a color.</summary>
        protected Image SkinImage(Transform parent, string skinName, Sprite fallback, Color fallbackColor, bool raycast = false)
        {
            var s = Ctx.Sprite(skinName);
            var img = UIKit.Image(parent, skinName, s != null ? s : fallback, s != null ? Color.white : fallbackColor, raycast);
            if (s != null) img.preserveAspect = true;
            return img;
        }

        protected Button SkinButton(Transform parent, string skinName, string label, Color fallbackColor, Action onClick, float fontSize = 44, int radius = 40)
        {
            var b = UIKit.Button(parent, skinName, label, fallbackColor, Color.white, onClick, fontSize, radius);
            var s = Ctx.Sprite(skinName);
            if (s != null) { b.image.sprite = s; b.image.color = Color.white; b.image.type = s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; b.image.preserveAspect = s.border == Vector4.zero; }
            return b;
        }

        protected void FloatText(Vector2 pos, string text, Color color, float size = 60)
        {
            StartCoroutine(FloatRoutine(pos, text, color, size));
        }

        IEnumerator FloatRoutine(Vector2 pos, string text, Color color, float size)
        {
            var t = UIKit.Text(Board, "Float", text, size, color, TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(400, 120));
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 150);
            var g = UIKit.Group(t.gameObject);
            yield return Tween.Run(0.8f, Ease.OutCubic, k =>
            {
                if (t == null) return;
                t.rectTransform.anchoredPosition = pos + new Vector2(0, 120 * k);
                g.alpha = 1 - k * k;
            });
            if (t != null) Destroy(t.gameObject);
        }

        protected IEnumerator Shake(RectTransform rt, float amount = 18f, float duration = 0.3f)
        {
            var p = rt.anchoredPosition;
            float t = 0;
            while (t < duration && rt != null)
            {
                t += Time.deltaTime;
                rt.anchoredPosition = p + new Vector2(Mathf.Sin(t * 80f) * amount * (1 - t / duration), 0);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = p;
        }

        protected IEnumerator Bounce(RectTransform rt, float scale = 1.15f)
        {
            if (rt == null) yield break;
            var baseScale = Vector3.one;
            yield return Tween.Scale(rt, baseScale * scale, 0.08f, Ease.OutQuad);
            if (rt != null) yield return Tween.Scale(rt, baseScale, 0.25f, Ease.OutBack);
        }

        protected static readonly Color[] Palette =
        {
            new Color(0.98f, 0.36f, 0.55f), new Color(0.35f, 0.75f, 1f), new Color(1f, 0.78f, 0.25f), new Color(0.45f, 0.9f, 0.55f),
            new Color(0.7f, 0.5f, 1f), new Color(1f, 0.55f, 0.3f), new Color(0.3f, 0.9f, 0.85f), new Color(0.95f, 0.45f, 0.9f),
            new Color(0.6f, 0.85f, 0.3f), new Color(0.95f, 0.3f, 0.3f), new Color(0.4f, 0.5f, 0.95f), new Color(0.85f, 0.7f, 0.5f)
        };

        /// <summary>Built-in symbol sprites (shapes) for games without custom art.</summary>
        protected static Sprite Symbol(int i)
        {
            switch (i % 8)
            {
                case 0: return ProceduralArt.Heart();
                case 1: return ProceduralArt.Star();
                case 2: return ProceduralArt.Circle(48);
                case 3: return ProceduralArt.Diamond();
                case 4: return ProceduralArt.Triangle("up");
                case 5: return ProceduralArt.RoundedRect(16);
                case 6: return ProceduralArt.Ring(48, 14);
                default: return ProceduralArt.MenuIcon();
            }
        }
    }

    /// <summary>Finds minigame classes by name.</summary>
    public static class MinigameRegistry
    {
        static Dictionary<string, Type> types;

        public static IEnumerable<string> Names
        {
            get { Build(); return types.Keys; }
        }

        static void Build()
        {
            if (types != null) return;
            types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] all;
                try { all = asm.GetTypes(); } catch { continue; }
                foreach (var t in all)
                {
                    if (t.IsAbstract || !typeof(Minigame).IsAssignableFrom(t)) continue;
                    var attr = (MinigameAttribute)Attribute.GetCustomAttribute(t, typeof(MinigameAttribute));
                    if (attr == null) continue;
                    foreach (var n in attr.Names) types[n] = t;
                }
            }
        }

        public static Type Find(string name)
        {
            Build();
            return types.TryGetValue(name, out var t) ? t : null;
        }
    }
}
