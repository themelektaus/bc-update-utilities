using System;
using System.Windows.Forms;

using Mutex = System.Threading.Mutex;

namespace BCUpdateUtilities;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
#if RELEASE
        if (args.Length >= 1 && args[0] == "publish")
        {
            Update.Publish(
                publicFolder: args.Length >= 2 ? args[1] : null,
                version: args.Length >= 3 ? args[2] : null
            );
            return;
        }
#endif

        var mutex = new Mutex(true, AppInfo.currentProcessName, out var createdNew);

        if (!createdNew)
            return;

#if RELEASE
        if (!Utils.IsAdmin())
        {
            Utils.StartAsAdmin(AppInfo.currentProcessExeName, createNoWindow: false);
            return;
        }

        var variable = "NODE_TLS_REJECT_UNAUTHORIZED";
        var value = "0";
        var target = EnvironmentVariableTarget.Machine;

        if (Environment.GetEnvironmentVariable(variable, target) != value)
        {
            Environment.SetEnvironmentVariable(variable, value, target);

            Utils.StartAsAdmin(AppInfo.currentProcessExeName, createNoWindow: false);
            return;
        }
#endif

        ApplicationConfiguration.Initialize();

        using (var app = new App())
        {
#if RELEASE
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger.Error(e.ExceptionObject.ToString());
                Environment.Exit(0);
            };
#endif

            Application.Run(app.mainForm);
        }

        GC.KeepAlive(mutex);
    }
}
