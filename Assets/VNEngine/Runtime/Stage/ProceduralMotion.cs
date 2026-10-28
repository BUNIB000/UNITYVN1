using UnityEngine;

namespace VNEngine
{
    /// <summary>An offset applied on top of a character's placement. Offset is in character heights.</summary>
    public struct MotionPose
    {
        public Vector2 offset;
        public float rotation;
        public Vector2 scale;

        public static MotionPose Identity => new MotionPose { scale = Vector2.one };

        public static MotionPose Lerp(MotionPose a, MotionPose b, float k) => new MotionPose
        {
            offset = Vector2.LerpUnclamped(a.offset, b.offset, k),
            rotation = Mathf.LerpUnclamped(a.rotation, b.rotation, k),
            scale = Vector2.LerpUnclamped(a.scale, b.scale, k)
        };
    }

    /// <summary>
    /// Body motion for named gestures, applied to any art. This is what makes a single
    /// still image jump, nod, laugh or tremble. Rigged characters with their own animation
    /// for a name skip this.
    /// </summary>
    public static class ProceduralMotion
    {
        public delegate MotionPose Curve(float k, float t);

        /// <summary>Gestures that keep their final pose until the next animation.</summary>
        public static bool Holds(string gesture)
        {
            switch (gesture)
            {
                case "think": case "lean": case "pout": case "faint": case "shy": case "bow_hold": case "sulk": return true;
            }
            return false;
        }

