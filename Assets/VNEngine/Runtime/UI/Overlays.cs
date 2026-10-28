using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>Screen fades, flashes, toasts, item close-ups, location cards and the name input box.</summary>
    public sealed class Overlays : MonoBehaviour
    {
        VNTheme theme;
        UIRoot ui;
        Image fader;
        RectTransform imageLayer, toastLayer, inputLayer, errorLayer, cardLayer;
        readonly Dictionary<string, RectTransform> shownImages = new Dictionary<string, RectTransform>();
        readonly Queue<string> toasts = new Queue<string>();
        bool toastRunning;

        public void Init(UIRoot root, VNTheme t)
        {
            ui = root;
            theme = t;
            imageLayer = root.Layer("Images");
            cardLayer = root.Layer("Location");
            toastLayer = root.Layer("Toasts");
            inputLayer = root.Layer("Input");
            inputLayer.gameObject.SetActive(false);
            fader = UIKit.Image(root.Layer("Fader"), "Fader", ProceduralArt.White(), new Color(0, 0, 0, 0));
            fader.raycastTarget = false;
            errorLayer = root.Layer("Errors");
        }

        // ---------- fades ----------

        public bool IsFadedOut => fader.color.a > 0.99f;

        public IEnumerator FadeOut(Color color, float duration)
        {
            fader.raycastTarget = true;
            var from = new Color(color.r, color.g, color.b, fader.color.a);
            yield return Tween.Run(duration, Ease.InOutSine, k => fader.color = new Color(color.r, color.g, color.b, Mathf.Lerp(from.a, 1f, k)));
        }

        public IEnumerator FadeIn(float duration)
        {
            var c = fader.color;
            yield return Tween.Run(duration, Ease.InOutSine, k => fader.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, k)));
            fader.raycastTarget = false;
        }

        public void SetFade(float alpha, Color color)
        {
            fader.color = new Color(color.r, color.g, color.b, alpha);
            fader.raycastTarget = alpha > 0.5f;
        }

        public IEnumerator Flash(Color color, float duration)
        {
            fader.color = new Color(color.r, color.g, color.b, 0.95f);
            yield return Tween.Run(duration, Ease.OutQuad, k => fader.color = new Color(color.r, color.g, color.b, 0.95f * (1 - k)));
        }

        // ---------- toasts ----------

        public void Notify(string message, string icon = null)
        {
            toasts.Enqueue((icon ?? "") + "|" + message);
            if (!toastRunning) StartCoroutine(RunToasts());
        }

        IEnumerator RunToasts()
        {
            toastRunning = true;
            while (toasts.Count > 0)
            {
                var parts = toasts.Dequeue().Split(new[] { '|' }, 2);
                yield return ShowToast(parts[1], parts[0]);
            }
            toastRunning = false;
        }

        IEnumerator ShowToast(string message, string icon)
        {
            var frameH = ui.Frame.rect.height;
            var toast = UIKit.Rect("Toast", toastLayer, false);
            float w = Mathf.Min(860, ui.Frame.rect.width - 80);
            UIKit.Place(toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 220), new Vector2(w, 130));
            var shadow = UIKit.Image(toast, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.35f));
            UIKit.Stretch(shadow.rectTransform, -22);
            var bg = UIKit.Image(toast, "Bg", ProceduralArt.RoundedRect(40), theme.panelColor);
            var iconSprite = string.IsNullOrEmpty(icon) ? ProceduralArt.Star() : (AssetLibrary.LoadSprite("Images", icon) ?? ProceduralArt.Star());
            var ic = UIKit.Image(toast, "Icon", iconSprite, theme.accentColor);
            UIKit.Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70, 0), new Vector2(64, 64));
            var txt = UIKit.Text(toast, "Text", message, 38, theme.panelTextColor, TextAlignmentOptions.Left);
            UIKit.Stretch(txt.rectTransform);
            txt.rectTransform.offsetMin = new Vector2(124, 14);
            txt.rectTransform.offsetMax = new Vector2(-30, -14);
            float th = txt.GetPreferredValues(message, w - 160, 0).y + 50;
            toast.sizeDelta = new Vector2(w, Mathf.Max(130, th));
            AudioDirector.Instance?.PlaySound("success", 0.5f);

            yield return Tween.Anchored(toast, new Vector2(0, -60), 0.45f, Ease.OutBack);
            StartCoroutine(Spin(ic.rectTransform));
            yield return Tween.Wait(2.2f);
            yield return Tween.Anchored(toast, new Vector2(0, 240), 0.3f, Ease.InBack);
            Destroy(toast.gameObject);
        }

        static IEnumerator Spin(RectTransform rt)
        {
            yield return Tween.Run(0.6f, Ease.OutBack, k => { if (rt) rt.localRotation = Quaternion.Euler(0, 0, 360 * k); });
        }

        // ---------- item close-ups (@show letter) ----------

        public IEnumerator ShowImage(string name, string position, float duration)
        {
            if (shownImages.ContainsKey(name)) yield break;
            var sprite = AssetLibrary.Image(name);
            var holder = UIKit.Rect("Image " + name, imageLayer, false);
            float maxW = ui.Frame.rect.width * 0.75f, maxH = ui.Frame.rect.height * 0.5f;
            Vector2 size = new Vector2(maxW, maxW * 0.7f);
            if (sprite != null)
            {
                var s = sprite.rect.size;
                float k = Mathf.Min(maxW / s.x, maxH / s.y);
                size = s * k;
            }
            float y = position == "top" ? ui.Frame.rect.height * 0.22f : position == "bottom" ? -ui.Frame.rect.height * 0.15f : ui.Frame.rect.height * 0.05f;
            UIKit.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), size);
            var shadow = UIKit.Image(holder, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.4f));
            UIKit.Stretch(shadow.rectTransform, -30);
            if (sprite != null)
            {
                var img = UIKit.Image(holder, "Image", sprite, Color.white);
                img.preserveAspect = true;
            }
            else
            {
                UIKit.Image(holder, "Card", ProceduralArt.RoundedRect(30), new Color(1f, 0.97f, 0.9f));
                var t = UIKit.Text(holder, "Label", name.Replace('_', ' '), 54, new Color(0.3f, 0.25f, 0.2f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(t.rectTransform, 30);
                AssetLibrary.WarnOnce($"No image called '{name}'. Put one in Resources/Images.");
            }
            shownImages[name] = holder;
            var g = UIKit.Group(holder.gameObject);
            g.alpha = 0;
            holder.localScale = Vector3.one * 0.7f;
            holder.localRotation = Quaternion.Euler(0, 0, -6);
            StartCoroutine(Tween.Fade(g, 1, duration * 0.6f));
            yield return Tween.Run(duration, Ease.OutBack, k =>
            {
                holder.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, k);
                holder.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(-6f, 0f, k));
            });
        }

        public IEnumerator HideImage(string name, float duration)
        {
            var keys = new List<string>();
            if (string.IsNullOrEmpty(name) || name == "all") keys.AddRange(shownImages.Keys);
            else if (shownImages.ContainsKey(name)) keys.Add(name);
            foreach (var k in keys)
            {
                var holder = shownImages[k];
                shownImages.Remove(k);
                var g = UIKit.Group(holder.gameObject);
                StartCoroutine(Tween.Scale(holder, Vector3.one * 0.8f, duration, Ease.InBack));
                yield return Tween.Fade(g, 0, duration);
                Destroy(holder.gameObject);
            }
        }

        public void ClearImages()
        {
            foreach (var h in shownImages.Values) if (h) Destroy(h.gameObject);
            shownImages.Clear();
        }

        // ---------- location card ----------

        public IEnumerator LocationCard(string heading)
        {
            string title = heading;
            string sub = null;
            int dash = heading.LastIndexOf(" - ", System.StringComparison.Ordinal);
            if (dash > 0) { title = heading.Substring(0, dash); sub = heading.Substring(dash + 3); }
            foreach (var pre in new[] { "INT./EXT.", "EXT./INT.", "INT.", "EXT.", "INT", "EXT", "I/E" })
                if (title.StartsWith(pre)) { title = title.Substring(pre.Length).Trim(); break; }
            var card = UIKit.Rect("Location Card", cardLayer, false);
            UIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, ui.Frame.rect.height * 0.3f), new Vector2(ui.Frame.rect.width, 200));
            var g = UIKit.Group(card.gameObject);
            g.alpha = 0;
            var t = UIKit.Text(card, "Title", title, 72, Color.white, TextAlignmentOptions.Center, true);
            t.rectTransform.offsetMin = new Vector2(40, 60);
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(0, 0, 0, 140);
            if (sub != null)
            {
                var s = UIKit.Text(card, "Sub", sub, 40, new Color(1, 1, 1, 0.9f));
                s.rectTransform.offsetMax = new Vector2(-40, -130);
                s.characterSpacing = 12;
            }
            yield return Tween.Fade(g, 1, 0.5f);
            yield return Tween.Wait(1.2f);
            yield return Tween.Fade(g, 0, 0.6f);
            Destroy(card.gameObject);
        }

        // ---------- name input ----------

        public string InputResult { get; private set; }

        public IEnumerator AskText(string prompt, string defaultValue)
        {
            InputResult = null;
            inputLayer.gameObject.SetActive(true);
            foreach (Transform c in inputLayer) Destroy(c.gameObject);
            var dim = UIKit.Image(inputLayer, "Dim", ProceduralArt.White(), new Color(0, 0, 0, 0.5f), true);
            var panel = UIKit.Rect("Panel", inputLayer, false);
            float w = Mathf.Min(900, ui.Frame.rect.width - 80);
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(w, 520));
            var sh = UIKit.Image(panel, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.4f));
            UIKit.Stretch(sh.rectTransform, -26);
            UIKit.Image(panel, "Bg", ProceduralArt.RoundedRect(48), theme.panelColor);
            var title = UIKit.Text(panel, "Prompt", prompt, 52, theme.panelTextColor, TextAlignmentOptions.Center, true);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(w - 80, 120));

            // Input field
            var fieldRt = UIKit.Rect("Field", panel, false);
            UIKit.Place(fieldRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(w - 120, 120));
            var fieldBg = fieldRt.gameObject.AddComponent<Image>();
            fieldBg.sprite = ProceduralArt.RoundedRect(30);
            fieldBg.type = Image.Type.Sliced;
            fieldBg.color = Color.white;
            var viewport = UIKit.Rect("Viewport", fieldRt);
            UIKit.Stretch(viewport, 20);
            viewport.gameObject.AddComponent<RectMask2D>();
            var txt = UIKit.Text(viewport, "Text", "", 50, theme.bubbleTextColor, TextAlignmentOptions.Center);
            UIKit.Wrap(txt, false);
            var placeholder = UIKit.Text(viewport, "Placeholder", defaultValue ?? "", 50, new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Center);
            placeholder.fontStyle = FontStyles.Italic;
            var field = fieldRt.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = txt;
            field.placeholder = placeholder;
            field.characterLimit = 24;
            field.fontAsset = UIKit.BodyFont;
            field.pointSize = 50;
            field.caretColor = theme.accentColor;
            field.customCaretColor = true;
            field.selectionColor = new Color(theme.accentColor.r, theme.accentColor.g, theme.accentColor.b, 0.3f);

            bool done = false;
            var ok = UIKit.Button(panel, "OK", "OK", theme.accentColor, Color.white, () => done = true, 48);
            UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(360, 120));
            field.onSubmit.AddListener(_ => done = true);

            var g = UIKit.Group(panel.gameObject);
            g.alpha = 0;
            panel.localScale = Vector3.one * 0.85f;
            StartCoroutine(Tween.Fade(g, 1, 0.2f));
            yield return Tween.Scale(panel, Vector3.one, 0.3f, Ease.OutBack);
            field.ActivateInputField();

            while (!done) yield return null;
            InputResult = string.IsNullOrWhiteSpace(field.text) ? defaultValue : field.text.Trim();
            yield return Tween.Fade(g, 0, 0.15f);
            inputLayer.gameObject.SetActive(false);
            foreach (Transform c in inputLayer) Destroy(c.gameObject);
        }

        // ---------- errors ----------

        /// <summary>Shows script errors on screen with line numbers so writers can fix them fast.</summary>
        public void ShowErrors(string title, IEnumerable<StoryError> errors)
        {
            foreach (Transform c in errorLayer) Destroy(c.gameObject);
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var e in errors)
            {
                if (n++ > 12) { sb.AppendLine("…and more. See the Console."); break; }
                sb.Append(e.IsWarning ? "<color=#ffcc66>Line " : "<color=#ff7a7a>Line ").Append(e.Line).Append("</color>  ").AppendLine(e.Message);
                if (!string.IsNullOrEmpty(e.Source)) sb.Append("<color=#9aa0b4><size=80%>    ").Append(e.Source.Replace("<", "&lt;")).AppendLine("</size></color>");
            }
            ShowMessage(title, sb.ToString());
        }

        public void ShowMessage(string title, string body)
        {
            foreach (Transform c in errorLayer) Destroy(c.gameObject);
            UIKit.Image(errorLayer, "Dim", ProceduralArt.White(), new Color(0, 0, 0, 0.7f), true);
            var panel = UIKit.Rect("Panel", errorLayer);
            UIKit.Stretch(panel, 40);
            UIKit.Image(panel, "Bg", ProceduralArt.RoundedRect(40), new Color(0.12f, 0.11f, 0.16f, 0.98f));
            var t = UIKit.Text(panel, "Title", title, 54, new Color(1f, 0.5f, 0.55f), TextAlignmentOptions.TopLeft, true);
            t.rectTransform.offsetMin = new Vector2(50, 0);
            t.rectTransform.offsetMax = new Vector2(-50, -50);
            var b = UIKit.Text(panel, "Body", body, 34, Color.white, TextAlignmentOptions.TopLeft);
            b.rectTransform.offsetMin = new Vector2(50, 160);
            b.rectTransform.offsetMax = new Vector2(-50, -150);
            var close = UIKit.Button(panel, "Close", "Close", theme.accentColor, Color.white, () => { foreach (Transform c in errorLayer) Destroy(c.gameObject); }, 40);
            UIKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(320, 100));
        }
    }
}
