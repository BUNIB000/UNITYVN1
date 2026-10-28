using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VNEngine
{
    /// <summary>
    /// Story variables. Names are case-insensitive. Values are double, string or bool.
    /// Reading a variable that was never set gives 0 (which also counts as false).
    /// </summary>
    public sealed class VariableStore
    {
        readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public event Action<string, object> Changed;

        public IEnumerable<KeyValuePair<string, object>> All => values;

        public bool Has(string name) => values.ContainsKey(name);

        public object Get(string name) => values.TryGetValue(name, out var v) ? v : 0.0;

        public void Set(string name, object value)
        {
            if (value is int i) value = (double)i;
            if (value is float f) value = (double)f;
            values[name] = value;
            Changed?.Invoke(name, value);
        }

        public double GetNumber(string name) => Expression.ToNumber(Get(name));
        public bool GetBool(string name) => Expression.ToBool(Get(name));
        public string GetString(string name) => Expression.ToText(Get(name));

        public void Clear() => values.Clear();

        /// <summary>Replaces {name} with the variable's value. {{ gives a literal brace.</summary>
        public string Interpolate(string text, Func<string, string> resolver = null)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{' && i + 1 < text.Length && text[i + 1] == '{') { sb.Append('{'); i++; continue; }
                if (c == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close > i)
                    {
                        string name = text.Substring(i + 1, close - i - 1).Trim();
                        string resolved = resolver?.Invoke(name);
                        sb.Append(resolved ?? Expression.ToText(Get(name)));
                        i = close;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// A small expression language for conditions and 'set':
    /// numbers, "strings", true/false, variables, + - * / %, comparisons, and/or/not, parentheses.
    /// </summary>
    public abstract class Expression
    {
        public string Source;

        public abstract object Evaluate(VariableStore vars);

        public bool IsTrue(VariableStore vars) => ToBool(Evaluate(vars));

        public override string ToString() => Source;

        public static Expression Parse(string source)
        {
            var p = new Parser(source);
            var e = p.ParseOr();
            p.SkipSpace();
            if (!p.AtEnd) throw new FormatException($"Unexpected '{source.Substring(p.Pos)}' in '{source}'");
            e.Source = source;
            return e;
        }

        public static double ToNumber(object v)
        {
            switch (v)
            {
                case null: return 0;
                case double d: return d;
                case bool b: return b ? 1 : 0;
                case string s: return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
                default: return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
        }

        public static bool ToBool(object v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case double d: return Math.Abs(d) > 1e-9;
                case string s: return s.Length > 0 && !s.Equals("false", StringComparison.OrdinalIgnoreCase) && s != "0";
                default: return true;
            }
        }

        public static string ToText(object v)
        {
            switch (v)
            {
                case null: return "";
                case double d: return Util.FormatNumber(d);
                case bool b: return b ? "true" : "false";
                default: return v.ToString();
            }
        }

        sealed class Literal : Expression
        {
            public object Value;
            public override object Evaluate(VariableStore vars) => Value;
        }

        sealed class Variable : Expression
        {
            public string Name;
            public override object Evaluate(VariableStore vars) => vars.Get(Name);
        }

        sealed class Unary : Expression
        {
            public string Op;
            public Expression Operand;
            public override object Evaluate(VariableStore vars)
            {
                var v = Operand.Evaluate(vars);
                return Op == "-" ? (object)(-ToNumber(v)) : !ToBool(v);
            }
        }

        sealed class Binary : Expression
        {
            public string Op;
            public Expression Left, Right;

            public override object Evaluate(VariableStore vars)
            {
                if (Op == "and") return ToBool(Left.Evaluate(vars)) && ToBool(Right.Evaluate(vars));
                if (Op == "or") return ToBool(Left.Evaluate(vars)) || ToBool(Right.Evaluate(vars));
                var a = Left.Evaluate(vars);
                var b = Right.Evaluate(vars);
                switch (Op)
                {
                    case "+":
                        if (a is string || b is string) return ToText(a) + ToText(b);
                        return ToNumber(a) + ToNumber(b);
                    case "-": return ToNumber(a) - ToNumber(b);
                    case "*": return ToNumber(a) * ToNumber(b);
                    case "/": { var d = ToNumber(b); return Math.Abs(d) < 1e-12 ? 0.0 : ToNumber(a) / d; }
                    case "%": { var d = ToNumber(b); return Math.Abs(d) < 1e-12 ? 0.0 : ToNumber(a) % d; }
                    case "==": return AreEqual(a, b);
                    case "!=": return !AreEqual(a, b);
                    case "<": return ToNumber(a) < ToNumber(b);
                    case "<=": return ToNumber(a) <= ToNumber(b);
                    case ">": return ToNumber(a) > ToNumber(b);
                    case ">=": return ToNumber(a) >= ToNumber(b);
                }
                throw new InvalidOperationException("Unknown operator " + Op);
            }

            static bool AreEqual(object a, object b)
            {
                if (a is string || b is string) return string.Equals(ToText(a), ToText(b), StringComparison.OrdinalIgnoreCase);
                if (a is bool || b is bool) return ToBool(a) == ToBool(b);
                return Math.Abs(ToNumber(a) - ToNumber(b)) < 1e-9;
            }
        }

        sealed class Parser
        {
            readonly string s;
            public int Pos;
            public Parser(string s) { this.s = s; }
            public bool AtEnd => Pos >= s.Length;

            public void SkipSpace() { while (Pos < s.Length && char.IsWhiteSpace(s[Pos])) Pos++; }

            bool Match(string token, bool word = false)
            {
                SkipSpace();
                if (string.Compare(s, Pos, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
                if (word && Pos + token.Length < s.Length && (char.IsLetterOrDigit(s[Pos + token.Length]) || s[Pos + token.Length] == '_')) return false;
                Pos += token.Length;
                return true;
            }

            public Expression ParseOr()
            {
                var left = ParseAnd();
                while (Match("or", true) || Match("||")) left = new Binary { Op = "or", Left = left, Right = ParseAnd() };
                return left;
            }

            Expression ParseAnd()
            {
                var left = ParseNot();
                while (Match("and", true) || Match("&&")) left = new Binary { Op = "and", Left = left, Right = ParseNot() };
                return left;
            }

            Expression ParseNot()
            {
                if (Match("not", true)) return new Unary { Op = "not", Operand = ParseNot() };
                SkipSpace();
                if (Pos < s.Length && s[Pos] == '!' && (Pos + 1 >= s.Length || s[Pos + 1] != '='))
                {
                    Pos++;
                    return new Unary { Op = "not", Operand = ParseNot() };
                }
                return ParseCompare();
            }

            static readonly string[] CompareOps = { "==", "!=", "<=", ">=", "<", ">", "=" };

            Expression ParseCompare()
            {
                var left = ParseAdd();
                foreach (var op in CompareOps)
                {
                    if (Match(op))
                        return new Binary { Op = op == "=" ? "==" : op, Left = left, Right = ParseAdd() };
                }
                if (Match("is not", true)) return new Binary { Op = "!=", Left = left, Right = ParseAdd() };
                if (Match("is", true)) return new Binary { Op = "==", Left = left, Right = ParseAdd() };
                return left;
            }

            Expression ParseAdd()
            {
                var left = ParseMul();
                while (true)
                {
                    if (Match("+")) left = new Binary { Op = "+", Left = left, Right = ParseMul() };
                    else if (Match("-")) left = new Binary { Op = "-", Left = left, Right = ParseMul() };
                    else return left;
                }
            }

            Expression ParseMul()
            {
                var left = ParseUnary();
                while (true)
                {
                    if (Match("*")) left = new Binary { Op = "*", Left = left, Right = ParseUnary() };
                    else if (Match("/")) left = new Binary { Op = "/", Left = left, Right = ParseUnary() };
                    else if (Match("%")) left = new Binary { Op = "%", Left = left, Right = ParseUnary() };
                    else return left;
                }
            }

            Expression ParseUnary()
            {
                if (Match("-")) return new Unary { Op = "-", Operand = ParseUnary() };
                return ParseAtom();
            }

            Expression ParseAtom()
            {
                SkipSpace();
                if (AtEnd) throw new FormatException($"Expression '{s}' ends too early");
                char c = s[Pos];
                if (c == '(')
                {
                    Pos++;
                    var e = ParseOr();
                    if (!Match(")")) throw new FormatException($"Missing ')' in '{s}'");
                    return e;
                }
                if (c == '"' || c == '\'')
                {
                    int end = s.IndexOf(c, Pos + 1);
                    if (end < 0) throw new FormatException($"Missing closing quote in '{s}'");
                    var lit = new Literal { Value = s.Substring(Pos + 1, end - Pos - 1) };
                    Pos = end + 1;
                    return lit;
                }
                if (char.IsDigit(c) || c == '.')
                {
                    int start = Pos;
                    while (Pos < s.Length && (char.IsDigit(s[Pos]) || s[Pos] == '.')) Pos++;
                    return new Literal { Value = double.Parse(s.Substring(start, Pos - start), CultureInfo.InvariantCulture) };
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = Pos;
                    while (Pos < s.Length && (char.IsLetterOrDigit(s[Pos]) || s[Pos] == '_' || s[Pos] == '.')) Pos++;
                    string name = s.Substring(start, Pos - start);
                    if (name.Equals("true", StringComparison.OrdinalIgnoreCase)) return new Literal { Value = true };
                    if (name.Equals("false", StringComparison.OrdinalIgnoreCase)) return new Literal { Value = false };
                    return new Variable { Name = name };
                }
                throw new FormatException($"Unexpected '{c}' in '{s}'");
            }
        }
    }
}
