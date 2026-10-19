using System.Globalization;
using System.Text.RegularExpressions;

namespace HalogenWindows;

internal sealed class HalogenInterpreter(Action<string> write, Func<string, string> ask)
{
    private readonly Dictionary<string, object> vars = new(StringComparer.OrdinalIgnoreCase);

    public void Run(string source)
    {
        source = Regex.Replace(source, @"#/[\\s\\S]*?\\#|#/[\\s\\S]*$", m => new string(' ', m.Length));
        Execute(source.Replace("\r", "").Split('\n').ToList(), 0, null);
    }

    private int Execute(List<string> lines, int start, string? stop)
    {
        for (int i = start; i < lines.Count; i++)
        {
            string s = lines[i].Trim();
            if (s.Length == 0) continue;
            if (s == "end" || s == "else" || s.StartsWith("elif ")) return i;
            var m = Regex.Match(s, @"^rpt\\s+(.+?):?$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                int end = FindEnd(lines, i + 1);
                int count = Convert.ToInt32(Eval(m.Groups[1].Value), CultureInfo.InvariantCulture);
                if (count < 0 || count > 10000) throw new Exception($"Line {i + 1}: rpt count must be 0–10000.");
                for (int n = 0; n < count; n++) Execute(lines.GetRange(i + 1, end - i - 1), 0, null);
                i = end;
                continue;
            }
            m = Regex.Match(s, @"^if\\s+(.+?):?$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                int end = FindEnd(lines, i + 1);
                int branch = -1, elseAt = -1, depth = 0;
                for (int j = i + 1; j < end; j++)
                {
                    string t = lines[j].Trim();
                    if (t.StartsWith("if ") || t.StartsWith("rpt ")) depth++;
                    if (t == "end") depth--;
                    if (depth == 0 && (t == "else" || t.StartsWith("elif "))) { if (elseAt < 0) elseAt = j; }
                }
                if (Truthy(Eval(m.Groups[1].Value)))
                {
                    int stopAt = elseAt < 0 ? end : elseAt;
                    Execute(lines.GetRange(i + 1, stopAt - i - 1), 0, null);
                }
                else if (elseAt >= 0 && lines[elseAt].Trim() == "else")
                    Execute(lines.GetRange(elseAt + 1, end - elseAt - 1), 0, null);
                i = end;
                continue;
            }
            if (s.StartsWith("def ")) { i = FindEnd(lines, i + 1); continue; }
            m = Regex.Match(s, @"^var\\(\\s*[\""'](?<n>[^\\"']+)[\\"']\\s*\\)\\s+set\\s+(.+)$", RegexOptions.IgnoreCase);
            if (m.Success) { vars[m.Groups["n"].Value] = Eval(m.Groups[2].Value); continue; }
            m = Regex.Match(s, @"^str\\(\\s*[\""'](?<n>[^\\"']+)[\\"']\\s*\\)\\s*=\\s*(.*)$", RegexOptions.IgnoreCase);
            if (m.Success) { vars[m.Groups["n"].Value] = EvalText(m.Groups[2].Value); continue; }
            m = Regex.Match(s, @"^bool\\(\\s*[\""'](?<n>[^\\"']+)[\\"']\\s*\\)\\s*=\\s*(T|t|F|f)$", RegexOptions.IgnoreCase);
            if (m.Success) { vars[m.Groups["n"].Value] = m.Groups[2].Value.Equals("T", StringComparison.OrdinalIgnoreCase); continue; }
            m = Regex.Match(s, @"^(?<n>[A-Za-z_]\\w*)\\s+get$");
            if (m.Success) { if (!vars.TryGetValue(m.Groups["n"].Value, out var v)) throw new Exception($"Line {i + 1}: unknown variable."); write(Show(v)); continue; }
            m = Regex.Match(s, @"^write\\((.*)\\)$", RegexOptions.IgnoreCase);
            if (m.Success) { write(Show(Eval(m.Groups[1].Value))); continue; }
            m = Regex.Match(s, @"^ask\\((.*)\\)$", RegexOptions.IgnoreCase);
            if (m.Success) { ask(EvalText(m.Groups[1].Value)); continue; }
            if (s == "else" || s.StartsWith("elif ")) throw new Exception($"Line {i + 1}: unmatched {s}.");
            throw new Exception($"Line {i + 1}: unsupported syntax: {s}");
        }
        return lines.Count;
    }

    private static int FindEnd(List<string> lines, int start)
    {
        int depth = 1;
        for (int i = start; i < lines.Count; i++)
        {
            string s = lines[i].Trim();
            if (s.StartsWith("if ") || s.StartsWith("rpt ") || s.StartsWith("def ")) depth++;
            if (s == "end" && --depth == 0) return i;
        }
        throw new Exception("Block is missing 'end'.");
    }

    private object Eval(string expression)
    {
        string s = expression.Trim();
        if (s.StartsWith("ask(", StringComparison.OrdinalIgnoreCase) && s.EndsWith(')')) return ask(EvalText(s[4..^1]));
        if ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'"))) return s[1..^1];
        if (s.Equals("T", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Equals("F", StringComparison.OrdinalIgnoreCase)) return false;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)) return n;
        if (vars.TryGetValue(s, out var v)) return v;
        foreach (string op in new[] { "==", "!=", ">=", "<=", ">", "<" })
        {
            int at = s.IndexOf(op, StringComparison.Ordinal);
            if (at > 0)
            {
                object a = Eval(s[..at]), b = Eval(s[(at + op.Length)..]);
                if (double.TryParse(Show(a), out double x) && double.TryParse(Show(b), out double y))
                    return op switch { "==" => x == y, "!=" => x != y, ">" => x > y, "<" => x < y, ">=" => x >= y, "<=" => x <= y, _ => false };
                return op switch { "==" => Show(a) == Show(b), "!=" => Show(a) != Show(b), _ => false };
            }
        }
        if (s.Contains('+')) { var p = s.Split('+', 2); return Show(Eval(p[0])) + Show(Eval(p[1])); }
        if (s.Contains('*')) { var p = s.Split('*', 2); return Convert.ToDouble(Eval(p[0]), CultureInfo.InvariantCulture) * Convert.ToDouble(Eval(p[1]), CultureInfo.InvariantCulture); }
        if (s.Contains('/')) { var p = s.Split('/', 2); double b = Convert.ToDouble(Eval(p[1]), CultureInfo.InvariantCulture); if (b == 0) throw new Exception("Division by zero."); return Convert.ToDouble(Eval(p[0]), CultureInfo.InvariantCulture) / b; }
        throw new Exception($"Unknown value '{s}'.");
    }

    private string EvalText(string s) => Show(Eval(s));
    private static bool Truthy(object v) => v is bool b ? b : v is double d ? d != 0 : bool.TryParse(Show(v), out var r) && r;
    private static string Show(object v) => v is bool b ? (b ? "T" : "F") : Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
}
