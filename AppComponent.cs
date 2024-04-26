#pragma warning disable IDE1006

using Microsoft.AspNetCore.Components;

using System.Threading.Tasks;

namespace BCUpdateUtilities;

using Web;

public abstract class AppComponent : ComponentBase
{
    protected static App app => App.Instance;

    protected static Config.BC.RemoteMachine remoteMachine
        => Config.Instance.bc.remoteMachine;

    protected static Config.BC.RemoteMachine.SqlServer sqlServer
        => remoteMachine.sqlServer;

    protected static async Task GotoAsync<T>() where T : Page
    {
        await Root.Instance.GotoAsync(typeof(T));
    }

    protected static void Refresh()
    {
        Root.Instance.Refresh();
    }

    protected async Task Run(System.Func<Task> task)
    {
        await App.Instance.Run(task);

        this.RenderLater();
    }
}
