using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;

using System.Drawing;
using System.Windows.Forms;

namespace BCUpdateUtilities;

public partial class MainForm : Form
{
    BlazorWebView blazorWebView;

    public MainForm()
    {
        SuspendLayout();

        Text = $"{AppInfo.name} - v{AppInfo.version}";
        Icon = Properties.Resources.Icon;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.Manual;
        Margin = Padding.Empty;

        SetupBlazorWebView();

        Opacity = 0;

        ResumeLayout(true);
    }

    void SetupBlazorWebView()
    {
        blazorWebView = new BlazorWebView
        {
            HostPage = AppInfo.hostPage,
            Location = Point.Empty,
            Margin = Padding.Empty,
            Dock = DockStyle.Fill
        };

        blazorWebView.WebView.DefaultBackgroundColor = Color.Transparent;

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        blazorWebView.Services = services.BuildServiceProvider();

        blazorWebView.RootComponents.Add<Web.Root>("#root");

        Controls.Add(blazorWebView);
    }

    public void OnAfterFirstRender()
    {
        RefreshWindow();

        Opacity = 1;

        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.UserClosing)
            return;

        var config = Config.Instance;

        if (WindowState == FormWindowState.Maximized)
        {
            config.maximized = true;
        }
        else
        {
            config.bounds = Bounds;
            config.maximized = false;
        }
    }

    public void RefreshWindow()
    {
        Config.Instance.SetupBounds();
        RefreshWindowInternal();
    }

    public void ResetWindow()
    {
        Config.Instance.ResetBounds();
        RefreshWindowInternal();
    }

    void RefreshWindowInternal()
    {
        RefreshWindowInternal(zoomFactor: 1, Config.Instance.bounds, Config.Instance.maximized);
    }

    void RefreshWindowInternal(float zoomFactor, Rectangle bounds, bool maximized)
    {
        WindowState = maximized ? FormWindowState.Maximized : FormWindowState.Normal;

        blazorWebView.WebView.ZoomFactor = zoomFactor;

        Point location;
        Size size;

        if (bounds.Location.IsEmpty)
        {
            var dpiFactor = blazorWebView.WebView.DeviceDpi / 96f;
            size = (bounds.Size * zoomFactor * dpiFactor).ToSize();

            var screenSize = Screen.PrimaryScreen.Bounds.Size;
            location = (Point) ((screenSize - size) / 2);
        }
        else
        {
            location = bounds.Location;
            size = bounds.Size;
        }

        if (Size != size || Location != location)
        {
            Size = size;
            Location = location;
        }
    }
}
