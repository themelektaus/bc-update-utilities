using BCUpdateUtilities.Web;

using System;
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
    }

    public void Dispose()
    {
        var task = updateCheckTask;

        updateCheckTask = null;

        PowerShellSessionManager.Dispose();

        Config.Instance.Save();

        task.Wait();
    }

    int business;

    public bool IsBusy()
    {
        return business > 0;
    }

    public async Task Run(Func<Task> task)
    {
        await IncreaseBusinessAsync();
        await task();
        await DecreaseBusinessAsync();
    }

    public async Task IncreaseBusinessAsync()
    {
        business++;
        await (Root.Instance?.RenderLaterAsync() ?? Task.CompletedTask);
    }

    public async Task DecreaseBusinessAsync()
    {
        business--;
        await (Root.Instance?.RenderLaterAsync() ?? Task.CompletedTask);
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
            await Root.Instance.RenderLaterAsync();
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

    public bool LogViewVisible { get; set; }

    public async Task ToggleLogViewAsync()
    {
        LogViewVisible = !LogViewVisible;
        await (Root.Instance?.RenderLaterAsync() ?? Task.CompletedTask);
    }
}
