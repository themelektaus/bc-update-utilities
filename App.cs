using BCUpdateUtilities.Web;
using BCUpdateUtilities.Web.Components;

using System;
using System.Threading.Tasks;
using System.Windows.Forms;

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

    public class Components
    {
        public Root root;
        public Menu menu;
    }
    public readonly Components components = new();

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
            while (components.root is null)
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
                    return;

                NextUpdateCheckCountdown--;
                await Task.Delay(1000);
            }
            goto Loop;
        });

        Logger.Info("Welcome");
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

            Config.Instance.Save();

            task.Wait();
        }
    }

    int business;

    public bool IsBusy()
    {
        return business > 0;
    }

    public void IncreaseBusiness()
    {
        business++;
        components.root?.RenderLater();
    }

    public void DecreaseBusiness()
    {
        business--;
        components.root?.RenderLater();
    }

    public async Task CheckForUpdates()
    {
        bool IsUpdateAvailable() => Update?.available ?? false;

        var updateAvailable = IsUpdateAvailable();

        Update = components.root is null
            ? null
            : await Update.Check();

        if (components.root is null)
            return;

        if (updateAvailable != IsUpdateAvailable())
            components.root.RenderLater();
    }

    public async Task PerformUpdate()
    {
        IncreaseBusiness();

        Logger.Pending($"Downloading v{Update.remoteVersion}");

        await Update.Prepare();

        Utils.StartAsAdmin("Update.bat", createNoWindow: true);

        Logger.Info($"The application is going to be closed now");

        await Task.Delay(3000);

        mainForm.Close();
    }

    public async Task<bool> DownloadFile(string file)
    {
        Logger.Pending($"Downloading {file}");

        if (await Config.Instance.DownloadFile(file))
        {
            Logger.Success($"Download of {file}");
            return true;
        }

        Logger.Error($"Download of {file}");
        return false;
    }

    public async Task<string> ShowOpenFileDialog(string fileName)
    {
        Logger.Pending("Opening File");

        using var dialog = Utils.CreateOpenFileDialog(fileName);

        var dialogResult = await dialog.ShowDialogAsync();
        if (dialogResult == DialogResult.OK)
            return dialog.FileName;

        return null;
    }
}
