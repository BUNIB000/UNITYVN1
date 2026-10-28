using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VNEngine
{
    /// <summary>Character settings from a 'character' line in a story.</summary>
    public sealed class CharacterDefinition
    {
        public string Id;
        public string DisplayName;
        public Dictionary<string, string> Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A parsed, ready-to-run story.</summary>
    public sealed class StoryProgram
    {
        public string Name;
        public readonly List<Instruction> Instructions = new List<Instruction>();
        public readonly Dictionary<string, int> Labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, CharacterDefinition> Characters = new Dictionary<string, CharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        public readonly List<StoryError> Errors = new List<StoryError>();

        public bool HasErrors => Errors.Any(e => !e.IsWarning);

        public string Title => Meta.TryGetValue("title", out var t) ? t : Name;

        /// <summary>Every character name mentioned anywhere (for preloading and validation).</summary>
        public IEnumerable<string> MentionedCharacters()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in Instructions)
            {
                if (i is CharacterInstruction c) set.Add(c.Character);
                else if (i is SayInstruction s && s.Speaker != null) set.Add(s.Speaker);
            }
            foreach (var k in Characters.Keys) set.Add(k);
            return set;
        }
    }

    /// <summary>
    /// Turns story text into a StoryProgram. See Docs/WRITING_STORIES.md for the format.
    /// The parser never throws: problems are collected in StoryProgram.Errors with line numbers.
    /// </summary>
    public static class StoryParser
    {
        public static readonly HashSet<string> CommandVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wait", "pause", "zoom", "pan", "shake", "flash", "fade", "transition", "music", "sound", "sfx",
            "ambience", "ambient", "notify", "weather", "overlay", "show", "hide", "tint", "clear", "vibrate",
            "autosave", "voice", "textbox", "bubble", "camera", "stop"
        };

        static readonly HashSet<string> MetaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "title", "series", "episode", "author", "description", "cover", "genre", "start"
        };

        static readonly Dictionary<string, string> ReactionVerbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"jumps", "jump"}, {"nods", "nod"}, {"shakes", "shake_head"}, {"laughs", "laugh"}, {"bounces", "bounce"},
            {"gasps", "gasp"}, {"sighs", "sigh"}, {"shivers", "shiver"}, {"trembles", "shiver"}, {"waves", "wave"},
            {"cries", "cry"}, {"dances", "dance"}, {"spins", "spin"}, {"shrugs", "shrug"}, {"bows", "bow"},
            {"cheers", "cheer"}, {"flinches", "flinch"}, {"giggles", "giggle"}, {"pouts", "pout"}, {"thinks", "think"},
            {"leans", "lean"}, {"stomps", "stomp"}, {"hops", "hop"}, {"faints", "faint"}, {"blushes", "blush"}
        };

        static readonly Regex SceneHeading = new Regex(@"^(INT\.?/EXT|EXT\.?/INT|I/E|INT|EXT)[\.\s]", RegexOptions.Compiled);
        static readonly Regex SpeakerLine = new Regex(@"^(?<name>[A-Z][A-Z0-9_'\.\- ]*?)\s*(\((?<anim>[^)]*)\))?\s*(\[(?<style>[^\]]*)\])?\s*(:\s*(?<text>.*))?$", RegexOptions.Compiled);
        static readonly Regex SetLine = new Regex(@"^set\s+(?<var>[A-Za-z_][A-Za-z0-9_\.]*)\s*(?<op>\+=|-=|\*=|/=|=|\+|-|to\b)?\s*(?<expr>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex BracketTag = new Regex(@"\[(?<tag>[^\]]*)\]", RegexOptions.Compiled);

        sealed class Node
        {
            public int Line;
            public int Indent;
            public string Text;
            public bool BlankBefore;
            public readonly List<Node> Children = new List<Node>();
        }

        public static StoryProgram Parse(string source, string name = "story")
        {
            var program = new StoryProgram { Name = name };
            var ctx = new Context(program);
            var roots = BuildTree(source ?? "", ctx);
            ctx.CompileBlock(roots);
            program.Instructions.Add(new EndInstruction { Line = roots.Count > 0 ? roots[roots.Count - 1].Line : 1 });
            ctx.ResolveJumps();
            return program;
        }

        static List<Node> BuildTree(string source, Context ctx)
        {
            var roots = new List<Node>();
            var stack = new List<Node>();
            var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool blank = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Replace("\t", "    ");
                string trimmed = raw.Trim();
                if (trimmed.Length == 0) { blank = true; continue; }
                if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;
                if (trimmed == "{" || trimmed == "}") continue; // tolerate Episode-style braces

                int indent = raw.Length - raw.TrimStart().Length;
                var node = new Node { Line = i + 1, Indent = indent, Text = trimmed.TrimEnd('{').TrimEnd(), BlankBefore = blank };
                blank = false;
                while (stack.Count > 0 && stack[stack.Count - 1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
                if (stack.Count == 0) roots.Add(node);
                else stack[stack.Count - 1].Children.Add(node);
                stack.Add(node);
            }
            return roots;
        }

        sealed class Context
        {
            readonly StoryProgram p;
            readonly List<(JumpInstruction jump, CallInstruction call, int line)> pendingJumps = new List<(JumpInstruction, CallInstruction, int)>();

            public Context(StoryProgram program) { p = program; }

            List<Instruction> Out => p.Instructions;

            void Error(Node n, string msg) => p.Errors.Add(new StoryError { Line = n.Line, Message = msg, Source = n.Text });
            void Warn(Node n, string msg) => p.Errors.Add(new StoryError { Line = n.Line, Message = msg, Source = n.Text, IsWarning = true });

            T Emit<T>(T ins, Node n, bool parallel = false) where T : Instruction
            {
                ins.Line = n.Line;
                ins.Parallel = parallel;
                Out.Add(ins);
                return ins;
            }

            public void ResolveJumps()
            {
                foreach (var (jump, call, line) in pendingJumps)
                {
                    string label = jump != null ? jump.Label : call.Label;
                    if (p.Labels.TryGetValue(label, out int target))
                    {
                        if (jump != null) jump.Target = target; else call.Target = target;
                    }
                    else
                    {
                        p.Errors.Add(new StoryError { Line = line, Message = $"There is no label called '{label}'. Add a line 'label {label}' somewhere in this story." });
                    }
                }
            }

            public void CompileBlock(List<Node> nodes)
            {
                string speaker = null, anim = null, style = null;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n.BlankBefore) { speaker = null; anim = null; style = null; }
                    try
                    {
                        i = CompileStatement(nodes, i, ref speaker, ref anim, ref style);
                    }
                    catch (StoryException e)
                    {
                        p.Errors.Add(new StoryError { Line = e.Line, Message = e.Message, Source = n.Text });
                    }
                    catch (FormatException e)
                    {
                        Error(n, e.Message);
                    }
                }
            }

            /// <summary>Compiles nodes[i] (and any siblings it consumes). Returns the index of the last consumed node.</summary>
            int CompileStatement(List<Node> nodes, int i, ref string speaker, ref string anim, ref string style)
            {
                var n = nodes[i];
                string t = n.Text;
                bool parallel = false;
                if (t.StartsWith("&"))
                {
                    parallel = true;
                    t = t.Substring(1).TrimStart();
                    if (!t.StartsWith("@") && !IsKeywordLine(t)) t = "@" + t;
                }

                // Story header: "title: The Coffee Shop"
                int colon = t.IndexOf(':');
                if (colon > 0 && n.Indent == 0 && MetaKeys.Contains(t.Substring(0, colon).Trim()))
                {
                    p.Meta[t.Substring(0, colon).Trim()] = t.Substring(colon + 1).Trim();
                    return i;
                }

                if (SceneHeading.IsMatch(t))
                {
                    CompileBackground(n, t, t, parallel);
                    speaker = null;
                    return i;
                }

                string first = FirstWord(t);
                string firstLower = first.ToLowerInvariant();

                if (t.StartsWith("@"))
                {
                    string body = t.Substring(1).TrimStart();
                    string verb = FirstWord(body);
                    string rest = body.Substring(verb.Length).Trim();
                    string verbKey = verb.ToLowerInvariant();
                    if (verbKey == "background" || verbKey == "bg" || verbKey == "scene")
                    {
                        CompileBackground(n, rest, null, parallel);
                        return i;
                    }
                    if (CommandVerbs.Contains(verbKey))
                    {
                        CompileCommand(n, verbKey, rest, parallel);
                        return i;
                    }
                    if (verbKey == "end") { Emit(new EndInstruction(), n); return i; }
                    if (verb.Length == 0) { Error(n, "'@' must be followed by a character name or a command."); return i; }
                    if (verb != verb.ToUpperInvariant() && !p.Characters.ContainsKey(verb))
                    {
                        Error(n, $"'{verb}' is not a command I know. Commands are things like @zoom, @music, @pause. Character names are written in CAPITALS, like @{verb.ToUpperInvariant()}.");
                        return i;
                    }
                    CompileCharacter(n, verb.ToUpperInvariant(), rest, parallel);
                    return i;
                }

                switch (firstLower)
                {
                    case "character":
                        CompileCharacterDefinition(n, t.Substring(first.Length));
                        return i;
                    case "label":
                    {
                        string label = t.Substring(first.Length).Trim().TrimEnd(':');
                        if (label.Length == 0) { Error(n, "A label needs a name, like 'label coffee_date'."); return i; }
                        if (p.Labels.ContainsKey(label)) Error(n, $"The label '{label}' is used twice.");
                        p.Labels[label] = Out.Count;
                        Emit(new LabelInstruction { Name = label }, n);
                        return i;
                    }
                    case "goto":
                    case "jump":
                    {
                        var j = Emit(new JumpInstruction { Label = t.Substring(first.Length).Trim() }, n);
                        pendingJumps.Add((j, null, n.Line));
                        return i;
                    }
                    case "call":
                    {
                        var c = Emit(new CallInstruction { Label = t.Substring(first.Length).Trim() }, n);
                        pendingJumps.Add((null, c, n.Line));
                        return i;
                    }
                    case "return":
                        Emit(new ReturnInstruction(), n);
                        return i;
                    case "end":
                        Emit(new EndInstruction(), n);
                        return i;
                    case "next":
                    {
                        var words = Util.Tokenize(t.Substring(first.Length));
                        if (words.Count == 0) { Error(n, "'next' needs the name of the story to continue with."); return i; }
                        Emit(new NextStoryInstruction { Story = words[0], Label = words.Count > 1 ? words[words.Count - 1] : null }, n);
                        return i;
                    }
                    case "set":
                        CompileSet(n, t);
                        return i;
                    case "input":
                        CompileInput(n, t.Substring(first.Length));
                        return i;
                    case "choice":
                        CompileChoice(n, t.Substring(first.Length));
                        speaker = null;
                        return i;
                    case "if":
                        return CompileIf(nodes, i);
                    case "elif":
                    case "else":
                        Error(n, $"'{firstLower}' must come right after an 'if' block at the same indentation.");
                        return i;
                    case "minigame":
                        CompileMinigame(n, t.Substring(first.Length));
                        return i;
                }

                if (CommandVerbs.Contains(firstLower) && first == firstLower)
                {
                    CompileCommand(n, firstLower, t.Substring(first.Length).Trim(), parallel);
                    return i;
                }

                if (n.Children.Count > 0)
                    Warn(n, "This line has indented lines under it, but it isn't a choice, if, or minigame. The indented lines will play normally.");

                // Speaker line: "MAYA (talk_happy)" or "MAYA (smile): Hello!"
                var m = SpeakerLine.Match(t);
                if (m.Success && IsSpeakerName(m.Groups["name"].Value.Trim()))
                {
                    string name = m.Groups["name"].Value.Trim();
                    string a = m.Groups["anim"].Success ? m.Groups["anim"].Value.Trim() : null;
                    string st = m.Groups["style"].Success ? m.Groups["style"].Value.Trim().ToLowerInvariant() : null;
                    string inline = m.Groups["text"].Success ? m.Groups["text"].Value.Trim() : null;
                    bool nextIsText = i + 1 < nodes.Count && !nodes[i + 1].BlankBefore && IsPlainText(nodes[i + 1].Text);
                    if (!string.IsNullOrEmpty(inline) || nextIsText || name == "NARRATOR" || p.Characters.ContainsKey(name))
                    {
                        speaker = name == "NARRATOR" ? null : name;
                        anim = a;
                        style = st;
                        if (!string.IsNullOrEmpty(inline)) EmitSay(n, speaker, anim, style, inline);
                        CompileChildren(n);
                        return i;
                    }
                }

                // Plain text: dialogue for the current speaker, or narration.
                EmitSay(n, speaker, anim, style, t);
                anim = null; // the animation plays once, on the first bubble
                CompileChildren(n);
                return i;
            }

            void CompileChildren(Node n)
            {
                if (n.Children.Count > 0) CompileBlock(n.Children);
            }

            void EmitSay(Node n, string speaker, string anim, string style, string text)
            {
                if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"') text = text.Substring(1, text.Length - 2);
                Emit(new SayInstruction
                {
                    Speaker = speaker,
                    Animation = string.IsNullOrEmpty(anim) ? null : anim,
                    Style = speaker == null ? (style ?? "narration") : (style ?? "speech"),
                    Text = text
                }, n);
            }

            static bool IsKeywordLine(string t)
            {
                string w = FirstWord(t).ToLowerInvariant();
                return w == "set" || w == "goto" || w == "label";
            }

            bool IsSpeakerName(string name)
            {
                if (name.Length == 0 || name.Length > 40) return false;
                if (p.Characters.ContainsKey(name)) return true;
                return name == name.ToUpperInvariant() && name.Any(char.IsLetter);
            }

            bool IsPlainText(string t)
            {
                if (t.StartsWith("@") || t.StartsWith("&") || SceneHeading.IsMatch(t)) return false;
                string w = FirstWord(t).ToLowerInvariant();
                if (w == t.Split(' ')[0] && (CommandVerbs.Contains(w) || w == "choice" || w == "if" || w == "set" || w == "goto" || w == "label" || w == "minigame" || w == "input" || w == "next" || w == "end" || w == "call" || w == "return"))
                    return false;
                var m = SpeakerLine.Match(t);
                if (m.Success && IsSpeakerName(m.Groups["name"].Value.Trim()) && (m.Groups["anim"].Success || m.Groups["text"].Success)) return false;
                return true;
            }

            static string FirstWord(string t)
            {
                int k = 0;
                while (k < t.Length && !char.IsWhiteSpace(t[k]) && t[k] != ':' && t[k] != '(' && t[k] != '[') k++;
                return t.Substring(0, k);
            }

            // ---------- backgrounds ----------

            void CompileBackground(Node n, string text, string heading, bool parallel)
            {
                var ins = new BackgroundInstruction();
                // Bracket options: [fade 1.5] [zone 2]
                foreach (Match m in BracketTag.Matches(text)) ApplyBackgroundOption(ins, Util.Tokenize(m.Groups["tag"].Value));
                text = BracketTag.Replace(text, "").Trim();

                // "with dissolve in 1.2 zone 2" tail
                int with = IndexOfWord(text, "with");
                if (with >= 0)
                {
                    ApplyBackgroundOption(ins, Util.Tokenize(text.Substring(with + 4)));
                    text = text.Substring(0, with).Trim();
                }
                else if (heading == null)
                {
                    var words = Util.Tokenize(text);
                    if (words.Count > 1)
                    {
                        text = words[0];
                        ApplyBackgroundOption(ins, words.Skip(1).ToList());
                    }
                }
                ins.Name = text.Trim();
                if (ins.Name.Length == 0) { Error(n, "Which background? For example: @background coffee_shop"); return; }
                Emit(ins, n, parallel);
            }

            static void ApplyBackgroundOption(BackgroundInstruction ins, List<string> words)
            {
                for (int k = 0; k < words.Count; k++)
                {
                    string w = words[k].ToLowerInvariant();
                    if (w == "in" || w == "for") continue;
                    if (w == "zone" && k + 1 < words.Count && int.TryParse(words[k + 1], out int z)) { ins.Zone = z; k++; continue; }
                    if (Util.TryFloat(w, out float d)) { ins.Duration = d; continue; }
                    ins.Transition = w;
                }
            }

            static int IndexOfWord(string text, string word)
            {
                var m = Regex.Match(text, @"\s" + word + @"\s", RegexOptions.IgnoreCase);
                return m.Success ? m.Index + 1 : -1;
            }

            // ---------- commands ----------

            void CompileCommand(Node n, string verb, string rest, bool parallel)
            {
                var ins = new CommandInstruction { Verb = verb };
                if (verb == "pause") ins.Verb = "wait";
                if (verb == "sfx") ins.Verb = "sound";
                if (verb == "ambient") ins.Verb = "ambience";
                ins.Args = Util.Tokenize(rest, ins.Options);
                // "@pause for 1", "@zoom ... in 0.5" -> normalise "in"/"for" durations into options
                for (int k = 0; k < ins.Args.Count; k++)
                {
                    string w = ins.Args[k].ToLowerInvariant();
                    if ((w == "in" || w == "for" || w == "over") && k + 1 < ins.Args.Count && Util.TryFloat(ins.Args[k + 1], out _))
                    {
                        ins.Options["time"] = ins.Args[k + 1];
                        ins.Args.RemoveRange(k, 2);
                        k--;
                    }
                }
                Emit(ins, n, parallel);
            }

            // ---------- characters ----------

            void CompileCharacterDefinition(Node n, string rest)
            {
                var def = new CharacterDefinition();
                var words = Util.Tokenize(rest, def.Options);
                if (words.Count == 0) { Error(n, "Write the character's name, like: character MAYA name=\"Maya\" color=#ff6699"); return; }
                def.Id = words[0].ToUpperInvariant();
                def.DisplayName = def.Options.TryGetValue("name", out var dn) ? dn : (words.Count > 1 ? string.Join(" ", words.Skip(1)) : null);
                p.Characters[def.Id] = def;
            }

            void CompileCharacter(Node n, string name, string rest, bool parallel)
            {
                var ins = new CharacterInstruction { Character = name };
                if (rest.Length == 0) { Error(n, $"What should {name} do? For example: @{name} enters from left"); return; }
                foreach (var part in SplitAnd(rest))
                {
                    var words = Util.Tokenize(part);
                    if (words.Count == 0) continue;
                    ins.Actions.Add(ParseAction(n, name, words));
                }
                Emit(ins, n, parallel);
            }

            static IEnumerable<string> SplitAnd(string s)
            {
                return Regex.Split(s, @"\s+(?:and|then)\s+", RegexOptions.IgnoreCase).Where(x => x.Trim().Length > 0);
            }

            CharacterAction ParseAction(Node n, string name, List<string> w)
            {
                string verb = w[0].ToLowerInvariant();
                var a = new CharacterAction();
                int k = 1;
                switch (verb)
                {
                    case "enters":
                    case "enter":
                    case "comes":
                        a.Kind = CharacterActionKind.Enter;
                        if (Peek(w, k) == "in") k++; // "comes in from"
                        if (Peek(w, k) == "from") { a.Value = Peek(w, k + 1) ?? "left"; k += 2; }
                        else a.Value = "fade";
                        if (Peek(w, k) == "screen") { a.Value = Peek(w, k + 1); k += 2; }
                        if (Peek(w, k) == "to") { k++; a.Position = ParsePosition(n, w, ref k); }
                        break;
                    case "exits":
                    case "exit":
                    case "leaves":
                    case "leave":
                        a.Kind = CharacterActionKind.Exit;
                        if (Peek(w, k) == "to" || Peek(w, k) == "toward" || Peek(w, k) == "towards") k++;
                        if (Peek(w, k) == "screen") k++;
                        if (Peek(w, k) != null && Peek(w, k) != "in") { a.Value = Peek(w, k); k++; }
                        break;
                    case "walks":
                    case "walk":
                    case "runs":
                    case "run":
                    case "moves":
                    case "move":
                    case "slides":
                    case "goes":
                        a.Kind = verb.StartsWith("walk") || verb == "goes" ? CharacterActionKind.Walk : verb.StartsWith("run") ? CharacterActionKind.Run : CharacterActionKind.Move;
                        if (Peek(w, k) == "to" || Peek(w, k) == "over") k++;
                        if (Peek(w, k) == "layer") { a.Kind = CharacterActionKind.Layer; a.Layer = ParseInt(n, Peek(w, k + 1)); k += 2; break; }
                        a.Position = ParsePosition(n, w, ref k);
                        break;
                    case "spot":
                        a.Kind = CharacterActionKind.Spot;
                        k = 0;
                        a.Position = ParsePosition(n, w, ref k);
                        break;
                    case "stands":
                    case "stand":
                    case "sits":
                    case "waits":
                        // Episode's "@MAYA stands screen left": place instantly.
                        a.Kind = CharacterActionKind.Spot;
                        if (Peek(w, k) == "at" || Peek(w, k) == "in" && Peek(w, k + 1) != "zone") k++;
                        a.Position = Peek(w, k) == null ? new StagePosition { Named = "center" } : ParsePosition(n, w, ref k);
                        break;
                    case "is":
                    case "does":
                    case "plays":
                    case "starts":
                        a.Kind = CharacterActionKind.Animate;
                        a.Value = Need(n, w, k++, "an animation name, like talk_happy");
                        break;
                    case "looks":
                    case "feels":
                        a.Kind = CharacterActionKind.Look;
                        if (Peek(w, k) == "at") { a.Kind = CharacterActionKind.Face; k++; }
                        a.Value = Need(n, w, k++, "an expression, like happy");
                        break;
                    case "faces":
                    case "turns":
                    case "face":
                        a.Kind = CharacterActionKind.Face;
                        if (Peek(w, k) == "to" || Peek(w, k) == "toward" || Peek(w, k) == "towards") k++;
                        a.Value = Need(n, w, k++, "left, right, or another character");
                        break;
                    case "wears":
                    case "wear":
                    case "changes":
                    case "puts":
                        a.Kind = CharacterActionKind.Wear;
                        if (Peek(w, k) == "into" || Peek(w, k) == "on") k++;
                        if (w.Count - k >= 2 && Peek(w, k + 1) != "in") { a.Slot = w[k]; a.Value = w[k + 1]; k += 2; }
                        else a.Value = Need(n, w, k++, "an outfit name");
                        break;
                    case "appears":
                    case "appear":
                        a.Kind = CharacterActionKind.Appear;
                        if (Peek(w, k) == "at" || Peek(w, k) == "in") { if (Peek(w, k) == "at") k++; if (Peek(w, k) != "in" || !IsNumber(Peek(w, k + 1))) a.Position = ParsePosition(n, w, ref k); }
                        break;
                    case "disappears":
                    case "disappear":
                    case "vanishes":
                        a.Kind = CharacterActionKind.Disappear;
                        break;
                    case "layer":
                        a.Kind = CharacterActionKind.Layer;
                        a.Layer = ParseInt(n, Need(n, w, k++, "a layer number"));
                        break;
                    default:
                        if (ReactionVerbs.TryGetValue(verb, out var anim))
                        {
                            a.Kind = CharacterActionKind.Animate;
                            a.Value = anim;
                            break;
                        }
                        throw new StoryException(n.Line, $"I don't know how to make {name} '{w[0]}'. Try enters, exits, walks to, moves to, stands, spot, is <animation>, looks <expression>, faces, wears, appears, or disappears.");
                }

                // Trailing modifiers: "in 0.5", "at layer 2", "in zone 2"
                for (; k < w.Count; k++)
                {
                    string word = w[k].ToLowerInvariant();
                    if ((word == "in" || word == "over" || word == "for") && IsNumber(Peek(w, k + 1))) { Util.TryFloat(w[k + 1], out var d); a.Duration = d; k++; }
                    else if (word == "in" && Peek(w, k + 1) == "zone") { EnsurePos(a).Zone = ParseInt(n, Peek(w, k + 2)); k += 2; }
                    else if (word == "zone") { EnsurePos(a).Zone = ParseInt(n, Peek(w, k + 1)); k++; }
                    else if (word == "at" && Peek(w, k + 1) == "layer") { a.Layer = ParseInt(n, Peek(w, k + 2)); k += 2; }
                    else if (word == "layer") { a.Layer = ParseInt(n, Peek(w, k + 1)); k++; }
                    else if (word == "slowly") a.Duration = (a.Duration ?? 1f) * 1.6f;
                    else if (word == "quickly") a.Duration = (a.Duration ?? 1f) * 0.5f;
                    else if (word == "loop" || word == "looping") a.Value += "_loop";
                    else throw new StoryException(n.Line, $"I don't understand '{w[k]}' in '{string.Join(" ", w)}'.");
                }
                return a;
            }

            static StagePosition EnsurePos(CharacterAction a) => a.Position ?? (a.Position = new StagePosition());

            StagePosition ParsePosition(Node n, List<string> w, ref int k)
            {
                var pos = new StagePosition();
                string word = Peek(w, k);
                if (word == null) throw new StoryException(n.Line, "Where to? For example: left, center, right, or spot 1 160 0");
                if (word == "screen" || word == "the") { k++; word = Peek(w, k); }
                if (word == "spot")
                {
                    k++;
                    pos.Scale = ParseFloat(n, Peek(w, k++), "spot needs: scale x y, like spot 1.0 160 0");
                    pos.X = ParseFloat(n, Peek(w, k++), "spot needs: scale x y, like spot 1.0 160 0");
                    pos.Y = IsNumber(Peek(w, k)) ? ParseFloat(n, Peek(w, k++), null) : 0f;
                    return pos;
                }
                if (IsNumber(word))
                {
                    pos.X = ParseFloat(n, word, null);
                    k++;
                    if (IsNumber(Peek(w, k)) && Peek(w, k - 2) != "in") { pos.Y = ParseFloat(n, Peek(w, k), null); k++; }
                    return pos;
                }
                if (word == "off" || word == "offscreen" || word == "far")
                {
                    string side = Peek(w, k + 1);
                    pos.Named = (word == "far" ? "far_" : "offscreen_") + side;
                    k += 2;
                    return pos;
                }
                if (word == "next" && Peek(w, k + 1) == "to")
                {
                    pos.NextTo = w[k + 2].ToUpperInvariant();
                    k += 3;
                    return pos;
                }
                if (word == "middle" || word == "centre") word = "center";
                if (word == "left" || word == "right" || word == "center" || word == "top" || word == "bottom" || word.StartsWith("offscreen_") || word.StartsWith("far_"))
                {
                    pos.Named = word;
                    k++;
                    return pos;
                }
                if (w[k] == w[k].ToUpperInvariant() && w[k].Any(char.IsLetter))
                {
                    pos.NextTo = w[k];
                    k++;
                    return pos;
                }
                throw new StoryException(n.Line, $"'{w[k]}' isn't a place on screen. Use left, center, right, far left, offscreen right, a character's name, or spot <scale> <x> <y>.");
            }

            static string Peek(List<string> w, int k) => k < w.Count ? w[k].ToLowerInvariant() : null;

            static bool IsNumber(string s) => s != null && Util.TryFloat(s, out _);

            static string Need(Node n, List<string> w, int k, string what)
            {
                if (k < w.Count) return w[k];
                throw new StoryException(n.Line, $"'{string.Join(" ", w)}' needs {what}.");
            }

            static float ParseFloat(Node n, string s, string help)
            {
                if (s != null && Util.TryFloat(s, out var f)) return f;
                throw new StoryException(n.Line, help ?? $"'{s}' should be a number.");
            }

            static int ParseInt(Node n, string s)
            {
                if (s != null && int.TryParse(s, out var v)) return v;
                throw new StoryException(n.Line, $"'{s}' should be a whole number.");
            }

            // ---------- variables ----------

            void CompileSet(Node n, string t)
            {
                var m = SetLine.Match(t);
                if (!m.Success) { Error(n, "Write 'set' like: set trust + 1, set met_maya = true, set name = \"Alex\""); return; }
                string op = m.Groups["op"].Success ? m.Groups["op"].Value.ToLowerInvariant() : "";
                string expr = m.Groups["expr"].Value.Trim();
                switch (op)
                {
                    case "": op = "="; if (expr.Length == 0) expr = "true"; break;
                    case "to": op = "="; break;
                    case "+": op = "+="; break;
                    case "-": op = "-="; break;
                }
                if (expr.Length == 0) { Error(n, $"'set {m.Groups["var"].Value} {op}' needs a value."); return; }
                Emit(new SetInstruction { Variable = m.Groups["var"].Value, Op = op, Value = ParseExpr(n, expr) }, n);
            }

            Expression ParseExpr(Node n, string expr)
            {
                try { return Expression.Parse(expr); }
                catch (FormatException e) { throw new StoryException(n.Line, "Couldn't read the condition: " + e.Message); }
            }

            void CompileInput(Node n, string rest)
            {
                var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var words = Util.Tokenize(rest, opts);
                if (words.Count == 0) { Error(n, "input needs a variable name, like: input player_name \"What's your name?\""); return; }
                Emit(new InputInstruction
                {
                    Variable = words[0],
                    Prompt = words.Count > 1 ? words[1] : "Enter a name",
                    Default = opts.TryGetValue("default", out var d) ? d : null
                }, n);
            }

            // ---------- choices ----------

            void CompileChoice(Node n, string rest)
            {
                var ins = new ChoiceInstruction();
                var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match bm in BracketTag.Matches(rest))
                {
                    var tw = Util.Tokenize(bm.Groups["tag"].Value);
                    if (tw.Count >= 1 && (tw[0] == "timer" || tw[0] == "timed") && tw.Count > 1 && Util.TryFloat(tw[1], out var tm)) ins.Timer = tm;
                }
                rest = BracketTag.Replace(rest, "");
                var words = Util.Tokenize(rest, opts);
                for (int k = 0; k < words.Count; k++)
                {
                    if ((words[k] == "timed" || words[k] == "timer") && k + 1 < words.Count && Util.TryFloat(words[k + 1], out var tm)) { ins.Timer = tm; k++; }
                    else ins.Prompt = ins.Prompt == null ? words[k] : ins.Prompt + " " + words[k];
                }
                if (opts.TryGetValue("timer", out var ts) && Util.TryFloat(ts, out var tv)) ins.Timer = tv;
                Emit(ins, n);

                if (n.Children.Count == 0) Error(n, "A choice needs options under it, each in quotes and indented.");
                var jumpsToEnd = new List<JumpInstruction>();
                foreach (var child in n.Children)
                {
                    string ct = child.Text;
                    if (!ct.StartsWith("\"") && !ct.StartsWith("“"))
                    {
                        p.Errors.Add(new StoryError { Line = child.Line, Message = "Each choice option must start with a quote, like: \"Order a latte\"", Source = ct });
                        continue;
                    }
                    var option = new ChoiceOption { Line = child.Line };
                    int close = ct.IndexOfAny(new[] { '"', '”' }, 1);
                    if (close < 0) { p.Errors.Add(new StoryError { Line = child.Line, Message = "This option is missing its closing quote.", Source = ct }); continue; }
                    option.Text = ct.Substring(1, close - 1);
                    string tail = ct.Substring(close + 1);
                    foreach (Match bm in BracketTag.Matches(tail))
                    {
                        string tag = bm.Groups["tag"].Value.Trim();
                        string tagLower = tag.ToLowerInvariant();
                        if (tagLower.StartsWith("if ")) option.Condition = ParseExpr(child, tag.Substring(3));
                        else if (tagLower.StartsWith("locked if ") || tagLower.StartsWith("requires "))
                        {
                            option.Condition = ParseExpr(child, tag.Substring(tag.IndexOf(' ', tagLower.StartsWith("locked") ? 7 : 0) + 1));
                            option.ShowLocked = true;
                        }
                        else if (tagLower == "default") option.IsDefault = true;
                        else option.Tag = tag;
                    }
                    // "-> label" shortcut
                    int arrow = tail.IndexOf("->", StringComparison.Ordinal);
                    option.Target = Out.Count;
                    ins.Options.Add(option);
                    if (child.Children.Count > 0) CompileBlock(child.Children);
                    if (arrow >= 0)
                    {
                        var jl = Emit(new JumpInstruction { Label = BracketTag.Replace(tail.Substring(arrow + 2), "").Trim() }, child);
                        pendingJumps.Add((jl, null, child.Line));
                    }
                    else
                    {
                        jumpsToEnd.Add(Emit(new JumpInstruction(), child));
                    }
                }
                ins.End = Out.Count;
                foreach (var j in jumpsToEnd) j.Target = ins.End;
            }

            // ---------- if / elif / else ----------

            int CompileIf(List<Node> nodes, int i)
            {
                var endJumps = new List<JumpInstruction>();
                BranchInstruction pending = null;
                int last = i;
                for (int k = i; k < nodes.Count; k++)
                {
                    var n = nodes[k];
                    string word = FirstWord(n.Text).ToLowerInvariant();
                    string rest = n.Text.Substring(FirstWord(n.Text).Length).Trim();
                    string cond;
                    if (k == i) cond = rest;
                    else if (word == "elif") cond = rest;
                    else if (word == "else" && rest.StartsWith("if ", StringComparison.OrdinalIgnoreCase)) cond = rest.Substring(3);
                    else if (word == "else") cond = null;
                    else break;

                    last = k;
                    if (pending != null) pending.FalseTarget = Out.Count;
                    pending = null;
                    if (cond == null)
                    {
                        CompileBlock(n.Children);
                        break;
                    }
                    cond = cond.Trim().TrimEnd(':').Trim();
                    if (cond.Length == 0) { Error(n, "'if' needs a condition, like: if trust >= 3"); continue; }
                    pending = Emit(new BranchInstruction { Condition = ParseExpr(n, cond) }, n);
                    if (n.Children.Count == 0) Warn(n, "This 'if' has nothing indented under it.");
                    CompileBlock(n.Children);
                    endJumps.Add(Emit(new JumpInstruction(), n));
                }
                if (pending != null) pending.FalseTarget = Out.Count;
                foreach (var j in endJumps) j.Target = Out.Count;
                return last;
            }

            // ---------- minigames ----------

            void CompileMinigame(Node n, string rest)
            {
                var ins = new MinigameInstruction();
                var words = Util.Tokenize(rest, ins.Params);
                if (words.Count == 0) { Error(n, "Which minigame? For example: minigame memory pairs=6"); return; }
                ins.Game = words[0].ToLowerInvariant();
                for (int k = 1; k < words.Count; k++) ins.Params["arg" + k] = words[k];
                Emit(ins, n);

                var endJumps = new List<JumpInstruction>();
                foreach (var child in n.Children)
                {
                    string w = FirstWord(child.Text).ToLowerInvariant().TrimEnd(':');
                    if ((w == "win" || w == "won" || w == "success" || w == "lose" || w == "lost" || w == "fail") && child.Text.TrimEnd(':').Trim().Length == w.Length)
                    {
                        if (w.StartsWith("w") || w == "success") ins.WinTarget = Out.Count; else ins.LoseTarget = Out.Count;
                        CompileBlock(child.Children);
                        endJumps.Add(Emit(new JumpInstruction(), child));
                    }
                    else
                    {
                        FlattenData(child, ins.Data, 0);
                    }
                }
                ins.End = Out.Count;
                foreach (var j in endJumps) j.Target = ins.End;
            }

            static void FlattenData(Node node, List<string> data, int depth)
            {
                data.Add(new string(' ', depth * 4) + node.Text);
                foreach (var c in node.Children) FlattenData(c, data, depth + 1);
            }
        }
    }
}
