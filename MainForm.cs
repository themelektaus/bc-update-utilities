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

        SetupBlazorView();

        Opacity = 0;

        ResumeLayout(true);
    }

    void SetupBlazorView()
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

    public void OnAfterFirstRender(Rectangle? bounds, bool maximized)
    {
        RefreshWindow(zoomFactor: 1, bounds, maximized);

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

    public void RefreshWindow(float zoomFactor, Rectangle? bounds, bool maximized)
    {
        WindowState = maximized ? FormWindowState.Maximized : FormWindowState.Normal;

        blazorWebView.WebView.ZoomFactor = zoomFactor;

        Point location;
        Size size;

        if (bounds.HasValue)
        {
            location = bounds.Value.Location;
            size = bounds.Value.Size;
        }
        else
        {
            var dpiFactor = blazorWebView.WebView.DeviceDpi / 96f;
            size = (new Size(1120, 720) * zoomFactor * dpiFactor).ToSize();

            var screenSize = Screen.PrimaryScreen.Bounds.Size;
            location = (Point) ((screenSize - size) / 2);
        }

        if (Size != size || Location != location)
        {
            Size = size;
            Location = location;
        }
    }
}
