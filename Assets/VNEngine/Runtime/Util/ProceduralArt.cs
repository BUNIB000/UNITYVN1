using System;
using System.Collections.Generic;
using UnityEngine;

namespace VNEngine
{
    /// <summary>
    /// Generates the engine's built-in sprites (bubbles, buttons, icons, placeholder art)
    /// so everything works before any assets exist. All results are cached.
    /// </summary>
    public static class ProceduralArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public delegate float Sdf(float x, float y);

        /// <summary>Rasterises a signed distance function (negative inside) into an anti-aliased white sprite.</summary>
        public static Sprite FromSdf(string key, int w, int h, Sdf sdf, Vector2 pivot, float ppu = 100f, Vector4 border = default, float softness = 1f)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = sdf(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(0.5f - d / softness);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            cache[key] = s;
            return s;
        }

        // ---------- distance functions ----------

        public static float RoundedBox(float px, float py, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(px - cx) - hw + r;
            float qy = Mathf.Abs(py - cy) - hh + r;
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        public static float Circle(float px, float py, float cx, float cy, float r) => new Vector2(px - cx, py - cy).magnitude - r;

        public static float Ellipse(float px, float py, float cx, float cy, float rx, float ry)
        {
            float k = new Vector2((px - cx) / rx, (py - cy) / ry).magnitude;
            return (k - 1f) * Mathf.Min(rx, ry);
        }

        public static float Segment(float px, float py, Vector2 a, Vector2 b, float r)
        {
            var p = new Vector2(px, py);
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        public static float Triangle(float px, float py, Vector2 a, Vector2 b, Vector2 c)
        {
            // Inside test + distance to edges
            var p = new Vector2(px, py);
            float d = Mathf.Min(Segment(px, py, a, b, 0), Mathf.Min(Segment(px, py, b, c, 0), Segment(px, py, c, a, 0)));
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // ---------- UI sprites ----------

        /// <summary>9-sliced rounded rectangle. Radius in pixels.</summary>
        public static Sprite RoundedRect(int radius = 32)
        {
            int size = radius * 2 + 4;
            return FromSdf("rr" + radius, size, size,
                (x, y) => RoundedBox(x, y, size / 2f, size / 2f, size / 2f - 1, size / 2f - 1, radius),
                new Vector2(0.5f, 0.5f), 100f, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }

        /// <summary>9-sliced rounded rectangle outline (ring).</summary>
        public static Sprite RoundedOutline(int radius = 32, int thickness = 6)
        {
            int size = radius * 2 + 4;
            return FromSdf("ro" + radius + "_" + thickness, size, size,
                (x, y) => Mathf.Abs(RoundedBox(x, y, size / 2f, size / 2f, size / 2f - 1 - thickness / 2f, size / 2f - 1 - thickness / 2f, radius - thickness / 2f)) - thickness / 2f,
                new Vector2(0.5f, 0.5f), 100f, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }

        /// <summary>Soft blurred rounded rectangle for drop shadows.</summary>
        public static Sprite SoftShadow(int radius = 40)
        {
            int size = radius * 2 + 40;
            return FromSdf("shadow" + radius, size, size,
                (x, y) => RoundedBox(x, y, size / 2f, size / 2f, size / 2f - 20, size / 2f - 20, radius),
                new Vector2(0.5f, 0.5f), 100f, new Vector4(radius + 20, radius + 20, radius + 20, radius + 20), 22f);
        }

        public static Sprite Circle(int r = 64) =>
            FromSdf("circle" + r, r * 2 + 2, r * 2 + 2, (x, y) => Circle(x, y, r + 1, r + 1, r), new Vector2(0.5f, 0.5f));

        public static Sprite SoftCircle(int r = 64) =>
            FromSdf("softcircle" + r, r * 2 + 2, r * 2 + 2, (x, y) => Circle(x, y, r + 1, r + 1, r * 0.35f), new Vector2(0.5f, 0.5f), 100f, default, r * 0.65f);

        public static Sprite Ring(int r = 64, int thickness = 8) =>
            FromSdf("ring" + r + "_" + thickness, r * 2 + 2, r * 2 + 2, (x, y) => Mathf.Abs(Circle(x, y, r + 1, r + 1, r - thickness / 2f)) - thickness / 2f, new Vector2(0.5f, 0.5f));

        /// <summary>Speech bubble tail pointing down, pivot at the top centre so it can hang from a bubble.</summary>
        public static Sprite Tail() =>
            FromSdf("tail", 64, 64, (x, y) => Triangle(x, y, new Vector2(4, 64), new Vector2(60, 64), new Vector2(26, 2)) - 1f, new Vector2(0.5f, 1f));

        public static Sprite Triangle(string dir = "right")
        {
            Vector2 a, b, c;
            switch (dir)
            {
                case "down": a = new Vector2(4, 56); b = new Vector2(60, 56); c = new Vector2(32, 8); break;
                case "up": a = new Vector2(4, 8); b = new Vector2(60, 8); c = new Vector2(32, 56); break;
                case "left": a = new Vector2(56, 4); b = new Vector2(56, 60); c = new Vector2(8, 32); break;
                default: a = new Vector2(8, 4); b = new Vector2(8, 60); c = new Vector2(56, 32); break;
            }
            return FromSdf("tri_" + dir, 64, 64, (x, y) => Triangle(x, y, a, b, c) - 2f, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Star icon, used for notifications and premium choices.</summary>
        public static Sprite Star()
        {
            return FromSdf("star", 96, 96, (x, y) =>
            {
                float cx = 48, cy = 46;
                float ang = Mathf.Atan2(y - cy, x - cx);
                float r = new Vector2(x - cx, y - cy).magnitude;
                float k = Mathf.Cos(5f * (ang - Mathf.PI / 2f));
                float edge = Mathf.Lerp(20f, 44f, (k + 1f) / 2f);
                return r - edge;
            }, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Diamond icon for premium choices.</summary>
        public static Sprite Diamond() =>
            FromSdf("diamond", 80, 80, (x, y) => (Mathf.Abs(x - 40) / 30f + Mathf.Abs(y - 40) / 36f - 1f) * 26f, new Vector2(0.5f, 0.5f));

        /// <summary>Lock icon for locked choices.</summary>
        public static Sprite Lock()
        {
            return FromSdf("lock", 80, 96, (x, y) =>
            {
                float body = RoundedBox(x, y, 40, 30, 28, 24, 6);
                float shackle = Mathf.Max(Mathf.Abs(Circle(x, y, 40, 54, 17)) - 5f, 50f - y);
                return Mathf.Min(body, shackle);
            }, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Three horizontal bars (menu icon).</summary>
        public static Sprite MenuIcon() =>
            FromSdf("menu", 64, 64, (x, y) => Mathf.Min(RoundedBox(x, y, 32, 14, 24, 4, 4), Mathf.Min(RoundedBox(x, y, 32, 32, 24, 4, 4), RoundedBox(x, y, 32, 50, 24, 4, 4))), new Vector2(0.5f, 0.5f));

        public static Sprite Heart()
        {
            return FromSdf("heart", 96, 96, (x, y) =>
            {
                float px = (x - 48) / 40f, py = (y - 40) / 40f;
                px = Mathf.Abs(px);
                float a = px * px + py * py - 1f;
                float v = a * a * a - px * px * py * py * py;
                return v * 30f;
            }, new Vector2(0.5f, 0.5f));
        }

        // ---------- textures ----------

        /// <summary>Vertical gradient sprite.</summary>
        public static Sprite Gradient(string key, Color bottom, Color top, int w = 4, int h = 256, float ppu = 100f)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = key };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                var c = Color.Lerp(bottom, top, y / (h - 1f));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
            cache[key] = s;
            return s;
        }

        public static Sprite White()
        {
            if (cache.TryGetValue("white", out var s) && s != null) return s;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "white" };
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            cache["white"] = s;
            return s;
        }

        /// <summary>
        /// A stylised placeholder backdrop: sky gradient, soft shapes and a floor, tinted by the
        /// time of day found in the name (day, night, sunset, morning, evening).
        /// </summary>
        public static Sprite PlaceholderBackground(string name, int zones = 2)
        {
            string key = "bg_" + Util.Slug(name) + "_" + zones;
            if (cache.TryGetValue(key, out var s) && s != null) return s;

            string n = name.ToLowerInvariant();
            Color top, bottom, floor, accent;
            if (n.Contains("night")) { top = Hex("#141a3a"); bottom = Hex("#3a3170"); floor = Hex("#1c1830"); accent = Hex("#ffe6a8"); }
            else if (n.Contains("sunset") || n.Contains("evening") || n.Contains("dusk")) { top = Hex("#5b3f8c"); bottom = Hex("#ff9a76"); floor = Hex("#4a2d4f"); accent = Hex("#ffd38a"); }
            else if (n.Contains("morning") || n.Contains("dawn")) { top = Hex("#9ad0f5"); bottom = Hex("#ffe1c4"); floor = Hex("#c9a98b"); accent = Hex("#fff3d6"); }
            else { top = Hex("#7ec4ff"); bottom = Hex("#dff1ff"); floor = Hex("#b9a28a"); accent = Hex("#ffffff"); }
            bool interior = n.StartsWith("int");
            if (interior)
            {
                top = Color.Lerp(top, Hex("#f2d9c4"), 0.55f);
                bottom = Color.Lerp(bottom, Hex("#fff2e6"), 0.5f);
                floor = Color.Lerp(floor, Hex("#8a5a44"), 0.5f);
            }

            int h = 512, w = Mathf.RoundToInt(h * 9f / 16f * zones);
            var rnd = new System.Random(key.GetHashCode());
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = key };
            var px = new Color[w * h];
            int floorY = Mathf.RoundToInt(h * 0.28f);

            // Soft shapes: windows/buildings for interiors, hills/clouds for exteriors.
            var shapes = new List<(Rect r, Color c)>();
            for (int i = 0; i < 6 * zones; i++)
            {
                float sw = (float)(rnd.NextDouble() * 0.18 + 0.08) * w / zones;
                float sh = (float)(rnd.NextDouble() * 0.35 + 0.15) * h;
                float sx = (float)rnd.NextDouble() * w;
                float sy = interior ? floorY + (float)rnd.NextDouble() * h * 0.3f : floorY;
                var c = Color.Lerp(top, floor, (float)rnd.NextDouble() * 0.5f + 0.25f);
                c.a = interior ? 0.35f : 0.5f;
                shapes.Add((new Rect(sx, sy, sw, sh), c));
            }

            for (int y = 0; y < h; y++)
            {
                Color sky = Color.Lerp(bottom, top, Mathf.Pow(y / (float)h, 0.8f));
                for (int x = 0; x < w; x++)
                {
                    Color c = sky;
                    foreach (var (r, sc) in shapes)
                        if (r.Contains(new Vector2(x, y))) c = Color.Lerp(c, sc, sc.a);
                    if (y < floorY)
                    {
                        float k = y / (float)floorY;
                        c = Color.Lerp(floor * 0.8f, floor, k);
                        c.a = 1f;
                    }
                    // subtle vignette
                    float vx = (x % (w / zones)) / (float)(w / zones) - 0.5f;
                    c *= 1f - vx * vx * 0.25f;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            // a few light dots (stars, lamps)
            for (int i = 0; i < 40 * zones; i++)
            {
                int x = rnd.Next(w), y = rnd.Next(floorY + 20, h);
                if (!n.Contains("night") && rnd.NextDouble() > 0.2) continue;
                px[y * w + x] = accent;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0f), 100f);
            s.name = key;
            cache[key] = s;
            return s;
        }

        public static Color Hex(string hex, Color fallback = default)
        {
            if (hex == null) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        /// <summary>A pleasant colour derived from a name, so placeholders are distinct but stable.</summary>
        public static Color ColorFromName(string name, float s = 0.55f, float v = 0.9f)
        {
            int h = 17;
            foreach (char ch in name) h = h * 31 + ch;
            float hue = (h & 0xFFFF) / 65535f;
            return Color.HSVToRGB(hue, s, v);
        }
    }
}
