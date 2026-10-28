using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>Builds the engine's UI in code so there are no prefabs to wire up.</summary>
    public static class UIKit
    {
        /// <summary>Word wrap on or off; the property was renamed in Unity 6.</summary>
        public static void Wrap(TMP_Text t, bool on)
        {
#if UNITY_6000_0_OR_NEWER
            t.textWrappingMode = on ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
#else
            t.enableWordWrapping = on;
#endif
        }

        static TMP_FontAsset bodyFont, titleFont;

        public static TMP_FontAsset BodyFont
        {
            get
            {
                if (bodyFont != null) return bodyFont;
                var theme = VNTheme.Load();
                if (theme.font != null) return bodyFont = theme.font;
                bodyFont = FromFont(theme.sourceFont) ?? FromFont(Resources.Load<Font>("VNEngine/Fonts/VarelaRound-Regular")) ?? TMP_Settings.defaultFontAsset;
                return bodyFont;
            }
        }

        public static TMP_FontAsset TitleFont
        {
            get
            {
                if (titleFont != null) return titleFont;
                var theme = VNTheme.Load();
                if (theme.titleFont != null) return titleFont = theme.titleFont;
                titleFont = FromFont(Resources.Load<Font>("VNEngine/Fonts/LilitaOne-Regular")) ?? BodyFont;
                return titleFont;
            }
        }

        static TMP_FontAsset FromFont(Font f)
        {
            if (f == null) return null;
            try
            {
                var asset = TMP_FontAsset.CreateFontAsset(f);
                if (asset != null && TMP_Settings.defaultFontAsset != null && asset.fallbackFontAssetTable != null)
                    asset.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
                return asset;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VN Engine] Could not build a font from " + f.name + ": " + e.Message);
                return null;
            }
        }

        public static RectTransform Rect(string name, Transform parent, bool stretch = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (stretch) Stretch(rt);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool title = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = title ? TitleFont : BodyFont;
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            Wrap(t, true);
            t.richText = true;
            return t;
        }

        /// <summary>A rounded button with a label, press bounce and click sound.</summary>
        public static Button Button(Transform parent, string name, string label, Color fill, Color textColor, Action onClick, float fontSize = 44f, int radius = 48)
        {
            var bg = Image(parent, name, ProceduralArt.RoundedRect(radius), fill, true);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            bg.gameObject.AddComponent<PressFeedback>();
            if (!string.IsNullOrEmpty(label))
            {
                var t = Text(bg.transform, "Label", label, fontSize, textColor);
                Stretch(t.rectTransform, 12);
            }
            btn.onClick.AddListener(() =>
            {
                AudioDirector.Instance?.Click();
                onClick?.Invoke();
            });
            return btn;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        /// <summary>Adds a soft drop shadow behind a rounded panel.</summary>
        public static Image Shadow(RectTransform target, float spread = 26f, float alpha = 0.28f, Vector2 offset = default)
        {
            var sh = Image(target.parent, target.name + " Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, alpha));
            var rt = sh.rectTransform;
            rt.anchorMin = target.anchorMin; rt.anchorMax = target.anchorMax; rt.pivot = target.pivot;
            rt.anchoredPosition = target.anchoredPosition + (offset == default ? new Vector2(0, -10) : offset);
            rt.sizeDelta = target.sizeDelta + new Vector2(spread * 2, spread * 2);
            rt.SetSiblingIndex(target.GetSiblingIndex());
            return sh;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Squash-and-release on press, like mobile game buttons.</summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Vector3 baseScale = Vector3.one;
        bool pressed;
        Coroutine anim;

        void Awake() => baseScale = transform.localScale;

        public void OnPointerDown(PointerEventData e)
        {
            pressed = true;
            Animate(baseScale * 0.94f, 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!pressed) return;
            pressed = false;
            Animate(baseScale, 0.3f, Ease.OutElastic);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (!pressed) return;
            pressed = false;
            Animate(baseScale, 0.2f, Ease.OutQuad);
        }

        void Animate(Vector3 to, float d, Ease ease)
        {
            if (!isActiveAndEnabled) return;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Tween.Scale(transform, to, d, ease, true));
        }
    }

    /// <summary>Invisible full-area click catcher.</summary>
    public sealed class TapArea : MonoBehaviour, IPointerClickHandler
    {
        public event Action Tapped;
        public void OnPointerClick(PointerEventData e) => Tapped?.Invoke();

        public static TapArea Create(Transform parent, string name = "Tap Area")
        {
            var img = UIKit.Image(parent, name, null, new Color(0, 0, 0, 0), true);
            return img.gameObject.AddComponent<TapArea>();
        }
    }

    /// <summary>The overlay canvas, kept inside the same letterboxed frame as the stage camera.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public CanvasScaler Scaler { get; private set; }
        public RectTransform Frame { get; private set; }
        VNTheme theme;

        public static UIRoot Create(Transform parent, VNTheme theme)
        {
            var go = new GameObject("VN UI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            var root = go.AddComponent<UIRoot>();
            root.theme = theme;
            root.Canvas = go.AddComponent<Canvas>();
            root.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.Canvas.sortingOrder = 10;
            root.Scaler = go.AddComponent<CanvasScaler>();
            root.Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            bool portrait = theme.designAspect < 1f;
            root.Scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            root.Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            root.Frame = UIKit.Rect("Frame", go.transform);
            var mask = root.Frame.gameObject.AddComponent<RectMask2D>();
            root.UpdateFrame(new Rect(0, 0, 1, 1));
            return root;
        }

        public void UpdateFrame(Rect viewport)
        {
            Frame.anchorMin = viewport.min;
            Frame.anchorMax = viewport.max;
            Frame.offsetMin = Frame.offsetMax = Vector2.zero;
            float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : theme.designAspect;
            // Wider than the design: fit height. Taller: fit width.
            Scaler.matchWidthOrHeight = screenAspect > theme.designAspect ? 1f : 0f;
        }

        /// <summary>A full-frame layer. Later layers draw on top.</summary>
        public RectTransform Layer(string name)
        {
            var existing = Frame.Find(name);
            if (existing != null) return (RectTransform)existing;
            return UIKit.Rect(name, Frame);
        }

        /// <summary>Converts a world point seen by the stage camera to a local point in a RectTransform.</summary>
        public Vector2 WorldToLocal(Camera cam, Vector3 world, RectTransform target)
        {
            Vector2 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screen, null, out var local);
            return local;
        }
    }

    /// <summary>Keyboard shortcuts that work with either input system.</summary>
    public static class VNInput
    {
        public static bool Advance()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) return true;
#endif
            return false;
        }

        public static bool Back()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        /// <summary>Held to fast-forward (Ctrl).</summary>
        public static bool SkipHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return true;
#endif
            return false;
        }
    }

    public static class EventSystemSetup
    {
        /// <summary>Makes sure the scene has an EventSystem with an input module that matches the project's input settings.</summary>
        public static void Ensure()
        {
#if UNITY_2023_1_OR_NEWER
            if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
#else
            if (EventSystem.current != null || UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
#endif
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            Type inputSystemModule = null;
#if ENABLE_INPUT_SYSTEM
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                inputSystemModule = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                if (inputSystemModule != null) break;
            }
#endif
            if (inputSystemModule != null) go.AddComponent(inputSystemModule);
            else go.AddComponent<StandaloneInputModule>();
        }
    }
}
