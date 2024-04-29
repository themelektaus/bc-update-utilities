using System.Collections.Generic;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public class Config_RemoteMachine
{
    public string Hostname { get; set; } = "localhost";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public List<string> navAdminTools = [];

    public Config_ServerInstance oldServerInstance = new();
    public Config_ServerInstance newServerInstance = new();

    public Config_SqlServer sqlServer = new();

    public Config_RemoteMachine()
    {
        oldServerInstance.remoteMachine = this;
        newServerInstance.remoteMachine = this;

        sqlServer.remoteMachine = this;
    }

    public async Task<PowerShellSession> GetSessionAsync()
    {
        return await PowerShellSessionManager.GetSessionAsync(this, string.Empty);
    }

    public async Task FetchNavAdminToolsAsync()
    {
        navAdminTools.Clear();

        var session = await GetSessionAsync();

        var items = await session.GetObjectListAsync(
            $@"Get-ChildItem -Path ""{Config.BC.NAV_PATH}"" -Recurse -Filter ""NavAdminTool.ps1"""
        );

        foreach (dynamic item in items)
        {
            navAdminTools.Add(item.FullName);
        }
    }
}
