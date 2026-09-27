using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmySonicVisualizer;

internal static class Program
{
    private const string AppMutexId = "AmySonicVisualizer_Mutex_v1";
    private const string PipeName = "AmySonicVisualizer_IPC_Pipe_v1";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    // Expose the main form so the pipe server can invoke on its UI thread
    private static ViewerForm? MainForm { get; set; }

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Check if an instance is already running across the system
        using var mutex = new Mutex(true, AppMutexId, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // This is a secondary instance. Send arguments to the first instance and exit.
            string filePathToSend = args.Length > 0 ? args[0] : string.Empty;
            SendArgsToFirstInstance(filePathToSend);
            return;
        }

        Application.SetColorMode(SystemColorMode.System);
        ApplicationConfiguration.Initialize();

        MainForm = new ViewerForm();
        if (args.Length > 0)
            _ = MainForm.LoadFileAsync(args[0]);

        StartNamedPipeServer();

        Application.Run(MainForm);
    }

    /// <summary>
    /// Sends the command line arguments to the running primary instance.
    /// </summary>
    private static void SendArgsToFirstInstance(string arg)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            // Timeout quickly so the secondary instance closes instantly if there's an issue
            client.Connect(1000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(arg);
        }
        catch (Exception)
        {
            // Silently ignore if we can't connect (e.g., primary instance is hanging or closing down)
        }
    }

    /// <summary>
    /// Starts a background task in the primary instance to listen for incoming files from secondary instances.
    /// </summary>
    private static void StartNamedPipeServer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync();

                    using var reader = new StreamReader(server);
                    string? receivedArg = await reader.ReadLineAsync();

                    if (MainForm == null || MainForm.IsDisposed || !MainForm.IsHandleCreated)
                        continue;

                    MainForm.Invoke(new Action(async () =>
                    {
                        BringToFront();

                        if (!string.IsNullOrWhiteSpace(receivedArg))
                            await MainForm.LoadFileAsync(receivedArg);
                    }));
                }
                catch (Exception)
                {
                    // Delay slightly to prevent rapid CPU looping on persistent pipe errors, then resume listening
                    await Task.Delay(500);
                }
            }
        });
    }

    /// <summary>
    /// Restores the main form if it's minimized and brings it to the foreground.
    /// </summary>
    private static void BringToFront()
    {
        if (MainForm == null || MainForm.IsDisposed || !MainForm.IsHandleCreated)
            return;

        IntPtr handle = MainForm.Handle;

        // If window is minimized to the taskbar, restore it
        if (IsIconic(handle))
        {
            ShowWindow(handle, SW_RESTORE);
        }

        // Bring to front and forcibly activate
        SetForegroundWindow(handle);
    }

    /// <summary>
    /// Generalized file prompting to be usable at startup and via runtime menus.
    /// </summary>
    public static readonly string[] SupportedExtensions = [".mp3", ".wav", ".flac"];

    public static string? PromptOpenFile()
    {
        var pattern = string.Join(";", SupportedExtensions.Select(e => "*" + e));
        using var ofd = new OpenFileDialog
        {
            Filter = $"Audio Files ({pattern})|{pattern}",
            Title = "Select Audio File to Analyze"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            return ofd.FileName;
        }

        return null;
    }
}