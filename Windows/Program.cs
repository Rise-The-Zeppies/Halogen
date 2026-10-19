namespace HalogenWindows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string? initialFile = args.FirstOrDefault(path =>
            Path.GetExtension(path).Equals(".hg", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(path));
        Application.Run(new MainForm(initialFile));
    }
}
