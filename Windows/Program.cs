namespace HalogenWindows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // The exporter puts Main.hg beside its EXE. In that folder, launching
        // the EXE directly runs the bundled UI rather than opening the editor.
        string? explicitUiProject = args.Length >= 2 && args[0].Equals("--ui", StringComparison.OrdinalIgnoreCase)
            ? args[1]
            : null;
        if (!string.IsNullOrWhiteSpace(explicitUiProject) && File.Exists(explicitUiProject))
        {
            Application.Run(new HalogenUIWindow(File.ReadAllText(explicitUiProject)));
            return;
        }

        string bundledProject = Path.Combine(AppContext.BaseDirectory, "Main.hg");
        if (args.Length == 0 && File.Exists(bundledProject))
        {
            Application.Run(new HalogenUIWindow(File.ReadAllText(bundledProject)));
            return;
        }

        string? initialFile = args.FirstOrDefault(path =>
            Path.GetExtension(path).Equals(".hg", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(path));
        Application.Run(new MainForm(initialFile));
    }
}
