using System;
using System.Collections.Generic;

namespace VNEngine
{
    // Plain serializable classes so Unity's JsonUtility can write them.

    [Serializable]
    public sealed class SavedVariable
    {
        public string name;
        /// <summary>n = number, s = string, b = bool</summary>
        public string type;
        public string value;
    }

    [Serializable]
    public sealed class SavedCharacter
    {
        public string id;
        public float x, y, scale = 1f;
        public int zone = 1;
        public int layer;
        public bool facingRight = true;
        public string animation;
        public string emotion;
        public List<string> outfitSlots = new List<string>();
        public List<string> outfitLabels = new List<string>();
    }

    [Serializable]
    public sealed class StageSnapshot
    {
        public string background;
        public int zone = 1;
        public string music;
        public string ambience;
        public string weather;
        public string tint;
        public List<SavedCharacter> characters = new List<SavedCharacter>();
    }

    [Serializable]
    public sealed class HistoryEntry
    {
        public string speaker;
        public string text;
    }

    [Serializable]
    public sealed class SaveData
    {
        public string series;
        public string story;
        public int pc;
        public List<int> callStack = new List<int>();
        public List<SavedVariable> variables = new List<SavedVariable>();
        public StageSnapshot stage = new StageSnapshot();
        public List<HistoryEntry> history = new List<HistoryEntry>();
        public string savedAt;
        public bool finished;

        public void WriteVariables(VariableStore vars)
        {
            variables.Clear();
            foreach (var kv in vars.All)
            {
                var v = new SavedVariable { name = kv.Key };
                switch (kv.Value)
                {
                    case bool b: v.type = "b"; v.value = b ? "1" : "0"; break;
                    case double d: v.type = "n"; v.value = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture); break;
                    default: v.type = "s"; v.value = Expression.ToText(kv.Value); break;
                }
                variables.Add(v);
            }
        }

        public void ReadVariables(VariableStore vars)
        {
            vars.Clear();
            foreach (var v in variables)
            {
                switch (v.type)
                {
                    case "b": vars.Set(v.name, v.value == "1"); break;
                    case "n": vars.Set(v.name, Expression.ToNumber(v.value)); break;
                    default: vars.Set(v.name, v.value); break;
                }
            }
        }
    }
}
