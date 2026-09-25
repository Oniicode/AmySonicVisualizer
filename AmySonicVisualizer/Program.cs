namespace AmySonicVisualizer;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetColorMode(SystemColorMode.System);
        ApplicationConfiguration.Initialize();

        string? initialFilePath = null;
        if (args.Length > 0)
            initialFilePath = args[0];

        Application.Run(new ViewerForm(initialFilePath));
    }

    /// <summary>
    /// Generalized file prompting to be usable at startup and via runtime menus.
    /// </summary>
    public static string? PromptOpenFile()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Audio Files (*.mp3;*.wav;*.flac)|*.mp3;*.wav;*.flac",
            Title = "Select Audio File to Analyze"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            return ofd.FileName;
        }

        return null;
    }
}