using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AmySonicVisualizer;

public static partial class FileAssociationHelper
{
    public static void SetAppIconForOpenWith()
    {
        string exeName = Path.GetFileName(Application.ExecutablePath),
            subKeyPath = $@"Software\Classes\Applications\{exeName}\DefaultIcon",
            iconPath = @"%SystemRoot%\System32\imageres.dll,125";

        try
        {
            using var existingKey = Registry.CurrentUser.OpenSubKey(subKeyPath);
            if (existingKey != null)
                return;

            using var iconKey = Registry.CurrentUser.CreateSubKey(subKeyPath);
            iconKey.SetValue(null, iconPath);

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to set application icon for 'Open With': {ex.Message}");
        }
    }

    [LibraryImport("shell32.dll", SetLastError = true)]
    private static partial void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    private const uint
        SHCNE_ASSOCCHANGED = 0x08000000,
        SHCNF_IDLIST = 0x0000;
}