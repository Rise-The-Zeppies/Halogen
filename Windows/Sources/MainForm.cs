using System.Text;

namespace HalogenWindows;

internal sealed class MainForm : Form
{
    private readonly RichTextBox editor = new();
    private readonly RichTextBox console = new();
    private string? currentFile;

    public MainForm()
    {
        Text = "Halogen";
        Width = 1180;
        Height = 760;
        MinimumSize = new Size(800, 520);
        BackColor = Color.FromArgb(30, 33, 42);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(6), BackColor = Color.FromArgb(38, 42, 53) };
        AddButton(toolbar, "New", (_, _) => { editor.Clear(); currentFile = null; });
        AddButton(toolbar, "Open .hg", (_, _) => OpenSource());
        AddButton(toolbar, "Save", (_, _) => SaveSource(false));
        AddButton(toolbar, "Save As", (_, _) => SaveSource(true));
        AddButton(toolbar, "▶ Run", (_, _) => RunSource());

        editor.Dock = DockStyle.Fill;
        editor.Font = new Font("Consolas", 12);
        editor.AcceptsTab = true;
        editor.WordWrap = false;
        editor.BackColor = Color.FromArgb(20, 23, 31);
        editor.ForeColor = Color.FromArgb(225, 232, 241);
        editor.Text = "#/ Welcome to Halogen! \\#\nwrite(\"Hello, world!\")\nvar(\"score\") set 100\nscore get\nrpt 3:\n    write(\"Again!\")\nend\n";

        console.Dock = DockStyle.Fill;
        console.ReadOnly = true;
        console.Font = new Font("Consolas", 10);
        console.BackColor = Color.FromArgb(16, 19, 25);
        console.ForeColor = Color.FromArgb(145, 235, 174);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 480, BackColor = Color.FromArgb(48, 53, 65) };
        split.Panel1.Controls.Add(editor);
        split.Panel2.Controls.Add(console);
        Controls.Add(split);
        Controls.Add(toolbar);
        RunSource();
    }

    private static void AddButton(Control parent, string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(55, 63, 78), ForeColor = Color.White };
        button.Click += click;
        parent.Controls.Add(button);
    }

    private void OpenSource()
    {
        using var dialog = new OpenFileDialog { Filter = "Halogen source (*.hg)|*.hg|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { editor.Text = File.ReadAllText(dialog.FileName, Encoding.UTF8); currentFile = dialog.FileName; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open failed"); }
    }

    private void SaveSource(bool saveAs)
    {
        if (saveAs || string.IsNullOrWhiteSpace(currentFile))
        {
            using var dialog = new SaveFileDialog { Filter = "Halogen source (*.hg)|*.hg", DefaultExt = "hg", AddExtension = true, FileName = "untitled.hg" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            currentFile = dialog.FileName;
        }
        try { File.WriteAllText(currentFile!, editor.Text, new UTF8Encoding(false)); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save failed"); }
    }

    private void RunSource()
    {
        console.Clear();
        try { new HalogenInterpreter(s => console.AppendText(s + Environment.NewLine), prompt => Prompt(prompt)).Run(editor.Text); }
        catch (Exception ex) { console.AppendText("ERROR: " + ex.Message + Environment.NewLine); }
    }

    private string Prompt(string prompt)
    {
        using var dialog = new Form { Text = "Halogen ask()", Width = 440, Height = 160, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog };
        var label = new Label { Text = prompt, Left = 12, Top = 12, Width = 390, Height = 30 };
        var input = new TextBox { Left = 12, Top = 48, Width = 390 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 326, Top = 82, Width = 76 };
        dialog.Controls.AddRange(new Control[] { label, input, ok });
        dialog.AcceptButton = ok;
        return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : "";
    }
}
