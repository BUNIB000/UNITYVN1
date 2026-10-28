using System.Collections.Generic;
using UnityEngine;

namespace VNEngine
{
    /// <summary>Rain, snow, petals, leaves, sparkles, dust. Lives in front of the stage, follows the camera.</summary>
    public sealed class Weather : MonoBehaviour
    {
        struct Particle
        {
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float spin, phase, size;
        }

        readonly List<Particle> particles = new List<Particle>();
        StageView stage;
        public string Kind { get; private set; }
        float intensity = 1f;

        public void Init(StageView s) { stage = s; }

        public void Set(string kind, float amount)
        {
            kind = string.IsNullOrEmpty(kind) ? null : kind.ToLowerInvariant();
            if (kind == "none" || kind == "stop" || kind == "clear" || kind == "off") kind = null;
            Kind = kind;
            intensity = Mathf.Clamp(amount, 0.1f, 3f);
            foreach (var p in particles) if (p.sr) Destroy(p.sr.gameObject);
            particles.Clear();
            if (kind == null) return;

            int count = Mathf.RoundToInt(Count(kind) * intensity);
            var sprite = SpriteFor(kind);
            var custom = AssetLibrary.LoadSprite("Images", "weather_" + kind);
            for (int i = 0; i < count; i++)
            {
                var sr = new GameObject("p").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = custom != null ? custom : sprite;
                sr.sortingOrder = 4000;
                sr.color = ColorFor(kind);
                var p = new Particle { sr = sr };
                Respawn(ref p, true);
                particles.Add(p);
            }
        }

        static int Count(string kind)
        {
            switch (kind)
            {
                case "rain": case "storm": return 140;
                case "snow": return 90;
                case "sparkles": case "magic": return 40;
                case "dust": return 30;
                default: return 45;
            }
        }

        static Sprite SpriteFor(string kind)
        {
            switch (kind)
            {
                case "rain":
                case "storm":
                    return ProceduralArt.FromSdf("w_rain", 6, 60, (x, y) => ProceduralArt.Segment(x, y, new Vector2(3, 3), new Vector2(3, 57), 1.4f), new Vector2(0.5f, 0.5f));
                case "petals":
                case "sakura":
                case "leaves":
                    return ProceduralArt.FromSdf("w_petal", 40, 28, (x, y) => ProceduralArt.Ellipse(x, y, 20, 14, 18, 9), new Vector2(0.5f, 0.5f));
                case "sparkles":
                case "magic":
                    return ProceduralArt.Star();
                default:
                    return ProceduralArt.SoftCircle(16);
            }
        }

        static Color ColorFor(string kind)
        {
            switch (kind)
            {
                case "rain": case "storm": return new Color(0.8f, 0.88f, 1f, 0.45f);
                case "snow": return new Color(1, 1, 1, 0.9f);
                case "petals": case "sakura": return new Color(1f, 0.75f, 0.85f, 0.95f);
                case "leaves": return new Color(0.9f, 0.55f, 0.2f, 0.95f);
                case "sparkles": case "magic": return new Color(1f, 0.95f, 0.6f, 0.9f);
                case "dust": return new Color(1f, 0.95f, 0.85f, 0.35f);
            }
            return new Color(1, 1, 1, 0.6f);
        }

        void Respawn(ref Particle p, bool anywhere)
        {
            float w = stage.ViewWidth * 1.3f, h = stage.ViewHeight * 1.2f;
            float x = Random.Range(-w / 2, w / 2);
            float y = anywhere ? Random.Range(-h / 2, h / 2) : h / 2 + Random.Range(0f, 0.4f);
            p.pos = new Vector2(x, y);
            p.phase = Random.value * 10f;
            switch (Kind)
            {
                case "rain": case "storm":
                    p.vel = new Vector2(Kind == "storm" ? -1.4f : -0.5f, Random.Range(-9f, -7f));
                    p.size = Random.Range(0.6f, 1f);
                    break;
                case "snow":
                    p.vel = new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(-0.5f, -0.3f));
                    p.size = Random.Range(0.6f, 1.6f);
                    break;
                case "sparkles": case "magic":
                    p.vel = new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(0.05f, 0.2f));
                    p.size = Random.Range(0.2f, 0.45f);
                    if (!anywhere) p.pos.y = -h / 2;
                    break;
                case "dust":
                    p.vel = new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(-0.05f, 0.05f));
                    p.size = Random.Range(0.4f, 1f);
                    break;
                default:
                    p.vel = new Vector2(Random.Range(-0.5f, -0.2f), Random.Range(-0.8f, -0.5f));
                    p.size = Random.Range(0.5f, 0.9f);
                    p.spin = Random.Range(-180f, 180f);
                    break;
            }
            p.sr.transform.localScale = Vector3.one * p.size;
        }

        void Update()
        {
            if (particles.Count == 0) return;
            float dt = Time.deltaTime;
            float h = stage.ViewHeight * 1.2f, w = stage.ViewWidth * 1.3f;
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                p.phase += dt;
                var v = p.vel;
                if (Kind == "snow" || Kind == "petals" || Kind == "sakura" || Kind == "leaves") v.x += Mathf.Sin(p.phase * 1.7f) * 0.25f;
                p.pos += v * dt;
                bool outside = p.pos.y < -h / 2 - 0.3f || p.pos.y > h / 2 + 0.5f || p.pos.x < -w / 2 - 0.3f || p.pos.x > w / 2 + 0.3f;
                if (outside) Respawn(ref p, false);
                p.sr.transform.localPosition = new Vector3(p.pos.x, p.pos.y, 0);
                if (Kind == "rain" || Kind == "storm")
                    p.sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(p.vel.y, p.vel.x) * Mathf.Rad2Deg + 90);
                else
                    p.sr.transform.localRotation = Quaternion.Euler(0, 0, p.spin * p.phase);
                if (Kind == "sparkles" || Kind == "magic" || Kind == "dust")
                {
                    var c = p.sr.color;
                    c.a = (0.5f + 0.5f * Mathf.Sin(p.phase * 3f)) * ColorFor(Kind).a;
                    p.sr.color = c;
                }
                particles[i] = p;
            }
        }
    }
}
