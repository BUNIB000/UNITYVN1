using System;
using System.Collections.Generic;
using System.Linq;

namespace VNEngine
{
    /// <summary>
    /// Walks a StoryProgram. Control flow (if, goto, set, labels) is handled here;
    /// everything the player sees or waits for is handed back from Next().
    /// Pure C# so it can be tested without Unity.
    /// </summary>
    public sealed class StoryRunner
    {
        public StoryProgram Program { get; private set; }
        public VariableStore Variables { get; }
        /// <summary>Index of the next instruction to run.</summary>
        public int Pc { get; private set; }
        /// <summary>Index of the last instruction handed out by Next(); saves resume from here.</summary>
        public int CurrentPc { get; private set; }
        public bool Finished { get; private set; }

        readonly Stack<int> callStack = new Stack<int>();
        public IEnumerable<int> CallStack => callStack.Reverse();

        /// <summary>Safety net against 'label a / goto a' loops with nothing to show.</summary>
        const int MaxSilentSteps = 100000;

        public StoryRunner(StoryProgram program, VariableStore variables = null)
        {
            Variables = variables ?? new VariableStore();
            Load(program);
        }

        public void Load(StoryProgram program, string label = null)
        {
            Program = program;
            callStack.Clear();
            Finished = false;
            Pc = 0;
            if (label == null && program.Meta.TryGetValue("start", out var start)) label = start;
            if (!string.IsNullOrEmpty(label)) Goto(label);
        }

        public void Goto(string label)
        {
            if (!Program.Labels.TryGetValue(label, out int target))
                throw new StoryException(0, $"There is no label called '{label}' in {Program.Name}.");
            Pc = target;
            Finished = false;
        }

        public void Restore(int pc, IEnumerable<int> stack)
        {
            Pc = Math.Max(0, Math.Min(pc, Program.Instructions.Count - 1));
            callStack.Clear();
            if (stack != null) foreach (var s in stack) callStack.Push(s);
            Finished = false;
        }

        /// <summary>
        /// Runs control flow until something presentable comes up, and returns it.
        /// Returns an EndInstruction when the story is over.
        /// Choice, Minigame and Input instructions keep the runner parked on them until
        /// Choose / ResolveMinigame / SubmitInput is called.
        /// </summary>
        public Instruction Next()
        {
            for (int guard = 0; guard < MaxSilentSteps; guard++)
            {
                if (Pc < 0 || Pc >= Program.Instructions.Count)
                {
                    Finished = true;
                    return new EndInstruction { Line = Program.Instructions.Count > 0 ? Program.Instructions[Program.Instructions.Count - 1].Line : 0 };
                }

                var ins = Program.Instructions[Pc];
                switch (ins)
                {
                    case LabelInstruction _:
                        Pc++;
                        continue;
                    case JumpInstruction j:
                        Pc = j.Target;
                        continue;
                    case BranchInstruction b:
                        Pc = b.Condition.IsTrue(Variables) ? Pc + 1 : b.FalseTarget;
                        continue;
                    case CallInstruction c:
                        callStack.Push(Pc + 1);
                        Pc = c.Target;
                        continue;
                    case ReturnInstruction _:
                        if (callStack.Count == 0) { Finished = true; CurrentPc = Pc; return new EndInstruction { Line = ins.Line }; }
                        Pc = callStack.Pop();
                        continue;
                    case SetInstruction s:
                        ApplySet(s);
                        Pc++;
                        continue;
                    case EndInstruction _:
                        Finished = true;
                        CurrentPc = Pc;
                        return ins;
                    case ChoiceInstruction _:
                    case MinigameInstruction _:
                    case InputInstruction _:
                        CurrentPc = Pc;
                        return ins; // parked until resolved
                    default:
                        CurrentPc = Pc;
                        Pc++;
                        return ins;
                }
            }
            throw new StoryException(Program.Instructions[Math.Max(0, Math.Min(Pc, Program.Instructions.Count - 1))].Line,
                "The story is stuck in a loop that never shows anything. Check your goto lines.");
        }

        void ApplySet(SetInstruction s)
        {
            object value = s.Value.Evaluate(Variables);
            if (s.Op == "=") { Variables.Set(s.Variable, value); return; }
            double cur = Variables.GetNumber(s.Variable);
            double v = Expression.ToNumber(value);
            switch (s.Op)
            {
                case "+=":
                    if (value is string str && !(Variables.Get(s.Variable) is double)) Variables.Set(s.Variable, Variables.GetString(s.Variable) + str);
                    else Variables.Set(s.Variable, cur + v);
                    break;
                case "-=": Variables.Set(s.Variable, cur - v); break;
                case "*=": Variables.Set(s.Variable, cur * v); break;
                case "/=": Variables.Set(s.Variable, Math.Abs(v) < 1e-12 ? 0 : cur / v); break;
            }
        }

        /// <summary>The options of a choice the player can see, with whether each is selectable.</summary>
        public List<(ChoiceOption option, bool enabled)> VisibleOptions(ChoiceInstruction choice)
        {
            var list = new List<(ChoiceOption, bool)>();
            foreach (var o in choice.Options)
            {
                bool ok = o.Condition == null || o.Condition.IsTrue(Variables);
                if (ok) list.Add((o, true));
                else if (o.ShowLocked) list.Add((o, false));
            }
            return list;
        }

        public void Choose(ChoiceInstruction choice, ChoiceOption option)
        {
            Variables.Set("last_choice", option.Text);
            Pc = option.Target;
        }

        public void ResolveMinigame(MinigameInstruction game, bool won, double score)
        {
            Variables.Set("minigame_won", won);
            Variables.Set("minigame_score", score);
            if (game.Params.TryGetValue("as", out var name))
            {
                Variables.Set(name + "_won", won);
                Variables.Set(name + "_score", score);
            }
            int target = won ? game.WinTarget : game.LoseTarget;
            Pc = target >= 0 ? target : game.End;
        }

        public void SubmitInput(InputInstruction input, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) value = input.Default ?? "";
            Variables.Set(input.Variable, value.Trim());
            Pc = CurrentPc + 1;
        }

        public void SkipCurrent()
        {
            Pc = CurrentPc + 1;
        }
    }
}
