using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VNEngine
{
    /// <summary>
    /// Finds content by name inside any Resources folder, forgiving about case, spaces and
    /// file naming. The folders are:
    ///   Stories/  Backgrounds/  Characters/NAME/  Music/  Sounds/  Covers/  Minigames/GAME/SKIN/  Images/
    /// </summary>
    public static class AssetLibrary
    {
        static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();
        static readonly Dictionary<string, Sprite[]> folderCache = new Dictionary<string, Sprite[]>();
        static readonly HashSet<string> warned = new HashSet<string>();

        public static void ClearCache() { cache.Clear(); folderCache.Clear(); }

        public static IEnumerable<string> NameVariants(string name)
        {
            if (string.IsNullOrEmpty(name)) yield break;
            var seen = new HashSet<string>();
            string slug = Util.Slug(name);
            var list = new List<string> { name, name.Trim(), slug, slug.Replace("_", "-"), slug.Replace("_", " "), name.ToLowerInvariant(), name.ToUpperInvariant() };
            // Scene headings: "int_coffee_shop_day" -> "coffee_shop_day", "coffee_shop"
            string noPrefix = slug;
            foreach (var pre in new[] { "int_ext_", "ext_int_", "i_e_", "int_", "ext_" })
                if (noPrefix.StartsWith(pre)) { noPrefix = noPrefix.Substring(pre.Length); break; }
            list.Add(noPrefix);
            foreach (var suf in new[] { "_day", "_night", "_morning", "_evening", "_sunset", "_dusk", "_dawn", "_afternoon" })
                if (noPrefix.EndsWith(suf)) list.Add(noPrefix.Substring(0, noPrefix.Length - suf.Length));
            foreach (var v in list)
                if (!string.IsNullOrEmpty(v) && seen.Add(v)) yield return v;
        }

        public static T Load<T>(string folder, string name) where T : Object
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = typeof(T).Name + ":" + folder + "/" + name;
            if (cache.TryGetValue(key, out var hit)) return hit as T;
            T found = null;
            foreach (var v in NameVariants(name))
            {
                string path = string.IsNullOrEmpty(folder) ? v : folder + "/" + v;
                found = Resources.Load<T>(path);
                if (found != null) break;
            }
            cache[key] = found;
            return found;
        }

        /// <summary>Loads a sprite, accepting textures that weren't imported as sprites.</summary>
        public static Sprite LoadSprite(string folder, string name, Vector2? pivot = null)
        {
            var sprite = Load<Sprite>(folder, name);
            if (sprite != null) return sprite;
            var tex = Load<Texture2D>(folder, name);
            if (tex == null) return null;
            string key = "texsprite:" + folder + "/" + name;
            if (cache.TryGetValue(key, out var hit) && hit != null) return (Sprite)hit;
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot ?? new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            cache[key] = sprite;
            return sprite;
        }

        public static Sprite[] LoadAllSprites(string folder)
        {
            if (folderCache.TryGetValue(folder, out var hit)) return hit;
            var sprites = Resources.LoadAll<Sprite>(folder).ToList();
            var names = new HashSet<string>(sprites.Select(s => s.name));
            foreach (var tex in Resources.LoadAll<Texture2D>(folder))
            {
                if (names.Contains(tex.name)) continue;
                var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
                s.name = tex.name;
                sprites.Add(s);
            }
            var result = sprites.ToArray();
            folderCache[folder] = result;
            return result;
        }

        public static Sprite Background(string name) => LoadSprite("Backgrounds", name, new Vector2(0, 0));
        public static Sprite Image(string name) => LoadSprite("Images", name) ?? LoadSprite("Backgrounds", name) ?? LoadSprite("", name);
        public static Sprite Cover(string name) => LoadSprite("Covers", name);
        public static AudioClip Music(string name) => Load<AudioClip>("Music", name) ?? Load<AudioClip>("Audio/Music", name) ?? Load<AudioClip>("Sounds", name);
        public static AudioClip Sound(string name) => Load<AudioClip>("Sounds", name) ?? Load<AudioClip>("Audio/Sounds", name) ?? Load<AudioClip>("Music", name);

        public static void WarnOnce(string message)
        {
            if (warned.Add(message)) Debug.LogWarning("[VN Engine] " + message);
        }
    }
}
