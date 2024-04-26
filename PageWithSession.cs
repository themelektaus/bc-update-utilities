#pragma warning disable IDE1006

using System.Threading.Tasks;

namespace BCUpdateUtilities;

public abstract class PageWithSession : Page
{
    protected PowerShellSession session { get; private set; }

    protected override async Task OnInitAsync()
    {
        session = await remoteMachine.GetSessionAsync();

        await base.OnInitAsync();
    }
}
