using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>What to show in one bubble.</summary>
    public sealed class DialogueLine
    {
        public string Name;
        public Color NameColor;
        public string Text;
        public string Style = "speech";
        /// <summary>Speaker's head in world space, or null when they aren't on stage.</summary>
        public Vector3? Anchor;
        public AudioClip Voice;
    }

    /// <summary>
    /// Episode-style speech bubbles that pop in above the speaker, narration boxes,
    /// thought bubbles, shouts, whispers and phone messages, with a typewriter.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        VNTheme theme;
        UIRoot ui;
        Func<Camera> stageCamera;
        RectTransform layer;

        // bubble parts
        RectTransform bubble, bubbleBody, tail, nameTag, nextArrow, thoughtDots;
        Image fill, outline, tailFill, tailOutline, nameFill, shadow;
        TextMeshProUGUI text, nameText;
        CanvasGroup bubbleGroup;

        public bool IsTyping { get; private set; }
        public bool IsShowing { get; private set; }
        public float SpeedMultiplier = 1f;
        public bool Instant;

        /// <summary>Raised for each revealed character, for lip flap and blips.</summary>
        public event Action<char> Letter;

        bool completeRequested;
        Coroutine bob;
        string currentStyle;

        const float Padding = 46f;
        const float MaxWidth = 900f;

        public static float TextSpeedSetting
        {
            get => PlayerPrefs.GetFloat("vn_text_speed", 1f);
            set => PlayerPrefs.SetFloat("vn_text_speed", value);
        }

        public void Init(UIRoot root, VNTheme t, Func<Camera> cam)
        {
            ui = root;
            theme = t;
            stageCamera = cam;
            layer = root.Layer("Dialogue");
            Build();
            bubble.gameObject.SetActive(false);
        }

        void Build()
        {
            bubble = UIKit.Rect("Bubble", layer, false);
            bubbleGroup = UIKit.Group(bubble.gameObject);
            bubbleGroup.blocksRaycasts = false;

            bubbleBody = UIKit.Rect("Body", bubble, true);
            shadow = UIKit.Image(bubbleBody, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.22f));
            UIKit.Stretch(shadow.rectTransform, -24);
            shadow.rectTransform.anchoredPosition = new Vector2(0, -12);

            // tail sits behind the body so the seam is hidden
            tail = UIKit.Rect("Tail", bubbleBody, false);
            tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0f);
            tail.pivot = new Vector2(0.5f, 1f);
            tail.sizeDelta = new Vector2(64, 64);
            tailOutline = UIKit.Image(tail, "Outline", ProceduralArt.Tail(), theme.bubbleOutline);
            tailOutline.rectTransform.offsetMin = new Vector2(-6, -8);
            tailOutline.rectTransform.offsetMax = new Vector2(6, 0);
            tailFill = UIKit.Image(tail, "Fill", ProceduralArt.Tail(), theme.bubbleColor);

            outline = UIKit.Image(bubbleBody, "Outline", theme.bubbleSprite != null ? theme.bubbleSprite : ProceduralArt.RoundedRect(48), theme.bubbleOutline);
            fill = UIKit.Image(bubbleBody, "Fill", theme.bubbleSprite != null ? theme.bubbleSprite : ProceduralArt.RoundedRect(48), theme.bubbleColor);
            UIKit.Stretch(fill.rectTransform, 6);
            // cover the top of the tail with the fill
            var cover = UIKit.Image(tail, "Cover", ProceduralArt.White(), theme.bubbleColor);
            UIKit.Place(cover.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 10), new Vector2(46, 16));
            cover.transform.SetParent(bubbleBody, true);
            cover.transform.SetSiblingIndex(fill.transform.GetSiblingIndex() + 1);
            tailCover = cover.rectTransform;

            thoughtDots = UIKit.Rect("Thought Dots", bubbleBody, false);
            thoughtDots.anchorMin = thoughtDots.anchorMax = new Vector2(0.5f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float s = 34 - i * 9;
                var o = UIKit.Image(thoughtDots, "DotOutline" + i, ProceduralArt.Circle(32), theme.bubbleOutline);
                UIKit.Place(o.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-i * 26, -i * 34 - 26), new Vector2(s + 10, s + 10));
                var d = UIKit.Image(thoughtDots, "Dot" + i, ProceduralArt.Circle(32), theme.thoughtColor);
                UIKit.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-i * 26, -i * 34 - 26), new Vector2(s, s));
            }

            text = UIKit.Text(bubbleBody, "Text", "", theme.dialogueFontSize, theme.bubbleTextColor, TextAlignmentOptions.Center);
            UIKit.Stretch(text.rectTransform, Padding);
            text.lineSpacing = 4f;

            nameTag = UIKit.Rect("Name Tag", bubble, false);
            nameTag.anchorMin = nameTag.anchorMax = new Vector2(0f, 1f);
            nameTag.pivot = new Vector2(0f, 0.5f);
            nameFill = UIKit.Image(nameTag, "Fill", ProceduralArt.RoundedRect(36), theme.defaultNameColor);
            nameText = UIKit.Text(nameTag, "Name", "", theme.nameFontSize, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(nameText.rectTransform, 6);
            UIKit.Wrap(nameText, false);

            nextArrow = UIKit.Image(bubble, "Next", ProceduralArt.Triangle("down"), theme.bubbleOutline).rectTransform;
            UIKit.Place(nextArrow, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-48, 34), new Vector2(30, 30));
        }

        RectTransform tailCover;

        Vector2 FrameSize => ui.Frame.rect.size;

        public IEnumerator Show(DialogueLine line)
        {
            if (IsShowing && currentStyle != null) yield return HideRoutine(0.08f);
            currentStyle = (line.Style ?? "speech").ToLowerInvariant();
            bool narration = currentStyle == "narration" || currentStyle == "narrate" || string.IsNullOrEmpty(line.Name) && currentStyle == "speech";
            if (narration) currentStyle = "narration";
            bool thought = currentStyle == "thought" || currentStyle == "thinks" || currentStyle == "think";
            bool shout = currentStyle == "shout" || currentStyle == "shouts" || currentStyle == "yell";
            bool whisper = currentStyle == "whisper" || currentStyle == "whispers";
            bool phone = currentStyle == "phone" || currentStyle == "text" || currentStyle == "sms";

            // Style the bubble
            Color bodyColor = narration ? theme.narrationColor : thought ? theme.thoughtColor : phone ? theme.phoneColor : theme.bubbleColor;
            Color textColor = narration || phone ? theme.narrationTextColor : theme.bubbleTextColor;
            var bodySprite = narration && theme.narrationSprite != null ? theme.narrationSprite
                : theme.bubbleSprite != null && !narration ? theme.bubbleSprite
                : ProceduralArt.RoundedRect(narration ? 28 : thought ? 80 : phone ? 44 : 48);
            fill.sprite = bodySprite;
            outline.sprite = bodySprite;
            fill.color = bodyColor;
            tailFill.color = bodyColor;
            tailCover.GetComponent<Image>().color = bodyColor;
            outline.enabled = !narration && !phone;
            outline.color = shout ? Color.Lerp(theme.bubbleOutline, new Color(0.85f, 0.15f, 0.2f), 0.6f) : theme.bubbleOutline;
            UIKit.Stretch(fill.rectTransform, narration || phone ? 0 : shout ? 9 : 6);
            text.color = whisper ? new Color(textColor.r, textColor.g, textColor.b, 0.75f) : textColor;
            text.fontStyle = whisper ? FontStyles.Italic : shout ? FontStyles.Bold : FontStyles.Normal;
            text.fontSize = theme.dialogueFontSize * (shout ? 1.12f : whisper ? 0.9f : 1f);
            text.alignment = phone ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            shadow.color = new Color(0, 0, 0, narration ? 0.35f : 0.22f);

            bool hasName = !narration && !string.IsNullOrEmpty(line.Name);
            nameTag.gameObject.SetActive(hasName);
            if (hasName)
            {
                nameText.text = line.Name.ToUpperInvariant();
                nameFill.color = line.NameColor;
                nameText.ForceMeshUpdate();
                float w = nameText.GetPreferredValues(nameText.text).x + 56;
                nameTag.sizeDelta = new Vector2(Mathf.Max(140, w), 64);
                nameTag.anchoredPosition = new Vector2(34, 4);
            }

            // Size to fit the text
            float frameW = FrameSize.x, frameH = FrameSize.y;
            float maxW = Mathf.Min(MaxWidth, frameW - 70);
            text.text = line.Text;
            text.maxVisibleCharacters = 0;
            Vector2 pref = text.GetPreferredValues(line.Text, maxW - Padding * 2, 0);
            float w2 = Mathf.Clamp(pref.x + Padding * 2 + 8, narration ? maxW : 340, maxW);
            float h = Mathf.Max(phone ? 120 : 150, text.GetPreferredValues(line.Text, w2 - Padding * 2, 0).y + Padding * 2);
            bubble.sizeDelta = new Vector2(w2, h);
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);

            // Position
            Vector2 pos;
            bool showTail = !narration && !phone && line.Anchor.HasValue;
            tail.gameObject.SetActive(showTail && !thought);
            tailCover.gameObject.SetActive(showTail && !thought);
            thoughtDots.gameObject.SetActive(showTail && thought);
            if (narration)
            {
                pos = new Vector2(0, frameH * 0.28f);
                bubble.pivot = new Vector2(0.5f, 0.5f);
            }
            else if (showTail)
            {
                Vector2 head = ui.WorldToLocal(stageCamera(), line.Anchor.Value, ui.Frame);
                float top = frameH / 2f - 150f;
                float bottom = head.y + 70f;
                bottom = Mathf.Min(bottom, top - h);
                bottom = Mathf.Max(bottom, -frameH / 2f + 360f);
                float x = Mathf.Clamp(head.x, -frameW / 2f + w2 / 2f + 24, frameW / 2f - w2 / 2f - 24);
                bubble.pivot = new Vector2(0.5f, 0f);
                pos = new Vector2(x, bottom);
                float tailX = Mathf.Clamp(head.x - x, -w2 / 2f + 70, w2 / 2f - 70);
                tail.anchoredPosition = new Vector2(tailX, 8);
                tailCover.anchoredPosition = new Vector2(tailX, 0);
                tailCover.anchorMin = tailCover.anchorMax = new Vector2(0.5f, 0f);
                tailCover.pivot = new Vector2(0.5f, 0f);
                // lean the tail toward the speaker
                float lean = Mathf.Clamp((head.x - (x + tailX)) / 6f, -25f, 25f);
                tail.localRotation = Quaternion.Euler(0, 0, lean);
                thoughtDots.anchoredPosition = new Vector2(tailX + 20, 0);
            }
            else
            {
                bubble.pivot = new Vector2(0.5f, 0.5f);
                pos = new Vector2(phone ? frameW * 0.05f : 0, frameH * 0.2f);
            }
            bubble.anchoredPosition = pos;
            nextArrow.gameObject.SetActive(false);
            var arrowImg = nextArrow.GetComponent<Image>();
            arrowImg.color = narration || phone ? theme.narrationTextColor : theme.bubbleOutline;

            // Pop in
            bubble.gameObject.SetActive(true);
            IsShowing = true;
            bubbleGroup.alpha = 0;
            bubble.localScale = Vector3.one * (narration ? 0.96f : 0.6f);
            AudioDirector.Instance?.Pop();
            float popTime = Instant ? 0.05f : narration ? 0.25f : 0.3f;
            StartCoroutine(Tween.Fade(bubbleGroup, 1f, popTime * 0.6f, Ease.OutQuad));
            if (shout) StartCoroutine(ShoutShake());
            yield return Tween.Scale(bubble, Vector3.one, popTime, narration ? Ease.OutCubic : Ease.OutBack);

            // Typewriter
            yield return TypeRoutine(line);
            nextArrow.gameObject.SetActive(true);
            if (bob != null) StopCoroutine(bob);
            bob = StartCoroutine(BobArrow());
        }

        IEnumerator ShoutShake()
        {
            float t = 0;
            Vector2 p = bubble.anchoredPosition;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                bubble.anchoredPosition = p + new Vector2(Mathf.Sin(t * 90f), Mathf.Cos(t * 70f)) * 9f * (1 - t / 0.35f);
                yield return null;
            }
            bubble.anchoredPosition = p;
        }

        IEnumerator TypeRoutine(DialogueLine line)
        {
            IsTyping = true;
            completeRequested = false;
            text.ForceMeshUpdate();
            int total = text.textInfo.characterCount;
            float cps = theme.textSpeed * TextSpeedSetting * SpeedMultiplier;
            float budget = 0f;
            int shown = 0;
            int blipCounter = 0;
            while (shown < total)
            {
                if (completeRequested || Instant || cps > 900f) { shown = total; break; }
                budget += Time.deltaTime * cps;
                while (budget >= 1f && shown < total)
                {
                    budget -= 1f;
                    char c = text.textInfo.characterInfo[shown].character;
                    shown++;
                    Letter?.Invoke(c);
                    if (char.IsLetterOrDigit(c) && (blipCounter++ % 2 == 0)) AudioDirector.Instance?.TypeBlip(line.Voice);
                    if (c == '.' || c == '!' || c == '?') budget -= cps * 0.18f;
                    else if (c == ',' || c == ';' || c == ':' || c == '—') budget -= cps * 0.08f;
                }
                text.maxVisibleCharacters = shown;
                yield return null;
            }
            text.maxVisibleCharacters = total;
            IsTyping = false;
        }

        IEnumerator BobArrow()
        {
            var basePos = nextArrow.anchoredPosition;
            float t = 0;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                nextArrow.anchoredPosition = new Vector2(basePos.x, 34 + Mathf.Abs(Mathf.Sin(t * 4f)) * 8f);
                yield return null;
            }
        }

        public void CompleteTyping() => completeRequested = true;

        public IEnumerator Hide(float duration = 0.12f)
        {
            if (!IsShowing) yield break;
            yield return HideRoutine(duration);
        }

        IEnumerator HideRoutine(float duration)
        {
            if (bob != null) { StopCoroutine(bob); bob = null; }
            nextArrow.gameObject.SetActive(false);
            yield return Tween.All(this,
                Tween.Fade(bubbleGroup, 0f, duration, Ease.InQuad),
                Tween.Scale(bubble, Vector3.one * 0.92f, duration, Ease.InQuad));
            bubble.gameObject.SetActive(false);
            IsShowing = false;
            currentStyle = null;
        }

        public void HideInstant()
        {
            if (bob != null) { StopCoroutine(bob); bob = null; }
            bubble.gameObject.SetActive(false);
            IsShowing = false;
            IsTyping = false;
            currentStyle = null;
        }
    }
}
