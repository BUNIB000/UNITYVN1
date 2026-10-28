using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VNEngine
{
    /// <summary>
    /// A character built from separate pieces, moved like a cutout puppet.
    ///
    /// Characters/NAME/rig/ holds the pieces (all on transparent backgrounds):
    ///   head.png  head_happy.png  head_sad.png ...  one head per expression, each with a short neck, all on the same canvas size
    ///   head_talk.png / head_blink.png              neutral head with the mouth open / eyes closed (also head_happy_talk, head_happy_blink ...)
    ///   torso.png                                   shoulders to hips
    ///   upper_arm_l.png upper_arm_r.png             shoulder to elbow, hanging straight down (left and right as seen on screen)
    ///   forearm_l.png forearm_r.png                 elbow to fingertips, hanging straight down
    ///   leg_l.png leg_r.png                         hip to sole of the shoe (a missing side is mirrored from the other)
    ///   hair_back.png                               optional hair that trails behind the head
    /// Only head.png and torso.png are required. Outfits: put the pieces that change in Characters/NAME/rig_OUTFIT/.
    /// The arm angles, walk cycle and head tilt use the same numbers as the web preview, so every gesture in a story works.
    /// </summary>
    public sealed class PartsPuppetVisual : MonoBehaviour, ICharacterVisual
    {
        const float Rad = Mathf.Deg2Rad;
        static readonly HashSet<string> Holds = new HashSet<string> { "think", "lean", "pout", "faint", "shy", "sulk" };

        readonly Dictionary<string, Sprite> main = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Sprite> outfitSet;
        string charFolder, outfitLabel;
        float headMul = 1f;

        Transform body, headPivot, hairPivot, headScale;
        SpriteRenderer torsoSr, headSr, hairSr;
        readonly Transform[] legPivot = new Transform[2], legScale = new Transform[2];
        readonly SpriteRenderer[] legSr = new SpriteRenderer[2];
        readonly Transform[] shoulder = new Transform[2], armScale = new Transform[2], elbow = new Transform[2];
        readonly SpriteRenderer[] upperSr = new SpriteRenderer[2], foreSr = new SpriteRenderer[2];
        readonly List<SpriteRenderer> all = new List<SpriteRenderer>();
        Transform torsoPivot;

        string emotion = "neutral", gesture, lastHead;
        bool mouthOpen, eyesClosed, loop, walking, running;
        float gTime, gDur = 1.2f, walkPhase, headTilt, sway, swayV, lastTilt;
        readonly float[][] arms = { new[] { 7f, -4f }, new[] { 7f, -4f } };
        float nativeHeight = 4f;

        public Transform Root => transform;
        public float NativeHeight => nativeHeight;
        public float HeadTop => 0.99f;
        public bool HasAnimation(string name) => false;
        public bool HasWalkCycle => true;

        // ---------- creation ----------

        public static PartsPuppetVisual TryCreate(Transform parent, string id, CharacterDefinition def)
        {
            string folder = "Characters/" + SpriteFolderVisual.FindFolder(id);
            var sprites = AssetLibrary.LoadAllSprites(folder + "/rig");
            if (sprites.Length == 0) return null;
            var set = sprites.GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            if (!set.ContainsKey("head") || !set.ContainsKey("torso")) return null;

            var go = new GameObject("Puppet " + id);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PartsPuppetVisual>();
            foreach (var kv in set) v.main[kv.Key] = kv.Value;
            v.charFolder = folder;
            if (def != null && def.Options.TryGetValue("headsize", out var hs) && Util.TryFloat(hs, out var hv)) v.headMul = hv;
            v.Build();
            if (def != null && def.Options.TryGetValue("outfit", out var o)) v.SetOutfit("outfit", o);
            return v;
        }

        SpriteRenderer Piece(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            all.Add(sr);
            return sr;
        }

        Transform Pivot(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        void Build()
        {
            body = Pivot("body", transform);
            torsoPivot = Pivot("torso", body);
            torsoSr = Piece("torso image", torsoPivot, 3);
            headPivot = Pivot("head", body);
            hairPivot = Pivot("hair", headPivot);
            headScale = Pivot("head scale", headPivot);
            hairSr = Piece("hair back", hairPivot, 1);
            headSr = Piece("head image", headScale, 2);
            for (int i = 0; i < 2; i++)
            {
                legPivot[i] = Pivot(i == 0 ? "leg L" : "leg R", body);
                legScale[i] = Pivot("scale", legPivot[i]);
                legSr[i] = Piece("leg image", legScale[i], 0);
                shoulder[i] = Pivot(i == 0 ? "arm L" : "arm R", body);
                armScale[i] = Pivot("scale", shoulder[i]);
                upperSr[i] = Piece("upper arm", armScale[i], 4);
                elbow[i] = Pivot("elbow", armScale[i]);
                foreSr[i] = Piece("forearm", elbow[i], 5);
            }
            Layout();
        }

        // ---------- layout (the same proportions as the web preview) ----------

        Sprite Lookup(string name)
        {
            if (outfitSet != null && outfitSet.TryGetValue(name, out var o)) return o;
            return main.TryGetValue(name, out var s) ? s : null;
        }

        /// <summary>The piece for the left (0) or right (1) of the screen, mirroring the other side when only one exists.</summary>
        Sprite Side(string baseName, int side, out bool flip)
        {
            flip = false;
            var own = Lookup(baseName + (side == 0 ? "_l" : "_r"));
            if (own != null) return own;
            var other = Lookup(baseName + (side == 0 ? "_r" : "_l"));
            flip = other != null;
            return other;
        }

        /// <summary>Sets a sprite so its top centre (or bottom centre) sits on the parent's origin.</summary>
        static void Hang(SpriteRenderer sr, Sprite s, bool flip, bool fromBottom = false)
        {
            sr.sprite = s;
            sr.enabled = s != null;
            sr.flipX = flip;
            if (s == null) return;
            var b = s.bounds;
            float x = flip ? b.center.x : -b.center.x;
            sr.transform.localPosition = new Vector3(x, fromBottom ? -b.min.y : -b.max.y, 0);
        }

        void Layout()
        {
            var torso = Lookup("torso"); var head0 = Lookup("head");
            float th = torso.bounds.size.y, tw = torso.bounds.size.x;
            var legs = new Sprite[2]; var legFlip = new bool[2];
            float legH = 0;
            for (int i = 0; i < 2; i++) { legs[i] = Side("leg", i, out legFlip[i]); if (legs[i] != null) legH = Mathf.Max(legH, legs[i].bounds.size.y); }
            float ls = legH > 0 ? Mathf.Clamp(1.3f * th / legH, 0.7f, 1.7f) : 1f;

            var up = new Sprite[2]; var fore = new Sprite[2]; var upFlip = new bool[2]; var foreFlip = new bool[2];
            for (int i = 0; i < 2; i++) { up[i] = Side("upper_arm", i, out upFlip[i]); fore[i] = Side("forearm", i, out foreFlip[i]); }
            float armLen = (up[0] != null ? up[0].bounds.size.y : 0) + (fore[0] != null ? fore[0].bounds.size.y : 0);
            float asc = armLen > 0 ? Mathf.Clamp(1.42f * th / armLen, 0.6f, 1.7f) : 1f;

            // Heads are drawn on their own area of the sheet, so match the head to the torso: about 0.72 of its height.
            float hk = Mathf.Clamp(0.72f * th / head0.bounds.size.y, 0.3f, 3f) * headMul;
            float hH = head0.bounds.size.y * hk;
            float torsoTop = th + 0.88f * legH * ls;
            nativeHeight = 0.92f * hH + th + 0.88f * legH * ls;

            torsoPivot.localPosition = new Vector3(0, torsoTop, 0);
            Hang(torsoSr, torso, false);
            headPivot.localPosition = new Vector3(0, torsoTop - 0.16f * hH, 0);
            headScale.localScale = Vector3.one * hk;
            hairPivot.localPosition = new Vector3(0, hH * 0.92f, 0);
            hairPivot.localScale = Vector3.one * hk;
            Hang(hairSr, Lookup("hair_back"), false);
            RefreshHead();

            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1 : 1;
                legPivot[i].localPosition = new Vector3(s * tw * 0.2f, torsoTop - th + (legs[i] != null ? legs[i].bounds.size.y * ls * 0.12f : 0), 0);
                legScale[i].localScale = Vector3.one * ls;
                Hang(legSr[i], legs[i], legFlip[i]);

                float uw = up[i] != null ? up[i].bounds.size.x * asc : 0;
                shoulder[i].localPosition = new Vector3(s * (tw / 2 - uw * 0.6f), torsoTop - uw * 0.12f, 0);
                armScale[i].localScale = Vector3.one * asc;
                Hang(upperSr[i], up[i], upFlip[i]);
                elbow[i].localPosition = new Vector3(0, up[i] != null ? -up[i].bounds.size.y * 0.94f : 0, 0);
                Hang(foreSr[i], fore[i], foreFlip[i]);
            }
        }

        void RefreshHead()
        {
            string key = emotion == "neutral" ? "head" : "head_" + emotion;
            if (Lookup(key) == null) key = "head";
            Sprite pick = null;
            if (mouthOpen) pick = Lookup(key + "_talk");
            if (pick == null && eyesClosed) pick = Lookup(key + "_blink");
            if (pick == null) pick = Lookup(key);
            if (pick == null) pick = Lookup("head");
            if (headSr.sprite != pick) Hang(headSr, pick, false, true);
        }

        // ---------- ICharacterVisual ----------

        public bool Play(string gesture, string fullName, bool loop)
        {
            string g = string.IsNullOrEmpty(gesture) ? null : gesture.ToLowerInvariant();
            walking = g == "walk" || g == "run" || fullName == "run";
            running = g == "run" || fullName == "run";
            this.loop = loop;
            if (g == "idle" || walking) g = null;
            gesture = g;
            gTime = 0;
            gDur = g == "talk" ? 1.6f : 1.2f;
            return false; // body motion is still added procedurally
        }

        public void SetEmotion(string e)
        {
            emotion = string.IsNullOrEmpty(e) ? "neutral" : Emotions.Normalize(e.ToLowerInvariant());
            RefreshHead();
        }

        public void SetMouthOpen(bool open) { if (open != mouthOpen) { mouthOpen = open; RefreshHead(); } }
        public void SetEyesClosed(bool closed) { if (closed != eyesClosed) { eyesClosed = closed; RefreshHead(); } }

        public bool SetOutfit(string slot, string label)
        {
            if (!string.IsNullOrEmpty(slot) && slot != "outfit") return false;
            if (string.IsNullOrEmpty(label) || label.Equals("casual", StringComparison.OrdinalIgnoreCase) || label.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                if (outfitSet == null) return true;
                outfitSet = null; outfitLabel = null; Layout(); return true;
            }
            if (label == outfitLabel) return true;
            var sprites = AssetLibrary.LoadAllSprites(charFolder + "/rig_" + label);
            if (sprites.Length == 0) return false; // no pieces for this outfit: keep the current look
            outfitSet = sprites.GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            outfitLabel = label;
            Layout();
            return true;
        }

        public void SetColor(Color color)
        {
            foreach (var sr in all) sr.color = color;
        }

        // ---------- arm poses: [upper, lower] angles in degrees, 0 = hanging down, positive = away from the body ----------

        static bool GesturePose(string g, float k, float t, bool near, out float up, out float low, out float e)
        {
            e = Mathf.Min(1f, Mathf.Min(k * 5f, (1f - k) * 5f));
            up = low = 0;
            float S(float x) => Mathf.Sin(x);
            switch (g)
            {
                case "wave": if (!near) return false; up = 150; low = 18 + 28 * S(t * 13); return true;
                case "cheer": case "excited": up = 152 + 6 * S(t * 10); low = 14; return true;
                case "point": if (!near) return false; up = 78; low = -4; return true;
                case "think": if (!near) return false; up = -14; low = -152; e = 1; return true;
                case "shrug": up = 16; low = 74; return true;
                case "dance": up = 95 + 55 * S(t * 8 + (near ? 0 : Mathf.PI)); low = -45; return true;
                case "cry": up = -6; low = -160; return true;
                case "love": case "swoon": case "blush": case "shy": up = 3; low = -138; return true;
                case "gasp": case "surprised": case "flinch": case "shocked": if (!near) return false; up = -4; low = -142; return true;
                case "angry": case "stomp": up = 28; low = -62; e = 1; return true;
                case "talk": if (!near) return false; up = 22 + 6 * S(t * 2.7f); low = 104 + 16 * S(t * 4.1f); e = 1; return true;
                case "laugh": case "giggle": if (!near) return false; up = -4; low = -96; return true;
                case "bow": up = -6; low = -12; return true;
                case "pout": up = 6; low = -110; e = 1; return true;
                case "cross": case "crossed": case "arms_crossed": up = -20; low = -120; e = 1; return true;
            }
            return false;
        }

        static bool EmotionPose(string emotion, bool near, out float up, out float low)
        {
            up = low = 0;
            switch (emotion)
            {
                case "angry": up = 28; low = -62; return true;
                case "smug": if (!near) return false; up = 28; low = -62; return true;
                case "think": if (!near) return false; up = -14; low = -152; return true;
                case "worried": case "scared": case "embarrassed": up = -2; low = -112; return true;
                case "love": up = 3; low = -138; return true;
                case "sad": case "cry": up = 3; low = -4; return true;
            }
            return false;
        }

        static float TiltFor(string emotion)
        {
            switch (emotion)
            {
                case "think": return 6; case "sad": return -3; case "cry": return -4; case "embarrassed": return -4;
                case "love": return 5; case "happy": return 2; case "smug": return -3; case "worried": return -2;
            }
            return 0;
        }

        public void Tick(float dt)
        {
            if (shoulder[0] == null) return;
            float t = Time.time;
            gTime += dt;
            float k = 0;
            if (gesture != null)
            {
                k = gTime / gDur;
                if (k >= 1f)
                {
                    if (loop) { gTime = 0; k = 0; }
                    else if (Holds.Contains(gesture)) k = 1f;
                    else { gesture = null; k = 0; }
                }
            }
            if (walking) walkPhase += dt * (running ? 3.6f : 2.2f) * Mathf.PI;

            for (int i = 0; i < 2; i++)
            {
                bool near = i == 1;
                float up = 7, low = -4;
                if (EmotionPose(emotion, near, out var eu, out var el)) { up = eu; low = el; }
                if (gesture != null && GesturePose(gesture, Mathf.Min(k, 0.999f), t, near, out var gu, out var gl, out var e))
                {
                    up += (gu - up) * e; low += (gl - low) * e;
                }
                if (walking) { up += Mathf.Sin(walkPhase + (i == 0 ? 0 : Mathf.PI)) * 10f; low -= 8f; }
                up += Mathf.Sin(t * 1.7f + i) * 1.2f;
                float r = 1f - Mathf.Exp(-dt * 11f);
                arms[i][0] += (up - arms[i][0]) * r;
                arms[i][1] += (low - arms[i][1]) * r;

                float s = i == 0 ? -1f : 1f;
                bool hasFore = foreSr[i].sprite != null;
                shoulder[i].localRotation = Quaternion.Euler(0, 0, s * (hasFore ? arms[i][0] : arms[i][0] + arms[i][1] * 0.5f));
                elbow[i].localRotation = Quaternion.Euler(0, 0, s * arms[i][1]);
                // arms hanging close to the body tuck behind the torso so its sleeves hide the shoulder joins
                bool tuck = Mathf.Abs(arms[i][0]) < 40f && Mathf.Abs(arms[i][0] + arms[i][1]) < 70f;
                upperSr[i].sortingOrder = tuck ? -2 : 4;
                foreSr[i].sortingOrder = tuck ? -1 : 5;
            }

            // legs swing while walking and settle when they stop
            float swing = walking ? (running ? 30f : 20f) : 0f;
            for (int i = 0; i < 2; i++)
            {
                float target = swing * Mathf.Sin(walkPhase + (i == 0 ? 0 : Mathf.PI));
                legPivot[i].localRotation = Quaternion.Euler(0, 0, -target);
            }

            // head tilt follows the mood, and the hair trails behind like a spring
            float tiltTarget = TiltFor(emotion) + Mathf.Sin(t * 0.9f) * 1.2f;
            headTilt += (tiltTarget - headTilt) * (1f - Mathf.Exp(-dt * 4f));
            headPivot.localRotation = Quaternion.Euler(0, 0, -headTilt);
            float tiltV = (headTilt - lastTilt) / Mathf.Max(dt, 0.0001f); lastTilt = headTilt;
            swayV += -tiltV * 0.02f - sway * 0.25f; swayV *= 0.8f; sway += swayV * 0.5f;
            sway = Mathf.Clamp(sway + (walking ? Mathf.Sin(walkPhase * 2f) * 0.6f : 0f), -25f, 25f);
            hairPivot.localRotation = Quaternion.Euler(0, 0, sway * 0.35f);
        }
    }
}
