using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace VNEngine
{
    /// <summary>
    /// The whole engine in one component. Put it on an empty GameObject in an empty scene and press Play.
    /// It builds the camera, stage, UI and audio itself, shows the story library, and plays stories.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryPlayer : MonoBehaviour
    {
        [Tooltip("Story file to play straight away (name of a file in Resources/Stories). Leave empty to show the story library.")]
        public string startStory;
        [Tooltip("Label to start from, for testing a scene in the middle of a story.")]
        public string startLabel;
        [Tooltip("Continue from the autosave when starting a story directly.")]
        public bool continueFromSave = true;
        [Tooltip("Show the story library when a story ends, and at startup if no start story is set.")]
        public bool useLibrary = true;
        [Tooltip("Optional. Without one, a VNTheme in Resources is used, or built-in defaults.")]
        public VNTheme theme;

        public static StoryPlayer Instance { get; private set; }

        public StageView Stage { get; private set; }
        public StoryRunner Runner { get; private set; }
        public VariableStore Variables { get; } = new VariableStore();

        AudioDirector audioDirector;
        UIRoot ui;
        DialogueView dialogue;
        ChoiceView choices;
        Overlays overlays;
        GameMenu menu;
        LibraryView library;
        RectTransform minigameLayer;

        StoryProgram program;
        string storyFile, seriesName;
        readonly List<HistoryEntry> history = new List<HistoryEntry>();
        readonly Dictionary<string, CharacterDefinition> cast = new Dictionary<string, CharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        Coroutine storyRoutine;
        bool advanceRequested, auto, skip, skipHeld, inBlockingUI;
        bool firstSceneShown;

        /// <summary>Raised when a story reaches its end.</summary>
        public event Action<string> StoryFinished;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (theme != null) VNTheme.Use(theme);
            theme = VNTheme.Load();
            Application.targetFrameRate = Mathf.Max(Application.targetFrameRate, 60);
            EventSystemSetup.Ensure();

            // Our cameras take over; keep the scene's audio listener or add one.
            foreach (var cam in FindAll<Camera>()) if (cam.transform.root != transform) cam.enabled = false;
            audioDirector = gameObject.AddComponent<AudioDirector>();

            Stage = new GameObject("Stage").AddComponent<StageView>();
            Stage.transform.SetParent(transform, false);
            Stage.Init(theme, DefinitionFor);
            if (FindAll<AudioListener>().All(l => !l.isActiveAndEnabled)) Stage.Camera.gameObject.AddComponent<AudioListener>();

            ui = UIRoot.Create(transform, theme);
            Stage.ViewportChanged += ui.UpdateFrame;
            ui.UpdateFrame(Stage.Viewport);

            // Layer order, bottom to top.
            var tap = TapArea.Create(ui.Layer("Tap"));
            tap.Tapped += OnTap;
            foreach (var l in new[] { "Images", "Location", "Dialogue", "Choices", "Minigames", "Input", "HUD", "Toasts", "Menu", "Library", "Fader", "Errors" }) ui.Layer(l);
            minigameLayer = ui.Layer("Minigames");

            dialogue = gameObject.AddComponent<DialogueView>();
            dialogue.Init(ui, theme, () => Stage.Camera);
            dialogue.Letter += OnLetter;
            choices = gameObject.AddComponent<ChoiceView>();
            choices.Init(ui, theme);
            overlays = gameObject.AddComponent<Overlays>();
            overlays.Init(ui, theme);
            menu = gameObject.AddComponent<GameMenu>();
            menu.Init(ui, theme);
            menu.GetAuto = () => auto;
            menu.GetSkip = () => skip;
            menu.ToggleAuto = () => { auto = !auto; if (auto) skip = false; };
            menu.ToggleSkip = () => { skip = !skip; if (skip) auto = false; };
            menu.GetHistory = () => history;
            menu.ExitToLibrary = () => { Autosave(); StopStory(); ShowLibrary(); };
            menu.RestartEpisode = () => { StopStory(); PlayStory(storyFile, false); };
            library = gameObject.AddComponent<LibraryView>();
            library.Init(ui, theme);
            library.Play = (s, ep, cont) => { library.Hide(); PlayStory(ep.File, cont); };
            menu.SetHudVisible(false);
        }

        static T[] FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return FindObjectsOfType<T>();
#endif
        }

        void Start()
        {
            if (UIKit.BodyFont == null)
            {
                Debug.LogError("[VN Engine] TextMeshPro resources are missing. In Unity choose Window > TextMeshPro > Import TMP Essential Resources, then press Play again.");
            }
            string pending = PendingEditorStory;
            if (!string.IsNullOrEmpty(pending)) { PendingEditorStory = null; PlayStory(pending, false, PendingEditorLabel); return; }
            if (!string.IsNullOrEmpty(startStory)) PlayStory(startStory, continueFromSave, startLabel);
            else ShowLibrary();
        }

        /// <summary>Set by the editor's "Play This Story" command.</summary>
        public static string PendingEditorStory
        {
            get => PlayerPrefs.GetString("vn_pending_story", "");
            set { if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey("vn_pending_story"); else PlayerPrefs.SetString("vn_pending_story", value); }
        }

        public static string PendingEditorLabel
        {
            get => PlayerPrefs.GetString("vn_pending_label", "");
            set { if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey("vn_pending_label"); else PlayerPrefs.SetString("vn_pending_label", value); }
        }

        void Update()
        {
            if (menu.IsOpen)
            {
                if (VNInput.Back()) menu.Close();
                return;
            }
            if (storyRoutine != null)
            {
                if (VNInput.Advance()) OnTap();
                if (VNInput.Back()) menu.Open();
                skipHeld = VNInput.SkipHeld();
                bool fast = (skip || skipHeld) && !inBlockingUI;
                Time.timeScale = fast ? 6f : 1f;
                dialogue.Instant = fast;
                menu.RefreshHud();
            }
        }

        void OnTap()
        {
            if (menu.IsOpen || storyRoutine == null || inBlockingUI) return;
            if (auto) { auto = false; menu.RefreshHud(); }
            if (dialogue.IsTyping) dialogue.CompleteTyping();
            else advanceRequested = true;
        }

        StageCharacter currentSpeaker;

        void OnLetter(char c) => currentSpeaker?.OnLetter(c);

        CharacterDefinition DefinitionFor(string id)
        {
            if (program != null && program.Characters.TryGetValue(id, out var d)) return d;
            return cast.TryGetValue(id, out var c) ? c : null;
        }

        // ================== library & lifecycle ==================

        public void ShowLibrary()
        {
            if (!useLibrary) return;
            menu.SetHudVisible(false);
            overlays.SetFade(0, Color.black);
            Stage.HideAllInstant();
            dialogue.HideInstant();
            overlays.ClearImages();
            AudioDirector.Instance?.StopMusic(0.6f);
            AudioDirector.Instance?.PlayAmbience(null);
            Stage.SetWeather(null);
            StoryCatalog.Refresh();
            library.ShowSeriesList();
        }

        public void StopStory()
        {
            if (storyRoutine != null) StopCoroutine(storyRoutine);
            storyRoutine = null;
            StopAllCoroutinesOnViews();
            Time.timeScale = 1f;
            auto = skip = false;
        }

        void StopAllCoroutinesOnViews()
        {
            dialogue.StopAllCoroutines();
            dialogue.HideInstant();
            choices.StopAllCoroutines();
            foreach (Transform c in minigameLayer) Destroy(c.gameObject);
            foreach (Transform c in ui.Layer("Choices")) if (c.name != "Dim") Destroy(c.gameObject);
            ui.Layer("Choices").gameObject.SetActive(false);
            inBlockingUI = false;
        }

        /// <summary>Starts a story file. With resume, picks up from its series' autosave.</summary>
        public void PlayStory(string file, bool resume, string label = null)
        {
            StopStory();
            library.Hide();
            var prog = StoryCatalog.Compile(file);
            if (prog == null)
            {
                overlays.ShowMessage("Story not found", $"There's no story called '{file}'.\n\nPut it in a folder named Resources/Stories (as a .txt or .story file).");
                return;
            }
            var series = StoryCatalog.SeriesOf(file);
            seriesName = series != null ? series.Name : prog.Meta.TryGetValue("series", out var sn) ? sn : prog.Title;
            SaveData save = resume ? SaveSystem.Load(seriesName) : null;
            if (save != null && save.story != file) save = null;

            if (!LoadProgram(prog, file))
            {
                if (useLibrary) library.ShowSeriesList();
                return;
            }
            Runner = new StoryRunner(program, Variables);
            history.Clear();
            Stage.HideAllInstant();
            overlays.ClearImages();
            firstSceneShown = false;

            if (save != null)
            {
                save.ReadVariables(Variables);
                Runner.Restore(save.pc, save.callStack);
                Stage.Restore(save.stage);
                if (!string.IsNullOrEmpty(save.stage.music)) AudioDirector.Instance?.PlayMusic(save.stage.music, 1f);
                if (!string.IsNullOrEmpty(save.stage.ambience)) AudioDirector.Instance?.PlayAmbience(save.stage.ambience);
                history.AddRange(save.history);
                firstSceneShown = true;
            }
            else
            {
                if (IsFirstEpisode(file)) Variables.Clear();
                else
                {
                    // Carry choices over from the previous episode's save.
                    var prev = SaveSystem.Load(seriesName);
                    if (prev != null) prev.ReadVariables(Variables);
                }
                if (!string.IsNullOrEmpty(label)) Runner.Goto(label);
            }
            overlays.SetFade(1, Color.black);
            menu.SetHudVisible(true);
            storyRoutine = StartCoroutine(RunLoop(save != null));
        }

        bool IsFirstEpisode(string file)
        {
            var s = StoryCatalog.SeriesOf(file);
            return s == null || s.Episodes.Count == 0 || string.Equals(s.Episodes[0].File, file, StringComparison.OrdinalIgnoreCase);
        }

        bool LoadProgram(StoryProgram prog, string file)
        {
            foreach (var e in prog.Errors) Debug.Log($"[VN Engine] {file}: {e}");
            if (prog.HasErrors)
            {
                overlays.ShowErrors($"Problems in '{file}'", prog.Errors);
                return false;
            }
            program = prog;
            storyFile = file;
            foreach (var kv in prog.Characters) cast[kv.Key] = kv.Value;
            return true;
        }

        // ================== the main loop ==================

        IEnumerator RunLoop(bool resumed)
        {
            if (resumed) yield return overlays.FadeIn(0.6f);
            while (true)
            {
                Instruction ins;
                try
                {
                    ins = Runner.Next();
                }
                catch (StoryException e)
                {
                    overlays.ShowErrors("Story stopped", new[] { new StoryError { Line = e.Line, Message = e.Message } });
                    storyRoutine = null;
                    yield break;
                }

                if (ins is EndInstruction)
                {
                    yield return EndOfEpisode();
                    yield break;
                }
                if (ins is NextStoryInstruction next)
                {
                    var prog = StoryCatalog.Compile(next.Story);
                    if (prog == null || !LoadProgram(prog, next.Story))
                    {
                        if (prog == null) overlays.ShowMessage("Story not found", $"'next {next.Story}' points to a story that doesn't exist.");
                        storyRoutine = null;
                        yield break;
                    }
                    Runner.Load(program, next.Label);
                    Autosave();
                    continue;
                }

                // The opening fade: reveal the stage on the first real content.
                if (!firstSceneShown && !(ins is BackgroundInstruction) && !(ins is CommandInstruction ci && (ci.Verb == "music" || ci.Verb == "ambience" || ci.Verb == "sound" || ci.Verb == "weather" || ci.Verb == "tint"))
                    && !(ins is CharacterInstruction))
                {
                    firstSceneShown = true;
                    yield return overlays.FadeIn(0.6f);
                }

                if (ins.Parallel) StartCoroutine(Safe(Execute(ins), ins.Line));
                else yield return Safe(Execute(ins), ins.Line);
            }
        }

        /// <summary>Runs a nested coroutine, reporting errors with the script line instead of stopping the story.</summary>
        IEnumerator Safe(IEnumerator routine, int line)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                object current;
                try
                {
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    current = top.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    overlays.Notify($"Line {line}: {e.Message}");
                    yield break;
                }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
        }

        IEnumerator Execute(Instruction ins)
        {
            switch (ins)
            {
                case SayInstruction s: return Say(s);
                case CharacterInstruction c: return CharacterCommand(c);
                case BackgroundInstruction b: return Background(b);
                case CommandInstruction c: return Command(c);
                case ChoiceInstruction c: return Choice(c);
                case MinigameInstruction m: return RunMinigame(m);
                case InputInstruction i: return AskInput(i);
            }
            return Nothing();
        }

        static IEnumerator Nothing() { yield break; }

        string Interpolate(string text) => Variables.Interpolate(text, name =>
        {
            // {MAYA} is a character's display name; anything else is a variable.
            if (DefinitionFor(name) != null) return DisplayNameOf(name);
            if (name.Length > 1 && name == name.ToUpperInvariant() && name.Any(char.IsLetter) && !Variables.Has(name)) return DisplayNameOf(name);
            return null;
        });

        string DisplayNameOf(string id)
        {
            var def = DefinitionFor(id);
            string n = def?.DisplayName;
            if (string.IsNullOrEmpty(n))
            {
                if (id == "PLAYER" || id == "YOU" || id == "ME")
                {
                    foreach (var v in new[] { "player", "player_name", "name" })
                        if (Variables.Has(v)) return Variables.GetString(v);
                }
                return char.ToUpper(id[0]) + id.Substring(1).ToLowerInvariant();
            }
            return Variables.Interpolate(n);
        }

        Instruction PeekNext()
        {
            int pc = Runner.Pc;
            var list = program.Instructions;
            for (int guard = 0; guard < 50 && pc >= 0 && pc < list.Count; guard++)
            {
                var i = list[pc];
                if (i is LabelInstruction || i is SetInstruction) { pc++; continue; }
                if (i is JumpInstruction j) { pc = j.Target; continue; }
                return i;
            }
            return null;
        }

        // ================== dialogue ==================

        IEnumerator Say(SayInstruction s)
        {
            string text = Interpolate(s.Text);
            StageCharacter ch = s.Speaker != null ? Stage.Character(s.Speaker) : null;
            bool onStage = ch != null && ch.OnStage;
            if (onStage && s.Animation != null) ch.PlayAnimation(s.Animation);
            currentSpeaker = onStage ? ch : null;
            Stage.SetSpeaker(currentSpeaker);

            AudioClip voice = null;
            var def = s.Speaker != null ? DefinitionFor(s.Speaker) : null;
            if (def != null && def.Options.TryGetValue("voice", out var vname)) voice = AssetLibrary.Sound(vname);

            var line = new DialogueLine
            {
                Name = s.Speaker != null ? DisplayNameOf(s.Speaker) : null,
                NameColor = ch != null ? ch.NameColor : theme.defaultNameColor,
                Text = text,
                Style = s.Style,
                Anchor = onStage ? ch.HeadWorldPosition : (Vector3?)null,
                Voice = voice
            };
            history.Add(new HistoryEntry { speaker = line.Name, text = text });
            if (history.Count > 200) history.RemoveAt(0);
            Autosave();

            advanceRequested = false;
            yield return dialogue.Show(line);
            currentSpeaker?.SetTalking(false);

            float waited = 0;
            float autoDelay = theme.autoAdvanceDelay + text.Length * 0.025f;
            while (!advanceRequested)
            {
                waited += Time.unscaledDeltaTime;
                if (auto && waited >= autoDelay) break;
                if ((skip || skipHeld) && waited >= 0.04f) break;
                yield return null;
            }
            advanceRequested = false;
            AudioDirector.Instance?.Click();

            var next = PeekNext();
            if (!(next is SayInstruction))
            {
                yield return dialogue.Hide(0.12f);
                Stage.SetSpeaker(null);
            }
            currentSpeaker = null;
        }

        // ================== characters ==================

        IEnumerator CharacterCommand(CharacterInstruction ci)
        {
            var ch = Stage.Character(ci.Character);
            foreach (var a in ci.Actions)
            {
                if (a.Layer.HasValue) ch.SetLayer(a.Layer.Value);
                yield return CharacterAction(ch, a);
            }
        }

        float WalkDuration(StageCharacter ch, float fromX, float toX, CharacterAction a, bool run)
        {
            if (a.Duration.HasValue) return a.Duration.Value;
            float speed = run ? theme.runSpeed : theme.walkSpeed;
            return Mathf.Clamp(Mathf.Abs(toX - fromX) / speed, 0.45f, 2.6f);
        }

        IEnumerator CharacterAction(StageCharacter ch, CharacterAction a)
        {
            switch (a.Kind)
            {
                case CharacterActionKind.Enter:
                {
                    if (ch.OnStage && ch.X > -50 && ch.X < theme.stageUnitsWide + 50)
                    {
                        // Already here: treat as a walk.
                        Stage.Resolve(a.Position, ch, out var x0, out var y0, out var s0, out var z0);
                        yield return ch.MoveTo(x0, y0, s0, z0, WalkDuration(ch, ch.X, x0, a, false), "walk");
                        break;
                    }
                    Stage.Resolve(a.Position ?? new StagePosition { Named = "center" }, ch, out var x, out var y, out var s, out var z);
                    if (a.Position == null) { x = theme.centerX; y = 0; s = 1; }
                    string side = (a.Value ?? "left").ToLowerInvariant();
                    ch.BringToFront(Stage.NextOrder());
                    switch (side)
                    {
                        case "fade":
                        case "nowhere":
                            ch.PlaceAt(x, y, s, z);
                            ch.Show();
                            ch.SetAlpha(0);
                            yield return Appear(ch, a.Duration ?? 0.45f);
                            break;
                        case "top":
                        case "bottom":
                        {
                            float off = side == "top" ? 700 : -700;
                            ch.PlaceAt(x, y + off, s, z);
                            ch.Show();
                            ch.SetAlpha(1);
                            yield return ch.MoveTo(x, y, s, z, a.Duration ?? theme.enterDuration, "slide");
                            break;
                        }
                        default:
                        {
                            float startX = Stage.NamedX(side == "right" ? "offscreen_right" : "offscreen_left");
                            ch.PlaceAt(startX, y, s, z);
                            ch.Face(side != "right", true);
                            ch.Show();
                            ch.SetAlpha(1);
                            yield return ch.MoveTo(x, y, s, z, WalkDuration(ch, startX, x, a, false), "walk");
                            ch.Face(x <= theme.centerX + 1, false);
                            break;
                        }
                    }
                    break;
                }
                case CharacterActionKind.Exit:
                {
                    if (!ch.OnStage) break;
                    string side = (a.Value ?? (ch.X < theme.centerX ? "left" : "right")).ToLowerInvariant();
                    if (side == "fade")
                    {
                        yield return Disappear(ch, a.Duration ?? 0.4f);
                        break;
                    }
                    if (side == "top" || side == "bottom")
                    {
                        yield return ch.MoveTo(ch.X, ch.Y + (side == "top" ? 700 : -700), ch.SpotScale, ch.Zone, a.Duration ?? theme.enterDuration, "slide");
                    }
                    else
                    {
                        float target = Stage.NamedX(side == "right" ? "offscreen_right" : "offscreen_left");
                        yield return ch.MoveTo(target, ch.Y, ch.SpotScale, ch.Zone, WalkDuration(ch, ch.X, target, a, false), "walk");
                    }
                    ch.Hide();
                    break;
                }
                case CharacterActionKind.Walk:
                case CharacterActionKind.Run:
                case CharacterActionKind.Move:
                {
                    bool run = a.Kind == CharacterActionKind.Run;
                    Stage.Resolve(a.Position, ch, out var x, out var y, out var s, out var z);
                    if (!ch.OnStage)
                    {
                        // Walking in from nowhere: start offscreen on the near side.
                        float startX = Stage.NamedX(x < theme.centerX ? "offscreen_left" : "offscreen_right");
                        ch.PlaceAt(startX, y, s, z);
                        ch.Show();
                        ch.SetAlpha(1);
                        ch.BringToFront(Stage.NextOrder());
                    }
                    string mode = a.Kind == CharacterActionKind.Move ? "slide" : run ? "run" : "walk";
                    float d = a.Kind == CharacterActionKind.Move ? (a.Duration ?? theme.defaultMoveDuration) : WalkDuration(ch, ch.X, x, a, run);
                    // Moving to another zone while the camera watches: walk, then let the author pan.
                    yield return ch.MoveTo(x, y, s, z, d, mode);
                    break;
                }
                case CharacterActionKind.Spot:
                {
                    Stage.Resolve(a.Position, ch, out var x, out var y, out var s, out var z);
                    ch.PlaceAt(x, y, s, z);
                    if (!ch.OnStage)
                    {
                        ch.Show();
                        ch.SetAlpha(1);
                        ch.BringToFront(Stage.NextOrder());
                    }
                    break;
                }
                case CharacterActionKind.Animate:
                {
                    ch.PlayAnimation(a.Value);
                    Emotions.Split(a.Value, out var g, out _, out var loop);
                    if (!loop && g != null && g != "idle" && g != "talk" && ProceduralMotion.TryGet(g, out var dur, out _))
                        yield return Tween.Wait(Mathf.Min(dur, 0.6f));
                    break;
                }
                case CharacterActionKind.Look:
                    ch.SetEmotion(a.Value);
                    break;
                case CharacterActionKind.Face:
                {
                    string v = a.Value.ToLowerInvariant();
                    if (v == "left") ch.Face(false);
                    else if (v == "right") ch.Face(true);
                    else if (v == "away" || v == "around") ch.Face(!ch.FacingRight);
                    else
                    {
                        var other = Stage.Character(a.Value, false);
                        if (other != null && other.OnStage) ch.Face(other.X > ch.X);
                    }
                    yield return Tween.Wait(0.18f);
                    break;
                }
                case CharacterActionKind.Wear:
                    ch.Wear(a.Slot, a.Value);
                    break;
                case CharacterActionKind.Appear:
                {
                    Stage.Resolve(a.Position, ch, out var x, out var y, out var s, out var z);
                    ch.PlaceAt(x, y, s, z);
                    ch.BringToFront(Stage.NextOrder());
                    ch.Show();
                    ch.SetAlpha(0);
                    yield return Appear(ch, a.Duration ?? 0.4f);
                    break;
                }
                case CharacterActionKind.Disappear:
                    if (ch.OnStage) yield return Disappear(ch, a.Duration ?? 0.35f);
                    break;
                case CharacterActionKind.Layer:
                    if (a.Layer.HasValue) ch.SetLayer(a.Layer.Value);
                    break;
            }
        }

        IEnumerator Appear(StageCharacter ch, float d)
        {
            float baseY = ch.Y;
            yield return Tween.Run(d, Ease.OutCubic, k =>
            {
                ch.SetAlpha(k);
                ch.transform.localPosition = Stage.StageToWorld(ch.X, baseY - 12 * (1 - k), ch.Zone);
            });
            ch.PlaceAt(ch.X, baseY, ch.SpotScale, ch.Zone);
        }

        IEnumerator Disappear(StageCharacter ch, float d)
        {
            yield return Tween.Run(d, Ease.InQuad, k => ch.SetAlpha(1 - k));
            ch.Hide();
        }

        // ================== backgrounds ==================

        static bool IsSceneHeading(string name)
        {
            var n = name.TrimStart();
            return n.StartsWith("INT", StringComparison.Ordinal) || n.StartsWith("EXT", StringComparison.Ordinal) || n.StartsWith("I/E", StringComparison.Ordinal);
        }

        IEnumerator Background(BackgroundInstruction b)
        {
            bool newScene = IsSceneHeading(b.Name);
            string transition = (b.Transition ?? theme.defaultTransition).ToLowerInvariant();
            float d = b.Duration ?? theme.transitionDuration;
            bool missingArt = AssetLibrary.Background(b.Name) == null;

            if (newScene)
            {
                yield return dialogue.Hide(0.1f);
                overlays.ClearImages();
            }

            bool throughColor = transition.StartsWith("fade") || transition == "black" || transition == "white";
            if (!firstSceneShown || overlays.IsFadedOut)
            {
                if (newScene) Stage.HideAllInstant();
                yield return Stage.SetBackground(b.Name, "cut", 0, b.Zone);
                Stage.ResetCamera();
                firstSceneShown = true;
                yield return overlays.FadeIn(Mathf.Max(0.5f, d * 0.75f));
            }
            else if (throughColor)
            {
                var color = transition.Contains("white") ? Color.white : Color.black;
                yield return overlays.FadeOut(color, d / 2f);
                if (newScene) Stage.HideAllInstant();
                yield return Stage.SetBackground(b.Name, "cut", 0, b.Zone);
                Stage.ResetCamera();
                yield return Tween.Wait(0.1f);
                yield return overlays.FadeIn(d / 2f);
            }
            else
            {
                if (newScene) StartCoroutine(Stage.ClearCharacters(Mathf.Min(0.3f, d)));
                Stage.ResetCamera();
                yield return Stage.SetBackground(b.Name, transition, d, b.Zone);
                if (newScene) Stage.HideAllInstant();
            }
            if (newScene && missingArt) StartCoroutine(overlays.LocationCard(b.Name));
        }

        // ================== commands ==================

        IEnumerator Command(CommandInstruction c)
        {
            float time = c.OptFloat("time", -1);
            switch (c.Verb)
            {
                case "wait":
                    yield return Tween.Wait(time >= 0 ? time : c.ArgFloat(0, 1f));
                    break;

                case "zoom":
                case "camera":
                    yield return Zoom(c, time);
                    break;

                case "pan":
                {
                    float t = time >= 0 ? time : 1.2f;
                    var args = c.Args.Select(x => x.ToLowerInvariant()).Where(x => x != "to" && x != "the" && x != "camera").ToList();
                    if (args.Count >= 2 && args[0] == "zone" && int.TryParse(args[1], out var zone)) yield return Stage.PanToZone(zone, t);
                    else if (args.Count >= 1 && int.TryParse(args[0], out var z2)) yield return Stage.PanToZone(z2, t);
                    else if (args.Count >= 1 && (args[0] == "left" || args[0] == "right"))
                    {
                        float amount = args.Count >= 2 && Util.TryFloat(args[1], out var am) ? am : theme.stageUnitsWide;
                        yield return Stage.PanBy(args[0] == "left" ? -amount : amount, t);
                    }
                    else if (args.Count >= 1)
                    {
                        var ch = Stage.Character(c.Args.Last(), false);
                        if (ch != null && ch.OnStage) yield return Stage.PanToZone(ch.Zone, t);
                    }
                    break;
                }

                case "shake":
                {
                    float strength = 1f;
                    foreach (var a in c.Args) if (Util.TryFloat(a, out var f)) { strength = f; break; }
                    yield return Stage.Shake(strength, time >= 0 ? time : 0.5f);
                    break;
                }

                case "flash":
                {
                    var col = ProceduralArt.Hex(c.Arg(0), Color.white);
                    if (c.Arg(0) == "red") col = new Color(1f, 0.2f, 0.2f);
                    else if (c.Arg(0) == "black") col = Color.black;
                    yield return overlays.Flash(col, time >= 0 ? time : 0.45f);
                    break;
                }

                case "fade":
                {
                    var args = c.Args.Select(x => x.ToLowerInvariant()).ToList();
                    bool inward = args.Contains("in");
                    var col = args.Contains("white") ? Color.white : Color.black;
                    if (inward) yield return overlays.FadeIn(time >= 0 ? time : 0.8f);
                    else yield return overlays.FadeOut(col, time >= 0 ? time : 0.8f);
                    break;
                }

                case "transition":
                {
                    float t = time >= 0 ? time : 0.8f;
                    yield return overlays.FadeOut(Color.black, t / 2);
                    yield return overlays.FadeIn(t / 2);
                    break;
                }

                case "music":
                {
                    string name = c.Arg(0);
                    if (name == null || name == "stop" || name == "off" || name == "none") AudioDirector.Instance?.StopMusic(c.OptFloat("fade", time >= 0 ? time : 1.2f));
                    else AudioDirector.Instance?.PlayMusic(name, c.OptFloat("fade", time >= 0 ? time : 1.2f), c.OptFloat("volume", 1f), c.Opt("loop", "true") != "false");
                    break;
                }

                case "sound":
                    AudioDirector.Instance?.PlaySound(c.Arg(0), c.OptFloat("volume", 1f));
                    if (c.Opt("wait") != null) yield return Tween.Wait(c.OptFloat("wait", 0.5f));
                    break;

                case "voice":
                    AudioDirector.Instance?.PlaySound(c.Arg(0), c.OptFloat("volume", 1f));
                    break;

                case "ambience":
                    AudioDirector.Instance?.PlayAmbience(c.Arg(0), c.OptFloat("fade", 1.5f), c.OptFloat("volume", 0.7f));
                    break;

                case "stop":
                {
                    string what = c.Arg(0, "music").ToLowerInvariant();
                    if (what == "music") AudioDirector.Instance?.StopMusic(1f);
                    else if (what == "ambience") AudioDirector.Instance?.PlayAmbience(null);
                    else if (what == "weather") Stage.SetWeather(null);
                    break;
                }

                case "notify":
                    overlays.Notify(Interpolate(string.Join(" ", c.Args)), c.Opt("icon"));
                    break;

                case "weather":
                    Stage.SetWeather(c.Arg(0), c.ArgFloat(1, 1f));
                    break;

                case "tint":
                    yield return Stage.SetTint(c.Arg(0), time >= 0 ? time : 0.8f);
                    break;

                case "show":
                case "overlay":
                    yield return overlays.ShowImage(c.Arg(0), c.Arg(1, "center").ToLowerInvariant(), time >= 0 ? time : 0.45f);
                    break;

                case "hide":
                    yield return overlays.HideImage(c.Arg(0), time >= 0 ? time : 0.3f);
                    break;

                case "clear":
                    yield return Stage.ClearCharacters(time >= 0 ? time : 0.4f);
                    break;

                case "vibrate":
#if UNITY_ANDROID || UNITY_IOS
                    Handheld.Vibrate();
#endif
                    yield return Stage.Shake(0.4f, 0.25f);
                    break;

                case "autosave":
                    Autosave();
                    break;

                default:
                    AssetLibrary.WarnOnce($"Line {c.Line}: '@{c.Verb}' isn't supported yet.");
                    break;
            }
        }

        IEnumerator Zoom(CommandInstruction c, float time)
        {
            float t = time >= 0 ? time : 0.7f;
            var args = c.Args.Where(a => !string.Equals(a, "camera", StringComparison.OrdinalIgnoreCase)).ToList();
            string first = args.Count > 0 ? args[0].ToLowerInvariant() : "reset";
            if (first == "reset" || first == "out" || first == "off" || first == "normal")
            {
                Stage.ResetCamera(); // target only; animate from current
                yield return Stage.ZoomTo(new Vector2(((Stage.CurrentZone - 1) * theme.stageUnitsWide + theme.stageUnitsWide / 2f) * StageView.UnitsToWorld, Stage.ViewHeight / 2f), 1f, t);
                yield break;
            }
            int k = first == "on" || first == "to" || first == "in" ? 1 : 0;
            if (k < args.Count && !Util.TryFloat(args[k], out _))
            {
                var ch = Stage.Character(args[k], false);
                float zoom = k + 1 < args.Count && Util.TryFloat(args[k + 1], out var zf) ? zf : 1.5f;
                if (ch != null && ch.OnStage) yield return Stage.ZoomTo(Stage.FocusOn(ch), zoom, t);
                yield break;
            }
            var nums = args.Skip(k).Select(a => Util.TryFloat(a, out var f) ? f : float.NaN).Where(f => !float.IsNaN(f)).ToList();
            if (nums.Count >= 3) yield return Stage.ZoomTo(Stage.FocusOnStagePoint(nums[0], nums[1]), nums[2], t);
            else if (nums.Count == 1)
            {
                var center = new Vector2(((Stage.CurrentZone - 1) * theme.stageUnitsWide + theme.stageUnitsWide / 2f) * StageView.UnitsToWorld, Stage.ViewHeight * 0.6f);
                yield return Stage.ZoomTo(center, nums[0], t);
            }
        }

        // ================== choices, input, minigames ==================

        IEnumerator Choice(ChoiceInstruction c)
        {
            if (dialogue.IsShowing) yield return dialogue.Hide(0.1f);
            Stage.SetSpeaker(null);
            Autosave();
            skip = false;
            inBlockingUI = true;
            Time.timeScale = 1f;
            var visible = Runner.VisibleOptions(c);
            var items = visible.Select(v => new ChoiceItem { Text = Interpolate(v.option.Text), Enabled = v.enabled, Tag = v.option.Tag, IsDefault = v.option.IsDefault }).ToList();
            if (items.Count == 0)
            {
                inBlockingUI = false;
                Runner.Restore(c.End, Runner.CallStack);
                yield break;
            }
            yield return choices.Show(c.Prompt != null ? Interpolate(c.Prompt) : null, items, c.Timer);
            inBlockingUI = false;
            var picked = visible[choices.Selected].option;
            history.Add(new HistoryEntry { speaker = null, text = "<i>▸ " + Interpolate(picked.Text) + "</i>" });
            Runner.Choose(c, picked);
        }

        IEnumerator AskInput(InputInstruction i)
        {
            if (dialogue.IsShowing) yield return dialogue.Hide(0.1f);
            inBlockingUI = true;
            skip = false;
            Time.timeScale = 1f;
            yield return overlays.AskText(Interpolate(i.Prompt), i.Default);
            inBlockingUI = false;
            Runner.SubmitInput(i, overlays.InputResult);
        }

        IEnumerator RunMinigame(MinigameInstruction m)
        {
            if (dialogue.IsShowing) yield return dialogue.Hide(0.1f);
            Stage.SetSpeaker(null);
            Autosave();
            var type = MinigameRegistry.Find(m.Game);
            if (type == null)
            {
                overlays.ShowMessage("Unknown minigame", $"Line {m.Line}: there's no minigame called '{m.Game}'.\n\nAvailable: {string.Join(", ", MinigameRegistry.Names.OrderBy(n => n))}");
                Runner.ResolveMinigame(m, true, 0);
                yield break;
            }
            skip = false;
            auto = false;
            inBlockingUI = true;
            Time.timeScale = 1f;
            menu.SetHudVisible(false);

            var area = UIKit.Rect("Minigame " + m.Game, minigameLayer);
            var game = (Minigame)area.gameObject.AddComponent(type);
            var ctx = new MinigameContext
            {
                Game = m.Game,
                Skin = m.Params.TryGetValue("skin", out var skin) ? skin : "default",
                Area = area,
                Theme = theme,
                Variables = Variables,
                Stage = Stage,
                Data = m.Data.Select(Interpolate).ToList()
            };
            foreach (var kv in m.Params) ctx.Params[kv.Key] = kv.Value;
            yield return game.Run(ctx);
            Destroy(area.gameObject);

            menu.SetHudVisible(true);
            inBlockingUI = false;
            history.Add(new HistoryEntry { text = $"<i>{(ctx.Won ? "Won" : "Lost")} {m.Game} ({Util.FormatNumber(ctx.Score)})</i>" });
            Runner.ResolveMinigame(m, ctx.Won, ctx.Score);
        }

        // ================== saving & ending ==================

        public void Autosave()
        {
            if (Runner == null || program == null || storyFile == null) return;
            var data = new SaveData
            {
                series = seriesName,
                story = storyFile,
                pc = Runner.CurrentPc,
                callStack = Runner.CallStack.ToList(),
                stage = Stage.Snapshot(AudioDirector.Instance),
                history = history.Skip(Math.Max(0, history.Count - 60)).ToList()
            };
            data.WriteVariables(Variables);
            SaveSystem.Save(data);
        }

        IEnumerator EndOfEpisode()
        {
            yield return dialogue.Hide(0.2f);
            Stage.SetSpeaker(null);
            var series = StoryCatalog.SeriesOf(storyFile);
            EpisodeInfo nextEp = null;
            if (series != null)
            {
                int idx = series.Episodes.FindIndex(e => string.Equals(e.File, storyFile, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0 && idx + 1 < series.Episodes.Count) nextEp = series.Episodes[idx + 1];
            }

            // Save progress: variables carry into the next episode.
            var data = new SaveData { series = seriesName, story = nextEp != null ? nextEp.File : storyFile, pc = 0, finished = nextEp == null };
            data.WriteVariables(Variables);
            SaveSystem.Save(data);
            StoryFinished?.Invoke(storyFile);
            storyRoutine = null;
            menu.SetHudVisible(false);

            yield return overlays.FadeOut(Color.black, 1.2f);
            Stage.HideAllInstant();
            AudioDirector.Instance?.StopMusic(1.5f);
            AudioDirector.Instance?.PlayAmbience(null);
            Stage.SetWeather(null);
            yield return EndCard(nextEp);
        }

        IEnumerator EndCard(EpisodeInfo nextEp)
        {
            var layer = ui.Layer("Errors");
            var root = UIKit.Rect("End Card", layer);
            var g = UIKit.Group(root.gameObject);
            g.alpha = 0;
            UIKit.Image(root, "Bg", ProceduralArt.Gradient("end_bg", new Color(0.05f, 0.04f, 0.1f), new Color(0.25f, 0.12f, 0.3f)), Color.white, true);
            string heading = nextEp != null ? "Episode Complete" : program.Meta.TryGetValue("ending", out var e) ? e : "The End";
            var t = UIKit.Text(root, "Title", heading, 104, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(ui.Frame.rect.width - 80, 300));
            var sub = UIKit.Text(root, "Sub", program.Title, 48, new Color(1, 1, 1, 0.7f));
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(ui.Frame.rect.width - 80, 100));
            string choice = null;
            float y = -80;
            if (nextEp != null)
            {
                var b = UIKit.Button(root, "Next", "NEXT EPISODE", theme.accentColor, Color.white, () => choice = "next", 50, 60);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(620, 140));
                y -= 180;
            }
            if (useLibrary)
            {
                var b2 = UIKit.Button(root, "Library", "BACK TO STORIES", new Color(1, 1, 1, 0.12f), Color.white, () => choice = "library", 44, 60);
                UIKit.Place((RectTransform)b2.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(620, 130));
            }
            overlays.SetFade(0, Color.black);
            yield return Tween.Fade(g, 1, 0.6f);
            while (choice == null) yield return null;
            yield return Tween.Fade(g, 0, 0.3f);
            Destroy(root.gameObject);
            if (choice == "next") PlayStory(nextEp.File, false);
            else ShowLibrary();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && storyRoutine != null) Autosave();
        }

        void OnApplicationQuit()
        {
            if (storyRoutine != null) Autosave();
        }
    }
}
