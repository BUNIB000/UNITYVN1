using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VNEngine.Editor
{
    /// <summary>Lets Unity read .story files as text, and reports script errors with line numbers on import.</summary>
    [ScriptedImporter(1, new[] { "story", "vn" })]
    public sealed class StoryImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            var asset = new TextAsset(text);
            ctx.AddObjectToAsset("story", asset);
            ctx.SetMainObject(asset);

            var program = StoryParser.Parse(text, Path.GetFileNameWithoutExtension(ctx.assetPath));
            foreach (var e in program.Errors)
            {
                string msg = $"{Path.GetFileName(ctx.assetPath)} line {e.Line}: {e.Message}";
                if (e.IsWarning) Debug.LogWarning(msg);
                else Debug.LogError(msg);
            }
        }
    }

    /// <summary>Imports story art with sensible settings the first time it's added.</summary>
    public sealed class StoryArtPostprocessor : AssetPostprocessor
    {
        static readonly string[] Folders = { "/Backgrounds/", "/Characters/", "/Covers/", "/Images/", "/Minigames/" };

        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/") || !Folders.Any(p.Contains)) return;
            if (!assetImporter.importSettingsMissing) return; // only on first import; respect later changes
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (p.Contains("/Characters/"))
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                ti.maxTextureSize = 4096;
            }
            if (p.Contains("/Backgrounds/"))
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
                ti.maxTextureSize = 4096;
            }
            ti.SetTextureSettings(settings);
        }
    }

    [InitializeOnLoad]
    public static class VNEngineMenu
    {
        public const string ScenePath = "Assets/Scenes/Story.unity";
        const string StoriesFolder = "Assets/StoryContent/Resources/Stories";

        static VNEngineMenu()
        {
            EditorApplication.delayCall += FirstRunSetup;
        }

        static void FirstRunSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureTextMeshPro();
            if (!File.Exists(ScenePath) && !SessionState.GetBool("vn_scene_offered", false))
            {
                SessionState.SetBool("vn_scene_offered", true);
                CreateStoryScene(openAfter: IsSceneEmpty(SceneManager.GetActiveScene()));
                Debug.Log("[VN Engine] Created " + ScenePath + ". Open it and press Play.");
            }
        }

        static bool IsSceneEmpty(Scene scene)
        {
            if (!scene.IsValid()) return true;
            if (string.IsNullOrEmpty(scene.path)) return true;
            var roots = scene.GetRootGameObjects();
            return roots.All(r => r.GetComponent<Camera>() != null || r.GetComponent<Light>() != null || r.name.Contains("Global Volume") || r.name.Contains("Light"));
        }

        public static void EnsureTextMeshPro()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;
            if (Resources.Load("TMP Settings") != null) return;
            // TMP_PackageResourceImporter.ImportResources(importEssentials, importExamples, interactive)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("TMPro.TMP_PackageResourceImporter");
                var m = t?.GetMethod("ImportResources", new[] { typeof(bool), typeof(bool), typeof(bool) });
                if (m == null) continue;
                Debug.Log("[VN Engine] Importing TextMeshPro Essential Resources (fonts and shaders used by the story UI).");
                m.Invoke(null, new object[] { true, false, false });
                return;
            }
            Debug.LogWarning("[VN Engine] Please import TextMeshPro essentials: Window > TextMeshPro > Import TMP Essential Resources.");
        }

        [MenuItem("VN Engine/Create Story Scene", priority = 0)]
        public static void CreateStorySceneMenu() => CreateStoryScene(true);

        public static void CreateStoryScene(bool openAfter)
        {
            if (openAfter && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, openAfter ? NewSceneMode.Single : NewSceneMode.Additive);
            var go = new GameObject("Story Player");
            go.AddComponent<StoryPlayer>();
            SceneManager.MoveGameObjectToScene(go, scene);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!openAfter) EditorSceneManager.CloseScene(scene, true);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("VN Engine/New Story File", priority = 20)]
        public static void NewStory()
        {
            Directory.CreateDirectory(StoriesFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(StoriesFolder + "/my_story.story");
            File.WriteAllText(path, Template);
            AssetDatabase.ImportAsset(path);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        const string Template =
@"title: My Story
series: My Story
episode: 1
description: A short description shown in the story library.

character PLAYER name=""{player}""
character MAYA name=""Maya"" color=#ff6f91

INT. COFFEE SHOP - DAY
@music cafe
@MAYA enters from right to center and is talk_happy

MAYA (talk_happy)
Hi! What's your name?

input player ""What's your name?"" default=""Alex""

MAYA (laugh)
Nice to meet you, {player}!

choice
    ""Nice to meet you too.""
        set friendship + 1
        MAYA (happy): Aww!
    ""Do I know you?""
        MAYA (think): Hmm... maybe not yet.

@MAYA exits left
The end of the beginning.
";

        [MenuItem("VN Engine/Validate All Stories", priority = 40)]
        public static void ValidateAll()
        {
            var report = new StringBuilder();
            int errors = 0, warnings = 0;
            foreach (var asset in Resources.LoadAll<TextAsset>("Stories"))
            {
                var program = StoryParser.Parse(asset.text, asset.name);
                var problems = new List<string>();
                foreach (var e in program.Errors)
                {
                    problems.Add((e.IsWarning ? "  warning, line " : "  ERROR, line ") + e.Line + ": " + e.Message);
                    if (e.IsWarning) warnings++; else errors++;
                }
                foreach (var ins in program.Instructions)
                {
                    switch (ins)
                    {
                        case BackgroundInstruction b when AssetLibrary.Background(b.Name) == null:
                            problems.Add($"  missing background, line {b.Line}: '{b.Name}' (a placeholder will be drawn)"); warnings++; break;
                        case CommandInstruction c when c.Verb == "music" && c.Arg(0) != null && c.Arg(0) != "stop" && AssetLibrary.Music(c.Arg(0)) == null:
                            problems.Add($"  missing music, line {c.Line}: '{c.Arg(0)}'"); warnings++; break;
                        case CommandInstruction c when (c.Verb == "sound" || c.Verb == "ambience") && c.Arg(0) != null && c.Arg(0) != "stop" && AssetLibrary.Sound(c.Arg(0)) == null && !IsBuiltInSound(c.Arg(0)):
                            problems.Add($"  missing sound, line {c.Line}: '{c.Arg(0)}'"); warnings++; break;
                        case MinigameInstruction m when MinigameRegistry.Find(m.Game) == null:
                            problems.Add($"  ERROR, line {m.Line}: unknown minigame '{m.Game}'"); errors++; break;
                    }
                }
                foreach (var id in program.MentionedCharacters())
                {
                    string folder = "Characters/" + SpriteFolderVisual.FindFolder(id);
                    if (Resources.LoadAll<Sprite>(folder).Length == 0 && Resources.LoadAll<GameObject>(folder).Length == 0 && Resources.LoadAll<Texture2D>(folder).Length == 0)
                        problems.Add($"  no art for {id} yet (Resources/{folder}) — a placeholder character will be drawn");
                }
                report.AppendLine(asset.name + (problems.Count == 0 ? ": OK" : ":"));
                foreach (var p in problems) report.AppendLine(p);
            }
            AssetLibrary.ClearCache();
            Debug.Log("[VN Engine] Story check\n" + report);
            EditorUtility.DisplayDialog("VN Engine: Story check",
                errors == 0 ? $"No errors. {warnings} warning(s). Details are in the Console." : $"{errors} error(s) and {warnings} warning(s). Details are in the Console.", "OK");
        }

        static bool IsBuiltInSound(string n) => new[] { "click", "pop", "blip", "success", "fail", "whoosh", "win", "lose" }.Contains(n.ToLowerInvariant());

        [MenuItem("Assets/VN Engine/Play This Story", priority = 0)]
        public static void PlaySelected()
        {
            var asset = Selection.activeObject as TextAsset;
            if (asset == null) return;
            StoryPlayer.PendingEditorStory = asset.name;
            StoryPlayer.PendingEditorLabel = null;
            PlayStoryScene();
        }

        [MenuItem("Assets/VN Engine/Play This Story", true)]
        static bool PlaySelectedValid() => Selection.activeObject is TextAsset && AssetDatabase.GetAssetPath(Selection.activeObject).Contains("/Resources/Stories/");

        [MenuItem("VN Engine/Play", priority = 1)]
        public static void PlayStoryScene()
        {
            if (!File.Exists(ScenePath)) CreateStoryScene(false);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("VN Engine/Delete Saves", priority = 60)]
        public static void DeleteSaves()
        {
            string folder = Path.Combine(Application.persistentDataPath, "vn_saves");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Debug.Log("[VN Engine] Saves deleted.");
        }

        [MenuItem("VN Engine/Create Theme", priority = 61)]
        public static void CreateTheme()
        {
            const string folder = "Assets/StoryContent/Resources";
            Directory.CreateDirectory(folder);
            string path = folder + "/VNTheme.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VNTheme>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<VNTheme>();
                AssetDatabase.CreateAsset(existing, path);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
        }

        [MenuItem("VN Engine/Open Writing Guide", priority = 80)]
        public static void OpenGuide()
        {
            var guide = Directory.GetFiles(".", "WRITING_STORIES.md", SearchOption.AllDirectories).FirstOrDefault();
            if (guide != null) EditorUtility.OpenWithDefaultApp(guide);
            else Debug.Log("[VN Engine] The writing guide is Docs/WRITING_STORIES.md in the engine repository.");
        }
    }

    [CustomEditor(typeof(StoryPlayer))]
    public sealed class StoryPlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stories in Resources/Stories", EditorStyles.boldLabel);
            var stories = Resources.LoadAll<TextAsset>("Stories");
            if (stories.Length == 0) EditorGUILayout.HelpBox("No stories yet. Use VN Engine > New Story File.", MessageType.Info);
            foreach (var s in stories.OrderBy(s => s.name))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(s.name);
                    if (GUILayout.Button("Start with this", GUILayout.Width(120))) { ((StoryPlayer)target).startStory = s.name; EditorUtility.SetDirty(target); }
                    if (GUILayout.Button("Play", GUILayout.Width(60)))
                    {
                        StoryPlayer.PendingEditorStory = s.name;
                        EditorApplication.isPlaying = true;
                    }
                }
            }
        }
    }
}
