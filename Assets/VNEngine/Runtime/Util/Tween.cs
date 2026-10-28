using System;
using System.Collections;
using UnityEngine;

namespace VNEngine
{
    public enum Ease
    {
        Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic, OutBack, InBack, InOutBack,
        OutElastic, OutBounce, InOutSine, OutSine, InSine, OutExpo
    }

    public static class Easing
    {
        public static float Apply(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1 - (1 - t) * (1 - t);
                case Ease.InOutQuad: return t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1 - Mathf.Pow(1 - t, 3);
                case Ease.InOutCubic: return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
                case Ease.InBack: { const float c1 = 1.70158f, c3 = c1 + 1; return c3 * t * t * t - c1 * t * t; }
                case Ease.InOutBack:
                {
                    const float c2 = 1.70158f * 1.525f;
                    return t < 0.5f
                        ? Mathf.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2) / 2
                        : (Mathf.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0 || t >= 1) return t;
                    const float c4 = 2 * Mathf.PI / 3;
                    return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * c4) + 1;
                }
                case Ease.OutBounce:
                {
                    const float n1 = 7.5625f, d1 = 2.75f;
                    if (t < 1 / d1) return n1 * t * t;
                    if (t < 2 / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                    if (t < 2.5 / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                    t -= 2.625f / d1; return n1 * t * t + 0.984375f;
                }
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
                case Ease.OutSine: return Mathf.Sin(t * Mathf.PI / 2);
                case Ease.InSine: return 1 - Mathf.Cos(t * Mathf.PI / 2);
                case Ease.OutExpo: return t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);
            }
            return t;
        }
    }

    /// <summary>
    /// Lightweight coroutine tweens. Every tween belongs to an owner MonoBehaviour,
    /// so destroying the owner stops its tweens.
    /// </summary>
    public static class Tween
    {
        /// <summary>Animates t from 0 to 1 over duration (scaled time), calling step with the eased value.</summary>
        public static IEnumerator Run(float duration, Ease ease, Action<float> step, bool unscaled = false)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }
            float t = 0f;
            step(Easing.Apply(ease, 0f));
            while (t < duration)
            {
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                step(Easing.Apply(ease, t / duration));
            }
        }

        public static Coroutine Start(MonoBehaviour owner, float duration, Ease ease, Action<float> step, bool unscaled = false)
        {
            if (owner == null || !owner.isActiveAndEnabled) { step(1f); return null; }
            return owner.StartCoroutine(Run(duration, ease, step, unscaled));
        }

        public static IEnumerator Move(Transform tr, Vector3 to, float duration, Ease ease = Ease.InOutQuad)
        {
            Vector3 from = tr.localPosition;
            return Run(duration, ease, k => { if (tr) tr.localPosition = Vector3.LerpUnclamped(from, to, k); });
        }

        public static IEnumerator Scale(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutBack, bool unscaled = false)
        {
            Vector3 from = tr.localScale;
            return Run(duration, ease, k => { if (tr) tr.localScale = Vector3.LerpUnclamped(from, to, k); }, unscaled);
        }

        public static IEnumerator Anchored(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, bool unscaled = false)
        {
            Vector2 from = rt.anchoredPosition;
            return Run(duration, ease, k => { if (rt) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, unscaled);
        }

        public static IEnumerator Fade(CanvasGroup g, float to, float duration, Ease ease = Ease.OutQuad, bool unscaled = false)
        {
            float from = g.alpha;
            return Run(duration, ease, k => { if (g) g.alpha = Mathf.LerpUnclamped(from, to, k); }, unscaled);
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { yield return null; t += Time.deltaTime; }
        }

        /// <summary>Runs several coroutines at once and finishes when all are done.</summary>
        public static IEnumerator All(MonoBehaviour owner, params IEnumerator[] routines)
        {
            int running = routines.Length;
            foreach (var r in routines) owner.StartCoroutine(Track(r, () => running--));
            while (running > 0) yield return null;
        }

        static IEnumerator Track(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }
    }
}
