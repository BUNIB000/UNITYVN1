using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>In-story buttons (menu, auto, skip) and the pause menu with history and settings.</summary>
    public sealed class GameMenu : MonoBehaviour
    {
        VNTheme theme;
        UIRoot ui;
        RectTransform hud, menuLayer;
        TextMeshProUGUI autoLabel, skipLabel;
        public bool IsOpen { get; private set; }

        public Func<bool> GetAuto, GetSkip;
        public Action ToggleAuto, ToggleSkip, ExitToLibrary, RestartEpisode;
        public Func<IReadOnlyList<HistoryEntry>> GetHistory;

        public void Init(UIRoot root, VNTheme t)
        {
            ui = root;
            theme = t;
            hud = root.Layer("HUD");
            menuLayer = root.Layer("Menu");
            menuLayer.gameObject.SetActive(false);

            var menuBtn = UIKit.Button(hud, "Menu Button", null, new Color(0, 0, 0, 0.35f), Color.white, Open, 30, 40);
            UIKit.Place((RectTransform)menuBtn.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -40), new Vector2(110, 110));
            var icon = UIKit.Image(menuBtn.transform, "Icon", ProceduralArt.MenuIcon(), Color.white);
            UIKit.Stretch(icon.rectTransform, 26);

            autoLabel = Pill("Auto", new Vector2(-160, -40), () => { ToggleAuto?.Invoke(); RefreshHud(); });
            skipLabel = Pill("Skip", new Vector2(-330, -40), () => { ToggleSkip?.Invoke(); RefreshHud(); });
            RefreshHud();
        }

        TextMeshProUGUI Pill(string label, Vector2 pos, Action onClick)
        {
            var b = UIKit.Button(hud, label, label.ToUpperInvariant(), new Color(0, 0, 0, 0.35f), Color.white, onClick, 30, 40);
            UIKit.Place((RectTransform)b.transform, new Vector2(1, 1), new Vector2(1, 1), pos, new Vector2(150, 74));
            return b.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void RefreshHud()
        {
            if (autoLabel == null) return;
            bool auto = GetAuto?.Invoke() ?? false, skip = GetSkip?.Invoke() ?? false;
            autoLabel.transform.parent.GetComponent<Image>().color = auto ? theme.accentColor : new Color(0, 0, 0, 0.35f);
            skipLabel.transform.parent.GetComponent<Image>().color = skip ? theme.accentColor : new Color(0, 0, 0, 0.35f);
        }

        public void SetHudVisible(bool visible) => hud.gameObject.SetActive(visible);

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Time.timeScale = 0f;
            BuildMain();
        }

        public void Close()
        {
            IsOpen = false;
            Time.timeScale = 1f;
            Clear();
            menuLayer.gameObject.SetActive(false);
        }

        void Clear()
        {
            foreach (Transform c in menuLayer) Destroy(c.gameObject);
        }

        RectTransform Panel(string title, float height)
        {
            Clear();
            menuLayer.gameObject.SetActive(true);
            var dim = UIKit.Image(menuLayer, "Dim", ProceduralArt.White(), new Color(0, 0, 0, 0.6f), true);
            var panel = UIKit.Rect("Panel", menuLayer, false);
            float w = Mathf.Min(860, ui.Frame.rect.width - 80);
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, height));
            var sh = UIKit.Image(panel, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.45f));
            UIKit.Stretch(sh.rectTransform, -26);
            UIKit.Image(panel, "Bg", ProceduralArt.RoundedRect(48), theme.panelColor);
            var t = UIKit.Text(panel, "Title", title, 64, theme.panelTextColor, TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(w, 100));
            panel.localScale = Vector3.one * 0.9f;
            StartCoroutine(Tween.Scale(panel, Vector3.one, 0.25f, Ease.OutBack, true));
            return panel;
        }

        Button Row(RectTransform panel, string label, float y, Action onClick, Color? color = null)
        {
            var b = UIKit.Button(panel, label, label, color ?? new Color(1, 1, 1, 0.1f), theme.panelTextColor, onClick, 42, 40);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(panel.sizeDelta.x - 100, 110));
            return b;
        }

        void BuildMain()
        {
            var p = Panel("Paused", 1280);
            float y = -170;
            Row(p, "Resume", y, Close, theme.accentColor); y -= 135;
            Row(p, "History", y, BuildHistory); y -= 135;
            Row(p, "Text speed: " + SpeedName(), y, () => { CycleSpeed(); BuildMain(); }); y -= 135;
            Row(p, "Music volume: " + Mathf.RoundToInt(AudioDirector.MusicVolume * 100) + "%", y, () => { AudioDirector.MusicVolume = Step(AudioDirector.MusicVolume); BuildMain(); }); y -= 135;
            Row(p, "Sound volume: " + Mathf.RoundToInt(AudioDirector.SoundVolume * 100) + "%", y, () => { AudioDirector.SoundVolume = Step(AudioDirector.SoundVolume); BuildMain(); }); y -= 135;
            Row(p, "Restart episode", y, () => { Close(); RestartEpisode?.Invoke(); }); y -= 135;
            Row(p, "Back to stories", y, () => { Close(); ExitToLibrary?.Invoke(); });
        }

        static float Step(float v)
        {
            v = Mathf.Round(v * 4f) / 4f + 0.25f;
            return v > 1.01f ? 0f : v;
        }

        static string SpeedName()
        {
            float s = DialogueView.TextSpeedSetting;
            if (s < 0.8f) return "Slow";
            if (s < 1.3f) return "Normal";
            if (s < 3f) return "Fast";
            return "Instant";
        }

        static void CycleSpeed()
        {
            float s = DialogueView.TextSpeedSetting;
            DialogueView.TextSpeedSetting = s < 0.8f ? 1f : s < 1.3f ? 1.8f : s < 3f ? 100f : 0.6f;
        }

        void BuildHistory()
        {
            var p = Panel("History", ui.Frame.rect.height - 200);
            var scrollRt = UIKit.Rect("Scroll", p);
            scrollRt.offsetMin = new Vector2(40, 170);
            scrollRt.offsetMax = new Vector2(-40, -150);
            scrollRt.gameObject.AddComponent<RectMask2D>();
            var img = scrollRt.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.01f);
            var content = UIKit.Rect("Content", scrollRt, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;

            var sb = new System.Text.StringBuilder();
            var history = GetHistory?.Invoke() ?? new List<HistoryEntry>();
            foreach (var h in history)
            {
                if (!string.IsNullOrEmpty(h.speaker)) sb.Append("<color=").Append(UIKit.Hex(theme.accentColor)).Append("><b>").Append(h.speaker).Append("</b></color>\n");
                sb.Append(h.text).Append("\n\n");
            }
            if (history.Count == 0) sb.Append("Nothing yet.");
            var text = UIKit.Text(content, "Text", sb.ToString(), 36, theme.panelTextColor, TextAlignmentOptions.TopLeft);
            float w = p.sizeDelta.x - 80;
            float h2 = text.GetPreferredValues(sb.ToString(), w, 0).y + 40;
            content.sizeDelta = new Vector2(0, h2);
            UIKit.Stretch(text.rectTransform);
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0;

            var back = UIKit.Button(p, "Back", "Back", theme.accentColor, Color.white, BuildMain, 42, 40);
            UIKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(340, 100));
        }
    }

    /// <summary>The story library: series cards, then episodes with Continue / Start over.</summary>
    public sealed class LibraryView : MonoBehaviour
    {
        VNTheme theme;
        UIRoot ui;
        RectTransform layer;
        public Action<SeriesInfo, EpisodeInfo, bool> Play; // bool = continue from save

        public void Init(UIRoot root, VNTheme t)
        {
            ui = root;
            theme = t;
            layer = root.Layer("Library");
            layer.gameObject.SetActive(false);
        }

        public bool IsOpen => layer.gameObject.activeSelf;

        public void Hide()
        {
            layer.gameObject.SetActive(false);
            foreach (Transform c in layer) Destroy(c.gameObject);
        }

        Image Background()
        {
            foreach (Transform c in layer) Destroy(c.gameObject);
            layer.gameObject.SetActive(true);
            var bgSprite = theme.menuBackground != null ? theme.menuBackground : ProceduralArt.Gradient("lib_bg", new Color(0.09f, 0.07f, 0.16f), new Color(0.32f, 0.16f, 0.36f));
            var bg = UIKit.Image(layer, "Background", bgSprite, Color.white, true);
            bg.preserveAspect = false;
            return bg;
        }

        public void ShowSeriesList()
        {
            Background();
            var title = UIKit.Text(layer, "Title", "Stories", 96, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(900, 140));

            var all = StoryCatalog.Series;
            if (all.Count == 0)
            {
                var msg = UIKit.Text(layer, "Empty", "No stories yet.\n\nPut a story file (.txt or .story) in a folder called <b>Resources/Stories</b>.", 44, new Color(1, 1, 1, 0.85f));
                UIKit.Stretch(msg.rectTransform, 80);
                return;
            }

            var content = ScrollArea(new Vector2(0, 260), 0);
            float w = ui.Frame.rect.width - 100;
            float cardH = 360, gap = 40, y = -20;
            int i = 0;
            foreach (var s in all)
            {
                var card = SeriesCard(content, s, w, cardH);
                card.anchoredPosition = new Vector2(0, y);
                y -= cardH + gap;
                var g = UIKit.Group(card.gameObject);
                g.alpha = 0;
                StartCoroutine(FadeUp(card, g, i++ * 0.06f));
            }
            content.sizeDelta = new Vector2(0, -y + 40);
        }

        RectTransform ScrollArea(Vector2 topInset, float bottom)
        {
            var scrollRt = UIKit.Rect("Scroll", layer);
            scrollRt.offsetMin = new Vector2(0, bottom);
            scrollRt.offsetMax = new Vector2(0, -topInset.y);
            scrollRt.gameObject.AddComponent<RectMask2D>();
            var hit = scrollRt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.01f);
            var content = UIKit.Rect("Content", scrollRt, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40;
            return content;
        }

        RectTransform SeriesCard(RectTransform parent, SeriesInfo s, float w, float h)
        {
            var card = UIKit.Rect(s.Name, parent, false);
            UIKit.Place(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(w, h));
            var sh = UIKit.Image(card, "Shadow", ProceduralArt.SoftShadow(40), new Color(0, 0, 0, 0.45f));
            UIKit.Stretch(sh.rectTransform, -24);
            var btn = UIKit.Button(card, "Card", null, Color.white, Color.white, () => ShowSeries(s), 40, 44);
            var mask = UIKit.Rect("Mask", btn.transform);
            mask.gameObject.AddComponent<RectMask2D>();
            var cover = AssetLibrary.Cover(s.Cover ?? s.Name) ?? AssetLibrary.Cover(s.Episodes[0].File);
            var color = ProceduralArt.ColorFromName(s.Name, 0.55f, 0.75f);
            var art = UIKit.Image(mask, "Cover", cover != null ? cover : ProceduralArt.Gradient("cov_" + Util.Slug(s.Name), color * 0.6f, color), Color.white);
            if (cover != null) { art.preserveAspect = false; var f = art.gameObject.AddComponent<AspectRatioFitter>(); f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; f.aspectRatio = cover.rect.width / cover.rect.height; }
            var shade = UIKit.Image(mask, "Shade", ProceduralArt.Gradient("cov_shade", new Color(0, 0, 0, 0.85f), new Color(0, 0, 0, 0f)), Color.white);
            var name = UIKit.Text(card, "Name", s.Name, 66, Color.white, TextAlignmentOptions.BottomLeft, true);
            name.rectTransform.offsetMin = new Vector2(40, 80);
            name.rectTransform.offsetMax = new Vector2(-40, -30);
            string sub = (s.Genre != null ? s.Genre.ToUpperInvariant() + "  ·  " : "") + s.Episodes.Count + (s.Episodes.Count == 1 ? " episode" : " episodes");
            var info = UIKit.Text(card, "Info", sub, 32, new Color(1, 1, 1, 0.85f), TextAlignmentOptions.BottomLeft);
            info.rectTransform.offsetMin = new Vector2(40, 30);
            info.rectTransform.offsetMax = new Vector2(-40, -30);
            var save = SaveSystem.Load(s.Name);
            if (save != null && !save.finished)
            {
                var badge = UIKit.Image(card, "Badge", ProceduralArt.RoundedRect(30), theme.accentColor);
                UIKit.Place(badge.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(220, 64));
                var bt = UIKit.Text(badge.transform, "Text", "CONTINUE", 30, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Stretch(bt.rectTransform);
            }
            return card;
        }

        IEnumerator FadeUp(RectTransform rt, CanvasGroup g, float delay)
        {
            var target = rt.anchoredPosition;
            rt.anchoredPosition = target - new Vector2(0, 60);
            yield return Tween.Wait(delay);
            StartCoroutine(Tween.Fade(g, 1, 0.3f));
            yield return Tween.Anchored(rt, target, 0.45f, Ease.OutCubic);
        }

        public void ShowSeries(SeriesInfo s)
        {
            Background();
            float W = ui.Frame.rect.width, H = ui.Frame.rect.height;
            var cover = AssetLibrary.Cover(s.Cover ?? s.Name) ?? AssetLibrary.Cover(s.Episodes[0].File);
            var color = ProceduralArt.ColorFromName(s.Name, 0.55f, 0.75f);
            var header = UIKit.Rect("Header", layer, false);
            UIKit.Place(header, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(W, H * 0.42f));
            header.gameObject.AddComponent<RectMask2D>();
            var art = UIKit.Image(header, "Cover", cover != null ? cover : ProceduralArt.Gradient("cov_" + Util.Slug(s.Name), color * 0.6f, color), Color.white);
            if (cover != null) { var f = art.gameObject.AddComponent<AspectRatioFitter>(); f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; f.aspectRatio = cover.rect.width / cover.rect.height; }
            UIKit.Image(header, "Shade", ProceduralArt.Gradient("hdr_shade", new Color(0.09f, 0.07f, 0.16f, 1f), new Color(0, 0, 0, 0f)), Color.white);
            var title = UIKit.Text(header, "Title", s.Name, 84, Color.white, TextAlignmentOptions.BottomLeft, true);
            title.rectTransform.offsetMin = new Vector2(50, 30);
            title.rectTransform.offsetMax = new Vector2(-50, -40);

            var back = UIKit.Button(layer, "Back", "‹", new Color(0, 0, 0, 0.4f), Color.white, ShowSeriesList, 70, 40);
            UIKit.Place((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -40), new Vector2(110, 110));

            float y = -H * 0.42f - 30;
            if (!string.IsNullOrEmpty(s.Description))
            {
                var d = UIKit.Text(layer, "Description", s.Description, 38, new Color(1, 1, 1, 0.85f), TextAlignmentOptions.TopLeft);
                float dh = d.GetPreferredValues(s.Description, W - 100, 0).y + 10;
                UIKit.Place(d.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(W - 100, dh));
                y -= dh + 30;
            }

            var save = SaveSystem.Load(s.Name);
            bool canContinue = save != null && !save.finished && s.Episodes.Any(e => e.File == save.story);
            var main = UIKit.Button(layer, "Play", canContinue ? "CONTINUE" : "PLAY", theme.accentColor, Color.white, () =>
            {
                var ep = canContinue ? s.Episodes.First(e => e.File == save.story) : s.Episodes[0];
                Play?.Invoke(s, ep, canContinue);
            }, 52, 60);
            UIKit.Place((RectTransform)main.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(W - 100, 140));
            StartCoroutine(Breathe((RectTransform)main.transform));
            y -= 180;

            var label = UIKit.Text(layer, "Episodes", "EPISODES", 34, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Left);
            UIKit.Place(label.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(W - 100, 60));
            y -= 70;

            var area = UIKit.Rect("Episode List", layer);
            area.offsetMax = new Vector2(0, y);
            area.offsetMin = Vector2.zero;
            area.gameObject.AddComponent<RectMask2D>();
            var hit = area.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.01f);
            var content = UIKit.Rect("Content", area, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            float ey = 0;
            foreach (var ep in s.Episodes)
            {
                var e = ep;
                var row = UIKit.Button(content, "Episode " + ep.Number, null, new Color(1, 1, 1, 0.08f), Color.white, () => Play?.Invoke(s, e, false), 40, 36);
                UIKit.Place((RectTransform)row.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, ey), new Vector2(W - 100, 120));
                var num = UIKit.Text(row.transform, "Num", ep.Number.ToString(), 48, theme.accentColor, TextAlignmentOptions.Center, true);
                UIKit.Place(num.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(90, 100));
                var t = UIKit.Text(row.transform, "Title", ep.Title, 40, Color.white, TextAlignmentOptions.Left);
                t.rectTransform.offsetMin = new Vector2(130, 10);
                t.rectTransform.offsetMax = new Vector2(-40, -10);
                ey -= 140;
            }
            content.sizeDelta = new Vector2(0, -ey + 40);
        }

        static IEnumerator Breathe(RectTransform rt)
        {
            float t = 0;
            while (rt != null)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * (1f + 0.015f * Mathf.Sin(t * 3f));
                yield return null;
            }
        }
    }
}
