using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VNEngine
{
    /// <summary>
    /// The 2D world: backgrounds with zones, characters, the camera and weather.
    /// Stage units follow Episode's spot directing: the screen is 320 units wide,
    /// x grows to the right, y grows up, and zone 2 starts where zone 1 ends.
    /// </summary>
    public sealed class StageView : MonoBehaviour
    {
        public const float UnitsToWorld = 0.01f;

        public VNTheme Theme { get; private set; }
        public Camera Camera { get; private set; }
        Camera letterboxCamera;

        Transform world, characterRoot;
        SpriteRenderer bgCurrent, bgNext, tintRenderer;
        public string BackgroundName { get; private set; }
        public int ZoneCount { get; private set; } = 1;
        public int CurrentZone { get; private set; } = 1;
        public string Tint { get; private set; }

        readonly Dictionary<string, StageCharacter> characters = new Dictionary<string, StageCharacter>(StringComparer.OrdinalIgnoreCase);
        int orderCounter;
        Func<string, CharacterDefinition> definitionLookup;

        // camera state
        Vector2 camCenter;
        float camZoom = 1f;
        Vector2 shakeOffset;
        float bgWidth = 3.2f, bgHeight = 5.7f;
        Weather weather;

        public float ViewWidth => Theme.stageUnitsWide * UnitsToWorld;
        public float DesignHeight => ViewWidth / Theme.designAspect;
        public float ViewHeight => ViewWidth / Mathf.Max(0.01f, Camera.aspect);

        /// <summary>Raised when the camera viewport changes, so the UI can follow the letterbox.</summary>
        public event Action<Rect> ViewportChanged;
        Rect lastViewport;

        public IEnumerable<StageCharacter> Characters => characters.Values;

        public void Init(VNTheme theme, Func<string, CharacterDefinition> lookup)
        {
            Theme = theme;
            definitionLookup = lookup;

            letterboxCamera = new GameObject("Letterbox Camera").AddComponent<Camera>();
            letterboxCamera.transform.SetParent(transform, false);
            letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
            letterboxCamera.backgroundColor = theme.letterboxColor;
            letterboxCamera.cullingMask = 0;
            letterboxCamera.depth = -100;
            letterboxCamera.orthographic = true;

            Camera = new GameObject("Stage Camera").AddComponent<Camera>();
            Camera.transform.SetParent(transform, false);
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Color.black;
            Camera.depth = 0;
            Camera.nearClipPlane = -50f;
            Camera.farClipPlane = 50f;
            Camera.transform.position = new Vector3(0, 0, -10);
            UpdateViewport(true);

            world = new GameObject("World").transform;
            world.SetParent(transform, false);
            bgCurrent = NewBackgroundRenderer("Background");
            bgNext = NewBackgroundRenderer("Background (next)");
            bgNext.enabled = false;
            characterRoot = new GameObject("Characters").transform;
            characterRoot.SetParent(world, false);

            tintRenderer = new GameObject("Tint").AddComponent<SpriteRenderer>();
            tintRenderer.transform.SetParent(Camera.transform, false);
            tintRenderer.transform.localPosition = new Vector3(0, 0, 5);
            tintRenderer.sprite = ProceduralArt.White();
            tintRenderer.sortingOrder = 5000;
            tintRenderer.color = new Color(0, 0, 0, 0);

            weather = new GameObject("Weather").AddComponent<Weather>();
            weather.transform.SetParent(Camera.transform, false);
            weather.transform.localPosition = new Vector3(0, 0, 4);
            weather.Init(this);

            ResetCamera();
        }

        SpriteRenderer NewBackgroundRenderer(string name)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(world, false);
            sr.sortingOrder = -1000;
            return sr;
        }

        void UpdateViewport(bool force = false)
        {
            float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : Theme.designAspect;
            Rect r = new Rect(0, 0, 1, 1);
            if (screenAspect > Theme.designAspect + 0.001f)
            {
                float w = Theme.designAspect / screenAspect;
                r = new Rect((1 - w) / 2, 0, w, 1);
            }
            if (force || r != lastViewport)
            {
                Camera.rect = r;
                lastViewport = r;
                ViewportChanged?.Invoke(r);
                if (bgCurrent != null && bgCurrent.sprite != null) FitBackground(bgCurrent);
            }
        }

        public Rect Viewport => lastViewport;

        // ---------- coordinates ----------

        public Vector3 StageToWorld(float x, float y, int zone)
        {
            float wx = ((zone - 1) * Theme.stageUnitsWide + x) * UnitsToWorld;
            float wy = Theme.characterBaseline * DesignHeight + y * UnitsToWorld;
            return new Vector3(wx, wy, 0);
        }

        public float NamedX(string name)
        {
            switch (name)
            {
                case "left": return Theme.leftX;
                case "right": return Theme.rightX;
                case "far_left": return Theme.farLeftX;
                case "far_right": return Theme.farRightX;
                case "offscreen_left": return -Theme.offscreenMargin;
                case "offscreen_right": return Theme.stageUnitsWide + Theme.offscreenMargin;
                default: return Theme.centerX;
            }
        }

        /// <summary>Resolves a StagePosition to stage units, starting from the character's current values.</summary>
        public void Resolve(StagePosition pos, StageCharacter self, out float x, out float y, out float scale, out int zone)
        {
            x = self.OnStage ? self.X : Theme.centerX;
            y = self.OnStage ? self.Y : 0f;
            scale = self.OnStage ? self.SpotScale : 1f;
            zone = self.OnStage ? self.Zone : CurrentZone;
            if (pos == null) return;
            if (pos.Zone.HasValue) zone = pos.Zone.Value;
            else if (!self.OnStage) zone = CurrentZone;
            if (pos.NextTo != null && characters.TryGetValue(pos.NextTo, out var other) && other.OnStage)
            {
                zone = other.Zone;
                float side = other.X > Theme.centerX ? -1f : 1f;
                x = other.X + side * 85f;
                y = other.Y;
                return;
            }
            if (pos.Named != null)
            {
                if (pos.Named == "top" || pos.Named == "bottom") return;
                x = NamedX(pos.Named);
            }
            if (pos.X.HasValue) x = pos.X.Value;
            if (pos.Y.HasValue) y = pos.Y.Value;
            if (pos.Scale.HasValue) scale = pos.Scale.Value;
        }

        // ---------- characters ----------

        public StageCharacter Character(string id, bool create = true)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (characters.TryGetValue(id, out var c) && c != null) return c;
            if (!create) return null;
            c = StageCharacter.Create(this, characterRoot, id.ToUpperInvariant(), definitionLookup?.Invoke(id), ++orderCounter);
            characters[id] = c;
            return c;
        }

        public void Preload(IEnumerable<string> ids)
        {
            foreach (var id in ids) Character(id);
        }

        public int NextOrder() => ++orderCounter;

        public IEnumerator ClearCharacters(float fade = 0.35f)
        {
            var visible = characters.Values.Where(c => c.OnStage).ToList();
            if (visible.Count == 0) yield break;
            yield return Tween.Run(fade, Ease.OutQuad, k => { foreach (var c in visible) c.SetAlpha(1 - k); });
            foreach (var c in visible) c.Hide();
        }

        public void HideAllInstant()
        {
            foreach (var c in characters.Values) c.Hide();
        }

        public void SetSpeaker(StageCharacter speaker)
        {
            foreach (var c in characters.Values)
            {
                if (!c.OnStage) continue;
                c.SetTalking(c == speaker);
                c.SetDim(Theme.dimListeners && speaker != null && c != speaker);
            }
        }

        // ---------- backgrounds ----------

        public IEnumerator SetBackground(string name, string transition, float duration, int? zone)
        {
            var sprite = AssetLibrary.Background(name);
            if (sprite == null)
            {
                AssetLibrary.WarnOnce($"No background image called '{name}'. Put one in Resources/Backgrounds (tried: {string.Join(", ", AssetLibrary.NameVariants(name).Take(5))}). Using a placeholder.");
                sprite = ProceduralArt.PlaceholderBackground(name, 2);
            }
            BackgroundName = name;
            transition = (transition ?? Theme.defaultTransition).ToLowerInvariant();

            bgNext.sprite = sprite;
            FitBackground(bgNext);
            int targetZone = Mathf.Clamp(zone ?? 1, 1, ZoneCountFor(bgNext));

            if (transition == "cut" || transition == "none" || duration <= 0f || bgCurrent.sprite == null)
            {
                SwapBackgrounds();
                SetZoneInstant(targetZone);
                yield break;
            }

            bgNext.enabled = true;
            bgNext.sortingOrder = -999;
            switch (transition)
            {
                case "slide_left":
                case "slide_right":
                case "slide":
                {
                    float dir = transition == "slide_right" ? -1 : 1;
                    Vector3 curFrom = bgCurrent.transform.localPosition;
                    Vector3 nextEnd = bgNext.transform.localPosition + new Vector3((CurrentZone - targetZone) * ViewWidth, 0, 0);
                    Vector3 nextFrom = nextEnd + new Vector3(dir * ViewWidth, 0, 0);
                    bgNext.transform.localPosition = nextFrom;
                    yield return Tween.Run(duration, Ease.InOutCubic, k =>
                    {
                        bgCurrent.transform.localPosition = curFrom - new Vector3(dir * ViewWidth * k, 0, 0);
                        bgNext.transform.localPosition = Vector3.LerpUnclamped(nextFrom, nextEnd, k);
                    });
                    break;
                }
                case "zoom":
                {
                    bgNext.sortingOrder = -1001;
                    Vector3 s0 = bgCurrent.transform.localScale;
                    yield return Tween.Run(duration, Ease.InQuad, k =>
                    {
                        bgCurrent.transform.localScale = s0 * (1 + 0.25f * k);
                        bgCurrent.color = new Color(1, 1, 1, 1 - k);
                    });
                    break;
                }
                default: // dissolve / crossfade
                {
                    bgNext.sortingOrder = -998;
                    bgNext.color = new Color(1, 1, 1, 0);
                    yield return Tween.Run(duration, Ease.InOutSine, k => bgNext.color = new Color(1, 1, 1, k));
                    break;
                }
            }
            SwapBackgrounds();
            SetZoneInstant(targetZone);
        }

        void SwapBackgrounds()
        {
            var tmp = bgCurrent;
            bgCurrent = bgNext;
            bgNext = tmp;
            bgCurrent.enabled = true;
            bgCurrent.color = Color.white;
            bgCurrent.sortingOrder = -1000;
            bgNext.enabled = false;
            bgNext.sprite = null;
            bgNext.color = Color.white;
            FitBackground(bgCurrent);
            ZoneCount = ZoneCountFor(bgCurrent);
        }

        int ZoneCountFor(SpriteRenderer sr) => sr.sprite == null ? 1 : Mathf.Max(1, Mathf.RoundToInt(sr.bounds.size.x / ViewWidth));

        void FitBackground(SpriteRenderer sr)
        {
            if (sr.sprite == null) return;
            var size = sr.sprite.bounds.size;
            float s = ViewHeight / size.y;
            if (size.x * s < ViewWidth) s = ViewWidth / size.x;
            sr.transform.localScale = new Vector3(s, s, 1);
            var b = sr.sprite.bounds;
            sr.transform.localPosition = new Vector3(-b.min.x * s, -b.min.y * s, 10);
            if (sr == bgCurrent)
            {
                bgWidth = size.x * s;
                bgHeight = size.y * s;
            }
        }

        public void SetZoneInstant(int zone)
        {
            CurrentZone = Mathf.Clamp(zone, 1, ZoneCount);
            camZoom = 1f;
            camCenter = new Vector2(ZoneCenterX(CurrentZone), ViewHeight / 2f);
        }

        float ZoneCenterX(int zone) => ((zone - 1) * Theme.stageUnitsWide + Theme.stageUnitsWide / 2f) * UnitsToWorld;

        // ---------- camera ----------

        public void ResetCamera()
        {
            camZoom = 1f;
            camCenter = new Vector2(ZoneCenterX(CurrentZone), ViewHeight / 2f);
        }

        public IEnumerator PanToZone(int zone, float duration)
        {
            zone = Mathf.Clamp(zone, 1, ZoneCount);
            CurrentZone = zone;
            var from = camCenter;
            var to = new Vector2(ZoneCenterX(zone), camCenter.y);
            yield return Tween.Run(duration, Ease.InOutCubic, k => camCenter = Vector2.LerpUnclamped(from, to, k));
        }

        public IEnumerator PanBy(float stageUnits, float duration)
        {
            var from = camCenter;
            var to = camCenter + new Vector2(stageUnits * UnitsToWorld, 0);
            yield return Tween.Run(duration, Ease.InOutCubic, k => camCenter = Vector2.LerpUnclamped(from, to, k));
            CurrentZone = Mathf.Clamp(Mathf.RoundToInt(camCenter.x / ViewWidth + 0.5f), 1, ZoneCount);
        }

        public IEnumerator ZoomTo(Vector2 focus, float zoom, float duration)
        {
            var fromC = camCenter;
            float fromZ = camZoom;
            yield return Tween.Run(duration, Ease.InOutCubic, k =>
            {
                camZoom = Mathf.LerpUnclamped(fromZ, zoom, k);
                camCenter = Vector2.LerpUnclamped(fromC, focus, k);
            });
        }

        /// <summary>Focus point for zooming on a character: around the face.</summary>
        public Vector2 FocusOn(StageCharacter c)
        {
            var head = c.HeadWorldPosition;
            return new Vector2(head.x, head.y - c.BaseHeight * c.SpotScale * 0.12f);
        }

        public Vector2 FocusOnStagePoint(float x, float y)
        {
            return new Vector2(((CurrentZone - 1) * Theme.stageUnitsWide + x) * UnitsToWorld, y * UnitsToWorld);
        }

        public IEnumerator Shake(float strength, float duration)
        {
            float amp = 0.06f * strength;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1 - t / duration;
                shakeOffset = new Vector2((Mathf.PerlinNoise(t * 30f, 0) - 0.5f) * 2, (Mathf.PerlinNoise(0, t * 30f) - 0.5f) * 2) * amp * k;
                yield return null;
            }
            shakeOffset = Vector2.zero;
        }

        public IEnumerator SetTint(string tint, float duration)
        {
            Tint = tint;
            Color target;
            switch ((tint ?? "none").ToLowerInvariant())
            {
                case "night": target = new Color(0.05f, 0.08f, 0.3f, 0.38f); break;
                case "sunset": case "evening": target = new Color(1f, 0.45f, 0.15f, 0.22f); break;
                case "sepia": case "flashback": case "memory": target = new Color(0.55f, 0.4f, 0.2f, 0.38f); break;
                case "red": case "danger": target = new Color(0.8f, 0.05f, 0.05f, 0.3f); break;
                case "dark": target = new Color(0, 0, 0, 0.5f); break;
                case "dream": target = new Color(0.8f, 0.6f, 1f, 0.25f); break;
                case "none": case "clear": case "off": target = new Color(0, 0, 0, 0); Tint = null; break;
                default: target = ProceduralArt.Hex(tint, new Color(0, 0, 0, 0)); if (target.a > 0.6f) target.a = 0.35f; break;
            }
            var from = tintRenderer.color;
            yield return Tween.Run(duration, Ease.InOutSine, k => tintRenderer.color = Color.LerpUnclamped(from, target, k));
        }

        public void SetWeather(string kind, float intensity = 1f) => weather.Set(kind, intensity);
        public string WeatherKind => weather.Kind;

        void LateUpdate()
        {
            if (Camera == null) return;
            UpdateViewport();
            float baseSize = ViewHeight / 2f;
            float size = baseSize / Mathf.Max(0.2f, camZoom);
            Camera.orthographicSize = size;
            float halfW = size * Camera.aspect;
            float minX = halfW, maxX = Mathf.Max(halfW, bgWidth - halfW);
            float minY = size, maxY = Mathf.Max(size, bgHeight - size);
            float x = Mathf.Clamp(camCenter.x, minX, maxX);
            float y = Mathf.Clamp(camCenter.y, minY, maxY);
            Camera.transform.position = new Vector3(x + shakeOffset.x, y + shakeOffset.y, -10);
            tintRenderer.transform.localScale = new Vector3(halfW * 2.2f / 0.04f, size * 2.2f / 0.04f, 1);
        }

        // ---------- save / load ----------

        public StageSnapshot Snapshot(AudioDirector audio)
        {
            var s = new StageSnapshot
            {
                background = BackgroundName,
                zone = CurrentZone,
                music = audio != null ? audio.CurrentMusic : null,
                ambience = audio != null ? audio.CurrentAmbience : null,
                weather = weather.Kind,
                tint = Tint
            };
            foreach (var c in characters.Values) if (c.OnStage) s.characters.Add(c.Save());
            return s;
        }

        public void Restore(StageSnapshot s)
        {
            HideAllInstant();
            if (!string.IsNullOrEmpty(s.background))
            {
                var e = SetBackground(s.background, "cut", 0, s.zone);
                while (e.MoveNext()) { }
            }
            foreach (var sc in s.characters) Character(sc.id).Load(sc);
            weather.Set(s.weather, 1f);
            var t = SetTint(s.tint, 0);
            while (t.MoveNext()) { }
        }
    }
}
