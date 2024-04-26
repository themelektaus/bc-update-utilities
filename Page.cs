#pragma warning disable IDE1006

using System.Threading.Tasks;

namespace BCUpdateUtilities;

public abstract class Page : AppComponent
{
    protected bool isInitialized { get; private set; }

    protected virtual Task OnInitAsync()
    {
        return Task.CompletedTask;
    }

    protected sealed override Task OnInitializedAsync() => Run(async () =>
    {
        await OnInitAsync();

        isInitialized = true;
    });
}
