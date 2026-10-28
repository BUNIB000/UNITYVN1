using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VNEngine
{
    /// <summary>
    /// The art side of a character. StageCharacter handles placement and procedural motion;
    /// a visual shows poses, expressions, lip flaps, blinks and outfits.
    /// </summary>
    public interface ICharacterVisual
    {
        Transform Root { get; }
        /// <summary>Height of the art in its own local units, feet to top of head.</summary>
        float NativeHeight { get; }
        /// <summary>Where the top of the head is, as a fraction of NativeHeight (for speech bubbles).</summary>
        float HeadTop { get; }
        /// <summary>True when the art itself has a full animation (rig/animator state) with this name.</summary>
        bool HasAnimation(string name);
        /// <summary>Shows a pose or gesture. Return true if the art fully handled it (no procedural motion needed).</summary>
        bool Play(string gesture, string fullName, bool loop);
        void SetEmotion(string emotion);
        void SetMouthOpen(bool open);
        void SetEyesClosed(bool closed);
        bool SetOutfit(string slot, string label);
        void SetColor(Color color);
        bool HasWalkCycle { get; }
        void Tick(float deltaTime);
    }

    public static class Emotions
    {
        public static readonly HashSet<string> Known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "neutral", "happy", "sad", "angry", "surprised", "shocked", "scared", "afraid", "think", "thinking", "confused",
            "love", "laugh", "laughing", "embarrassed", "blush", "smug", "smirk", "cry", "crying", "tired", "sleepy",
            "disgusted", "determined", "excited", "flirty", "worried", "nervous", "annoyed", "smile", "serious", "gasp",
            "proud", "hopeful", "shy", "upset", "furious", "joyful", "content", "bored", "pout", "giggle"
        };

        /// <summary>Splits "talk_happy_loop" into gesture "talk", emotion "happy", loop true.</summary>
        public static void Split(string anim, out string gesture, out string emotion, out bool loop)
        {
            gesture = null; emotion = null; loop = false;
            if (string.IsNullOrEmpty(anim)) return;
            var parts = anim.ToLowerInvariant().Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Remove("loop")) loop = true;
            var g = new List<string>();
            foreach (var p in parts)
            {
                if (emotion == null && Known.Contains(p)) emotion = Normalize(p);
                else g.Add(p);
            }
            gesture = g.Count > 0 ? string.Join("_", g) : null;
            // "think", "laugh", "cry", "giggle" are both a face and a gesture
            if (gesture == null && emotion != null && (emotion == "think" || emotion == "laugh" || emotion == "cry" || emotion == "giggle" || emotion == "pout"))
                gesture = emotion;
        }

        public static string Normalize(string e)
        {
            switch (e)
            {
                case "shocked": case "gasp": return "surprised";
                case "afraid": return "scared";
                case "thinking": case "confused": return "think";
                case "laughing": case "joyful": return "laugh";
                case "blush": case "shy": return "embarrassed";
                case "smirk": return "smug";
                case "crying": case "upset": return "cry";
                case "sleepy": case "bored": return "tired";
                case "nervous": return "worried";
                case "furious": case "annoyed": return "angry";
                case "smile": case "content": case "proud": case "hopeful": case "excited": return "happy";
                case "serious": case "determined": return "neutral";
            }
            return e;
        }
    }

    // =====================================================================
    // Placeholder: a drawn paper-doll so stories run before any art exists.
    // =====================================================================

    public sealed class PlaceholderVisual : MonoBehaviour, ICharacterVisual
    {
        SpriteRenderer legs, torso, neck, head, hairBack, bangs, eyeL, eyeR, browL, browR, mouth, blushL, blushR, armL, armR, handL, handR;
        Transform armLPivot, armRPivot;
        readonly List<SpriteRenderer> all = new List<SpriteRenderer>();
        readonly Dictionary<SpriteRenderer, Color> baseColors = new Dictionary<SpriteRenderer, Color>();
        string emotion = "neutral";
        string gesture = "idle";
        bool mouthOpen, eyesClosed, gestureLoop;
        float gestureTime;
        float armLAngle = 8, armRAngle = 8;
        Color shirt, pants, hair, skin;

        public Transform Root => transform;
        public float NativeHeight => 4.05f;
        public float HeadTop => 0.98f;
        public bool HasWalkCycle => false;
        public bool HasAnimation(string name) => false;

        public static PlaceholderVisual Create(Transform parent, string id, CharacterDefinition def)
        {
            var go = new GameObject("Placeholder " + id);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PlaceholderVisual>();
            v.Build(id, def);
            return v;
        }

        void Build(string id, CharacterDefinition def)
        {
            string Opt(string k) => def != null && def.Options.TryGetValue(k, out var s) ? s : null;
            shirt = ProceduralArt.Hex(Opt("color"), ProceduralArt.ColorFromName(id, 0.5f, 0.85f));
            pants = ProceduralArt.ColorFromName(id + "pants", 0.35f, 0.35f);
            hair = ProceduralArt.Hex(Opt("haircolor"), ProceduralArt.ColorFromName(id + "hair", 0.55f, 0.35f));
            var skins = new[] { "#f6d3b8", "#e8b892", "#c98e64", "#9a6643", "#6b4429", "#ffe0c7" };
            skin = ProceduralArt.Hex(Opt("skin"), ProceduralArt.Hex(skins[Mathf.Abs(id.GetHashCode()) % skins.Length]));

            var legSprite = ProceduralArt.FromSdf("ph_legs", 140, 160, (x, y) =>
                Mathf.Min(ProceduralArt.RoundedBox(x, y, 48, 80, 20, 78, 16), ProceduralArt.RoundedBox(x, y, 92, 80, 20, 78, 16)), new Vector2(0.5f, 0f));
            var torsoSprite = ProceduralArt.FromSdf("ph_torso", 150, 170, (x, y) =>
            {
                float w = Mathf.Lerp(56, 72, y / 170f);
                return ProceduralArt.RoundedBox(x, y, 75, 85, w, 82, 26);
            }, new Vector2(0.5f, 0f));
            var neckSprite = ProceduralArt.FromSdf("ph_neck", 40, 40, (x, y) => ProceduralArt.RoundedBox(x, y, 20, 20, 13, 19, 6), new Vector2(0.5f, 0f));
            var headSprite = ProceduralArt.FromSdf("ph_head", 110, 120, (x, y) => ProceduralArt.Ellipse(x, y, 55, 60, 50, 56), new Vector2(0.5f, 0.5f));
            var hairBackSprite = ProceduralArt.FromSdf("ph_hairback", 130, 140, (x, y) =>
                Mathf.Min(ProceduralArt.Ellipse(x, y, 65, 78, 60, 60), ProceduralArt.RoundedBox(x, y, 65, 50, 58, 48, 24)), new Vector2(0.5f, 0.5f));
            var bangsSprite = ProceduralArt.FromSdf("ph_bangs", 120, 70, (x, y) =>
                Mathf.Max(ProceduralArt.Ellipse(x, y, 60, 22, 56, 46), 14 - y + 8 * Mathf.Abs(Mathf.Sin(x * 0.09f))), new Vector2(0.5f, 0f));
            var eyeSprite = ProceduralArt.FromSdf("ph_eye", 24, 32, (x, y) => ProceduralArt.Ellipse(x, y, 12, 16, 8, 12), new Vector2(0.5f, 0.5f));
            var browSprite = ProceduralArt.FromSdf("ph_brow", 40, 14, (x, y) => ProceduralArt.Segment(x, y, new Vector2(6, 7), new Vector2(34, 7), 3.5f), new Vector2(0.5f, 0.5f));
            var blushSprite = ProceduralArt.SoftCircle(24);
            var armSprite = ProceduralArt.FromSdf("ph_arm", 34, 130, (x, y) => ProceduralArt.Segment(x, y, new Vector2(17, 113), new Vector2(17, 17), 14), new Vector2(0.5f, 0.88f));
            var handSprite = ProceduralArt.Circle(14);

            legs = Part("legs", legSprite, pants, new Vector3(0, 0, 0), 0);
            torso = Part("torso", torsoSprite, shirt, new Vector3(0, 1.45f, 0), 2);
            neck = Part("neck", neckSprite, skin * 0.92f, new Vector3(0, 3.0f, 0), 1);
            hairBack = Part("hairback", hairBackSprite, hair, new Vector3(0, 3.5f, 0), 3);
            head = Part("head", headSprite, skin, new Vector3(0, 3.48f, 0), 4);
            blushL = Part("blushL", blushSprite, new Color(1f, 0.45f, 0.5f, 0f), new Vector3(-0.27f, 3.3f, 0), 5);
            blushR = Part("blushR", blushSprite, new Color(1f, 0.45f, 0.5f, 0f), new Vector3(0.27f, 3.3f, 0), 5);
            eyeL = Part("eyeL", eyeSprite, new Color(0.15f, 0.12f, 0.18f), new Vector3(-0.18f, 3.47f, 0), 6);
            eyeR = Part("eyeR", eyeSprite, new Color(0.15f, 0.12f, 0.18f), new Vector3(0.18f, 3.47f, 0), 6);
            browL = Part("browL", browSprite, hair * 0.8f, new Vector3(-0.19f, 3.66f, 0), 6);
            browR = Part("browR", browSprite, hair * 0.8f, new Vector3(0.19f, 3.66f, 0), 6);
            mouth = Part("mouth", MouthSprite("smile"), new Color(0.55f, 0.2f, 0.25f), new Vector3(0, 3.2f, 0), 6);
            bangs = Part("bangs", bangsSprite, hair, new Vector3(0, 3.62f, 0), 7);

            armLPivot = new GameObject("armL").transform; armLPivot.SetParent(transform, false); armLPivot.localPosition = new Vector3(-0.64f, 2.95f, 0);
            armRPivot = new GameObject("armR").transform; armRPivot.SetParent(transform, false); armRPivot.localPosition = new Vector3(0.64f, 2.95f, 0);
            armL = Part("sleeveL", armSprite, shirt * 0.92f, Vector3.zero, 3, armLPivot);
            armR = Part("sleeveR", armSprite, shirt * 0.92f, Vector3.zero, 3, armRPivot);
            handL = Part("handL", handSprite, skin, new Vector3(0, -1.0f, 0), 3, armLPivot);
            handR = Part("handR", handSprite, skin, new Vector3(0, -1.0f, 0), 3, armRPivot);
            foreach (var c in new[] { blushL, blushR }) baseColors[c] = new Color(1f, 0.45f, 0.5f, 1f);
            ApplyFace();
        }

        SpriteRenderer Part(string name, Sprite sprite, Color color, Vector3 pos, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            all.Add(sr);
            baseColors[sr] = color;
            return sr;
        }

        static Sprite MouthSprite(string kind)
        {
            switch (kind)
            {
                case "open": return ProceduralArt.FromSdf("ph_m_open", 44, 36, (x, y) => ProceduralArt.Ellipse(x, y, 22, 18, 13, 11), new Vector2(0.5f, 0.5f));
                case "grin": return ProceduralArt.FromSdf("ph_m_grin", 56, 40, (x, y) => Mathf.Max(ProceduralArt.Ellipse(x, y, 28, 24, 22, 18), y - 24), new Vector2(0.5f, 0.5f));
                case "o": return ProceduralArt.FromSdf("ph_m_o", 36, 40, (x, y) => ProceduralArt.Ellipse(x, y, 18, 20, 8, 11), new Vector2(0.5f, 0.5f));
                case "frown": return ProceduralArt.FromSdf("ph_m_frown", 48, 28, (x, y) => Mathf.Abs(ProceduralArt.Circle(x, y, 24, -6, 22)) - 2.5f, new Vector2(0.5f, 0.5f));
                case "flat": return ProceduralArt.FromSdf("ph_m_flat", 44, 16, (x, y) => ProceduralArt.Segment(x, y, new Vector2(10, 8), new Vector2(34, 8), 2.5f), new Vector2(0.5f, 0.5f));
                case "smirk": return ProceduralArt.FromSdf("ph_m_smirk", 48, 28, (x, y) => Mathf.Max(Mathf.Abs(ProceduralArt.Circle(x, y, 30, 30, 18)) - 2.5f, y - 18), new Vector2(0.5f, 0.5f));
                default: return ProceduralArt.FromSdf("ph_m_smile", 48, 28, (x, y) => Mathf.Max(Mathf.Abs(ProceduralArt.Circle(x, y, 24, 30, 20)) - 2.5f, y - 16), new Vector2(0.5f, 0.5f));
            }
        }

        static Sprite HappyEye() =>
            ProceduralArt.FromSdf("ph_eye_happy", 32, 22, (x, y) => Mathf.Max(Mathf.Abs(ProceduralArt.Circle(x, y, 16, 4, 11)) - 2.6f, 4 - y), new Vector2(0.5f, 0.5f));

        public bool Play(string gesture, string fullName, bool loop)
        {
            this.gesture = string.IsNullOrEmpty(gesture) ? "idle" : gesture;
            gestureLoop = loop;
            gestureTime = 0f;
            return false; // body motion is still added procedurally
        }

        public void SetEmotion(string e)
        {
            emotion = string.IsNullOrEmpty(e) ? "neutral" : Emotions.Normalize(e.ToLowerInvariant());
            ApplyFace();
        }

        public void SetMouthOpen(bool open)
        {
            if (mouthOpen == open) return;
            mouthOpen = open;
            ApplyFace();
        }

        public void SetEyesClosed(bool closed)
        {
            if (eyesClosed == closed) return;
            eyesClosed = closed;
            ApplyFace();
        }

        public bool SetOutfit(string slot, string label)
        {
            var c = ProceduralArt.ColorFromName(label, 0.5f, 0.85f);
            if (string.IsNullOrEmpty(slot) || slot == "outfit") { baseColors[torso] = c; baseColors[armL] = c * 0.92f; baseColors[armR] = c * 0.92f; }
            else if (slot == "hair") { baseColors[hairBack] = c * 0.6f; baseColors[bangs] = c * 0.6f; }
            else return false;
            SetColor(lastColor);
            return true;
        }

        Color lastColor = Color.white;

        public void SetColor(Color color)
        {
            lastColor = color;
            foreach (var sr in all)
            {
                var b = baseColors[sr];
                if (sr == blushL || sr == blushR) continue; // alpha comes from the expression
                sr.color = new Color(b.r * color.r, b.g * color.g, b.b * color.b, b.a * color.a);
            }
            ApplyFace();
        }

        void ApplyFace()
        {
            if (mouth == null) return;
            string e = emotion;
            // eyes
            bool happyEyes = (e == "happy" || e == "laugh" || e == "love") && !mouthOpen && e != "neutral";
            happyEyes |= e == "laugh";
            float eyeScaleY = 1f, eyeScale = 1f;
            if (e == "surprised" || e == "scared") eyeScale = 1.3f;
            if (e == "smug" || e == "tired") eyeScaleY = 0.55f;
            var eyeSprite = happyEyes ? HappyEye() : ProceduralArt.FromSdf("ph_eye", 24, 32, (x, y) => ProceduralArt.Ellipse(x, y, 12, 16, 8, 12), new Vector2(0.5f, 0.5f));
            foreach (var eye in new[] { eyeL, eyeR })
            {
                eye.sprite = eyeSprite;
                eye.transform.localScale = new Vector3(eyeScale, eyesClosed && !happyEyes ? 0.12f : eyeScale * eyeScaleY, 1f);
            }

            // brows: rotation of the inner end (positive = inner end up = worried)
            float inner = 0f, lift = 0f;
            switch (e)
            {
                case "sad": case "cry": case "worried": case "scared": case "embarrassed": inner = 18f; break;
                case "angry": case "disgusted": case "pout": inner = -22f; break;
                case "surprised": lift = 0.07f; break;
                case "think": lift = 0.03f; break;
            }
            browL.transform.localRotation = Quaternion.Euler(0, 0, -inner);
            browR.transform.localRotation = Quaternion.Euler(0, 0, inner);
            browL.transform.localPosition = new Vector3(-0.19f, 3.66f + lift + (e == "think" ? 0.04f : 0), 0);
            browR.transform.localPosition = new Vector3(0.19f, 3.66f + lift, 0);

            // mouth
            string m;
            if (mouthOpen)
            {
                switch (e)
                {
                    case "happy": case "laugh": case "love": case "excited": m = "grin"; break;
                    case "surprised": case "scared": m = "o"; break;
                    default: m = "open"; break;
                }
            }
            else
            {
                switch (e)
                {
                    case "happy": case "love": m = "smile"; break;
                    case "laugh": m = "grin"; break;
                    case "sad": case "cry": case "angry": case "disgusted": case "pout": m = "frown"; break;
                    case "surprised": m = "o"; break;
                    case "smug": case "flirty": m = "smirk"; break;
                    case "think": case "worried": case "tired": case "scared": case "embarrassed": m = "flat"; break;
                    default: m = "smile"; break;
                }
            }
            mouth.sprite = MouthSprite(m);

            float blush = e == "embarrassed" || e == "love" || e == "flirty" ? 0.75f : e == "happy" || e == "laugh" ? 0.3f : 0f;
            foreach (var b in new[] { blushL, blushR })
            {
                var c = b.color;
                c.a = blush * lastColor.a;
                b.color = c;
            }
        }

        public void Tick(float dt)
        {
            gestureTime += dt;
            float t = gestureTime;
            float l = 8, r = 8;
            float osc = Mathf.Sin(t * 9f);
            bool done = !gestureLoop && t > 1.6f;
            switch (done ? "idle" : gesture)
            {
                case "talk": l = 10; r = 22 + 12 * Mathf.Sin(t * 5f); break;
                case "wave": l = 8; r = 150 + 22 * osc; break;
                case "point": l = 8; r = 92; break;
                case "shrug": l = r = 58; break;
                case "cheer": case "excited": l = r = 155 + 10 * osc; break;
                case "think": l = 8; r = -128; break;
                case "cross": case "arms_crossed": case "crossed": l = r = -62; break;
                case "angry": case "hips": case "stomp": l = r = 34; break;
                case "cry": case "facepalm": l = r = -138; break;
                case "dance": l = 90 + 50 * Mathf.Sin(t * 6f); r = 90 - 50 * Mathf.Sin(t * 6f); break;
                case "laugh": case "giggle": l = 12; r = -35 + 6 * osc; break;
                case "bow": l = r = 2; break;
                case "flinch": case "scared": case "shiver": l = r = -40; break;
                case "gasp": l = 8; r = -150; break;
            }
            armLAngle = Mathf.Lerp(armLAngle, l, 1 - Mathf.Exp(-dt * 10f));
            armRAngle = Mathf.Lerp(armRAngle, r, 1 - Mathf.Exp(-dt * 10f));
            armLPivot.localRotation = Quaternion.Euler(0, 0, -armLAngle);
            armRPivot.localRotation = Quaternion.Euler(0, 0, armRAngle);
        }
    }

    // =====================================================================
    // Sprite folders: single images per pose/expression, or layered parts.
    // =====================================================================

    /// <summary>
    /// Characters/NAME/ with either:
    ///  - single images named by expression or pose (neutral.png, happy.png, talk_happy.png, happy_talk.png for lip flap, happy_blink.png), or
    ///  - layered part folders: body/, outfit/, face/, eyes/, mouth/, brows/, hair/, accessory/ (all exported on the same canvas size).
    /// </summary>
    public sealed class SpriteFolderVisual : MonoBehaviour, ICharacterVisual
    {
        static readonly string[] SlotOrder = { "back", "body", "outfit", "face", "eyes", "mouth", "brows", "hair", "accessory", "front" };

        readonly Dictionary<string, Dictionary<string, Sprite>> slots = new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, SpriteRenderer> renderers = new Dictionary<string, SpriteRenderer>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> outfit = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Sprite> singles;
        SpriteRenderer single, singleFade;
        bool layered;
        string pose = "idle", emotion = "neutral";
        bool mouthOpen, eyesClosed;
        float nativeHeight = 4f, headTop = 0.95f;
        Color color = Color.white;
        float crossfade;

        public Transform Root => transform;
        public float NativeHeight => nativeHeight;
        public float HeadTop => headTop;
        public bool HasWalkCycle => Has("body", "walk") || (singles != null && singles.ContainsKey("walk"));

        public static SpriteFolderVisual TryCreate(Transform parent, string id, CharacterDefinition def)
        {
            string folder = "Characters/" + FindFolder(id);
            var body = AssetLibrary.LoadAllSprites(folder + "/body");
            var all = AssetLibrary.LoadAllSprites(folder);
            if (body.Length == 0 && all.Length == 0) return null;

            var go = new GameObject("Sprites " + id);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SpriteFolderVisual>();
            if (body.Length > 0)
            {
                v.layered = true;
                foreach (var slot in SlotOrder)
                {
                    var sprites = slot == "body" ? body : AssetLibrary.LoadAllSprites(folder + "/" + slot);
                    if (sprites.Length == 0) continue;
                    v.slots[slot] = sprites.GroupBy(s => s.name.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                    var sr = new GameObject(slot).AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(go.transform, false);
                    sr.sortingOrder = Array.IndexOf(SlotOrder, slot);
                    v.renderers[slot] = sr;
                }
            }
            else
            {
                v.singles = all.GroupBy(s => s.name.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                v.single = new GameObject("image").AddComponent<SpriteRenderer>();
                v.single.transform.SetParent(go.transform, false);
                v.singleFade = new GameObject("image (fade)").AddComponent<SpriteRenderer>();
                v.singleFade.transform.SetParent(go.transform, false);
                v.singleFade.sortingOrder = 1;
                v.singleFade.color = new Color(1, 1, 1, 0);
            }
            if (def != null && def.Options.TryGetValue("outfit", out var o)) v.outfit["outfit"] = o;
            if (def != null && def.Options.TryGetValue("hair", out var h)) v.outfit["hair"] = h;
            v.Refresh(true);
            v.MeasureHeight(def);
            return v;
        }

        /// <summary>Folder lookups are case-insensitive in practice, but try a few spellings.</summary>
        public static string FindFolder(string id)
        {
            foreach (var v in new[] { id, id.ToLowerInvariant(), char.ToUpper(id[0]) + id.Substring(1).ToLowerInvariant() })
            {
                if (Resources.LoadAll<Sprite>("Characters/" + v).Length > 0 || Resources.LoadAll<Texture2D>("Characters/" + v).Length > 0) return v;
            }
            return id;
        }

        void MeasureHeight(CharacterDefinition def)
        {
            Sprite reference = layered ? (renderers.TryGetValue("body", out var b) ? b.sprite : null) : single.sprite;
            if (reference != null)
            {
                nativeHeight = reference.bounds.size.y;
                // Pivot may not be at the feet; shift so the bottom of the art sits at y = 0.
                float bottom = reference.bounds.min.y;
                foreach (Transform child in transform) child.localPosition = new Vector3(child.localPosition.x, -bottom, 0);
            }
            if (def != null && def.Options.TryGetValue("head", out var head) && Util.TryFloat(head, out var ht)) headTop = ht;
        }

        bool Has(string slot, string name) => slots.TryGetValue(slot, out var d) && d.ContainsKey(name);

        Sprite Find(string slot, params string[] names)
        {
            if (!slots.TryGetValue(slot, out var d)) return null;
            foreach (var n in names) if (!string.IsNullOrEmpty(n) && d.TryGetValue(n, out var s)) return s;
            return null;
        }

        Sprite FindSingle(params string[] names)
        {
            foreach (var n in names) if (!string.IsNullOrEmpty(n) && singles.TryGetValue(n, out var s)) return s;
            return null;
        }

        public bool HasAnimation(string name)
        {
            if (layered) return Has("body", name);
            return singles.ContainsKey(name);
        }

        public bool Play(string gesture, string fullName, bool loop)
        {
            if (layered)
            {
                if (Has("body", fullName)) pose = fullName;
                else if (gesture != null && Has("body", gesture)) pose = gesture;
                else if (gesture == null || gesture == "idle") pose = "idle";
            }
            else
            {
                if (FindSingle(fullName) != null) pose = fullName;
                else if (gesture != null && FindSingle(gesture) != null) pose = gesture;
                else pose = null;
            }
            Refresh(false);
            return false; // single frames still get procedural motion
        }

        public void SetEmotion(string e)
        {
            emotion = string.IsNullOrEmpty(e) ? "neutral" : e.ToLowerInvariant();
            Refresh(false);
        }

        public void SetMouthOpen(bool open) { if (open != mouthOpen) { mouthOpen = open; Refresh(true); } }
        public void SetEyesClosed(bool closed) { if (closed != eyesClosed) { eyesClosed = closed; Refresh(true); } }

        public bool SetOutfit(string slot, string label)
        {
            slot = string.IsNullOrEmpty(slot) ? "outfit" : slot;
            outfit[slot] = label;
            Refresh(false);
            return true;
        }

        public void SetColor(Color c)
        {
            color = c;
            foreach (var r in renderers.Values) r.color = c;
            if (single != null) single.color = c;
            if (singleFade != null) singleFade.color = new Color(c.r, c.g, c.b, singleFade.color.a * c.a);
        }

        void Refresh(bool instant)
        {
            if (layered)
            {
                string normEmotion = Emotions.Normalize(emotion);
                string first(string slot) => slots.TryGetValue(slot, out var d) ? d.Keys.OrderBy(k => k).FirstOrDefault() : null;
                SetSlot("body", Find("body", pose, "idle", "default", "neutral") ?? Find("body", first("body")));
                string o = outfit.TryGetValue("outfit", out var oo) ? oo : null;
                SetSlot("outfit", Find("outfit", o != null ? o + "_" + pose : null, o, "default_" + pose, "default") ?? (o == null ? Find("outfit", first("outfit")) : null));
                SetSlot("face", Find("face", emotion, normEmotion, "neutral", "default"));
                SetSlot("eyes", eyesClosed
                    ? Find("eyes", emotion + "_closed", normEmotion + "_closed", "closed", "blink")
                    : Find("eyes", emotion, normEmotion, "open", "neutral", "default"));
                SetSlot("mouth", mouthOpen
                    ? Find("mouth", emotion + "_open", normEmotion + "_open", "open", "talk")
                    : Find("mouth", emotion, emotion + "_closed", normEmotion, normEmotion + "_closed", "closed", "neutral", "default"));
                SetSlot("brows", Find("brows", emotion, normEmotion, "neutral", "default"));
                string h = outfit.TryGetValue("hair", out var hh) ? hh : null;
                SetSlot("hair", Find("hair", h, "default") ?? (h == null ? Find("hair", first("hair")) : null));
                foreach (var s in new[] { "accessory", "back", "front" })
                {
                    string label = outfit.TryGetValue(s, out var a) ? a : "default";
                    SetSlot(s, label == "none" ? null : Find(s, label + "_" + pose, label));
                }
                return;
            }

            string em = emotion, ne = Emotions.Normalize(emotion), p = pose;
            string baseName = null;
            foreach (var candidate in new[] { p != null ? p + "_" + em : null, p != null ? em + "_" + p : null, p, em, ne, "neutral", "idle", "default" })
            {
                if (candidate != null && singles.ContainsKey(candidate)) { baseName = candidate; break; }
            }
            if (baseName == null) baseName = singles.Keys.OrderBy(k => k).First();
            Sprite sprite = null;
            if (eyesClosed) sprite = FindSingle(baseName + "_blink", baseName + "_closed");
            if (sprite == null && mouthOpen) sprite = FindSingle(baseName + "_talk", baseName + "_open");
            if (sprite == null) sprite = singles[baseName];
            if (single.sprite == sprite) return;
            if (!instant && single.sprite != null && isActiveAndEnabled)
            {
                singleFade.sprite = single.sprite;
                crossfade = 1f;
            }
            single.sprite = sprite;
        }

        void SetSlot(string slot, Sprite s)
        {
            if (renderers.TryGetValue(slot, out var r)) r.sprite = s;
        }

        public void Tick(float dt)
        {
            if (singleFade == null) return;
            if (crossfade > 0f)
            {
                crossfade = Mathf.Max(0f, crossfade - dt / 0.18f);
                singleFade.color = new Color(color.r, color.g, color.b, color.a * crossfade);
            }
        }
    }

    // =====================================================================
    // Prefabs: rigged characters made with Unity 2D Animation / PSD Importer.
    // =====================================================================

    /// <summary>
    /// Optional component on a character prefab to tell the engine about its rig.
    /// Without it, the engine finds the Animator and Sprite Resolvers by itself.
    /// </summary>
    public sealed class VNCharacterRig : MonoBehaviour
    {
        [Tooltip("Animator whose state names match the animation names in your scripts (talk_happy, idle, wave...).")]
        public Animator animator;
        [Tooltip("State the character goes back to after a one-shot animation.")]
        public string idleState = "idle";
        [Tooltip("Optional point at the top of the head, for speech bubbles.")]
        public Transform headTop;
        [Tooltip("Sprite Library categories that change with expressions.")]
        public string[] expressionCategories = { "Face", "Eyes", "Mouth", "Brows", "Expression" };
        public string mouthCategory = "Mouth";
        public string eyesCategory = "Eyes";
        [Tooltip("Objects shown while the mouth is open / closed, if you use objects instead of sprite swaps.")]
        public GameObject mouthOpen, mouthClosed;
        public GameObject eyesOpen, eyesClosed;
        [Tooltip("Name of an Animator layer with one state per expression (happy, sad...). Optional.")]
        public string faceLayer = "Face";
    }

    public sealed class PrefabVisual : MonoBehaviour, ICharacterVisual
    {
        Animator animator;
        VNCharacterRig rig;
        Renderer[] renderers;
        readonly List<SpriteResolverProxy> resolvers = new List<SpriteResolverProxy>();
        float nativeHeight = 4f, headTop = 0.95f, bottom;
        string currentState, returnState, emotion = "neutral";
        bool currentLoops;
        bool mouthOpen;
        int faceLayer = -1;

        public Transform Root => transform;
        public float NativeHeight => nativeHeight;
        public float HeadTop => headTop;
        public bool HasWalkCycle => HasAnimation("walk");

        public static PrefabVisual TryCreate(Transform parent, string id, CharacterDefinition def)
        {
            string folder = SpriteFolderVisual.FindFolder(id);
            var prefab = Resources.Load<GameObject>("Characters/" + folder + "/" + folder)
                         ?? Resources.Load<GameObject>("Characters/" + folder)
                         ?? Resources.Load<GameObject>("Characters/" + folder + "/" + id);
            if (prefab == null)
            {
                var any = Resources.LoadAll<GameObject>("Characters/" + folder);
                if (any.Length > 0) prefab = any[0];
            }
            if (prefab == null) return null;

            var holder = new GameObject("Rig " + id);
            holder.transform.SetParent(parent, false);
            var instance = Instantiate(prefab, holder.transform, false);
            instance.name = prefab.name;
            var v = holder.AddComponent<PrefabVisual>();
            v.Setup(instance);
            return v;
        }

        void Setup(GameObject instance)
        {
            rig = instance.GetComponentInChildren<VNCharacterRig>();
            animator = rig != null && rig.animator != null ? rig.animator : instance.GetComponentInChildren<Animator>();
            renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var c in instance.GetComponentsInChildren<Component>(true))
            {
                var p = SpriteResolverProxy.Wrap(c);
                if (p != null) resolvers.Add(p);
            }
            if (animator != null && rig != null && !string.IsNullOrEmpty(rig.faceLayer))
                faceLayer = animator.GetLayerIndex(rig.faceLayer);

            // Measure in local space at rest.
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                if (!(r is SpriteRenderer)) continue;
                var b = r.bounds;
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }
            if (!first)
            {
                Vector3 min = transform.InverseTransformPoint(bounds.min), max = transform.InverseTransformPoint(bounds.max);
                nativeHeight = Mathf.Max(0.01f, max.y - min.y);
                bottom = min.y;
                instance.transform.localPosition -= new Vector3(0, bottom, 0);
            }
            if (rig != null && rig.headTop != null)
                headTop = Mathf.Clamp01((transform.InverseTransformPoint(rig.headTop.position).y) / nativeHeight);
            if (animator != null && HasAnimation(IdleState)) animator.Play(IdleState, 0, 0f);
        }

        string IdleState => rig != null && !string.IsNullOrEmpty(rig.idleState) ? rig.idleState : "idle";

        public bool HasAnimation(string name)
        {
            if (animator == null || string.IsNullOrEmpty(name) || animator.runtimeAnimatorController == null) return false;
            return animator.HasState(0, Animator.StringToHash(name));
        }

        public bool Play(string gesture, string fullName, bool loop)
        {
            if (animator == null) return false;
            string state = HasAnimation(fullName) ? fullName : HasAnimation(fullName + "_loop") ? fullName + "_loop" : HasAnimation(gesture) ? gesture : null;
            if (state == null)
            {
                if (gesture == null || gesture == "idle")
                {
                    string idle = HasAnimation("idle_" + emotion) ? "idle_" + emotion : IdleState;
                    if (HasAnimation(idle)) { animator.CrossFadeInFixedTime(idle, 0.2f, 0); currentState = idle; currentLoops = true; }
                }
                return false;
            }
            animator.CrossFadeInFixedTime(state, 0.15f, 0);
            currentState = state;
            currentLoops = loop || state.EndsWith("_loop");
            returnState = HasAnimation("idle_" + emotion) ? "idle_" + emotion : IdleState;
            return true;
        }

        public void SetEmotion(string e)
        {
            emotion = string.IsNullOrEmpty(e) ? "neutral" : e.ToLowerInvariant();
            string norm = Emotions.Normalize(emotion);
            var cats = rig != null ? rig.expressionCategories : new[] { "Face", "Eyes", "Mouth", "Brows", "Expression" };
            foreach (var r in resolvers)
            {
                if (!cats.Any(c => string.Equals(c, r.Category, StringComparison.OrdinalIgnoreCase))) continue;
                if (!r.TrySet(emotion)) r.TrySet(norm);
            }
            if (faceLayer >= 0)
            {
                if (animator.HasState(faceLayer, Animator.StringToHash(emotion))) animator.CrossFadeInFixedTime(emotion, 0.1f, faceLayer);
                else if (animator.HasState(faceLayer, Animator.StringToHash(norm))) animator.CrossFadeInFixedTime(norm, 0.1f, faceLayer);
            }
        }

        public void SetMouthOpen(bool open)
        {
            if (mouthOpen == open) return;
            mouthOpen = open;
            if (rig != null && rig.mouthOpen != null) rig.mouthOpen.SetActive(open);
            if (rig != null && rig.mouthClosed != null) rig.mouthClosed.SetActive(!open);
            string cat = rig != null ? rig.mouthCategory : "Mouth";
            foreach (var r in resolvers)
            {
                if (!string.Equals(r.Category, cat, StringComparison.OrdinalIgnoreCase)) continue;
                if (open) { if (!r.TrySet(emotion + "_open")) r.TrySet("open"); }
                else { if (!r.TrySet(emotion)) if (!r.TrySet(emotion + "_closed")) r.TrySet("closed"); }
            }
        }

        public void SetEyesClosed(bool closed)
        {
            if (rig != null && rig.eyesOpen != null) rig.eyesOpen.SetActive(!closed);
            if (rig != null && rig.eyesClosed != null) rig.eyesClosed.SetActive(closed);
            string cat = rig != null ? rig.eyesCategory : "Eyes";
            foreach (var r in resolvers)
            {
                if (!string.Equals(r.Category, cat, StringComparison.OrdinalIgnoreCase)) continue;
                if (closed) { if (!r.TrySet(emotion + "_closed")) r.TrySet("closed"); }
                else { if (!r.TrySet(emotion)) r.TrySet("open"); }
            }
        }

        public bool SetOutfit(string slot, string label)
        {
            slot = string.IsNullOrEmpty(slot) ? "Outfit" : slot;
            bool any = false;
            foreach (var r in resolvers)
                if (string.Equals(r.Category, slot, StringComparison.OrdinalIgnoreCase) || r.Category.StartsWith(slot, StringComparison.OrdinalIgnoreCase))
                    any |= r.TrySet(label);
            if (!any && animator != null && HasAnimation("outfit_" + label)) animator.Play("outfit_" + label, 0, 0);
            return any;
        }

        public void SetColor(Color c)
        {
            foreach (var r in renderers)
                if (r is SpriteRenderer sr) sr.color = c;
        }

        public void Tick(float dt)
        {
            if (animator == null || currentState == null || currentLoops) return;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(currentState) && info.normalizedTime >= 1f && !animator.IsInTransition(0))
            {
                currentState = null;
                if (returnState != null && HasAnimation(returnState)) animator.CrossFadeInFixedTime(returnState, 0.25f, 0);
            }
        }
    }

    /// <summary>Talks to Unity's SpriteResolver (2D Animation package) without a hard dependency.</summary>
    public sealed class SpriteResolverProxy
    {
        static Type resolverType;
        static MethodInfo getCategory, setCategoryAndLabel, getLibrary;
        static MethodInfo librarySpriteLookup;
        static bool searched;

        readonly Component component;
        public string Category => getCategory?.Invoke(component, null) as string ?? "";

        SpriteResolverProxy(Component c) { component = c; }

        public static SpriteResolverProxy Wrap(Component c)
        {
            if (c == null) return null;
            if (!searched)
            {
                searched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    resolverType = asm.GetType("UnityEngine.U2D.Animation.SpriteResolver");
                    if (resolverType != null) break;
                }
                if (resolverType != null)
                {
                    getCategory = resolverType.GetMethod("GetCategory", Type.EmptyTypes);
                    setCategoryAndLabel = resolverType.GetMethod("SetCategoryAndLabel", new[] { typeof(string), typeof(string) });
                    getLibrary = resolverType.GetProperty("spriteLibrary")?.GetGetMethod();
                }
            }
            if (resolverType == null || !resolverType.IsInstanceOfType(c)) return null;
            return new SpriteResolverProxy(c);
        }

        /// <summary>Switches to label if the library has it.</summary>
        public bool TrySet(string label)
        {
            if (string.IsNullOrEmpty(label) || setCategoryAndLabel == null) return false;
            string cat = Category;
            var library = getLibrary?.Invoke(component, null);
            if (library != null)
            {
                if (librarySpriteLookup == null) librarySpriteLookup = library.GetType().GetMethod("GetSprite", new[] { typeof(string), typeof(string) });
                if (librarySpriteLookup != null && librarySpriteLookup.Invoke(library, new object[] { cat, label }) == null) return false;
            }
            var ok = setCategoryAndLabel.Invoke(component, new object[] { cat, label });
            return !(ok is bool b) || b;
        }
    }
}
