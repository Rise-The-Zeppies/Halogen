using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HalogenWindows;

internal sealed class HalogenUIWindow : Form
{
    private enum RepeatUnit { Frames, Seconds, Minutes, Hours }
    private sealed record RepeatCondition(RepeatUnit Unit, double Amount);
    private sealed record DrawCommand(string Kind, string Text, float X, float Y, float W, float H,
        string Color, IReadOnlyList<RepeatCondition> Conditions);

    private readonly List<DrawCommand> commands = new();
    private readonly Panel canvas = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly DateTime startedAt = DateTime.UtcNow;
    private int frameNumber;
    private string background = "#101820";

    public static bool ContainsShow(string source) =>
        Regex.IsMatch(source, @"(?im)^\s*ui\.show\(\)\s*$");

    public HalogenUIWindow(string source)
    {
        Parse(source);
        Text = "HalogenUI";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(320, 240);
        BackColor = Color.FromArgb(16, 24, 32);

        var titleMatch = Regex.Match(source, """\(title:\s*["'](?<title>.*?)["']\)""", RegexOptions.IgnoreCase);
        if (titleMatch.Success) Text = titleMatch.Groups["title"].Value;

        var sizeMatch = Regex.Match(source, """\(size:\s*(?<w>\d+)\s*x\s*(?<h>\d+)\)""", RegexOptions.IgnoreCase);
        if (sizeMatch.Success &&
            int.TryParse(sizeMatch.Groups["w"].Value, out int width) &&
            int.TryParse(sizeMatch.Groups["h"].Value, out int height))
        {
            ClientSize = new Size(Math.Clamp(width, 320, 4096), Math.Clamp(height, 240, 4096));
        }

        var iconMatch = Regex.Match(source, """\(icon\?\s*=\s*T\s*["'](?<path>.*?)["']\)""", RegexOptions.IgnoreCase);
        if (iconMatch.Success)
        {
            var iconPath = Environment.ExpandEnvironmentVariables(iconMatch.Groups["path"].Value);
            iconPath = iconPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (Path.GetExtension(iconPath).Equals(".ico", StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { /* Ignore invalid optional icons. */ }
            }
        }

        canvas.Dock = DockStyle.Fill;
        canvas.BackColor = ParseColor(background);
        canvas.Paint += PaintCanvas;
        canvas.Resize += (_, _) => canvas.Invalidate();
        Controls.Add(canvas);
        timer.Tick += (_, _) =>
        {
            frameNumber++;
            canvas.Invalidate();
        };
        timer.Start();
        FormClosed += (_, _) => timer.Stop();
    }

    private void Parse(string source)
    {
        source = Regex.Replace(source, """(?s)#/.*?(?:\\#|$)""", "");
        var lines = source.Replace("\r", "").Split('\n');
        var repeatStack = new List<RepeatCondition>();
        bool inDeclaration = false;
        bool declarationEndFollows = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("ui.declare", StringComparison.OrdinalIgnoreCase) && line.Contains('['))
            {
                inDeclaration = !line.Contains(']');
                declarationEndFollows = !inDeclaration;
                continue;
            }

            if (inDeclaration)
            {
                if (line.Contains(']'))
                {
                    inDeclaration = false;
                    declarationEndFollows = true;
                }
                continue;
            }

            if (declarationEndFollows && line == "end")
            {
                declarationEndFollows = false;
                continue;
            }
            declarationEndFollows = false;

            if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                if (repeatStack.Count > 0) repeatStack.RemoveAt(repeatStack.Count - 1);
                continue;
            }

            var repeat = Regex.Match(line, @"^ui\.(framerpt|secrpt|minrpt|hrsrpt)\s+(?<n>\d+(?:\.\d+)?)\s*:?$", RegexOptions.IgnoreCase);
            if (repeat.Success)
            {
                string kind = repeat.Groups[1].Value.ToLowerInvariant();
                double amount = double.Parse(repeat.Groups["n"].Value, CultureInfo.InvariantCulture);
                if (kind == "framerpt" && amount % 1 != 0)
                    throw new FormatException($"Line {i + 1}: ui.framerpt requires a whole number.");
                var unit = kind switch
                {
                    "framerpt" => RepeatUnit.Frames,
                    "secrpt" => RepeatUnit.Seconds,
                    "minrpt" => RepeatUnit.Minutes,
                    _ => RepeatUnit.Hours
                };
                repeatStack.Add(new RepeatCondition(unit, amount));
                continue;
            }

