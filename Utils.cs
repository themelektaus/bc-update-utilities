using System.Diagnostics;
using System.Security.Principal;

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

}
