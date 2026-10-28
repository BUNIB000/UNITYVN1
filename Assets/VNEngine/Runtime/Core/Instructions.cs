using System.Collections.Generic;

namespace VNEngine
{
    /// <summary>One compiled step of a story. The runner walks a flat list of these.</summary>
    public abstract class Instruction
    {
        /// <summary>1-based line in the source script, used for error messages.</summary>
        public int Line;
        /// <summary>True when the line started with '&amp;': run it without waiting for it to finish.</summary>
        public bool Parallel;
    }

    /// <summary>Dialogue or narration. Speaker is null for narration.</summary>
    public sealed class SayInstruction : Instruction
    {
        public string Speaker;
        public string Animation;
        /// <summary>speech, thought, shout, whisper, phone, narration.</summary>
        public string Style;
        public string Text;
    }

    public enum CharacterActionKind
    {
        Enter, Exit, Walk, Run, Move, Spot, Animate, Face, Wear, Appear, Disappear, Layer, Look
    }

    /// <summary>Where on stage something goes. Either a named spot or explicit spot directing numbers.</summary>
    public sealed class StagePosition
    {
        /// <summary>left, center, right, offscreen_left, offscreen_right, top, bottom, or null when explicit.</summary>
        public string Named;
        public float? Scale;
        public float? X;
        public float? Y;
        public int? Zone;
        /// <summary>Another character to stand next to.</summary>
        public string NextTo;

        public override string ToString()
        {
            if (NextTo != null) return "next to " + NextTo;
            if (Named != null) return Named;
            return $"spot {Scale} {X} {Y} zone {Zone}";
        }
    }

    public sealed class CharacterAction
    {
        public CharacterActionKind Kind;
        /// <summary>Animation name, outfit name, facing direction or entry side depending on Kind.</summary>
        public string Value;
        /// <summary>Second value, e.g. the wardrobe slot for 'wears hair ponytail'.</summary>
        public string Slot;
        public StagePosition Position;
        public float? Duration;
        public int? Layer;
    }

    /// <summary>'@MAYA walks to center and is talk_happy'.</summary>
    public sealed class CharacterInstruction : Instruction
    {
        public string Character;
        public List<CharacterAction> Actions = new List<CharacterAction>();
    }

    /// <summary>Background change from a scene heading or '@background'.</summary>
    public sealed class BackgroundInstruction : Instruction
    {
        public string Name;
        public string Transition;
        public float? Duration;
        public int? Zone;
    }

    /// <summary>Any other stage command: camera, audio, effects, waits, notifications.</summary>
    public sealed class CommandInstruction : Instruction
    {
        public string Verb;
        public List<string> Args = new List<string>();
        public Dictionary<string, string> Options = new Dictionary<string, string>();

        public string Arg(int i, string fallback = null) => i < Args.Count ? Args[i] : fallback;

        public float ArgFloat(int i, float fallback)
        {
            return i < Args.Count && Util.TryFloat(Args[i], out var f) ? f : fallback;
        }

        public string Opt(string key, string fallback = null)
        {
            return Options.TryGetValue(key, out var v) ? v : fallback;
        }

        public float OptFloat(string key, float fallback)
        {
            return Options.TryGetValue(key, out var v) && Util.TryFloat(v, out var f) ? f : fallback;
        }
    }

    public sealed class ChoiceOption
    {
        public string Text;
        public Expression Condition;
        /// <summary>When true the option is shown but locked if Condition fails, instead of hidden.</summary>
        public bool ShowLocked;
        public bool IsDefault;
        /// <summary>Optional icon or tag such as 'premium', shown on the button.</summary>
        public string Tag;
        public int Target;
        public int Line;
    }

    public sealed class ChoiceInstruction : Instruction
    {
        public string Prompt;
        public float? Timer;
        public List<ChoiceOption> Options = new List<ChoiceOption>();
        public int End;
    }

    public sealed class MinigameInstruction : Instruction
    {
        public string Game;
        public Dictionary<string, string> Params = new Dictionary<string, string>();
        /// <summary>Indented lines inside the minigame block that are not win/lose branches (quiz questions, item lists...).</summary>
        public List<string> Data = new List<string>();
        public int WinTarget = -1;
        public int LoseTarget = -1;
        public int End;
    }

    public sealed class InputInstruction : Instruction
    {
        public string Variable;
        public string Prompt;
        public string Default;
    }

    public sealed class SetInstruction : Instruction
    {
        public string Variable;
        /// <summary>=, +=, -=, *=, /=</summary>
        public string Op;
        public Expression Value;
    }

    public sealed class JumpInstruction : Instruction
    {
        public int Target;
        /// <summary>Label name, resolved to Target after the whole script is parsed.</summary>
        public string Label;
    }

    public sealed class BranchInstruction : Instruction
    {
        public Expression Condition;
        /// <summary>Where to go when the condition is false.</summary>
        public int FalseTarget;
    }

    public sealed class CallInstruction : Instruction
    {
        public int Target;
        public string Label;
    }

    public sealed class ReturnInstruction : Instruction { }

    public sealed class LabelInstruction : Instruction
    {
        public string Name;
    }

    public sealed class EndInstruction : Instruction { }

    /// <summary>'next episode_2' — continue in another story file.</summary>
    public sealed class NextStoryInstruction : Instruction
    {
        public string Story;
        public string Label;
    }
}
