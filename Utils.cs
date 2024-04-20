using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace BCUpdateUtilities;

public static class Utils
{
#if DEBUG
    public const bool DEBUG = true;
#else
    public const bool DEBUG = false;
#endif

    public static bool IsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void StartAsAdmin(string fileName, bool createNoWindow)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = createNoWindow,
            WindowStyle = createNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
        });
    }

    public static List<string> GetDataFileNames()
        => Directory.Exists("data")
            ? Directory
                .EnumerateFiles("data", "*.dat")
                .Select(Path.GetFileNameWithoutExtension)
                .ToList()
            : new();

    public static OpenFileDialog CreateOpenFileDialog(string fileName)
    {
        var dialog = new OpenFileDialog();

        if (!string.IsNullOrEmpty(fileName))
        {
            var file = new FileInfo(fileName);

            if (file.Exists)
            {
                dialog.InitialDirectory = file.DirectoryName;
                dialog.FileName = file.Name;
            }
            else if (Directory.Exists(fileName))
            {
                dialog.InitialDirectory = fileName;
            }
        }

        return dialog;
    }

}