            var conditions = repeatStack.ToArray();
            if (line.Equals("ui.show()", StringComparison.OrdinalIgnoreCase)) continue;

            var match = Regex.Match(line, """^ui\.background\(\s*["'](?<c>[^"']+)["']\s*\)$""", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                commands.Add(new DrawCommand("background", "", 0, 0, 0, 0, match.Groups["c"].Value, conditions));
                continue;
            }

            match = Regex.Match(line, """^ui\.rect\((?<args>.*)\)$""", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var a = SplitArgs(match.Groups["args"].Value);
                if (a.Count != 5) throw new FormatException($"Line {i + 1}: ui.rect expects x, y, width, height, color.");
                commands.Add(new DrawCommand("rect", "", Number(a[0]), Number(a[1]), Number(a[2]), Number(a[3]), Unquote(a[4]), conditions));
                continue;
            }

            match = Regex.Match(line, """^ui\.circle\((?<args>.*)\)$""", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var a = SplitArgs(match.Groups["args"].Value);
                if (a.Count != 4) throw new FormatException($"Line {i + 1}: ui.circle expects x, y, diameter, color.");
                commands.Add(new DrawCommand("circle", "", Number(a[0]), Number(a[1]), Number(a[2]), 0, Unquote(a[3]), conditions));
                continue;
            }

            match = Regex.Match(line, """^ui\.text\((?<args>.*)\)$""", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var a = SplitArgs(match.Groups["args"].Value);
                if (a.Count != 4) throw new FormatException($"Line {i + 1}: ui.text expects text, x, y, color.");
                commands.Add(new DrawCommand("text", Unquote(a[0]), Number(a[1]), Number(a[2]), 0, 0, Unquote(a[3]), conditions));
            }
        }
    }

    private void PaintCanvas(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ParseColor(background));
        double elapsed = (DateTime.UtcNow - startedAt).TotalSeconds;
        foreach (var command in commands)
        {
            if (command.Conditions.Any(condition => !IsActive(condition, elapsed))) continue;
            using var brush = new SolidBrush(ParseColor(command.Color));
            switch (command.Kind)
            {
                case "background":
                    e.Graphics.Clear(ParseColor(command.Color));
                    break;
                case "rect":
                    e.Graphics.FillRectangle(brush, command.X, command.Y, Math.Max(0, command.W), Math.Max(0, command.H));
                    break;
                case "circle":
                    e.Graphics.FillEllipse(brush, command.X, command.Y, Math.Max(0, command.W), Math.Max(0, command.W));
                    break;
                case "text":
                    using (var font = new Font("Segoe UI", 18, FontStyle.Regular, GraphicsUnit.Pixel))
                        e.Graphics.DrawString(command.Text, font, brush, command.X, command.Y);
                    break;
            }
        }
    }

    private bool IsActive(RepeatCondition condition, double elapsed) => condition.Unit switch
    {
        RepeatUnit.Frames => frameNumber < condition.Amount,
        RepeatUnit.Seconds => elapsed < condition.Amount,
        RepeatUnit.Minutes => elapsed < condition.Amount * 60,
        RepeatUnit.Hours => elapsed < condition.Amount * 3600,
        _ => false
    };

    private static Color ParseColor(string value)
    {
        try { return ColorTranslator.FromHtml(value); }
        catch { return Color.White; }
    }

    private static float Number(string value)
    {
        value = value.Trim();
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !float.IsFinite(number))
            throw new FormatException($"Expected a numeric UI coordinate, got '{value}'.");
        return number;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];
        return value;
    }

    private static List<string> SplitArgs(string source)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        char quote = '\0';
        foreach (char ch in source)
        {
            if (quote != '\0')
            {
                current.Append(ch);
                if (ch == quote) quote = '\0';
            }
            else if (ch == '"' || ch == '\'')
            {
                quote = ch;
                current.Append(ch);
            }
            else if (ch == ',')
            {
                args.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0) args.Add(current.ToString().Trim());
        return args;
    }
}
