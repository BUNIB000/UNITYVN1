using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VNEngine
{
    public sealed class EpisodeInfo
    {
        public string File;
        public string Title;
        public int Number;
        public TextAsset Asset;
    }

    public sealed class SeriesInfo
    {
        public string Name;
        public string Description;
        public string Genre;
        public string Author;
        public string Cover;
        public readonly List<EpisodeInfo> Episodes = new List<EpisodeInfo>();
    }

    /// <summary>
    /// Every story file in a Resources/Stories folder, grouped into series.
    /// Files can be .txt, .story (via the engine's importer), or anything Unity reads as a TextAsset.
    /// </summary>
    public static class StoryCatalog
    {
        static List<SeriesInfo> series;
        /// <summary>Stories written at runtime or loaded from disk, by name.</summary>
        static readonly Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<SeriesInfo> Series
        {
            get
            {
                if (series == null) Build();
                return series;
            }
        }

        public static void Refresh() => series = null;

        /// <summary>Registers story text directly (for tools, mods, or downloaded content).</summary>
        public static void Register(string name, string source)
        {
            overrides[name] = source;
            series = null;
        }

        static void Build()
        {
            series = new List<SeriesInfo>();
            var byName = new Dictionary<string, SeriesInfo>(StringComparer.OrdinalIgnoreCase);
            var assets = Resources.LoadAll<TextAsset>("Stories");
            foreach (var asset in assets.OrderBy(a => a.name))
            {
                var meta = ReadMeta(asset.text);
                string seriesName = meta.TryGetValue("series", out var s) ? s : meta.TryGetValue("title", out var t) ? t : asset.name;
                if (!byName.TryGetValue(seriesName, out var info))
                {
                    info = new SeriesInfo { Name = seriesName };
                    byName[seriesName] = info;
                    series.Add(info);
                }
                if (meta.TryGetValue("description", out var d) && string.IsNullOrEmpty(info.Description)) info.Description = d;
                if (meta.TryGetValue("cover", out var c) && string.IsNullOrEmpty(info.Cover)) info.Cover = c;
                if (meta.TryGetValue("genre", out var g) && string.IsNullOrEmpty(info.Genre)) info.Genre = g;
                if (meta.TryGetValue("author", out var au) && string.IsNullOrEmpty(info.Author)) info.Author = au;
                int number = meta.TryGetValue("episode", out var e) && int.TryParse(new string(e.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : info.Episodes.Count + 1;
                info.Episodes.Add(new EpisodeInfo
                {
                    File = asset.name,
                    Title = meta.TryGetValue("title", out var et) ? et : asset.name,
                    Number = number,
                    Asset = asset
                });
            }
            foreach (var i in series) i.Episodes.Sort((a, b) => a.Number.CompareTo(b.Number));
        }

        /// <summary>Reads only the "key: value" header lines at the top of a story.</summary>
        public static Dictionary<string, string> ReadMeta(string source)
        {
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StringReader(source ?? ""))
            {
                string line;
                int scanned = 0;
                while ((line = reader.ReadLine()) != null && scanned++ < 60)
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#") || t.StartsWith("//") || t.StartsWith("character ")) continue;
                    int colon = t.IndexOf(':');
                    if (colon <= 0) break;
                    string key = t.Substring(0, colon).Trim().ToLowerInvariant();
                    if (key.Contains(" ") || key.ToUpperInvariant() == t.Substring(0, colon).Trim()) break;
                    meta[key] = t.Substring(colon + 1).Trim();
                }
            }
            return meta;
        }

        public static string LoadSource(string name)
        {
            if (overrides.TryGetValue(name, out var src)) return src;
            var asset = AssetLibrary.Load<TextAsset>("Stories", name);
            return asset != null ? asset.text : null;
        }

        public static SeriesInfo SeriesOf(string storyFile)
        {
            return Series.FirstOrDefault(s => s.Episodes.Any(e => string.Equals(e.File, storyFile, StringComparison.OrdinalIgnoreCase)));
        }

        public static StoryProgram Compile(string name)
        {
            var src = LoadSource(name);
            if (src == null) return null;
            return StoryParser.Parse(src, name);
        }
    }

    /// <summary>One autosave per series, as JSON in the player's data folder.</summary>
    public static class SaveSystem
    {
        static string Folder => Path.Combine(Application.persistentDataPath, "vn_saves");

        static string PathFor(string series) => Path.Combine(Folder, Util.Slug(series ?? "default") + ".json");

        public static void Save(SaveData data)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                data.savedAt = DateTime.Now.ToString("o");
                System.IO.File.WriteAllText(PathFor(data.series), JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VN Engine] Could not save: " + e.Message);
            }
        }

        public static SaveData Load(string series)
        {
            try
            {
                var p = PathFor(series);
                if (!System.IO.File.Exists(p)) return null;
                return JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(p));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VN Engine] Could not read save: " + e.Message);
                return null;
            }
        }

        public static void Delete(string series)
        {
            var p = PathFor(series);
            if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
        }
    }
}
