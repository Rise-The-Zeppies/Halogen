using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HalogenWindows;

internal sealed class HalogenUIWindow : Form
{
    private enum RepeatUnit { Frames, Seconds, Minutes, Hours }
    private sealed record RepeatCondition(RepeatUnit Unit, double Amount);
    private sealed record DrawCommand(
        string Kind, string Text, float X, float Y, float W, float H,
        string Color, IReadOnlyList<RepeatCondition> Conditions);
    private sealed record ParsedProgram(
        string Title, int Width, int Height, string? IconPath, bool ShowsUI,
        IReadOnlyList<DrawCommand> Commands);
    private sealed class BufferedCanvas : Panel
    {
        public BufferedCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private readonly BufferedCanvas canvas = new();
    private readonly System.Windows.Forms.Timer renderTimer = new() { Interval = 16 };
    private IReadOnlyList<DrawCommand> commands = Array.Empty<DrawCommand>();
    private DateTime startedAt = DateTime.UtcNow;
    private int frameNumber;
    private bool showsUI;

    public static bool ContainsUI(string source)
    {
        string cleaned = RemoveComments(source);
        return Regex.IsMatch(
            cleaned,
            @"(?im)^\s*ui\.(?:declare\b|background\s*\(|rect\s*\(|circle\s*\(|text\s*\(|show\s*\(\s*\)|framerpt\b|secrpt\b|minrpt\b|hrsrpt\b)");
    }

    public HalogenUIWindow(string source)
    {
        Text = "HalogenUI";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(320, 240);
        BackColor = Color.FromArgb(30, 33, 42);

        canvas.Dock = DockStyle.Fill;
        canvas.Paint += PaintCanvas;
        Controls.Add(canvas);

        renderTimer.Tick += (_, _) =>
        {
            frameNumber++;
            canvas.Invalidate();
        };
        FormClosed += (_, _) =>
        {
            renderTimer.Stop();
            renderTimer.Dispose();
        };

        LoadSource(source);
        renderTimer.Start();
    }

    public void LoadSource(string source)
    {
        // Parse into a temporary object first so a bad edit doesn't destroy the
        // last working preview.
        ParsedProgram parsed = ParseSource(source);

        Text = parsed.Title;
        ClientSize = new Size(parsed.Width, parsed.Height);
        ApplyIcon(parsed.IconPath);
        commands = parsed.Commands;
        showsUI = parsed.ShowsUI;
        startedAt = DateTime.UtcNow;
        frameNumber = 0;
        canvas.Invalidate();
    }

    private static ParsedProgram ParseSource(string source)
    {
        string cleaned = RemoveComments(source);
        string title = "HalogenUI";
        double width = 900;
        double height = 620;
        string? iconPath = null;
        bool showsUI = false;

        var parsedCommands = new List<DrawCommand>();
        var repeatStack = new List<(RepeatCondition Condition, int Line)>();
        var lines = cleaned.Replace("\r", "").Split('\n');
        bool inDeclaration = false;
        bool declarationEndFollows = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("ui.declare", StringComparison.OrdinalIgnoreCase) && line.Contains('['))
            {
                ParseOptions(line, ref title, ref width, ref height, ref iconPath);
                inDeclaration = !line.Contains(']');
                declarationEndFollows = !inDeclaration;
                continue;
            }

            if (inDeclaration)
            {
                ParseOptions(line, ref title, ref width, ref height, ref iconPath);
                if (line.Contains(']'))
                {
                    inDeclaration = false;
                    declarationEndFollows = true;
                }
                continue;
            }

            if (declarationEndFollows && line.Equals("end", StringComparison.OrdinalIgnoreCase))
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

            var repeat = Regex.Match(
                line,
                """^ui\.(framerpt|secrpt|minrpt|hrsrpt)\s+(?<n>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*:?$""",
                RegexOptions.IgnoreCase);
            if (repeat.Success)
            {
                string kind = repeat.Groups[1].Value.ToLowerInvariant();
                double amount = double.Parse(repeat.Groups["n"].Value, CultureInfo.InvariantCulture);
                if (!double.IsFinite(amount) || amount < 0 || amount > 31_536_000 ||
                    (kind == "framerpt" && amount != Math.Truncate(amount)))
                {
                    throw new FormatException($"Line {i + 1}: ui.{kind} requires a valid non-negative duration; framerpt requires a whole frame count.");
                }

                var unit = kind switch
                {
                    "framerpt" => RepeatUnit.Frames,
                    "secrpt" => RepeatUnit.Seconds,
                    "minrpt" => RepeatUnit.Minutes,
                    _ => RepeatUnit.Hours
                };
                repeatStack.Add((new RepeatCondition(unit, amount), i + 1));
                continue;
            }

