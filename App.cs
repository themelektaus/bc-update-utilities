using BCUpdateUtilities.Web;
using BCUpdateUtilities.Web.Components;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BCUpdateUtilities;

public class App : IDisposable
{
    public static App Instance { get; private set; }

    public MainForm mainForm;

    public Update Update { get; private set; }

    Task updateCheckTask;

    int nextUpdateCheckCountdown;
    public int NextUpdateCheckCountdown
    {
        get => nextUpdateCheckCountdown;
        set => nextUpdateCheckCountdown = value;
    }

    public App()
    {
        Instance = this;

        mainForm = new();
        mainForm.FormClosing += (sender, e) =>
        {
            Logger.Pending("Closing");
        };

        updateCheckTask = Task.Run(async () =>
        {
            while (Root.Instance is null)
            {
                await Task.Delay(1);
            }

        Loop:
            await Task.Delay(1000);
            await CheckForUpdates();

            NextUpdateCheckCountdown = 900;

            while (NextUpdateCheckCountdown > 0)
            {
                if (updateCheckTask is null)
                {
                    return;
                }

                NextUpdateCheckCountdown--;
                await Task.Delay(1000);
            }

            goto Loop;
        });

        Logger.onUpdate += Logger_OnUpdate;
    }

    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (disposing)
        {
            var task = updateCheckTask;

            updateCheckTask = null;

            PowerShellSessionManager.Dispose();

            Config.Instance.Save();

            task.Wait();
        }
    }

    async Task RenderRootLaterAsync()
    {
        var root = Root.Instance;
        if (root is not null)
        {
            root.RenderLater();
            await Task.Delay(1);
        }
    }

    int business;

    public bool IsBusy()
    {
        return business > 0;
    }

    public async Task IncreaseBusinessAsync()
    {
        business++;
        await RenderRootLaterAsync();
    }

    public async Task DecreaseBusinessAsync()
    {
        business--;
        await RenderRootLaterAsync();
    }

    public async Task CheckForUpdates()
    {
        var updateAvailable = Update?.available ?? false;

        if (Root.Instance is null)
        {
            Update = null;
            return;
        }

        Update = await Update.Check();

        if (updateAvailable != Update.available)
        {
            await RenderRootLaterAsync();
        }
    }

    public async Task PerformUpdate()
    {
        await IncreaseBusinessAsync();

        Logger.Pending($"Downloading v{Update.remoteVersion}");

        await Update.Prepare();

        Utils.StartAsAdmin("Update.bat", createNoWindow: true);

        Logger.Info($"The application is going to be closed now");

        await Task.Delay(3000);

        mainForm.Close();
    }

    public readonly List<string> mssqlDatabases = [];

    public bool LogViewVisible { get; set; }

    public async Task ToggleLogViewAsync()
    {
        LogViewVisible = !LogViewVisible;
        await RenderRootLaterAsync();
    }

    void Logger_OnUpdate(string type, string message)
    {
        if (!LogViewVisible)
        {
            if (type == "warning" || type == "error" || type == "exception")
            {
                LogViewVisible = true;
                Menu.Instances.RenderLater();
                Root.Instance?.RenderLater();
            }
        }
    }
}
