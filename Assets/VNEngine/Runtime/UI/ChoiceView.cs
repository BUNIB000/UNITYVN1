using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    public sealed class ChoiceItem
    {
        public string Text;
        public bool Enabled = true;
        public string Tag;
        public bool IsDefault;
    }

    /// <summary>Stacked choice buttons that pop in one after another, with an optional timer.</summary>
    public sealed class ChoiceView : MonoBehaviour
    {
        VNTheme theme;
        RectTransform layer;
        CanvasGroup dim;

        public int Selected { get; private set; } = -1;

        public void Init(UIRoot ui, VNTheme t)
        {
            theme = t;
            layer = ui.Layer("Choices");
            var d = UIKit.Image(layer, "Dim", ProceduralArt.Gradient("choice_dim", new Color(0, 0, 0, 0.55f), new Color(0, 0, 0, 0.05f)), Color.white, true);
            dim = UIKit.Group(d.gameObject);
            dim.alpha = 0;
            layer.gameObject.SetActive(false);
        }

        /// <summary>Shows the options and waits for a pick. Read Selected afterwards.</summary>
        public IEnumerator Show(string prompt, List<ChoiceItem> items, float? timer)
        {
            Selected = -1;
            layer.gameObject.SetActive(true);
            foreach (Transform child in layer) if (child != dim.transform) Destroy(child.gameObject);

            var stack = UIKit.Rect("Stack", layer, false);
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0.5f);
            stack.pivot = new Vector2(0.5f, 0.5f);
            float frameW = ((RectTransform)layer).rect.width;
            float w = Mathf.Min(900, frameW - 100);
            float h = 132, gap = 30;
            float total = items.Count * h + (items.Count - 1) * gap;
            float y0 = total / 2f - h / 2f - 120f;

            var buttons = new List<RectTransform>();
            TextMeshProUGUI promptText = null;
            if (!string.IsNullOrEmpty(prompt))
            {
                var pr = UIKit.Rect("Prompt", stack, false);
                var bg = UIKit.Image(pr, "Bg", ProceduralArt.RoundedRect(30), theme.narrationColor);
                promptText = UIKit.Text(pr, "Text", prompt, theme.dialogueFontSize * 0.95f, theme.narrationTextColor);
                UIKit.Stretch(promptText.rectTransform, 30);
                float ph = Mathf.Max(110, promptText.GetPreferredValues(prompt, w - 60, 0).y + 60);
                UIKit.Place(pr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, y0 + h / 2f + 40), new Vector2(w, ph));
                buttons.Add(pr);
            }

            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var item = items[i];
                var shadowHolder = UIKit.Rect("Choice " + i, stack, false);
                UIKit.Place(shadowHolder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y0 - i * (h + gap)), new Vector2(w, h));
                var shadow = UIKit.Image(shadowHolder, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.3f));
                UIKit.Stretch(shadow.rectTransform, -22);
                shadow.rectTransform.anchoredPosition = new Vector2(0, -10);
                var accent = UIKit.Image(shadowHolder, "Accent", ProceduralArt.RoundedRect(60), item.Enabled ? theme.choiceAccent : theme.choiceLockedColor);
                accent.rectTransform.offsetMin = new Vector2(0, -8);
                var btn = UIKit.Button(shadowHolder, "Button", null, item.Enabled ? theme.choiceColor : Color.Lerp(theme.choiceColor, theme.choiceLockedColor, 0.6f), theme.choiceTextColor,
                    () => { if (Selected < 0 && item.Enabled) Selected = index; }, 44, 60);
                if (theme.choiceSprite != null) { btn.image.sprite = theme.choiceSprite; btn.image.type = Image.Type.Sliced; }
                btn.interactable = item.Enabled;
                var label = UIKit.Text(btn.transform, "Label", item.Text, theme.dialogueFontSize * 0.95f, item.Enabled ? theme.choiceTextColor : new Color(0.4f, 0.4f, 0.45f));
                UIKit.Stretch(label.rectTransform, 24);
                label.rectTransform.offsetMin = new Vector2(item.Tag != null || !item.Enabled ? 100 : 40, 12);
                label.rectTransform.offsetMax = new Vector2(item.Tag != null || !item.Enabled ? -100 : -40, -12);
                label.enableAutoSizing = true;
                label.fontSizeMin = 26;
                label.fontSizeMax = theme.dialogueFontSize * 0.95f;

                Sprite icon = !item.Enabled ? ProceduralArt.Lock() : TagIcon(item.Tag);
                if (icon != null)
                {
                    var ic = UIKit.Image(btn.transform, "Icon", icon, !item.Enabled ? new Color(0.45f, 0.45f, 0.5f) : theme.choiceAccent);
                    UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(58, 0), new Vector2(54, 54));
                }
                buttons.Add(shadowHolder);
            }

            // Timer bar
            Image timerFill = null;
            if (timer.HasValue)
            {
                var tb = UIKit.Rect("Timer", stack, false);
                UIKit.Place(tb, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y0 - items.Count * (h + gap) + h / 2f - 10), new Vector2(w * 0.8f, 18));
                UIKit.Image(tb, "Track", ProceduralArt.RoundedRect(9), new Color(1, 1, 1, 0.25f));
                timerFill = UIKit.Image(tb, "Fill", ProceduralArt.White(), theme.choiceAccent);
                timerFill.type = Image.Type.Filled;
                timerFill.fillMethod = Image.FillMethod.Horizontal;
                timerFill.fillAmount = 1;
                buttons.Add(tb);
            }

            // Animate in
            StartCoroutine(Tween.Fade(dim, 1f, 0.3f));
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                var g = UIKit.Group(b.gameObject);
                g.alpha = 0;
                b.localScale = Vector3.one * 0.85f;
                StartCoroutine(PopIn(b, g, i * 0.07f));
            }
            yield return Tween.Wait(0.1f);

            float remaining = timer ?? 0;
            while (Selected < 0)
            {
                if (timer.HasValue)
                {
                    remaining -= Time.deltaTime;
                    timerFill.fillAmount = Mathf.Clamp01(remaining / timer.Value);
                    timerFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.3f), theme.choiceAccent, timerFill.fillAmount * 1.5f);
                    if (remaining <= 0)
                    {
                        int def = items.FindIndex(x => x.IsDefault && x.Enabled);
                        if (def < 0) def = items.FindLastIndex(x => x.Enabled);
                        Selected = Mathf.Max(0, def);
                    }
                }
                yield return null;
            }

            // Celebrate the pick, fade the rest
            int offset = string.IsNullOrEmpty(prompt) ? 0 : 1;
            for (int i = 0; i < buttons.Count; i++)
            {
                var g = buttons[i].GetComponent<CanvasGroup>();
                if (i == Selected + offset)
                {
                    StartCoroutine(Pulse(buttons[i]));
                }
                else StartCoroutine(Tween.Fade(g, 0f, 0.2f));
            }
            yield return Tween.Wait(0.35f);
            var chosen = buttons[Selected + offset].GetComponent<CanvasGroup>();
            yield return Tween.All(this, Tween.Fade(chosen, 0f, 0.18f), Tween.Fade(dim, 0f, 0.25f));
            Destroy(stack.gameObject);
            layer.gameObject.SetActive(false);
        }

        static Sprite TagIcon(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            switch (tag.ToLowerInvariant())
            {
                case "premium": case "gem": case "gems": case "diamond": return ProceduralArt.Diamond();
                case "love": case "romance": case "heart": return ProceduralArt.Heart();
                case "star": case "special": return ProceduralArt.Star();
            }
            return AssetLibrary.LoadSprite("Images", tag) ?? ProceduralArt.Star();
        }

        IEnumerator PopIn(RectTransform rt, CanvasGroup g, float delay)
        {
            yield return Tween.Wait(delay);
            StartCoroutine(Tween.Fade(g, 1f, 0.2f));
            yield return Tween.Scale(rt, Vector3.one, 0.35f, Ease.OutBack);
        }

        IEnumerator Pulse(RectTransform rt)
        {
            yield return Tween.Scale(rt, Vector3.one * 1.07f, 0.12f, Ease.OutQuad);
            yield return Tween.Scale(rt, Vector3.one, 0.2f, Ease.OutBack);
        }
    }
}