            if (line.StartsWith("ui.framerpt", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("ui.secrpt", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("ui.minrpt", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("ui.hrsrpt", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"Line {i + 1}: invalid HalogenUI repetition command.");
            }

            var conditions = repeatStack.Select(item => item.Condition).ToArray();

            if (line.Equals("ui.show()", StringComparison.OrdinalIgnoreCase))
            {
                showsUI = true;
                continue;
            }

            if (TryCommandArguments(line, "ui.background", out var args))
            {
                if (args.Count != 1) throw new FormatException($"Line {i + 1}: ui.background expects one color.");
                parsedCommands.Add(new DrawCommand("background", "", 0, 0, 0, 0, Unquote(args[0]), conditions));
                continue;
            }

            if (TryCommandArguments(line, "ui.rect", out args))
            {
                if (args.Count != 5) throw new FormatException($"Line {i + 1}: ui.rect expects x, y, width, height, color.");
                parsedCommands.Add(new DrawCommand("rect", "", Number(args[0], i + 1), Number(args[1], i + 1),
                    Number(args[2], i + 1), Number(args[3], i + 1), Unquote(args[4]), conditions));
                continue;
            }

            if (TryCommandArguments(line, "ui.circle", out args))
            {
                if (args.Count != 4) throw new FormatException($"Line {i + 1}: ui.circle expects x, y, diameter, color.");
                parsedCommands.Add(new DrawCommand("circle", "", Number(args[0], i + 1), Number(args[1], i + 1),
                    Number(args[2], i + 1), 0, Unquote(args[3]), conditions));
                continue;
            }

            if (TryCommandArguments(line, "ui.text", out args))
            {
                if (args.Count != 4) throw new FormatException($"Line {i + 1}: ui.text expects text, x, y, color.");
                parsedCommands.Add(new DrawCommand("text", Unquote(args[0]), Number(args[1], i + 1),
                    Number(args[2], i + 1), 0, 0, Unquote(args[3]), conditions));
            }
            // As in the macOS preview, ordinary Halogen statements are not
            // interpreted by this renderer-only layer yet.
        }

        if (inDeclaration)
            throw new FormatException("HalogenUI window declaration is missing ']'.");
        if (repeatStack.Count > 0)
            throw new FormatException($"Line {repeatStack[0].Line}: repetition block is missing 'end'.");

        return new ParsedProgram(
            title,
            Math.Clamp((int)Math.Round(width), 320, 4096),
            Math.Clamp((int)Math.Round(height), 240, 4096),
            iconPath,
            showsUI,
            parsedCommands);
    }

    private static void ParseOptions(
        string line, ref string title, ref double width, ref double height, ref string? iconPath)
    {
        foreach (Match match in Regex.Matches(line, """\((?<item>[^()]*)\)"""))
        {
            string item = match.Groups["item"].Value.Trim();

            Match option = Regex.Match(item, """^title:\s*["'](?<value>.*?)["']\s*$""", RegexOptions.IgnoreCase);
            if (option.Success)
            {
                title = option.Groups["value"].Value;
                continue;
            }

            option = Regex.Match(item,
                """^size:\s*(?<w>(?:\d+(?:\.\d*)?|\.\d+))\s*x\s*(?<h>(?:\d+(?:\.\d*)?|\.\d+))\s*$""",
                RegexOptions.IgnoreCase);
            if (option.Success &&
                double.TryParse(option.Groups["w"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedWidth) &&
                double.TryParse(option.Groups["h"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedHeight) &&
                double.IsFinite(parsedWidth) && double.IsFinite(parsedHeight) &&
                parsedWidth > 0 && parsedHeight > 0)
            {
                width = Math.Min(parsedWidth, 4096);
                height = Math.Min(parsedHeight, 4096);
                continue;
            }

            option = Regex.Match(item, """^icon\?\s*=\s*(?<enabled>T|F)\s*(?:["'](?<path>.*?)["'])?\s*$""",
                RegexOptions.IgnoreCase);
            if (option.Success)
            {
                iconPath = option.Groups["enabled"].Value.Equals("T", StringComparison.OrdinalIgnoreCase)
                    ? option.Groups["path"].Value
                    : null;
            }
        }
    }

    private void ApplyIcon(string? path)
    {
        Icon = null;
        if (string.IsNullOrWhiteSpace(path)) return;

        path = Environment.ExpandEnvironmentVariables(path.Trim());
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

        // Windows window icons use .ico. macOS uses .icns; that native format
        // is intentionally not passed into WinForms.
        if (!Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return;

        try { Icon = new Icon(path); }
        catch (Exception) { /* An optional icon must not prevent the UI from opening. */ }
    }

    private void PaintCanvas(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (!showsUI)
        {
            e.Graphics.Clear(Color.FromArgb(30, 33, 42));
            using var placeholderBrush = new SolidBrush(Color.FromArgb(220, 225, 235));
            using var placeholderFont = new Font("Segoe UI", 18, FontStyle.Regular, GraphicsUnit.Pixel);
            using var detailFont = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
            var bounds = canvas.ClientRectangle;
            var titleRect = new RectangleF(bounds.Left, bounds.Top + bounds.Height / 2f - 34, bounds.Width, 30);
            var detailRect = new RectangleF(bounds.Left + 20, bounds.Top + bounds.Height / 2f + 5, bounds.Width - 40, 54);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("No UI to show yet", placeholderFont, placeholderBrush, titleRect, format);
            e.Graphics.DrawString("Open a .hg file containing ui.show() and click Run.", detailFont,
                Brushes.LightGray, detailRect, format);
            return;
        }

        double elapsed = Math.Max(0, (DateTime.UtcNow - startedAt).TotalSeconds);
        var activeCommands = commands.Where(command =>
            command.Conditions.All(condition => IsActive(condition, elapsed))).ToList();

        // Background is a canvas property, not a drawing primitive. Resolve it
        // before drawing so a later background command never erases earlier
        // shapes. An expired timed background falls back to the default.
        string activeBackground = activeCommands.LastOrDefault(command => command.Kind == "background")?.Color ?? "#101820";
        e.Graphics.Clear(ParseColor(activeBackground));

        foreach (var command in activeCommands)
        {
            using var brush = new SolidBrush(ParseColor(command.Color));
            switch (command.Kind)
            {
                case "background":
                    break;
                case "rect":
                    e.Graphics.FillRectangle(brush, command.X, command.Y, Math.Max(0, command.W), Math.Max(0, command.H));
                    break;
                case "circle":
                    e.Graphics.FillEllipse(brush, command.X, command.Y, Math.Max(0, command.W), Math.Max(0, command.W));
                    break;
                case "text":
                    using (var font = new Font("Segoe UI", 24, FontStyle.Regular, GraphicsUnit.Pixel))
                        e.Graphics.DrawString(command.Text, font, brush, command.X, command.Y);
                    break;
            }
        }
    }

    private static bool IsActive(RepeatCondition condition, double elapsed) => condition.Unit switch
    {
        RepeatUnit.Frames => condition.Amount > 0, // frame limit is checked in the instance-aware overload below
        RepeatUnit.Seconds => elapsed < condition.Amount,
        RepeatUnit.Minutes => elapsed < condition.Amount * 60,
        RepeatUnit.Hours => elapsed < condition.Amount * 3600,
        _ => false
    };

    private bool IsActive(RepeatCondition condition, double elapsed, bool frameCheck = true) =>
        condition.Unit == RepeatUnit.Frames
            ? frameNumber < condition.Amount
            : IsActive(condition, elapsed);

    private static Color ParseColor(string value)
    {
        string cleaned = value.Trim().Replace("#", "");
        if (cleaned.Length != 6 || !uint.TryParse(cleaned, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return Color.White;
        return Color.FromArgb((int)((rgb >> 16) & 0xFF), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF));
    }

    private static float Number(string value, int line)
    {
        if (!float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !float.IsFinite(number))
            throw new FormatException($"Line {line}: expected a numeric UI coordinate, got '{value}'.");
        return number;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];
        return value;
    }

    private static bool TryCommandArguments(string line, string command, out List<string> args)
    {
        Match match = Regex.Match(line, "^" + Regex.Escape(command) + @"\((?<args>.*)\)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            args = new List<string>();
            return false;
        }

        args = SplitArgs(match.Groups["args"].Value);
        return true;
    }

    private static List<string> SplitArgs(string source)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        char quote = '\0';
        int parentheses = 0;
        bool escaped = false;

        foreach (char ch in source)
        {
            if (quote != '\0')
            {
                current.Append(ch);
                if (ch == quote && !escaped) quote = '\0';
                if (ch == '\\' && !escaped) escaped = true;
                else escaped = false;
            }
            else if (ch == '"' || ch == '\'')
            {
                quote = ch;
                current.Append(ch);
            }
            else if (ch == '(')
            {
                parentheses++;
                current.Append(ch);
            }
            else if (ch == ')')
            {
                parentheses--;
                current.Append(ch);
            }
            else if (ch == ',' && parentheses == 0)
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

    private static string RemoveComments(string source) =>
        Regex.Replace(source, """(?s)#/.*?(?:\\#|$)""", "");
}
