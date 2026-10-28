using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VNEngine
{
    /// <summary>
    /// One character on stage. Owns placement (spot directing), walking, facing, layering,
    /// and the "alive" layer: breathing, blinking, lip flap and procedural gestures.
    /// </summary>
    public sealed class StageCharacter : MonoBehaviour
    {
        public string Id { get; private set; }
        public CharacterDefinition Definition { get; private set; }
        public ICharacterVisual Visual { get; private set; }

        public float X { get; private set; } = 160f;
        public float Y { get; private set; }
        public float SpotScale { get; private set; } = 1f;
        public int Zone { get; private set; } = 1;
        public int Layer { get; private set; }
        public bool FacingRight { get; private set; } = true;
        public bool OnStage { get; private set; }
        public string Animation { get; private set; }
        public string Emotion { get; private set; } = "neutral";
        public readonly Dictionary<string, string> Outfits = new Dictionary<string, string>();

        StageView stage;
        VNTheme theme;
        Transform flip, motion;
        SortingGroup sorting;
        int entryOrder;
        bool artFacesLeft;

        // procedural state
        MotionPose gesturePose = MotionPose.Identity;
        Coroutine gestureRoutine;
        float walkPhase, walkAmount, walkSpeed;
        float breathPhase;
        float blinkTimer, blinkUntil;
        float mouthUntil;
        bool talking;
        float alpha = 1f, dim = 1f, dimTarget = 1f;
        float flipCurrent = 1f;
        Coroutine moveRoutine;
        float heightScale = 1f;

        /// <summary>Character height in world units at spot scale 1.</summary>
        public float BaseHeight => theme.characterHeight * stage.DesignHeight * heightScale;

        public static StageCharacter Create(StageView stage, Transform parent, string id, CharacterDefinition def, int order)
        {
            var go = new GameObject("Character " + id);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<StageCharacter>();
            c.Init(stage, id, def, order);
            return c;
        }

        void Init(StageView stageView, string id, CharacterDefinition def, int order)
        {
            stage = stageView;
            theme = stageView.Theme;
            Id = id;
            Definition = def;
            entryOrder = order;
            sorting = gameObject.AddComponent<SortingGroup>();
            flip = new GameObject("flip").transform;
            flip.SetParent(transform, false);
            motion = new GameObject("motion").transform;
            motion.SetParent(flip, false);

            if (def != null)
            {
                if (def.Options.TryGetValue("height", out var h) && Util.TryFloat(h, out var hv)) heightScale = hv;
                if (def.Options.TryGetValue("faces", out var f)) artFacesLeft = f.ToLowerInvariant() == "left";
            }

            Visual = (ICharacterVisual)PrefabVisual.TryCreate(motion, id, def)
                     ?? (ICharacterVisual)PartsPuppetVisual.TryCreate(motion, id, def)
                     ?? (ICharacterVisual)SpriteFolderVisual.TryCreate(motion, id, def)
                     ?? PlaceholderVisual.Create(motion, id, def);
            Visual.Root.localScale = Vector3.one * (BaseHeight / Mathf.Max(0.01f, Visual.NativeHeight));
            if (def != null && def.Options.TryGetValue("expression", out var e)) Emotion = e;
            Visual.SetEmotion(Emotion);
            Visual.Play("idle", "idle", true);
            blinkTimer = Random.Range(1.5f, 4f);
            breathPhase = Random.value * 10f;
            UpdateSorting();
            SetAlpha(0f);
            gameObject.SetActive(false);
        }

        public string DisplayName
        {
            get
            {
                if (Definition != null && !string.IsNullOrEmpty(Definition.DisplayName)) return Definition.DisplayName;
                return char.ToUpper(Id[0]) + Id.Substring(1).ToLowerInvariant();
            }
        }

        public Color NameColor
        {
            get
            {
                if (Definition != null && Definition.Options.TryGetValue("color", out var c)) return ProceduralArt.Hex(c, theme.defaultNameColor);
                return ProceduralArt.ColorFromName(Id, 0.55f, 0.85f);
            }
        }

        // ---------- placement ----------

        void ApplyPlacement()
        {
            transform.localPosition = stage.StageToWorld(X, Y, Zone);
            transform.localScale = Vector3.one * SpotScale;
        }

        public void PlaceAt(float x, float y, float scale, int zone)
        {
            X = x; Y = y; SpotScale = scale; Zone = Mathf.Max(1, zone);
            ApplyPlacement();
        }

        public void SetLayer(int layer)
        {
            Layer = layer;
            UpdateSorting();
        }

        public void BringToFront(int order)
        {
            entryOrder = order;
            UpdateSorting();
        }

        void UpdateSorting()
        {
            if (sorting != null) sorting.sortingOrder = 100 + Layer * 100 + entryOrder;
        }

        public void Show()
        {
            OnStage = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            OnStage = false;
            talking = false;
            gameObject.SetActive(false);
        }

        public void SetAlpha(float a)
        {
            alpha = a;
            ApplyColor();
        }

        public void SetDim(bool dimmed) => dimTarget = dimmed ? 0.78f : 1f;

        void ApplyColor() => Visual?.SetColor(new Color(dim, dim, dim * 1.02f, alpha));

        public void Face(bool right, bool instant = false)
        {
            FacingRight = right;
            if (instant) flipCurrent = FlipTarget;
        }

        float FlipTarget => (FacingRight ^ artFacesLeft) ? 1f : -1f;

        /// <summary>Where a speech bubble should point, in world space.</summary>
        public Vector3 HeadWorldPosition
        {
            get
            {
                float h = BaseHeight * SpotScale * Visual.HeadTop;
                return transform.position + new Vector3(0, h, 0);
            }
        }

        // ---------- movement ----------

        public IEnumerator MoveTo(float x, float y, float scale, int zone, float duration, string mode)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            var done = false;
            moveRoutine = StartCoroutine(MoveRoutine(x, y, scale, zone, duration, mode, () => done = true));
            while (!done && this != null && isActiveAndEnabled) yield return null;
        }

        IEnumerator MoveRoutine(float x, float y, float scale, int zone, float duration, string mode, System.Action done)
        {
            Vector3 from = transform.localPosition;
            float fromScale = SpotScale;
            X = x; Y = y; SpotScale = scale; Zone = Mathf.Max(1, zone);
            Vector3 to = stage.StageToWorld(X, Y, Zone);
            bool walking = mode == "walk" || mode == "run";
            bool prevFacing = FacingRight;
            if (walking && Mathf.Abs(to.x - from.x) > 0.01f) Face(to.x > from.x);
            if (walking)
            {
                walkSpeed = mode == "run" ? 3.6f : 2.2f; // steps per second
                Visual.Play("walk", mode, true);
            }
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = walking ? Mathf.SmoothStep(0, 1, Mathf.SmoothStep(0, 1, k) * 0.35f + k * 0.65f) : Easing.Apply(Ease.InOutCubic, k);
                transform.localPosition = Vector3.LerpUnclamped(from, to, e);
                transform.localScale = Vector3.one * Mathf.Lerp(fromScale, SpotScale, e);
                if (walking) walkAmount = Mathf.MoveTowards(walkAmount, k > 0.9f ? 0 : 1, Time.deltaTime * 6f);
                yield return null;
            }
            ApplyPlacement();
            walkAmount = 0;
            if (walking)
            {
                Visual.Play(GestureOf(Animation) ?? "idle", Animation ?? "idle", true);
                // Turn back to the way they were facing, like Episode characters do after a walk.
                if (OnStage && FacingRight != prevFacing && !IsOffscreen(X)) Face(prevFacing);
            }
            moveRoutine = null;
            done();
        }

        bool IsOffscreen(float x) => x < -40 || x > theme.stageUnitsWide + 40;

        static string GestureOf(string anim)
        {
            if (anim == null) return null;
            Emotions.Split(anim, out var g, out _, out _);
            return g;
        }

        // ---------- animation & expression ----------

        /// <summary>Plays an Episode-style animation name such as talk_happy, idle_think_loop, jump or happy.</summary>
        public void PlayAnimation(string anim)
        {
            if (string.IsNullOrEmpty(anim)) return;
            Emotions.Split(anim, out var gesture, out var emotion, out var loop);
            Animation = anim;
            if (emotion != null) SetEmotion(emotion);

            string full = anim.ToLowerInvariant();
            bool handled = Visual.Play(gesture, full, loop);
            if (gestureRoutine != null) { StopCoroutine(gestureRoutine); gestureRoutine = null; }
            if (!gameObject.activeInHierarchy) { gesturePose = MotionPose.Identity; return; } // hidden: remember the pose, skip the motion
            if (handled && Visual.HasAnimation(full)) { gesturePose = MotionPose.Identity; return; }

            if (gesture != null && ProceduralMotion.TryGet(gesture, out var dur, out var curve))
                gestureRoutine = StartCoroutine(GestureRoutine(gesture, dur, curve, loop));
            else if (gesture == null || gesture == "idle")
                gestureRoutine = StartCoroutine(ReleaseGesture());
            else
                AssetLibrary.WarnOnce($"{Id} has no animation called '{anim}'. Built-in motions: jump, bounce, nod, shake_head, laugh, gasp, sigh, shiver, cry, angry, dance, wave, spin, shrug, bow, think, lean, faint, point, talk.");
        }

        IEnumerator GestureRoutine(string gesture, float duration, ProceduralMotion.Curve curve, bool loop)
        {
            var start = gesturePose;
            float blendIn = 0.12f;
            do
            {
                float t = 0;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    var pose = curve(Mathf.Clamp01(t / duration), t);
                    gesturePose = t < blendIn ? MotionPose.Lerp(start, pose, t / blendIn) : pose;
                    yield return null;
                }
                start = gesturePose;
            } while (loop);
            if (!ProceduralMotion.Holds(gesture)) yield return ReleaseGesture();
            gestureRoutine = null;
        }

        IEnumerator ReleaseGesture()
        {
            var start = gesturePose;
            yield return Tween.Run(0.25f, Ease.OutQuad, k => gesturePose = MotionPose.Lerp(start, MotionPose.Identity, k));
        }

        public void SetEmotion(string emotion)
        {
            Emotion = emotion;
            Visual.SetEmotion(emotion);
        }

        public void Wear(string slot, string label)
        {
            Outfits[string.IsNullOrEmpty(slot) ? "outfit" : slot] = label;
            Visual.SetOutfit(slot, label);
        }

        // ---------- talking ----------

        public void SetTalking(bool on)
        {
            talking = on;
            if (!on) { mouthUntil = 0; Visual.SetMouthOpen(false); }
        }

        /// <summary>Called for every letter the typewriter reveals.</summary>
        public void OnLetter(char c)
        {
            if (!talking) return;
            if (char.IsLetterOrDigit(c))
            {
                if (Time.time > mouthUntil + 0.04f) mouthUntil = Time.time + Random.Range(0.06f, 0.11f);
            }
            else if (c == '.' || c == ',' || c == '!' || c == '?') mouthUntil = 0;
        }

        // ---------- per-frame life ----------

        void LateUpdate()
        {
            if (Visual == null) return;
            float dt = Time.deltaTime;

            // Blinking
            if (theme.blinking)
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0)
                {
                    blinkUntil = Time.time + 0.11f;
                    blinkTimer = Random.value < 0.18f ? 0.22f : Random.Range(2.2f, 5.5f);
                }
                Visual.SetEyesClosed(Time.time < blinkUntil);
            }

            // Lip flap
            if (talking) Visual.SetMouthOpen(Time.time < mouthUntil);

            // Facing turns pass through zero width, which reads as a turn.
            flipCurrent = Mathf.MoveTowards(flipCurrent, FlipTarget, dt * 9f);
            flip.localScale = new Vector3(Mathf.Abs(flipCurrent) < 0.05f ? 0.05f * Mathf.Sign(FlipTarget) : flipCurrent, 1, 1);

            // Dimming for listeners
            if (!Mathf.Approximately(dim, dimTarget))
            {
                dim = Mathf.MoveTowards(dim, dimTarget, dt * 2.5f);
                ApplyColor();
            }

            // Compose motion layers
            float H = BaseHeight;
            breathPhase += dt;
            float breath = theme.breathing ? Mathf.Sin(breathPhase * 2f * Mathf.PI / 3.6f) : 0f;
            walkPhase += dt * walkSpeed * Mathf.PI;
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * walkAmount;
            float sway = Mathf.Sin(walkPhase) * walkAmount;

            var g = gesturePose;
            Vector2 offset = g.offset * H + new Vector2(0, bob * 0.022f * H);
            float rot = g.rotation + sway * 1.4f;
            Vector2 scale = new Vector2(g.scale.x * (1 - breath * 0.003f), g.scale.y * (1 + breath * 0.006f) * (1 - bob * 0.01f));
            motion.localPosition = new Vector3(offset.x, offset.y, 0);
            motion.localRotation = Quaternion.Euler(0, 0, rot);
            motion.localScale = new Vector3(scale.x, scale.y, 1);

            Visual.Tick(dt);
        }

        // ---------- saving ----------

        public SavedCharacter Save()
        {
            var s = new SavedCharacter
            {
                id = Id, x = X, y = Y, scale = SpotScale, zone = Zone, layer = Layer, facingRight = FacingRight,
                animation = Animation, emotion = Emotion
            };
            foreach (var kv in Outfits) { s.outfitSlots.Add(kv.Key); s.outfitLabels.Add(kv.Value); }
            return s;
        }

        public void Load(SavedCharacter s)
        {
            Show();
            PlaceAt(s.x, s.y, s.scale, s.zone);
            SetLayer(s.layer);
            Face(s.facingRight, true);
            for (int i = 0; i < s.outfitSlots.Count && i < s.outfitLabels.Count; i++) Wear(s.outfitSlots[i], s.outfitLabels[i]);
            if (!string.IsNullOrEmpty(s.emotion)) SetEmotion(s.emotion);
            if (!string.IsNullOrEmpty(s.animation) && (s.animation.Contains("loop") || s.animation.StartsWith("idle"))) PlayAnimation(s.animation);
            SetAlpha(1f);
        }
    }
}
