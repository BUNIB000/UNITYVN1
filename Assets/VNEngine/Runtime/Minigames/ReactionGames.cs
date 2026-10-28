using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>
    /// Quick-time event: targets pop up around the screen; tap each before its ring closes.
    /// minigame qte count=6 window=1.2 need=5
    /// Skin: target, ring, background
    /// </summary>
    [Minigame("qte", "quicktime", "reaction")]
    public sealed class QuickTimeGame : Minigame
    {
        public override string DefaultTitle => "Quick!";
        public override string DefaultInstructions => "Tap each target before its ring closes!";
        protected override bool UsesTimer => false;

        protected override IEnumerator Play()
        {
            int count = Ctx.GetInt("count", 6);
            float window = Ctx.GetFloat("window", Ctx.Difficulty(1.6f, 1.15f, 0.8f));
            int need = Ctx.GetInt("need", Mathf.Max(1, count - 1));
            int hits = 0;
            SetScore($"0 / {need}");
            for (int i = 0; i < count; i++)
            {
                yield return Tween.Wait(Ctx.Range(0.25f, 0.6f));
                bool hit = false, done = false;
                var holder = UIKit.Rect("Target", Board, false);
                var pos = new Vector2(Ctx.Range(-Width / 2 + 150, Width / 2 - 150), Ctx.Range(-Height / 2 + 150, Height / 2 - 150));
                UIKit.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(220, 220));
                var ring = SkinImage(holder, "ring", ProceduralArt.Ring(64, 8), Color.white);
                var btn = UIKit.Button(holder, "Button", null, Theme.accentColor, Color.white, () => { if (!done) { hit = true; done = true; } }, 40, 90);
                var custom = Ctx.Sprite("target");
                if (custom != null) { btn.image.sprite = custom; btn.image.color = Color.white; btn.image.type = Image.Type.Simple; btn.image.preserveAspect = true; }
                ((RectTransform)btn.transform).sizeDelta = new Vector2(-60, -60);
                var label = UIKit.Text(btn.transform, "Tap", "TAP", 40, Color.white, TextAlignmentOptions.Center, true);
                holder.localScale = Vector3.zero;
                StartCoroutine(Tween.Scale(holder, Vector3.one, 0.2f, Ease.OutBack));
                float t = 0;
                while (!done && t < window)
                {
                    t += Time.deltaTime;
                    float k = t / window;
                    ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.75f, k);
                    ring.color = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.35f), k);
                    yield return null;
                }
                if (hit)
                {
                    hits++;
                    Ctx.PlaySound("hit", "pop");
                    FloatText(pos, "Nice!", Theme.accentColor);
                }
                else
                {
                    Ctx.PlaySound("miss", "fail", 0.5f);
                    FloatText(pos, "Miss", new Color(0.8f, 0.8f, 0.9f));
                }
                SetScore($"{hits} / {need}");
                yield return Tween.Scale(holder, Vector3.zero, 0.15f, Ease.InBack);
                Destroy(holder.gameObject);
                if (hits >= need) break;
                if (count - i - 1 + hits < need) break;
            }
            Ctx.Score = hits;
            Ctx.Won = hits >= need;
        }

        protected override int StarsFor() => Mathf.Clamp((int)Ctx.Score - Ctx.GetInt("need", 1) + 2, 1, 3);
    }

    /// <summary>
    /// Timing bar: stop the moving needle inside the sweet spot. Lockpicking, cooking, archery, a dance move.
    /// minigame timing rounds=3 need=2 zone=0.2 speed=1
    /// Skin: bar, zone, needle, icon
    /// </summary>
    [Minigame("timing", "lockpick", "stop", "timingbar")]
    public sealed class TimingBarGame : Minigame
    {
        public override string DefaultTitle => "Perfect Timing";
        public override string DefaultInstructions => "Tap when the marker is inside the glowing zone.";
        protected override bool UsesTimer => false;

        protected override IEnumerator Play()
        {
            int rounds = Ctx.GetInt("rounds", 3);
            int need = Ctx.GetInt("need", Mathf.Max(1, rounds - 1));
            float zone = Ctx.GetFloat("zone", Ctx.Difficulty(0.26f, 0.18f, 0.11f));
            float speed = Ctx.GetFloat("speed", Ctx.Difficulty(0.7f, 1f, 1.45f));
            int hits = 0;
            float barW = Width - 120;

            var icon = Ctx.Sprite("icon");
            if (icon != null)
            {
                var ic = UIKit.Image(Board, "Icon", icon, Color.white);
                ic.preserveAspect = true;
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 320), new Vector2(360, 360));
            }
            var bar = SkinImage(Board, "bar", ProceduralArt.RoundedRect(40), new Color(1, 1, 1, 0.15f));
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(barW, 110));
            var zoneImg = SkinImage(bar.transform, "zone", ProceduralArt.RoundedRect(30), new Color(0.45f, 0.95f, 0.6f, 0.85f));
            var needle = SkinImage(bar.transform, "needle", ProceduralArt.RoundedRect(10), Color.white);
            var tap = UIKit.Button(Board, "Tap", "TAP!", Theme.accentColor, Color.white, null, 64, 70);
            UIKit.Place((RectTransform)tap.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(460, 200));
            var pips = new List<Image>();
            for (int i = 0; i < rounds; i++)
            {
                var p = UIKit.Image(Board, "Pip" + i, ProceduralArt.Circle(24), new Color(1, 1, 1, 0.2f));
                UIKit.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (rounds - 1) / 2f) * 70, 140), new Vector2(44, 44));
                pips.Add(p);
            }

            for (int r = 0; r < rounds; r++)
            {
                float zoneStart = Ctx.Range(0.1f, 0.9f - zone);
                UIKit.Place(zoneImg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(zoneStart * barW, 0), new Vector2(zone * barW, 96));
                UIKit.Place(needle.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 150));
                bool pressed = false;
                tap.onClick.RemoveAllListeners();
                tap.onClick.AddListener(() => pressed = true);
                float t = Ctx.Range(0f, 1f);
                float pos = 0;
                while (!pressed)
                {
                    t += Time.deltaTime * speed * (1 + r * 0.2f);
                    pos = Mathf.PingPong(t, 1f);
                    pos = Easing.Apply(Ease.InOutSine, pos);
                    needle.rectTransform.anchoredPosition = new Vector2(pos * barW, 0);
                    yield return null;
                }
                bool ok = pos >= zoneStart && pos <= zoneStart + zone;
                if (ok) { hits++; Ctx.PlaySound("hit", "pop"); FloatText(new Vector2(0, 130), "Perfect!", new Color(0.45f, 0.95f, 0.6f)); StartCoroutine(Bounce(zoneImg.rectTransform)); }
                else { Ctx.PlaySound("miss", "fail", 0.6f); FloatText(new Vector2(0, 130), "Missed", new Color(1f, 0.5f, 0.5f)); StartCoroutine(Shake(bar.rectTransform)); }
                pips[r].color = ok ? new Color(0.45f, 0.95f, 0.6f) : new Color(1f, 0.4f, 0.45f);
                SetScore($"{hits} / {need}");
                yield return Tween.Wait(0.6f);
                if (hits >= need || rounds - r - 1 + hits < need) break;
            }
            Ctx.Score = hits;
            Ctx.Won = hits >= need;
        }

        protected override int StarsFor() => Mathf.Clamp((int)Ctx.Score - Ctx.GetInt("need", 2) + 2, 1, 3);
    }

    /// <summary>
    /// Button mash: tap fast to fill the meter before time runs out. Arm wrestling, pushing a door, cheering.
    /// minigame mash taps=30 time=6
    /// Skin: button, meter, icon
    /// </summary>
    [Minigame("mash", "tap", "tapper", "buttonmash")]
    public sealed class MashGame : Minigame
    {
        public override string DefaultTitle => "Tap Tap Tap!";
        public override string DefaultInstructions => "Tap as fast as you can to fill the meter!";
        protected override float DefaultTime => 7f;

        protected override IEnumerator Play()
        {
            float goal = Ctx.GetInt("taps", Mathf.RoundToInt(Ctx.Difficulty(22, 32, 45)));
            float decay = Ctx.GetFloat("decay", Ctx.Difficulty(1.5f, 3f, 5f)); // taps lost per second
            float value = 0;
            var meterBg = UIKit.Image(Board, "Meter Track", ProceduralArt.RoundedRect(40), new Color(1, 1, 1, 0.15f));
            UIKit.Place(meterBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(Width - 160, 100));
            var meter = SkinImage(meterBg.transform, "meter", ProceduralArt.RoundedRect(40), Theme.accentColor);
            meter.type = Image.Type.Filled;
            meter.fillMethod = Image.FillMethod.Horizontal;
            meter.fillAmount = 0;
            var icon = Ctx.Sprite("icon");
            if (icon != null)
            {
                var ic = UIKit.Image(Board, "Icon", icon, Color.white);
                ic.preserveAspect = true;
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 480), new Vector2(300, 300));
            }
            var button = SkinButton(Board, "button", "TAP!", Theme.accentColor, null, 90, 200);
            var brt = (RectTransform)button.transform;
            UIKit.Place(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(460, 460));
            button.onClick.AddListener(() =>
            {
                value += 1;
                StartCoroutine(Bounce(brt, 1.08f));
                AudioDirector.Instance?.PlaySound("blip", 0.6f, 0.8f + value / goal * 0.8f);
            });
            while (!TimedOut && value < goal)
            {
                value = Mathf.Max(0, value - decay * Time.deltaTime);
                meter.fillAmount = Mathf.Lerp(meter.fillAmount, value / goal, Time.deltaTime * 14f);
                SetScore(Mathf.RoundToInt(value / goal * 100) + "%");
                yield return null;
            }
            meter.fillAmount = Mathf.Clamp01(value / goal);
            Ctx.Score = Mathf.RoundToInt(Mathf.Clamp01(value / goal) * 100);
            Ctx.Won = value >= goal;
        }
    }
}