        public static bool TryGet(string gesture, out float duration, out Curve curve)
        {
            duration = 0.6f;
            curve = null;
            if (string.IsNullOrEmpty(gesture)) return false;
            switch (gesture)
            {
                case "jump":
                case "hop":
                    duration = 0.55f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0, 0.075f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((k - 0.12f) / 0.76f))),
                        scale = new Vector2(1 + Squash(k) * 0.05f, 1 - Squash(k) * 0.06f)
                    };
                    return true;
                case "bounce":
                case "cheer":
                case "excited":
                    duration = 0.8f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0, 0.04f * Mathf.Abs(Mathf.Sin(2 * Mathf.PI * k))),
                        scale = new Vector2(1, 1 + 0.015f * Mathf.Sin(4 * Mathf.PI * k))
                    };
                    return true;
                case "nod":
                case "yes":
                    duration = 0.7f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0, -0.012f * Mathf.Abs(Mathf.Sin(2 * Mathf.PI * k))),
                        rotation = 0,
                        scale = new Vector2(1, 1 - 0.01f * Mathf.Abs(Mathf.Sin(2 * Mathf.PI * k)))
                    };
                    return true;
                case "shake_head":
                case "no":
                case "shake":
                    duration = 0.7f;
                    curve = (k, t) => new MotionPose { rotation = 3.5f * Mathf.Sin(5 * Mathf.PI * k) * (1 - k), scale = Vector2.one };
                    return true;
                case "laugh":
                case "giggle":
                    duration = 1.1f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0, 0.01f * Mathf.Abs(Mathf.Sin(9 * Mathf.PI * k)) * (1 - k * 0.5f)),
                        rotation = -1.5f * Mathf.Sin(Mathf.PI * k),
                        scale = new Vector2(1, 1 + 0.01f * Mathf.Sin(18 * Mathf.PI * k))
                    };
                    return true;
                case "gasp":
                case "surprised":
                case "flinch":
                case "shocked":
                    duration = 0.55f;
                    curve = (k, t) =>
                    {
                        float pop = k < 0.2f ? k / 0.2f : 1 - Easing.Apply(Ease.OutBack, (k - 0.2f) / 0.8f) * 0.85f;
                        return new MotionPose { offset = new Vector2(0, 0.025f * pop), rotation = 2.5f * pop, scale = new Vector2(1 - 0.02f * pop, 1 + 0.04f * pop) };
                    };
                    return true;
                case "sigh":
                case "sad":
                    duration = 1.5f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0, -0.018f * Mathf.Sin(Mathf.PI * k)),
                        scale = new Vector2(1 + 0.01f * Mathf.Sin(Mathf.PI * k), 1 - 0.022f * Mathf.Sin(Mathf.PI * k))
                    };
                    return true;
                case "shiver":
                case "scared":
                case "tremble":
                    duration = 1.1f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0.006f * Mathf.Sin(t * 70f) * (1 - k * 0.6f), -0.008f * Mathf.Sin(Mathf.PI * k)),
                        scale = Vector2.one
                    };
                    return true;
                case "cry":
                    duration = 1.6f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0.003f * Mathf.Sin(t * 40f), -0.015f * Mathf.Sin(Mathf.PI * k) - 0.004f * Mathf.Abs(Mathf.Sin(t * 8f))),
                        scale = new Vector2(1, 1 - 0.015f * Mathf.Sin(Mathf.PI * k))
                    };
                    return true;
                case "angry":
                case "stomp":
                    duration = 0.6f;
                    curve = (k, t) =>
                    {
                        float hit = k < 0.25f ? Mathf.Sin(k / 0.25f * Mathf.PI) : 0f;
                        return new MotionPose
                        {
                            offset = new Vector2(0.007f * Mathf.Sin(t * 55f) * (1 - k), -0.025f * hit),
                            scale = new Vector2(1 + 0.03f * hit, 1 - 0.03f * hit)
                        };
                    };
                    return true;
                case "dance":
                    duration = 1.8f;
                    curve = (k, t) => new MotionPose
                    {
                        offset = new Vector2(0.02f * Mathf.Sin(4 * Mathf.PI * k), 0.018f * Mathf.Abs(Mathf.Sin(4 * Mathf.PI * k))),
                        rotation = 5f * Mathf.Sin(4 * Mathf.PI * k),
                        scale = Vector2.one
                    };
                    return true;
                case "wave":
                    duration = 1.2f;
                    curve = (k, t) => new MotionPose { rotation = 1.5f * Mathf.Sin(6 * Mathf.PI * k) * Mathf.Sin(Mathf.PI * k), offset = new Vector2(0, 0.006f * Mathf.Sin(Mathf.PI * k)), scale = Vector2.one };
                    return true;
                case "spin":
                    duration = 0.6f;
                    curve = (k, t) => new MotionPose { scale = new Vector2(Mathf.Cos(2 * Mathf.PI * Easing.Apply(Ease.InOutQuad, k)), 1), offset = new Vector2(0, 0.02f * Mathf.Sin(Mathf.PI * k)) };
                    return true;
                case "shrug":
                    duration = 0.7f;
                    curve = (k, t) => new MotionPose { offset = new Vector2(0, 0.015f * Mathf.Sin(Mathf.PI * k)), scale = new Vector2(1 + 0.02f * Mathf.Sin(Mathf.PI * k), 1) };
                    return true;
                case "bow":
                    duration = 1.2f;
                    curve = (k, t) => new MotionPose { offset = new Vector2(0, -0.035f * Mathf.Sin(Mathf.PI * k)), rotation = -3f * Mathf.Sin(Mathf.PI * k), scale = new Vector2(1, 1 - 0.03f * Mathf.Sin(Mathf.PI * k)) };
                    return true;
                case "think":
                case "lean":
                case "pout":
                case "shy":
                case "sulk":
                    duration = 0.6f;
                    float angle = gesture == "lean" ? -5f : gesture == "think" ? 3f : 2f;
                    float drop = gesture == "shy" || gesture == "sulk" ? -0.01f : 0f;
                    curve = (k, t) => new MotionPose { rotation = angle * Easing.Apply(Ease.OutBack, k), offset = new Vector2(0, drop * k), scale = Vector2.one };
                    return true;
                case "faint":
                    duration = 1.2f;
                    curve = (k, t) =>
                    {
                        float f = Easing.Apply(Ease.OutBounce, k);
                        return new MotionPose { rotation = -80f * f, offset = new Vector2(-0.15f * f, -0.35f * f), scale = Vector2.one };
                    };
                    return true;
                case "point":
                    duration = 0.5f;
                    curve = (k, t) => new MotionPose { rotation = -2.5f * Mathf.Sin(Mathf.PI * k), offset = new Vector2(0.01f * Mathf.Sin(Mathf.PI * k), 0), scale = Vector2.one };
                    return true;
                case "talk":
                    duration = 1.2f;
                    curve = (k, t) => new MotionPose { offset = new Vector2(0, 0.004f * Mathf.Abs(Mathf.Sin(t * 7f))), rotation = 0.8f * Mathf.Sin(t * 2.3f), scale = Vector2.one };
                    return true;
                case "love":
                case "swoon":
                    duration = 1.0f;
                    curve = (k, t) => new MotionPose { rotation = 2.5f * Mathf.Sin(2 * Mathf.PI * k), offset = new Vector2(0, 0.012f * Mathf.Sin(Mathf.PI * k)), scale = new Vector2(1, 1 + 0.01f * Mathf.Sin(Mathf.PI * k)) };
                    return true;
            }
            return false;
        }

        static float Squash(float k)
        {
            if (k < 0.12f) return Mathf.Sin(k / 0.12f * Mathf.PI);
            if (k > 0.88f) return Mathf.Sin((k - 0.88f) / 0.12f * Mathf.PI);
            return k < 0.5f ? -0.4f * Mathf.Sin((k - 0.12f) / 0.38f * Mathf.PI) : 0f;
        }
    }
}
